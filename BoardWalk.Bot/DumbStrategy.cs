using BoardWalk.Monopoly.Contracts;

namespace BoardWalk.Bot
{
    /// <summary>One thing the bot decided to do: the API path (under /api/games/) and a short reason for the logs.</summary>
    public sealed record BotAction(string Path, string Reason);

    /// <summary>
    /// The simplest possible player. Rules, in order:
    ///  1. Decline every trade offer.
    ///  2. Owing money: pay if it can; otherwise mortgage its cheapest property; if nothing is left to mortgage, go bankrupt.
    ///  3. Landed on an unowned property: buy it if it has the cash; otherwise, if it already owns another street of the same colour,
    ///     mortgage other properties to raise the cash and then buy; otherwise pass.
    ///  4. In Detention: use a Harbour Pass if it has one, otherwise just roll (it never pays the fine).
    ///  5. Otherwise roll, then end the turn. It never builds and never proposes trades.
    /// It returns ONE action at a time; the worker re-reads the game after each, so decisions always use fresh state.
    /// </summary>
    public sealed class DumbStrategy
    {
        public BotAction? NextAction(BotTurnResponse turn, BoardResponse board)
        {
            var state = turn.State;

            // 1. Trade offers are answered even when it is not the bot's turn.
            if (turn.IncomingTradeIds.Count > 0)
                return new BotAction($"trades/{turn.IncomingTradeIds[0]}/decline", "declining a trade offer");

            var me = state.Players.FirstOrDefault(p => p.UserId == turn.UserId);
            if (me == null || me.IsBankrupt || !me.IsCurrentTurn) return null;

            var actions = state.AvailableActions;

            // 2. Debt.
            if (state.PendingDebt != null)
            {
                if (actions.Contains("payDebt"))
                    return new BotAction("debt/pay", $"paying a ${state.PendingDebt.Amount} debt");

                var toMortgage = CheapestMortgageable(state, board, me, excludeGroup: null);
                if (toMortgage != null)
                    return new BotAction($"properties/{toMortgage.Index}/mortgage", $"mortgaging {toMortgage.Name} to pay a debt");

                return new BotAction("bankruptcy", "cannot pay its debt");
            }

            // 3. Buying decision.
            if (state.PendingPurchaseTileIndex is int tileIndex)
            {
                var tile = board.Tiles[tileIndex];

                if (me.Cash >= tile.Price)
                    return new BotAction("buy", $"buying {tile.Name} for ${tile.Price}");

                if (tile.ColorGroup != null && OwnsAnotherInGroup(state, board, me, tile))
                {
                    var shortfall = tile.Price - me.Cash;
                    var raisable = MortgageableTiles(state, board, me, excludeGroup: tile.ColorGroup)
                        .Sum(t => t.Price / 2);

                    if (raisable >= shortfall)
                    {
                        var next = CheapestMortgageable(state, board, me, excludeGroup: tile.ColorGroup)!;
                        return new BotAction($"properties/{next.Index}/mortgage",
                            $"mortgaging {next.Name} to afford {tile.Name} and complete its colour set");
                    }
                }

                return new BotAction("pass", $"passing on {tile.Name}");
            }

            // 4 and 5. Detention, rolling, ending the turn.
            if (me.InJail && actions.Contains("useJailCard"))
                return new BotAction("jail/card", "using a Harbour Pass");

            if (actions.Contains("roll"))
                return new BotAction("roll", "rolling the dice");

            if (actions.Contains("endTurn"))
                return new BotAction("end-turn", "ending its turn");

            return null;
        }

        private static bool OwnsAnotherInGroup(GameStateResponse state, BoardResponse board, GamePlayerResponse me, TileResponse tile) =>
            me.OwnedTileIndexes.Any(i => i != tile.Index && board.Tiles[i].ColorGroup == tile.ColorGroup);

        /// <summary>Owned, not yet mortgaged, no buildings, and not in the excluded colour group.</summary>
        private static List<TileResponse> MortgageableTiles(GameStateResponse state, BoardResponse board, GamePlayerResponse me, string? excludeGroup)
        {
            var rows = state.Properties.ToDictionary(p => p.TileIndex);

            return me.OwnedTileIndexes
                .Where(i => rows.TryGetValue(i, out var row) && !row.IsMortgaged && row.Houses == 0)
                .Select(i => board.Tiles[i])
                .Where(t => excludeGroup == null || t.ColorGroup != excludeGroup)
                .ToList();
        }

        private static TileResponse? CheapestMortgageable(GameStateResponse state, BoardResponse board, GamePlayerResponse me, string? excludeGroup) =>
            MortgageableTiles(state, board, me, excludeGroup).OrderBy(t => t.Price).FirstOrDefault();
    }
}
