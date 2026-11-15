using System.Collections.Concurrent;
using BoardWalk.Api.Data.Models;
using BoardWalk.Api.Data.Repositories;
using BoardWalk.Api.Services.GameRules;
using BoardWalk.Api.Services.Interfaces;
using BoardWalk.Api.Services.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace BoardWalk.Api.Services.Implementations
{
    /// <summary>
    /// The game rules engine. Every mutating action runs under a per-game lock so two requests for the
    /// same game (a double-click, or two tabs) can never interleave. NOTE: the lock is in-process — if the
    /// API is ever scaled to several instances, swap it for a distributed lock (Redis is already available).
    /// </summary>
    public partial class GameService : IGameService
    {
        private const int RecentEventCount = 20;

        // One semaphore per game id. Entries are tiny and removed when a game finishes.
        private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> GameLocks = new();

        private readonly IUnitOfWork _unitOfWork;
        private readonly IGameRealtimeNotifier _notifier;
        private readonly ILogger<GameService> _logger;

        public GameService(IUnitOfWork unitOfWork, IGameRealtimeNotifier notifier, ILogger<GameService> logger)
        {
            _unitOfWork = unitOfWork;
            _notifier = notifier;
            _logger = logger;
        }

        // ------------------------------------------------------------------
        // Game creation
        // ------------------------------------------------------------------

        public async Task CreateGameFromLobbyAsync(Lobby lobby)
        {
            var rng = Random.Shared;
            var shuffled = lobby.Members.OrderBy(_ => rng.Next()).ToList();

            var game = new Game
            {
                Id = Guid.NewGuid(),
                LobbyId = lobby.Id,
                Status = GameStatus.InProgress,
                CurrentTurnUserId = shuffled[0].UserId,
                TurnNumber = 1,
                ChanceDeckOrder = ChanceDeck.NewShuffledOrder(rng),
                CreatedAt = DateTime.UtcNow
            };

            for (var i = 0; i < shuffled.Count; i++)
            {
                game.Players.Add(new GamePlayer
                {
                    Id = Guid.NewGuid(),
                    GameId = game.Id,
                    UserId = shuffled[i].UserId,
                    TurnOrder = i,
                    Cash = BoardDefinition.StartingCash,
                    Position = BoardDefinition.StartIndex
                });
            }

            foreach (var tile in BoardDefinition.OwnableTiles)
            {
                game.Properties.Add(new GameProperty
                {
                    Id = Guid.NewGuid(),
                    GameId = game.Id,
                    TileIndex = tile.Index
                });
            }

            AddEvent(game, "The game has started. Good luck!");

            await _unitOfWork.Games.AddAsync(game);
            _logger.LogInformation("Game {GameId} created for lobby {LobbyId} with {Count} players", game.Id, lobby.Id, shuffled.Count);
        }

        // ------------------------------------------------------------------
        // Queries
        // ------------------------------------------------------------------

        public BoardResponse GetBoard() => new()
        {
            PassStartSalary = BoardDefinition.PassStartSalary,
            StartingCash = BoardDefinition.StartingCash,
            DetentionFine = BoardDefinition.DetentionFine,
            Tiles = BoardDefinition.Tiles.Select(t => new TileResponse
            {
                Index = t.Index,
                Name = t.Name,
                Type = t.Type.ToString(),
                ColorGroup = t.ColorGroup,
                Price = t.Price,
                HouseCost = t.HouseCost,
                TaxAmount = t.TaxAmount,
                RentTiers = t.RentTiers()
            }).ToList()
        };

        public async Task<GameStateResponse?> GetCurrentGameAsync(Guid userId)
        {
            var game = await _unitOfWork.Games.GetActiveForUserAsync(userId);
            return game == null ? null : await MapStateAsync(game, userId);
        }

        // ------------------------------------------------------------------
        // Actions
        // ------------------------------------------------------------------

        public async Task<RollResultResponse> RollAsync(Guid userId)
        {
            int d1 = 0, d2 = 0;

            var state = await RunTurnActionAsync(userId, (game, me) =>
            {
                RequireNoOutstandingDecision(game);
                if (game.HasRolled) throw new InvalidOperationException("You have already rolled this turn.");

                d1 = Random.Shared.Next(1, 7);
                d2 = Random.Shared.Next(1, 7);
                game.LastDie1 = d1;
                game.LastDie2 = d2;
                game.LastChanceCardId = null;
                var total = d1 + d2;
                var doubles = d1 == d2;

                AddEvent(game, $"{Name(me)} rolled {total}{(doubles ? " (doubles)" : "")}.");

                if (me.InJail)
                {
                    ResolveRollInDetention(game, me, doubles, total);
                    return;
                }

                if (doubles)
                {
                    game.DoublesCount++;
                    if (game.DoublesCount >= 3)
                    {
                        AddEvent(game, $"{Name(me)} rolled doubles three times in a row and goes to Detention Cove.");
                        SendToDetention(game, me);
                        return;
                    }
                }
                else
                {
                    game.DoublesCount = 0;
                }

                // Doubles earn another roll once the landing is resolved.
                game.HasRolled = !doubles;
                MoveBy(game, me, total);
                ResolveLanding(game, me, total);
            }, afterSave: async game =>
            {
                await _notifier.NotifyDiceRolledAsync(game.LobbyId, userId, d1, d2);
            });

            return new RollResultResponse { Die1 = d1, Die2 = d2, IsDoubles = d1 == d2, State = state };
        }

        public Task<GameStateResponse> BuyPropertyAsync(Guid userId) =>
            RunTurnActionAsync(userId, (game, me) =>
            {
                if (game.PendingPurchaseTileIndex is not int tileIndex)
                    throw new InvalidOperationException("There is no property to buy right now.");

                var tile = BoardDefinition.Get(tileIndex);
                if (me.Cash < tile.Price)
                    throw new InvalidOperationException($"You need ${tile.Price} to buy {tile.Name}.");

                var row = game.Properties.First(p => p.TileIndex == tileIndex);
                me.Cash -= tile.Price;
                row.OwnerUserId = me.UserId;
                game.PendingPurchaseTileIndex = null;

                AddEvent(game, $"{Name(me)} bought {tile.Name} for ${tile.Price}.");
            });

        public Task<GameStateResponse> PassPropertyAsync(Guid userId) =>
            RunTurnActionAsync(userId, (game, me) =>
            {
                if (game.PendingPurchaseTileIndex is not int tileIndex)
                    throw new InvalidOperationException("There is no property to pass on right now.");

                game.PendingPurchaseTileIndex = null;
                AddEvent(game, $"{Name(me)} passed on {BoardDefinition.Get(tileIndex).Name}.");
            });

        public Task<GameStateResponse> EndTurnAsync(Guid userId) =>
            RunTurnActionAsync(userId, (game, me) =>
            {
                RequireNoOutstandingDecision(game);
                if (!game.HasRolled) throw new InvalidOperationException("Roll the dice before ending your turn.");

                AdvanceTurn(game);
            });

        public Task<GameStateResponse> PayJailFineAsync(Guid userId) =>
            RunTurnActionAsync(userId, (game, me) =>
            {
                RequireNoOutstandingDecision(game);
                if (!me.InJail) throw new InvalidOperationException("You are not in Detention Cove.");
                if (game.HasRolled) throw new InvalidOperationException("You can only pay before rolling.");
                if (me.Cash < BoardDefinition.DetentionFine)
                    throw new InvalidOperationException($"You need ${BoardDefinition.DetentionFine} to pay the fine.");

                me.Cash -= BoardDefinition.DetentionFine;
                me.InJail = false;
                me.JailTurns = 0;
                AddEvent(game, $"{Name(me)} paid ${BoardDefinition.DetentionFine} and left Detention Cove.");
            });

        public Task<GameStateResponse> UseJailCardAsync(Guid userId) =>
            RunTurnActionAsync(userId, (game, me) =>
            {
                RequireNoOutstandingDecision(game);
                if (!me.InJail) throw new InvalidOperationException("You are not in Detention Cove.");
                if (game.HasRolled) throw new InvalidOperationException("You can only use a card before rolling.");
                if (me.GetOutOfJailCards <= 0) throw new InvalidOperationException("You have no Harbour Pass card.");

                me.GetOutOfJailCards--;
                me.InJail = false;
                me.JailTurns = 0;
                AddEvent(game, $"{Name(me)} used a Harbour Pass and left Detention Cove.");
            });

        public Task<GameStateResponse> PayDebtAsync(Guid userId) =>
            RunTurnActionAsync(userId, (game, me) =>
            {
                if (game.PendingDebtAmount <= 0) throw new InvalidOperationException("You do not owe anything right now.");
                if (me.Cash < game.PendingDebtAmount)
                    throw new InvalidOperationException($"You still need ${game.PendingDebtAmount - me.Cash} more to settle this debt.");

                var amount = game.PendingDebtAmount;
                var creditorId = game.PendingDebtCreditorUserId;
                game.PendingDebtAmount = 0;
                game.PendingDebtCreditorUserId = null;

                me.Cash -= amount;
                if (creditorId != null)
                {
                    var creditor = game.Players.First(p => p.UserId == creditorId);
                    creditor.Cash += amount;
                    AddEvent(game, $"{Name(me)} paid {Name(creditor)} ${amount}.");
                }
                else
                {
                    AddEvent(game, $"{Name(me)} paid ${amount} to the bank.");
                }
            });

        public Task<GameStateResponse> DeclareBankruptcyAsync(Guid userId) =>
            RunTurnActionAsync(userId, (game, me) =>
            {
                var creditorId = game.PendingDebtAmount > 0 ? game.PendingDebtCreditorUserId : null;
                var creditor = creditorId == null ? null : game.Players.FirstOrDefault(p => p.UserId == creditorId);

                // Everything the player owns goes to the creditor, or back to the bank if there is none.
                foreach (var row in game.Properties.Where(p => p.OwnerUserId == me.UserId))
                {
                    row.Houses = 0; // buildings are cleared when a property changes hands via bankruptcy
                    if (creditor != null)
                    {
                        row.OwnerUserId = creditor.UserId;
                    }
                    else
                    {
                        row.OwnerUserId = null;
                        row.IsMortgaged = false;
                    }
                }

                if (creditor != null)
                {
                    creditor.Cash += me.Cash;
                    creditor.GetOutOfJailCards += me.GetOutOfJailCards;
                }

                me.Cash = 0;
                me.GetOutOfJailCards = 0;
                me.IsBankrupt = true;
                me.InJail = false;

                foreach (var trade in game.Trades.Where(t => t.Status == TradeStatus.Pending &&
                             (t.ProposerUserId == me.UserId || t.RecipientUserId == me.UserId)))
                {
                    trade.Status = TradeStatus.Cancelled;
                    trade.RespondedAt = DateTime.UtcNow;
                }
                game.PendingDebtAmount = 0;
                game.PendingDebtCreditorUserId = null;
                game.PendingPurchaseTileIndex = null;

                AddEvent(game, creditor != null
                    ? $"{Name(me)} declared bankruptcy. Their assets go to {Name(creditor)}."
                    : $"{Name(me)} declared bankruptcy. Their assets return to the bank.");

                var remaining = game.Players.Where(p => !p.IsBankrupt).ToList();
                if (remaining.Count == 1)
                {
                    FinishGame(game, remaining[0]);
                }
                else
                {
                    AdvanceTurn(game);
                }
            });

        // ------------------------------------------------------------------
        // Action runner: lock -> load -> validate turn -> mutate -> save -> notify
        // ------------------------------------------------------------------

        private async Task<GameStateResponse> RunTurnActionAsync(
            Guid userId,
            Action<Game, GamePlayer> action,
            Func<Game, Task>? afterSave = null,
            bool requireTurn = true)
        {
            var gameId = await _unitOfWork.Games.GetActiveGameIdForUserAsync(userId)
                ?? throw new InvalidOperationException("You are not in a game.");

            var gate = GameLocks.GetOrAdd(gameId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync();
            try
            {
                // Load AFTER taking the lock so we always act on the latest committed state.
                var game = await _unitOfWork.Games.GetFullAsync(gameId)
                    ?? throw new InvalidOperationException("Game not found.");

                if (game.Status != GameStatus.InProgress)
                    throw new InvalidOperationException("This game has already finished.");

                var me = game.Players.FirstOrDefault(p => p.UserId == userId)
                    ?? throw new InvalidOperationException("You are not a player in this game.");

                if (me.IsBankrupt)
                    throw new InvalidOperationException("You are bankrupt and out of the game.");

                if (requireTurn && game.CurrentTurnUserId != userId)
                    throw new InvalidOperationException("It is not your turn.");

                var cardBefore = game.LastChanceCardId;
                action(game, me);

                // A finished game releases its lobby and players in the same save as the final move.
                if (game.Status == GameStatus.Finished)
                    await ReleaseLobbyAsync(game);

                await SaveAsync();

                if (afterSave != null) await afterSave(game);

                if (game.Status == GameStatus.Finished)
                {
                    await _notifier.NotifyGameFinishedAsync(game.LobbyId, game.WinnerUserId);
                    GameLocks.TryRemove(gameId, out _);
                }
                else if (game.LastChanceCardId != null && game.LastChanceCardId != cardBefore)
                {
                    await _notifier.NotifyCardDrawnAsync(game.LobbyId, userId, ChanceDeck.Get(game.LastChanceCardId.Value));
                }

                await _notifier.NotifyGameUpdatedAsync(game.LobbyId);

                return await MapStateAsync(game, userId);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task SaveAsync()
        {
            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException(
                    "This action conflicted with another change happening at the same time. Please try again.");
            }
        }

        // ------------------------------------------------------------------
        // Rules
        // ------------------------------------------------------------------

        private static void RequireNoOutstandingDecision(Game game)
        {
            if (game.PendingDebtAmount > 0)
                throw new InvalidOperationException("You owe money. Pay your debt or declare bankruptcy first.");
            if (game.PendingPurchaseTileIndex != null)
                throw new InvalidOperationException("Buy or pass on the property you landed on first.");
        }

        private void ResolveRollInDetention(Game game, GamePlayer me, bool doubles, int total)
        {
            if (doubles)
            {
                me.InJail = false;
                me.JailTurns = 0;
                game.DoublesCount = 0;
                game.HasRolled = true; // leaving Detention with doubles does not grant an extra roll
                AddEvent(game, $"{Name(me)} rolled doubles and left Detention Cove.");
                MoveBy(game, me, total);
                ResolveLanding(game, me, total);
                return;
            }

            me.JailTurns++;
            game.HasRolled = true;

            if (me.JailTurns >= 3)
            {
                me.InJail = false;
                me.JailTurns = 0;
                AddEvent(game, $"{Name(me)} failed to roll doubles three times and must pay the ${BoardDefinition.DetentionFine} fine.");
                Charge(game, me, BoardDefinition.DetentionFine, creditorUserId: null);
                MoveBy(game, me, total);
                ResolveLanding(game, me, total);
            }
            else
            {
                AddEvent(game, $"{Name(me)} stays in Detention Cove (attempt {me.JailTurns} of 3).");
            }
        }

        /// <summary>Moves forward by <paramref name="steps"/> tiles, paying the Departure Gate salary on passing it.</summary>
        private void MoveBy(Game game, GamePlayer me, int steps)
        {
            var newPosition = (me.Position + steps) % BoardDefinition.TileCount;
            if (newPosition < me.Position)
                PaySalary(game, me);
            me.Position = newPosition;
        }

        private void PaySalary(Game game, GamePlayer me)
        {
            me.Cash += BoardDefinition.PassStartSalary;
            AddEvent(game, $"{Name(me)} passed Departure Gate and collected ${BoardDefinition.PassStartSalary}.");
        }

        private void SendToDetention(Game game, GamePlayer me)
        {
            me.Position = BoardDefinition.DetentionIndex;
            me.InJail = true;
            me.JailTurns = 0;
            game.DoublesCount = 0;
            game.HasRolled = true; // a trip to Detention always ends the turn
            game.PendingPurchaseTileIndex = null;
        }

        private void ResolveLanding(Game game, GamePlayer me, int diceTotal)
        {
            var tile = BoardDefinition.Get(me.Position);
            AddEvent(game, $"{Name(me)} landed on {tile.Name}.");

            switch (tile.Type)
            {
                case TileType.Property:
                case TileType.Station:
                case TileType.Utility:
                    ResolveOwnableLanding(game, me, tile, diceTotal);
                    break;

                case TileType.Tax:
                    AddEvent(game, $"{Name(me)} owes ${tile.TaxAmount} in tax.");
                    Charge(game, me, tile.TaxAmount, creditorUserId: null);
                    break;

                case TileType.Chance:
                    DrawFortuneCard(game, me, diceTotal);
                    break;

                case TileType.GoToDetention:
                    AddEvent(game, $"{Name(me)} goes to Detention Cove.");
                    SendToDetention(game, me);
                    break;

                // Start, Detention (just visiting) and RestStop have no effect.
            }
        }

        private void ResolveOwnableLanding(Game game, GamePlayer me, Tile tile, int diceTotal)
        {
            var row = game.Properties.First(p => p.TileIndex == tile.Index);

            if (row.OwnerUserId == null)
            {
                game.PendingPurchaseTileIndex = tile.Index;
                return;
            }

            if (row.OwnerUserId == me.UserId || row.IsMortgaged) return;

            var owner = game.Players.First(p => p.UserId == row.OwnerUserId);
            var rent = CalculateRent(game, row, tile, diceTotal);
            if (rent <= 0) return;

            AddEvent(game, $"{Name(me)} owes {Name(owner)} ${rent} rent.");
            Charge(game, me, rent, owner.UserId);
        }

        private static int CalculateRent(Game game, GameProperty row, Tile tile, int diceTotal)
        {
            var ownerId = row.OwnerUserId;

            switch (tile.Type)
            {
                case TileType.Station:
                {
                    var owned = game.Properties.Count(p => p.OwnerUserId == ownerId && !p.IsMortgaged &&
                        BoardDefinition.Get(p.TileIndex).Type == TileType.Station);
                    return BoardDefinition.StationRent(owned);
                }
                case TileType.Utility:
                {
                    var owned = game.Properties.Count(p => p.OwnerUserId == ownerId && !p.IsMortgaged &&
                        BoardDefinition.Get(p.TileIndex).Type == TileType.Utility);
                    return BoardDefinition.UtilityRent(owned, diceTotal);
                }
                default:
                {
                    var tiers = tile.RentTiers();
                    var rent = tiers[Math.Clamp(row.Houses, 0, 5)];

                    // Owning the whole colour group doubles the base rent of an unbuilt street.
                    if (row.Houses == 0 && OwnsWholeGroup(game, ownerId, tile.ColorGroup!))
                        rent *= 2;

                    return rent;
                }
            }
        }

        private static bool OwnsWholeGroup(Game game, Guid? ownerId, string colorGroup) =>
            BoardDefinition.InGroup(colorGroup).All(t =>
                game.Properties.First(p => p.TileIndex == t.Index).OwnerUserId == ownerId);

        /// <summary>
        /// Takes money from a player. If they cannot afford it, records a pending debt instead — they must
        /// raise cash (mortgage / sell, added in the player-actions branch) or declare bankruptcy before the turn can end.
        /// </summary>
        private void Charge(Game game, GamePlayer payer, int amount, Guid? creditorUserId)
        {
            if (amount <= 0) return;

            if (payer.Cash >= amount)
            {
                payer.Cash -= amount;
                if (creditorUserId != null)
                    game.Players.First(p => p.UserId == creditorUserId).Cash += amount;
                return;
            }

            // If a debt is already pending (rare: e.g. a detention fine followed by rent on the same turn),
            // the new amount is added to it and the original creditor is kept.
            if (game.PendingDebtAmount > 0)
            {
                game.PendingDebtAmount += amount;
            }
            else
            {
                game.PendingDebtAmount = amount;
                game.PendingDebtCreditorUserId = creditorUserId;
            }
            AddEvent(game, $"{Name(payer)} cannot afford ${amount} and must raise funds or go bankrupt.");
        }

        private void DrawFortuneCard(Game game, GamePlayer me, int diceTotal)
        {
            var order = ChanceDeck.ParseOrder(game.ChanceDeckOrder);
            var card = ChanceDeck.Get(order[game.ChanceDeckPosition % order.Length]);
            game.ChanceDeckPosition = (game.ChanceDeckPosition + 1) % order.Length;
            game.LastChanceCardId = card.Id;

            AddEvent(game, $"{Name(me)} drew \"{card.Title}\".");

            var others = game.Players.Where(p => !p.IsBankrupt && p.UserId != me.UserId).ToList();

            switch (card.Effect)
            {
                case ChanceEffect.MoveTo:
                    if (card.Value <= me.Position) PaySalary(game, me);
                    me.Position = card.Value;
                    ResolveLanding(game, me, diceTotal);
                    break;

                case ChanceEffect.MoveBack:
                    me.Position = (me.Position - card.Value + BoardDefinition.TileCount) % BoardDefinition.TileCount;
                    ResolveLanding(game, me, diceTotal);
                    break;

                case ChanceEffect.Gain:
                    me.Cash += card.Value;
                    break;

                case ChanceEffect.Pay:
                    Charge(game, me, card.Value, creditorUserId: null);
                    break;

                case ChanceEffect.GoToDetention:
                    SendToDetention(game, me);
                    break;

                case ChanceEffect.GetOutOfDetentionFree:
                    me.GetOutOfJailCards++;
                    break;

                case ChanceEffect.PayEachPlayer:
                {
                    var total = card.Value * others.Count;
                    if (me.Cash >= total)
                    {
                        me.Cash -= total;
                        foreach (var other in others) other.Cash += card.Value;
                    }
                    else
                    {
                        // Simplification: an unaffordable payout becomes a debt owed to the bank.
                        Charge(game, me, total, creditorUserId: null);
                    }
                    break;
                }

                case ChanceEffect.CollectFromEachPlayer:
                    foreach (var other in others)
                    {
                        // Simplification: each other player pays what they can, up to the card value.
                        var paid = Math.Min(other.Cash, card.Value);
                        other.Cash -= paid;
                        me.Cash += paid;
                    }
                    break;
            }
        }

        private void AdvanceTurn(Game game)
        {
            var ordered = game.Players.Where(p => !p.IsBankrupt).OrderBy(p => p.TurnOrder).ToList();
            var current = game.Players.First(p => p.UserId == game.CurrentTurnUserId);

            // Next active player after the current one in turn order, wrapping around.
            var next = ordered.FirstOrDefault(p => p.TurnOrder > current.TurnOrder) ?? ordered.First();

            game.CurrentTurnUserId = next.UserId;
            game.TurnNumber++;
            game.HasRolled = false;
            game.DoublesCount = 0;
            game.PendingPurchaseTileIndex = null;
            game.LastChanceCardId = null;

            AddEvent(game, $"It is now {Name(next)}'s turn.");
        }

        private void FinishGame(Game game, GamePlayer winner)
        {
            game.Status = GameStatus.Finished;
            game.WinnerUserId = winner.UserId;
            game.FinishedAt = DateTime.UtcNow;
            AddEvent(game, $"{Name(winner)} wins the game!");
        }

        /// <summary>Closes the lobby and frees every player so they can create or join a new lobby.</summary>
        private async Task ReleaseLobbyAsync(Game game)
        {
            foreach (var player in game.Players)
            {
                player.User.CurrentLobbyId = null;
                _unitOfWork.Users.Update(player.User);
            }

            var lobby = await _unitOfWork.Lobbies.GetWithMembersAsync(game.LobbyId);
            if (lobby != null)
            {
                foreach (var member in lobby.Members.ToList())
                    _unitOfWork.LobbyMembers.Delete(member);

                lobby.Status = LobbyStatus.Closed;
                lobby.ClosedAt = DateTime.UtcNow;
                _unitOfWork.Lobbies.Update(lobby);
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static string Name(GamePlayer p) => p.User.FirstName;

        /// <summary>
        /// Appends a line to the game log. Registered explicitly as Added (rather than relying on change
        /// detection to discover it through game.Events) so EF always INSERTs it.
        /// </summary>
        private void AddEvent(Game game, string message)
        {
            game.EventCounter++;
            _unitOfWork.GameEvents.Add(new GameEvent
            {
                Id = Guid.NewGuid(),
                GameId = game.Id,
                Sequence = game.EventCounter,
                Message = message,
                CreatedAt = DateTime.UtcNow
            });
        }

        // ------------------------------------------------------------------
        // Mapping
        // ------------------------------------------------------------------

        private async Task<GameStateResponse> MapStateAsync(Game game, Guid callerUserId)
        {
            var events = await _unitOfWork.Games.GetRecentEventsAsync(game.Id, RecentEventCount);

            var players = game.Players.OrderBy(p => p.TurnOrder).Select(p => new GamePlayerResponse
            {
                UserId = p.UserId,
                FirstName = p.User.FirstName,
                LastName = p.User.LastName,
                TurnOrder = p.TurnOrder,
                Cash = p.Cash,
                Position = p.Position,
                InJail = p.InJail,
                GetOutOfJailCards = p.GetOutOfJailCards,
                IsBankrupt = p.IsBankrupt,
                IsCurrentTurn = game.Status == GameStatus.InProgress && p.UserId == game.CurrentTurnUserId,
                OwnedTileIndexes = game.Properties
                    .Where(r => r.OwnerUserId == p.UserId)
                    .Select(r => r.TileIndex)
                    .OrderBy(i => i)
                    .ToList()
            }).ToList();

            ChanceCardResponse? lastCard = null;
            if (game.LastChanceCardId != null)
            {
                var card = ChanceDeck.Get(game.LastChanceCardId.Value);
                lastCard = new ChanceCardResponse { Id = card.Id, Title = card.Title, Description = card.Description };
            }

            return new GameStateResponse
            {
                Id = game.Id,
                Status = game.Status.ToString(),
                CurrentTurnUserId = game.CurrentTurnUserId,
                TurnNumber = game.TurnNumber,
                HasRolled = game.HasRolled,
                Die1 = game.LastDie1,
                Die2 = game.LastDie2,
                DoublesCount = game.DoublesCount,
                PendingPurchaseTileIndex = game.PendingPurchaseTileIndex,
                PendingDebt = game.PendingDebtAmount > 0
                    ? new PendingDebtResponse { Amount = game.PendingDebtAmount, CreditorUserId = game.PendingDebtCreditorUserId }
                    : null,
                LastChanceCard = lastCard,
                WinnerUserId = game.WinnerUserId,
                Players = players,
                Properties = game.Properties
                    .OrderBy(r => r.TileIndex)
                    .Select(r => new GamePropertyResponse
                    {
                        TileIndex = r.TileIndex,
                        OwnerUserId = r.OwnerUserId,
                        Houses = r.Houses,
                        IsMortgaged = r.IsMortgaged
                    }).ToList(),
                RecentEvents = events.Select(e => new GameEventResponse { Message = e.Message, CreatedAt = e.CreatedAt }).ToList(),
                AvailableActions = ComputeAvailableActions(game, callerUserId)
            };
        }

        private static List<string> ComputeAvailableActions(Game game, Guid callerUserId)
        {
            var actions = new List<string>();
            var me = game.Players.FirstOrDefault(p => p.UserId == callerUserId);

            if (game.Status != GameStatus.InProgress || me == null || me.IsBankrupt)
                return actions;

            // Trading is open to every active player at any time, not only on their own turn.
            actions.Add("proposeTrade");

            if (game.CurrentTurnUserId != callerUserId)
                return actions;

            actions.Add("manageProperties");

            if (game.PendingDebtAmount > 0)
            {
                if (me.Cash >= game.PendingDebtAmount) actions.Add("payDebt");
            }
            else if (game.PendingPurchaseTileIndex is int tileIndex)
            {
                if (me.Cash >= BoardDefinition.Get(tileIndex).Price) actions.Add("buy");
                actions.Add("pass");
            }
            else if (!game.HasRolled)
            {
                actions.Add("roll");
                if (me.InJail)
                {
                    if (me.Cash >= BoardDefinition.DetentionFine) actions.Add("payJailFine");
                    if (me.GetOutOfJailCards > 0) actions.Add("useJailCard");
                }
            }
            else
            {
                actions.Add("endTurn");
            }

            actions.Add("declareBankruptcy");
            return actions;
        }
    }
}
