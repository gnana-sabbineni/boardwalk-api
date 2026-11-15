using BoardWalk.Monopoly.Contracts;
using BoardWalk.Monopoly.Models;
using BoardWalk.Monopoly.Rules;

namespace BoardWalk.Monopoly.Services
{
    // Player-level actions: managing properties (build / sell / mortgage), trading, and the properties panel.
    public partial class GameService
    {
        /// <summary>Which kind of seat the current caller may see/act as: the bot service acts for bot seats, people for human seats.</summary>
        private SeatControl CallerControl => _actor.IsBotService ? SeatControl.Bot : SeatControl.Human;

        // ------------------------------------------------------------------
        // Properties panel
        // ------------------------------------------------------------------

        public async Task<PlayerPropertiesResponse> GetPlayerPropertiesAsync(Guid callerUserId, Guid targetUserId)
        {
            var game = await _store.GetActiveForUserAsync(callerUserId, CallerControl)
                ?? throw new InvalidOperationException("You are not in a game.");

            var target = game.Players.FirstOrDefault(p => p.UserId == targetUserId)
                ?? throw new InvalidOperationException("That player is not in your game.");

            var owned = game.Properties.Where(r => r.OwnerUserId == targetUserId).OrderBy(r => r.TileIndex).ToList();

            var groups = owned
                .GroupBy(r => GroupKey(BoardDefinition.Get(r.TileIndex)))
                .Select(g => new PropertyGroupResponse
                {
                    Group = g.Key,
                    OwnsWholeGroup = g.Key is not ("station" or "utility") && OwnsWholeGroup(game, targetUserId, g.Key),
                    Properties = g.Select(r =>
                    {
                        var tile = BoardDefinition.Get(r.TileIndex);
                        return new OwnedPropertyResponse
                        {
                            TileIndex = r.TileIndex,
                            Name = tile.Name,
                            Price = tile.Price,
                            HouseCost = tile.HouseCost,
                            Houses = r.Houses,
                            IsMortgaged = r.IsMortgaged,
                            CurrentRent = r.IsMortgaged ? 0 : tile.Type == TileType.Utility ? 0 : CalculateRent(game, r, tile, diceTotal: 0)
                        };
                    }).ToList()
                })
                .OrderBy(g => g.Group)
                .ToList();

            return new PlayerPropertiesResponse
            {
                UserId = target.UserId,
                FirstName = target.FirstName,
                LastName = target.LastName,
                Cash = target.Cash,
                IsBankrupt = target.IsBankrupt,
                TotalHouses = owned.Count(r => r.Houses is > 0 and < 5),
                TotalHotels = owned.Count(r => r.Houses == 5),
                CanManage = game.Status == GameStatus.InProgress && !target.IsBankrupt &&
                            callerUserId == targetUserId && game.CurrentTurnUserId == callerUserId,
                Groups = groups
            };
        }

        private static string GroupKey(Tile tile) => tile.Type switch
        {
            TileType.Station => "station",
            TileType.Utility => "utility",
            _ => tile.ColorGroup!
        };

        // ------------------------------------------------------------------
        // Build / sell / mortgage
        // ------------------------------------------------------------------

        public Task<GameStateResponse> BuildAsync(Guid userId, int tileIndex) =>
            RunActionAsync(userId, (game, me) =>
            {
                var (row, tile) = RequireOwnedStreet(game, me, tileIndex);

                if (game.PendingDebtAmount > 0)
                    throw new InvalidOperationException("Settle your debt before building.");
                if (row.IsMortgaged)
                    throw new InvalidOperationException("Lift the mortgage on this property before building.");
                if (!OwnsWholeGroup(game, me.UserId, tile.ColorGroup!))
                    throw new InvalidOperationException("You need every property in this colour group to build.");

                var group = GroupRows(game, tile);
                if (group.Any(r => r.IsMortgaged))
                    throw new InvalidOperationException("You cannot build while a property in this group is mortgaged.");
                if (row.Houses >= 5)
                    throw new InvalidOperationException("This property already has a hotel.");
                if (row.Houses > group.Min(r => r.Houses))
                    throw new InvalidOperationException("Build evenly: add to your least-developed property in this group first.");
                if (me.Cash < tile.HouseCost)
                    throw new InvalidOperationException($"You need ${tile.HouseCost} to build here.");

                me.Cash -= tile.HouseCost;
                row.Houses++;

                Log(game, GameEventType.Built, me.TurnOrder, tileIndex, row.Houses, tile.HouseCost);
            });

        public Task<GameStateResponse> SellBuildingAsync(Guid userId, int tileIndex) =>
            RunActionAsync(userId, (game, me) =>
            {
                var (row, tile) = RequireOwnedStreet(game, me, tileIndex);

                if (row.Houses <= 0)
                    throw new InvalidOperationException("There is nothing to sell on this property.");

                var group = GroupRows(game, tile);
                if (row.Houses < group.Max(r => r.Houses))
                    throw new InvalidOperationException("Sell evenly: sell from your most-developed property in this group first.");

                var refund = tile.HouseCost / 2;
                row.Houses--;
                me.Cash += refund;

                Log(game, GameEventType.BuildingSold, me.TurnOrder, tileIndex, row.Houses, refund);
            });

        public Task<GameStateResponse> MortgageAsync(Guid userId, int tileIndex) =>
            RunActionAsync(userId, (game, me) =>
            {
                var (row, tile) = RequireOwned(game, me, tileIndex);

                if (row.IsMortgaged)
                    throw new InvalidOperationException("This property is already mortgaged.");

                if (tile.Type == TileType.Property && GroupRows(game, tile).Any(r => r.Houses > 0))
                    throw new InvalidOperationException("Sell all buildings in this colour group before mortgaging.");

                var amount = tile.Price / 2;
                row.IsMortgaged = true;
                me.Cash += amount;

                Log(game, GameEventType.Mortgaged, me.TurnOrder, tileIndex, amount);
            });

        public Task<GameStateResponse> UnmortgageAsync(Guid userId, int tileIndex) =>
            RunActionAsync(userId, (game, me) =>
            {
                var (row, tile) = RequireOwned(game, me, tileIndex);

                if (!row.IsMortgaged)
                    throw new InvalidOperationException("This property is not mortgaged.");

                var cost = UnmortgageCost(tile);
                if (me.Cash < cost)
                    throw new InvalidOperationException($"You need ${cost} to lift this mortgage.");

                me.Cash -= cost;
                row.IsMortgaged = false;

                Log(game, GameEventType.Unmortgaged, me.TurnOrder, tileIndex, cost);
            });

        /// <summary>Half the price plus 10% interest on that half.</summary>
        private static int UnmortgageCost(Tile tile)
        {
            var half = tile.Price / 2;
            return half + half * BoardDefinition.MortgageInterestPercent / 100;
        }

        private static (GameProperty Row, Tile Tile) RequireOwned(Game game, GamePlayer me, int tileIndex)
        {
            if (tileIndex < 0 || tileIndex >= BoardDefinition.TileCount || !BoardDefinition.Get(tileIndex).IsOwnable)
                throw new InvalidOperationException("That tile cannot be owned.");

            var row = game.Properties.First(p => p.TileIndex == tileIndex);
            if (row.OwnerUserId != me.UserId)
                throw new InvalidOperationException("You do not own that property.");

            return (row, BoardDefinition.Get(tileIndex));
        }

        private static (GameProperty Row, Tile Tile) RequireOwnedStreet(Game game, GamePlayer me, int tileIndex)
        {
            var (row, tile) = RequireOwned(game, me, tileIndex);
            if (tile.Type != TileType.Property)
                throw new InvalidOperationException("Only streets can have buildings.");
            return (row, tile);
        }

        private static List<GameProperty> GroupRows(Game game, Tile tile) =>
            BoardDefinition.InGroup(tile.ColorGroup!)
                .Select(t => game.Properties.First(p => p.TileIndex == t.Index))
                .ToList();

        // ------------------------------------------------------------------
        // Trades
        // ------------------------------------------------------------------

        public async Task<List<TradeResponse>> GetTradesAsync(Guid userId)
        {
            var game = await _store.GetActiveForUserAsync(userId, CallerControl)
                ?? throw new InvalidOperationException("You are not in a game.");

            return game.Trades
                .Where(t => t.Status == TradeStatus.Pending &&
                            (t.ProposerUserId == userId || t.RecipientUserId == userId))
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => MapTrade(game, t))
                .ToList();
        }

        public async Task<TradeResponse> ProposeTradeAsync(Guid userId, ProposeTradeRequest request)
        {
            TradeResponse? result = null;
            Guid roomId = Guid.Empty;

            await RunActionAsync(userId, (game, me) =>
            {
                roomId = game.RoomId;

                var recipient = game.Players.FirstOrDefault(p => p.UserId == request.RecipientUserId)
                    ?? throw new InvalidOperationException("That player is not in your game.");
                if (recipient.UserId == me.UserId)
                    throw new InvalidOperationException("You cannot trade with yourself.");
                if (recipient.IsBankrupt)
                    throw new InvalidOperationException("That player is bankrupt.");

                var offered = request.OfferedTileIndexes.Distinct().ToList();
                var requested = request.RequestedTileIndexes.Distinct().ToList();

                if (offered.Count == 0 && requested.Count == 0 && request.OfferCash == 0 && request.RequestCash == 0)
                    throw new InvalidOperationException("A trade must include at least a property or some cash.");

                if (game.Trades.Any(t => t.Status == TradeStatus.Pending &&
                        t.ProposerUserId == me.UserId && t.RecipientUserId == recipient.UserId))
                    throw new InvalidOperationException("You already have a pending trade with this player. Cancel it first.");

                ValidateTrade(game, me, recipient, offered, requested, request.OfferCash, request.RequestCash, checkRecipientCash: false);

                var trade = new Trade
                {
                    Id = Guid.NewGuid(),
                    GameId = game.Id,
                    ProposerUserId = me.UserId,
                    RecipientUserId = recipient.UserId,
                    OfferCash = request.OfferCash,
                    RequestCash = request.RequestCash,
                    Status = TradeStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                foreach (var tile in offered)
                    trade.Items.Add(new TradeItem { Id = Guid.NewGuid(), TradeId = trade.Id, TileIndex = tile, FromProposer = true });
                foreach (var tile in requested)
                    trade.Items.Add(new TradeItem { Id = Guid.NewGuid(), TradeId = trade.Id, TileIndex = tile, FromProposer = false });

                // Registered explicitly as Added so EF always INSERTs the trade and its items.
                _store.AddTrade(trade);
                game.Trades.Add(trade);

                Log(game, GameEventType.TradeProposed, me.TurnOrder, recipient.TurnOrder,
                    request.OfferCash, request.RequestCash, offered.Count, requested.Count);
                result = MapTrade(game, trade);
            }, requireTurn: false);

            await _notifier.NotifyTradeUpdatedAsync(roomId);
            return result!;
        }

        public async Task<GameStateResponse> AcceptTradeAsync(Guid userId, Guid tradeId)
        {
            Guid roomId = Guid.Empty;

            var state = await RunActionAsync(userId, (game, me) =>
            {
                roomId = game.RoomId;

                var trade = RequirePendingTrade(game, tradeId);
                if (trade.RecipientUserId != me.UserId)
                    throw new InvalidOperationException("Only the player who received this trade can accept it.");

                var proposer = game.Players.First(p => p.UserId == trade.ProposerUserId);
                if (proposer.IsBankrupt)
                    throw new InvalidOperationException("The player who proposed this trade is no longer in the game.");

                var offered = trade.Items.Where(i => i.FromProposer).Select(i => i.TileIndex).ToList();
                var requested = trade.Items.Where(i => !i.FromProposer).Select(i => i.TileIndex).ToList();

                // Re-validate everything NOW: ownership, buildings and cash may have changed since it was proposed.
                ValidateTrade(game, proposer, me, offered, requested, trade.OfferCash, trade.RequestCash, checkRecipientCash: true);

                foreach (var tile in offered)
                    game.Properties.First(p => p.TileIndex == tile).OwnerUserId = me.UserId;
                foreach (var tile in requested)
                    game.Properties.First(p => p.TileIndex == tile).OwnerUserId = proposer.UserId;

                proposer.Cash += trade.RequestCash - trade.OfferCash;
                me.Cash += trade.OfferCash - trade.RequestCash;

                trade.Status = TradeStatus.Accepted;
                trade.RespondedAt = DateTime.UtcNow;

                // Any other pending trade that involved one of these properties is now stale.
                var moved = offered.Concat(requested).ToHashSet();
                foreach (var other in game.Trades.Where(t => t.Id != trade.Id && t.Status == TradeStatus.Pending &&
                             t.Items.Any(i => moved.Contains(i.TileIndex))))
                {
                    other.Status = TradeStatus.Cancelled;
                    other.RespondedAt = DateTime.UtcNow;
                }

                Log(game, GameEventType.TradeAccepted, proposer.TurnOrder, me.TurnOrder);
            }, requireTurn: false);

            await _notifier.NotifyTradeUpdatedAsync(roomId);
            return state;
        }

        public async Task<TradeResponse> DeclineTradeAsync(Guid userId, Guid tradeId)
        {
            TradeResponse? result = null;
            Guid roomId = Guid.Empty;

            await RunActionAsync(userId, (game, me) =>
            {
                roomId = game.RoomId;

                var trade = RequirePendingTrade(game, tradeId);
                if (trade.RecipientUserId != me.UserId)
                    throw new InvalidOperationException("Only the player who received this trade can decline it.");

                trade.Status = TradeStatus.Declined;
                trade.RespondedAt = DateTime.UtcNow;

                var proposer = game.Players.First(p => p.UserId == trade.ProposerUserId);
                Log(game, GameEventType.TradeDeclined, proposer.TurnOrder, me.TurnOrder);
                result = MapTrade(game, trade);
            }, requireTurn: false);

            await _notifier.NotifyTradeUpdatedAsync(roomId);
            return result!;
        }

        public async Task<TradeResponse> CancelTradeAsync(Guid userId, Guid tradeId)
        {
            TradeResponse? result = null;
            Guid roomId = Guid.Empty;

            await RunActionAsync(userId, (game, me) =>
            {
                roomId = game.RoomId;

                var trade = RequirePendingTrade(game, tradeId);
                if (trade.ProposerUserId != me.UserId)
                    throw new InvalidOperationException("Only the player who proposed this trade can cancel it.");

                trade.Status = TradeStatus.Cancelled;
                trade.RespondedAt = DateTime.UtcNow;

                var recipient = game.Players.First(p => p.UserId == trade.RecipientUserId);
                Log(game, GameEventType.TradeCancelled, me.TurnOrder, recipient.TurnOrder);
                result = MapTrade(game, trade);
            }, requireTurn: false);

            await _notifier.NotifyTradeUpdatedAsync(roomId);
            return result!;
        }

        private static Trade RequirePendingTrade(Game game, Guid tradeId)
        {
            var trade = game.Trades.FirstOrDefault(t => t.Id == tradeId)
                ?? throw new InvalidOperationException("Trade not found, or it is no longer pending.");
            if (trade.Status != TradeStatus.Pending)
                throw new InvalidOperationException("This trade is no longer pending.");
            return trade;
        }

        /// <summary>
        /// Checks that a trade is legal right now. Rules: each side must own what it gives, tradable properties
        /// are not mortgaged and have no buildings anywhere in their colour group, and each side can afford its cash.
        /// </summary>
        private static void ValidateTrade(
            Game game, GamePlayer proposer, GamePlayer recipient,
            List<int> offered, List<int> requested,
            int offerCash, int requestCash, bool checkRecipientCash)
        {
            if (offerCash < 0 || requestCash < 0)
                throw new InvalidOperationException("Cash amounts cannot be negative.");
            if (proposer.Cash < offerCash)
                throw new InvalidOperationException($"{proposer.FirstName} does not have ${offerCash} to offer.");
            if (checkRecipientCash && recipient.Cash < requestCash)
                throw new InvalidOperationException($"{recipient.FirstName} does not have ${requestCash} to give.");

            if (offered.Intersect(requested).Any())
                throw new InvalidOperationException("The same property cannot be on both sides of a trade.");

            foreach (var tileIndex in offered) RequireTradable(game, proposer, tileIndex);
            foreach (var tileIndex in requested) RequireTradable(game, recipient, tileIndex);
        }

        private static void RequireTradable(Game game, GamePlayer owner, int tileIndex)
        {
            if (tileIndex < 0 || tileIndex >= BoardDefinition.TileCount || !BoardDefinition.Get(tileIndex).IsOwnable)
                throw new InvalidOperationException("That tile cannot be traded.");

            var tile = BoardDefinition.Get(tileIndex);
            var row = game.Properties.First(p => p.TileIndex == tileIndex);

            if (row.OwnerUserId != owner.UserId)
                throw new InvalidOperationException($"{owner.FirstName} does not own {tile.Name}.");
            if (row.IsMortgaged)
                throw new InvalidOperationException($"{tile.Name} is mortgaged. Lift the mortgage before trading it.");
            if (tile.Type == TileType.Property && GroupRows(game, tile).Any(r => r.Houses > 0))
                throw new InvalidOperationException($"{tile.Name} is in a colour group with buildings. Sell the buildings before trading.");
        }

        private static TradeResponse MapTrade(Game game, Trade t)
        {
            var proposer = game.Players.First(p => p.UserId == t.ProposerUserId);
            var recipient = game.Players.First(p => p.UserId == t.RecipientUserId);

            return new TradeResponse
            {
                Id = t.Id,
                ProposerUserId = t.ProposerUserId,
                ProposerName = proposer.FirstName,
                RecipientUserId = t.RecipientUserId,
                RecipientName = recipient.FirstName,
                OfferCash = t.OfferCash,
                RequestCash = t.RequestCash,
                OfferedTileIndexes = t.Items.Where(i => i.FromProposer).Select(i => i.TileIndex).OrderBy(i => i).ToList(),
                RequestedTileIndexes = t.Items.Where(i => !i.FromProposer).Select(i => i.TileIndex).OrderBy(i => i).ToList(),
                Status = t.Status.ToString(),
                CreatedAt = t.CreatedAt
            };
        }
    }
}
