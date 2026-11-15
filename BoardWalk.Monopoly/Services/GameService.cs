using BoardWalk.Monopoly.Abstractions;
using BoardWalk.Monopoly.Contracts;
using BoardWalk.Monopoly.Data;
using BoardWalk.Monopoly.Models;
using BoardWalk.Monopoly.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BoardWalk.Monopoly.Services
{
    /// <summary>
    /// The rules engine. Every mutating action follows one pipeline (see RunActionAsync):
    /// take the per-game lock -> load -> validate -> mutate -> save -> notify. Reads never take the lock.
    /// </summary>
    public partial class GameService : IGameService
    {
        private const int RecentEventCount = 20;
        private const int MaxEventPageSize = 500;

        private readonly GameStore _store;
        private readonly IGameRealtimeNotifier _notifier;
        private readonly IGameCompletionHandler _completion;
        private readonly IGameActorContext _actor;
        private readonly IGameLock _lock;
        private readonly IDiceRoller _dice;
        private readonly GameOptions _options;
        private readonly ILogger<GameService> _logger;

        public GameService(
            GameStore store,
            IGameRealtimeNotifier notifier,
            IGameCompletionHandler completion,
            IGameActorContext actor,
            IGameLock gameLock,
            IDiceRoller dice,
            IOptions<GameOptions> options,
            ILogger<GameService> logger)
        {
            _store = store;
            _notifier = notifier;
            _completion = completion;
            _actor = actor;
            _lock = gameLock;
            _dice = dice;
            _options = options.Value;
            _logger = logger;
        }

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        public Task<Guid> CreateGameAsync(CreateGameRequest request)
        {
            if (request.Players.Count < 2)
                throw new InvalidOperationException("A game needs at least 2 players.");

            var rng = Random.Shared;
            var seats = request.Players.OrderBy(_ => rng.Next()).ToList();

            var game = new Game
            {
                Id = Guid.NewGuid(),
                RoomId = request.RoomId,
                Status = GameStatus.InProgress,
                CurrentTurnUserId = seats[0].UserId,
                TurnNumber = 1,
                DiceSeed = rng.Next(),
                ChanceDeckOrder = ChanceDeck.NewShuffledOrder(rng),
                CreatedAt = DateTime.UtcNow,
                TurnDeadlineAt = DateTime.UtcNow.AddSeconds(_options.TurnSeconds)
            };

            for (var i = 0; i < seats.Count; i++)
            {
                game.Players.Add(new GamePlayer
                {
                    Id = Guid.NewGuid(),
                    GameId = game.Id,
                    UserId = seats[i].UserId,
                    FirstName = seats[i].FirstName,
                    LastName = seats[i].LastName,
                    TurnOrder = i,
                    ControlledBy = seats[i].IsBot ? SeatControl.Bot : SeatControl.Human,
                    Cash = BoardDefinition.StartingCash,
                    Position = BoardDefinition.StartIndex
                });
            }

            foreach (var tile in BoardDefinition.OwnableTiles)
            {
                game.Properties.Add(new GameProperty { Id = Guid.NewGuid(), GameId = game.Id, TileIndex = tile.Index });
            }

            _store.AddGame(game);

            Log(game, GameEventType.GameStarted);
            CaptureSnapshots(game, turn: 0);
            Log(game, GameEventType.TurnStarted, 0);

            _logger.LogInformation("Game {GameId} created for room {RoomId} with {Count} players", game.Id, request.RoomId, seats.Count);
            return Task.FromResult(game.Id);
        }

        public async Task HandlePlayerLeftAsync(Guid userId)
        {
            var gameId = await _store.GetActiveGameIdForUserAsync(userId, SeatControl.Human);
            if (gameId == null) return;

            await using var handle = await AcquireAsync(gameId.Value);

            var game = await _store.GetFullAsync(gameId.Value);
            if (game == null || game.Status != GameStatus.InProgress) return;

            var me = game.Players.FirstOrDefault(p => p.UserId == userId && p.ControlledBy == SeatControl.Human);
            if (me == null) return;

            var botsBefore = BotSeats(game);
            TakeOverSeat(game, me, reason: 0);

            // With no humans left there is nobody to play for: stop the game instead of letting bots play alone.
            if (!game.Players.Any(p => p.ControlledBy == SeatControl.Human && !p.IsBankrupt))
            {
                game.Status = GameStatus.Abandoned;
                game.FinishedAt = DateTime.UtcNow;
                game.TurnDeadlineAt = null;
                Log(game, GameEventType.GameAbandoned);
                CaptureSnapshots(game, game.TurnNumber);
            }

            await CommitAsync(game, botsBefore);
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
            // A seat handed to a bot is no longer the human's game ("no rejoin for now").
            var control = _actor.IsBotService ? SeatControl.Bot : SeatControl.Human;
            var game = await _store.GetActiveForUserAsync(userId, control);
            return game == null ? null : await MapStateAsync(game, userId);
        }

        public async Task<EventPageResponse> GetEventsAsync(Guid callerUserId, Guid gameId, int afterSequence, int limit)
        {
            if (!await _store.IsParticipantAsync(gameId, callerUserId))
                throw new InvalidOperationException("You were not a player in that game.");

            limit = Math.Clamp(limit, 1, MaxEventPageSize);

            // Ask for one extra row to learn whether another page exists without a second query.
            var rows = await _store.GetEventsAfterAsync(gameId, afterSequence, limit + 1);
            var hasMore = rows.Count > limit;
            var page = rows.Take(limit).ToList();

            return new EventPageResponse
            {
                Events = page.Select(ToWire).ToList(),
                NextAfterSequence = page.Count > 0 ? page[^1].Sequence : afterSequence,
                HasMore = hasMore
            };
        }

        public async Task<List<TurnSummaryResponse>> GetSummariesAsync(Guid callerUserId, Guid gameId, int? fromTurn, int? toTurn)
        {
            if (!await _store.IsParticipantAsync(gameId, callerUserId))
                throw new InvalidOperationException("You were not a player in that game.");

            var players = (await _store.GetPlayersAsync(gameId)).ToDictionary(p => p.TurnOrder);
            var snapshots = await _store.GetSnapshotsAsync(gameId, fromTurn, toTurn);

            return snapshots
                .GroupBy(s => s.Turn)
                .Select(g =>
                {
                    var first = g.First();
                    return new TurnSummaryResponse
                    {
                        Turn = g.Key,
                        EventSequence = first.EventSequence,
                        Die1 = first.Die1,
                        Die2 = first.Die2,
                        Players = g.Select(s => new PlayerSnapshotResponse
                        {
                            Seat = s.Seat,
                            UserId = players[s.Seat].UserId,
                            FirstName = players[s.Seat].FirstName,
                            Cash = s.Cash,
                            NetWorth = s.NetWorth,
                            Position = s.Position,
                            Properties = s.Properties,
                            Houses = s.Houses,
                            Hotels = s.Hotels,
                            Mortgaged = s.Mortgaged,
                            InJail = s.InJail,
                            IsBankrupt = s.IsBankrupt,
                            ControlledBy = s.ControlledBy.ToString()
                        }).ToList()
                    };
                })
                .ToList();
        }

        // ------------------------------------------------------------------
        // Turn actions
        // ------------------------------------------------------------------

        public async Task<RollResultResponse> RollAsync(Guid userId)
        {
            int d1 = 0, d2 = 0;
            Guid roomId = Guid.Empty;

            var state = await RunActionAsync(userId, (game, me) =>
            {
                roomId = game.RoomId;
                (d1, d2) = RollCore(game, me);
            });

            await _notifier.NotifyDiceRolledAsync(roomId, userId, d1, d2);
            return new RollResultResponse { Die1 = d1, Die2 = d2, IsDoubles = d1 == d2, State = state };
        }

        public Task<GameStateResponse> BuyPropertyAsync(Guid userId) =>
            RunActionAsync(userId, (game, me) =>
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

                Log(game, GameEventType.PropertyBought, me.TurnOrder, tileIndex, tile.Price);
            });

        public Task<GameStateResponse> PassPropertyAsync(Guid userId) =>
            RunActionAsync(userId, (game, me) => PassCore(game, me));

        public Task<GameStateResponse> EndTurnAsync(Guid userId) =>
            RunActionAsync(userId, (game, me) => EndTurnCore(game));

        public Task<GameStateResponse> PayJailFineAsync(Guid userId) =>
            RunActionAsync(userId, (game, me) =>
            {
                RequireNoOutstandingDecision(game);
                if (!me.InJail) throw new InvalidOperationException("You are not in Detention Cove.");
                if (game.HasRolled) throw new InvalidOperationException("You can only pay before rolling.");
                if (me.Cash < BoardDefinition.DetentionFine)
                    throw new InvalidOperationException($"You need ${BoardDefinition.DetentionFine} to pay the fine.");

                me.Cash -= BoardDefinition.DetentionFine;
                me.InJail = false;
                me.JailTurns = 0;
                Log(game, GameEventType.LeftDetention, me.TurnOrder, 1);
            });

        public Task<GameStateResponse> UseJailCardAsync(Guid userId) =>
            RunActionAsync(userId, (game, me) =>
            {
                RequireNoOutstandingDecision(game);
                if (!me.InJail) throw new InvalidOperationException("You are not in Detention Cove.");
                if (game.HasRolled) throw new InvalidOperationException("You can only use a card before rolling.");
                if (me.GetOutOfJailCards <= 0) throw new InvalidOperationException("You have no Harbour Pass card.");

                me.GetOutOfJailCards--;
                me.InJail = false;
                me.JailTurns = 0;
                Log(game, GameEventType.LeftDetention, me.TurnOrder, 2);
            });

        public Task<GameStateResponse> PayDebtAsync(Guid userId) =>
            RunActionAsync(userId, (game, me) =>
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
                    game.Players.First(p => p.UserId == creditorId).Cash += amount;

                Log(game, GameEventType.DebtPaid, me.TurnOrder, SeatOrBank(game, creditorId), amount);
            });

        public Task<GameStateResponse> DeclareBankruptcyAsync(Guid userId) =>
            RunActionAsync(userId, (game, me) => DeclareBankruptcyCore(game, me));

        // ------------------------------------------------------------------
        // The action pipeline
        // ------------------------------------------------------------------

        /// <summary>
        /// Runs one player action: lock -> load -> validate who is acting -> mutate -> save -> notify.
        /// <paramref name="requireTurn"/> is false for actions any player may take any time (trades).
        /// </summary>
        private async Task<GameStateResponse> RunActionAsync(Guid userId, Action<Game, GamePlayer> action, bool requireTurn = true)
        {
            // A bot service acts for bot-controlled seats only; a human only for their own human seat.
            var control = _actor.IsBotService ? SeatControl.Bot : SeatControl.Human;

            var gameId = await _store.GetActiveGameIdForUserAsync(userId, control)
                ?? throw new InvalidOperationException(control == SeatControl.Human
                    ? "You are not in a game."
                    : "That seat is not controlled by a computer player.");

            await using var handle = await AcquireAsync(gameId);

            // Load AFTER taking the lock so we always act on the latest committed state.
            var game = await _store.GetFullAsync(gameId)
                ?? throw new InvalidOperationException("Game not found.");

            if (game.Status != GameStatus.InProgress)
                throw new InvalidOperationException("This game has already finished.");

            var me = game.Players.FirstOrDefault(p => p.UserId == userId && p.ControlledBy == control)
                ?? throw new InvalidOperationException("You are not a player in this game.");

            if (me.IsBankrupt)
                throw new InvalidOperationException("You are bankrupt and out of the game.");

            if (requireTurn && game.CurrentTurnUserId != userId)
                throw new InvalidOperationException("It is not your turn.");

            var botsBefore = BotSeats(game);
            var cardBefore = game.LastChanceCardId;
            var turnBefore = game.TurnNumber;

            action(game, me);

            if (requireTurn && game.Status == GameStatus.InProgress)
            {
                // The player is active: forgive earlier missed turns and give the clock a fresh window.
                me.MissedTurns = 0;
                if (game.TurnNumber == turnBefore) Rearm(game);
            }

            await CommitAsync(game, botsBefore);

            if (game.Status == GameStatus.InProgress && game.LastChanceCardId != null && game.LastChanceCardId != cardBefore)
                await _notifier.NotifyCardDrawnAsync(game.RoomId, userId, ChanceDeck.Get(game.LastChanceCardId.Value));

            return await MapStateAsync(game, userId);
        }

        /// <summary>
        /// Final step of every mutation: if the game just ended, let the host clean up (same DbContext, so it commits
        /// atomically with the final state); save; then tell everyone.
        /// </summary>
        private async Task CommitAsync(Game game, HashSet<Guid> botsBefore)
        {
            if (game.Status != GameStatus.InProgress)
            {
                await _completion.OnGameEndedAsync(new GameEndedInfo(
                    game.RoomId, game.Id, game.Status == GameStatus.Abandoned, game.WinnerUserId,
                    game.Players.Select(p => p.UserId).ToList()));
            }

            try
            {
                await _store.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException(
                    "This action conflicted with another change happening at the same time. Please try again.");
            }

            foreach (var seat in game.Players.Where(p => p.ControlledBy == SeatControl.Bot && !botsBefore.Contains(p.UserId)))
                await _notifier.NotifyPlayerReplacedByBotAsync(game.RoomId, seat.UserId, seat.TurnOrder);

            if (game.Status != GameStatus.InProgress)
                await _notifier.NotifyGameFinishedAsync(game.RoomId, game.WinnerUserId, game.Status == GameStatus.Abandoned);

            await _notifier.NotifyGameUpdatedAsync(game.RoomId);
        }

        private async Task<IAsyncDisposable> AcquireAsync(Guid gameId)
        {
            var handle = await _lock.TryAcquireAsync(gameId, TimeSpan.FromSeconds(_options.LockWaitSeconds));
            return handle ?? throw new InvalidOperationException("The game is busy. Please try again.");
        }

        private static HashSet<Guid> BotSeats(Game game) =>
            game.Players.Where(p => p.ControlledBy == SeatControl.Bot).Select(p => p.UserId).ToHashSet();

        private void Rearm(Game game) => game.TurnDeadlineAt = DateTime.UtcNow.AddSeconds(_options.TurnSeconds);

        // ------------------------------------------------------------------
        // Rules: rolling and moving
        // ------------------------------------------------------------------

        private static void RequireNoOutstandingDecision(Game game)
        {
            if (game.PendingDebtAmount > 0)
                throw new InvalidOperationException("You owe money. Pay your debt or declare bankruptcy first.");
            if (game.PendingPurchaseTileIndex != null)
                throw new InvalidOperationException("Buy or pass on the property you landed on first.");
        }

        private (int Die1, int Die2) RollCore(Game game, GamePlayer me)
        {
            RequireNoOutstandingDecision(game);
            if (game.HasRolled) throw new InvalidOperationException("You have already rolled this turn.");

            var (d1, d2) = _dice.Roll(game.DiceSeed, game.RollCounter++);
            game.LastDie1 = d1;
            game.LastDie2 = d2;
            game.LastChanceCardId = null;
            var total = d1 + d2;
            var doubles = d1 == d2;

            Log(game, GameEventType.DiceRolled, me.TurnOrder, d1, d2);

            if (me.InJail)
            {
                ResolveRollInDetention(game, me, doubles, total);
                return (d1, d2);
            }

            if (doubles)
            {
                game.DoublesCount++;
                if (game.DoublesCount >= 3)
                {
                    SendToDetention(game, me, reason: 2);
                    return (d1, d2);
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
            return (d1, d2);
        }

        private void ResolveRollInDetention(Game game, GamePlayer me, bool doubles, int total)
        {
            if (doubles)
            {
                me.InJail = false;
                me.JailTurns = 0;
                game.DoublesCount = 0;
                game.HasRolled = true; // leaving Detention with doubles does not grant an extra roll
                Log(game, GameEventType.LeftDetention, me.TurnOrder, 0);
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
                Log(game, GameEventType.LeftDetention, me.TurnOrder, 3);
                Charge(game, me, BoardDefinition.DetentionFine, creditorUserId: null);
                MoveBy(game, me, total);
                ResolveLanding(game, me, total);
            }
            else
            {
                Log(game, GameEventType.DetentionFailed, me.TurnOrder, me.JailTurns);
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
            Log(game, GameEventType.PassedStart, me.TurnOrder, BoardDefinition.PassStartSalary);
        }

        private void SendToDetention(Game game, GamePlayer me, int reason)
        {
            me.Position = BoardDefinition.DetentionIndex;
            me.InJail = true;
            me.JailTurns = 0;
            game.DoublesCount = 0;
            game.HasRolled = true; // a trip to Detention always ends the turn
            game.PendingPurchaseTileIndex = null;
            Log(game, GameEventType.SentToDetention, me.TurnOrder, reason);
        }

        // ------------------------------------------------------------------
        // Rules: landing
        // ------------------------------------------------------------------

        private void ResolveLanding(Game game, GamePlayer me, int diceTotal)
        {
            var tile = BoardDefinition.Get(me.Position);
            Log(game, GameEventType.LandedOn, me.TurnOrder, tile.Index);

            switch (tile.Type)
            {
                case TileType.Property:
                case TileType.Station:
                case TileType.Utility:
                    ResolveOwnableLanding(game, me, tile, diceTotal);
                    break;

                case TileType.Tax:
                    if (Charge(game, me, tile.TaxAmount, creditorUserId: null))
                        Log(game, GameEventType.TaxPaid, me.TurnOrder, tile.Index, tile.TaxAmount);
                    break;

                case TileType.Chance:
                    DrawFortuneCard(game, me, diceTotal);
                    break;

                case TileType.GoToDetention:
                    SendToDetention(game, me, reason: 0);
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

            if (Charge(game, me, rent, owner.UserId))
                Log(game, GameEventType.RentPaid, me.TurnOrder, owner.TurnOrder, tile.Index, rent);
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
        /// Takes money from a player and returns true. If they cannot afford it, records a pending debt instead (and
        /// returns false) — they must raise cash (mortgage / sell) or declare bankruptcy before the turn can end.
        /// </summary>
        private bool Charge(Game game, GamePlayer payer, int amount, Guid? creditorUserId)
        {
            if (amount <= 0) return true;

            if (payer.Cash >= amount)
            {
                payer.Cash -= amount;
                if (creditorUserId != null)
                    game.Players.First(p => p.UserId == creditorUserId).Cash += amount;
                return true;
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

            Log(game, GameEventType.DebtIncurred, payer.TurnOrder, SeatOrBank(game, game.PendingDebtCreditorUserId), amount);
            return false;
        }

        private void DrawFortuneCard(Game game, GamePlayer me, int diceTotal)
        {
            var order = ChanceDeck.ParseOrder(game.ChanceDeckOrder);
            var card = ChanceDeck.Get(order[game.ChanceDeckPosition % order.Length]);
            game.ChanceDeckPosition = (game.ChanceDeckPosition + 1) % order.Length;
            game.LastChanceCardId = card.Id;

            Log(game, GameEventType.CardDrawn, me.TurnOrder, card.Id);

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
                    Log(game, GameEventType.CardCash, me.TurnOrder, card.Value);
                    break;

                case ChanceEffect.Pay:
                    if (Charge(game, me, card.Value, creditorUserId: null))
                        Log(game, GameEventType.CardCash, me.TurnOrder, -card.Value);
                    break;

                case ChanceEffect.GoToDetention:
                    SendToDetention(game, me, reason: 1);
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
                        Log(game, GameEventType.CardCash, me.TurnOrder, -total);
                        foreach (var other in others)
                        {
                            other.Cash += card.Value;
                            Log(game, GameEventType.CardCash, other.TurnOrder, card.Value);
                        }
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
                        Log(game, GameEventType.CardCash, other.TurnOrder, -paid);
                        Log(game, GameEventType.CardCash, me.TurnOrder, paid);
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Rules: property decisions, ending turns, bankruptcy
        // ------------------------------------------------------------------

        private void PassCore(Game game, GamePlayer me)
        {
            if (game.PendingPurchaseTileIndex is not int tileIndex)
                throw new InvalidOperationException("There is no property to pass on right now.");

            game.PendingPurchaseTileIndex = null;
            Log(game, GameEventType.PropertyPassed, me.TurnOrder, tileIndex);
        }

        private void EndTurnCore(Game game)
        {
            RequireNoOutstandingDecision(game);
            if (!game.HasRolled) throw new InvalidOperationException("Roll the dice before ending your turn.");

            AdvanceTurn(game);
        }

        private void DeclareBankruptcyCore(Game game, GamePlayer me)
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

            Log(game, GameEventType.Bankrupt, me.TurnOrder, creditor?.TurnOrder ?? -1);

            var remaining = game.Players.Where(p => !p.IsBankrupt).ToList();
            if (remaining.Count == 1)
                FinishGame(game, remaining[0]);
            else
                AdvanceTurn(game);
        }

        private void AdvanceTurn(Game game)
        {
            // Snapshot the table as the turn ends, BEFORE the next TurnStarted event is logged.
            CaptureSnapshots(game, game.TurnNumber);

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
            Rearm(game);

            Log(game, GameEventType.TurnStarted, next.TurnOrder);
        }

        private void FinishGame(Game game, GamePlayer winner)
        {
            game.Status = GameStatus.Finished;
            game.WinnerUserId = winner.UserId;
            game.FinishedAt = DateTime.UtcNow;
            game.TurnDeadlineAt = null;
            Log(game, GameEventType.GameWon, winner.TurnOrder);
            CaptureSnapshots(game, game.TurnNumber);
        }

        private void TakeOverSeat(Game game, GamePlayer seat, int reason)
        {
            seat.ControlledBy = SeatControl.Bot;
            seat.MissedTurns = 0;
            Log(game, GameEventType.SeatTakenOverByBot, seat.TurnOrder, reason);
        }

        // ------------------------------------------------------------------
        // Turn clock
        // ------------------------------------------------------------------

        public Task<List<Guid>> GetOverdueGameIdsAsync() =>
            _store.GetOverdueGameIdsAsync(DateTime.UtcNow, take: 25);

        /// <summary>
        /// Plays out the turn of a game whose clock has run out. The caller must use a FRESH scope (fresh DbContext)
        /// per game: if this throws part-way, the half-changed entities must not leak into the next game's save.
        /// </summary>
        public async Task ExpireTurnAsync(Guid gameId)
        {
            // Zero wait: if someone is already acting on this game, the clock is not really "expired" — skip it.
            await using var handle = await _lock.TryAcquireAsync(gameId, TimeSpan.Zero);
            if (handle == null) return;

            var game = await _store.GetFullAsync(gameId);

            // Re-check under the lock: the player may have acted between the scan and now.
            if (game == null || game.Status != GameStatus.InProgress ||
                game.TurnDeadlineAt == null || game.TurnDeadlineAt > DateTime.UtcNow)
                return;

            var me = game.Players.First(p => p.UserId == game.CurrentTurnUserId);
            var botsBefore = BotSeats(game);
            int d1 = 0, d2 = 0;
            Guid userId = me.UserId;

            Log(game, GameEventType.TurnTimedOut, me.TurnOrder);
            AutoPlayTurn(game, me, (a, b) => { d1 = a; d2 = b; });

            if (me.ControlledBy == SeatControl.Human)
            {
                me.MissedTurns++;
                if (me.MissedTurns >= _options.MissedTurnsBeforeBot && game.Status == GameStatus.InProgress && !me.IsBankrupt)
                    TakeOverSeat(game, me, reason: 1);
            }

            await CommitAsync(game, botsBefore);
            if (d1 != 0) await _notifier.NotifyDiceRolledAsync(game.RoomId, userId, d1, d2);
        }

        /// <summary>
        /// The safest possible turn for a player whose clock ran out: roll if needed, pass on any purchase,
        /// and end the turn. A debt it cannot settle ends in bankruptcy (no automatic mortgaging).
        /// </summary>
        private void AutoPlayTurn(Game game, GamePlayer me, Action<int, int> onRolled)
        {
            for (var guard = 0; guard < 12; guard++)
            {
                if (game.Status != GameStatus.InProgress || game.CurrentTurnUserId != me.UserId) return;

                if (game.PendingDebtAmount > 0)
                {
                    DeclareBankruptcyCore(game, me);
                    return;
                }

                if (game.PendingPurchaseTileIndex != null)
                {
                    PassCore(game, me);
                    continue;
                }

                if (!game.HasRolled)
                {
                    var (d1, d2) = RollCore(game, me);
                    onRolled(d1, d2);
                    continue;
                }

                EndTurnCore(game);
                return;
            }
        }

        // ------------------------------------------------------------------
        // Bot service support
        // ------------------------------------------------------------------

        public async Task<List<BotTurnResponse>> GetBotTurnsAsync()
        {
            var result = new List<BotTurnResponse>();

            foreach (var gameId in await _store.GetGamesWithBotSeatsAsync())
            {
                var game = await _store.GetFullAsync(gameId);
                if (game == null) continue;

                foreach (var seat in game.Players.Where(p => p.ControlledBy == SeatControl.Bot && !p.IsBankrupt))
                {
                    var incoming = game.Trades
                        .Where(t => t.Status == TradeStatus.Pending && t.RecipientUserId == seat.UserId)
                        .Select(t => t.Id)
                        .ToList();

                    var isTurn = game.CurrentTurnUserId == seat.UserId;
                    if (!isTurn && incoming.Count == 0) continue;

                    result.Add(new BotTurnResponse
                    {
                        GameId = game.Id,
                        UserId = seat.UserId,
                        State = await MapStateAsync(game, seat.UserId),
                        IncomingTradeIds = incoming
                    });
                }
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Activity log and snapshots
        // ------------------------------------------------------------------

        /// <summary>Appends an event: a type plus positional integer args (see GameEventSchema for their meaning).</summary>
        private void Log(Game game, GameEventType type, params int[] args)
        {
            game.EventCounter++;
            _store.AddEvent(new GameEvent
            {
                Id = Guid.NewGuid(),
                GameId = game.Id,
                Sequence = game.EventCounter,
                Turn = game.TurnNumber,
                Type = (int)type,
                Args = args
            });
        }

        private static int[] ToWire(GameEvent e) =>
            new[] { e.Sequence, e.Turn, e.Type }.Concat(e.Args).ToArray();

        private static int SeatOrBank(Game game, Guid? userId) =>
            userId == null ? -1 : game.Players.First(p => p.UserId == userId).TurnOrder;

        private void CaptureSnapshots(Game game, int turn)
        {
            foreach (var p in game.Players)
            {
                var owned = game.Properties.Where(r => r.OwnerUserId == p.UserId).ToList();
                var netWorth = p.Cash;
                var houses = 0;
                var hotels = 0;

                foreach (var row in owned)
                {
                    var tile = BoardDefinition.Get(row.TileIndex);
                    netWorth += row.IsMortgaged ? tile.Price / 2 : tile.Price;
                    netWorth += row.Houses * tile.HouseCost; // a hotel counts as 5 building levels
                    if (row.Houses == 5) hotels++;
                    else houses += row.Houses;
                }

                _store.AddSnapshot(new GameSnapshot
                {
                    Id = Guid.NewGuid(),
                    GameId = game.Id,
                    Turn = turn,
                    Seat = p.TurnOrder,
                    EventSequence = game.EventCounter,
                    Die1 = game.LastDie1,
                    Die2 = game.LastDie2,
                    RollCounter = game.RollCounter,
                    Cash = p.Cash,
                    NetWorth = netWorth,
                    Position = p.Position,
                    Properties = owned.Count,
                    Houses = houses,
                    Hotels = hotels,
                    Mortgaged = owned.Count(r => r.IsMortgaged),
                    InJail = p.InJail,
                    IsBankrupt = p.IsBankrupt,
                    ControlledBy = p.ControlledBy
                });
            }
        }

        // ------------------------------------------------------------------
        // Mapping
        // ------------------------------------------------------------------

        private async Task<GameStateResponse> MapStateAsync(Game game, Guid callerUserId)
        {
            var events = await _store.GetRecentEventsAsync(game.Id, RecentEventCount);

            var players = game.Players.OrderBy(p => p.TurnOrder).Select(p => new GamePlayerResponse
            {
                UserId = p.UserId,
                FirstName = p.FirstName,
                LastName = p.LastName,
                Seat = p.TurnOrder,
                Cash = p.Cash,
                Position = p.Position,
                InJail = p.InJail,
                GetOutOfJailCards = p.GetOutOfJailCards,
                IsBankrupt = p.IsBankrupt,
                IsCurrentTurn = game.Status == GameStatus.InProgress && p.UserId == game.CurrentTurnUserId,
                ControlledBy = p.ControlledBy.ToString(),
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
                TurnDeadlineAt = game.TurnDeadlineAt,
                ServerTime = DateTime.UtcNow,
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
                RecentEvents = events.Select(ToWire).ToList(),
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
