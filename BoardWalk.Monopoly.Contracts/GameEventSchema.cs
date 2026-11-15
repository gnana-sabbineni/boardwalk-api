namespace BoardWalk.Monopoly.Contracts
{
    /// <summary>
    /// Every kind of activity-log event. The numeric value is what is stored and sent, so:
    /// NEVER renumber or reuse a value. Add new types at the end.
    /// </summary>
    public enum GameEventType
    {
        GameStarted = 1,
        TurnStarted = 2,
        DiceRolled = 3,
        PassedStart = 4,
        LandedOn = 5,
        PropertyBought = 6,
        PropertyPassed = 7,
        RentPaid = 8,
        TaxPaid = 9,
        CardDrawn = 10,
        CardCash = 11,
        SentToDetention = 12,
        LeftDetention = 13,
        DetentionFailed = 14,
        DebtIncurred = 15,
        DebtPaid = 16,
        Built = 17,
        BuildingSold = 18,
        Mortgaged = 19,
        Unmortgaged = 20,
        TradeProposed = 21,
        TradeAccepted = 22,
        TradeDeclined = 23,
        TradeCancelled = 24,
        Bankrupt = 25,
        GameWon = 26,
        SeatTakenOverByBot = 27,
        TurnTimedOut = 28,
        GameAbandoned = 29
    }

    public sealed record GameEventSchemaEntry(int Type, string Name, string[] Args);

    /// <summary>
    /// The meaning of each position in an event's args. Served at GET /api/games/events/schema so clients
    /// can decode the compact [sequence, turn, type, ...args] arrays. A "seat" is a player's turn-order number;
    /// -1 as a seat means "the bank".
    /// </summary>
    public static class GameEventSchema
    {
        public static readonly IReadOnlyList<GameEventSchemaEntry> Entries = new List<GameEventSchemaEntry>
        {
            new((int)GameEventType.GameStarted,        nameof(GameEventType.GameStarted),        Array.Empty<string>()),
            new((int)GameEventType.TurnStarted,        nameof(GameEventType.TurnStarted),        new[] { "seat" }),
            new((int)GameEventType.DiceRolled,         nameof(GameEventType.DiceRolled),         new[] { "seat", "die1", "die2" }),
            new((int)GameEventType.PassedStart,        nameof(GameEventType.PassedStart),        new[] { "seat", "amount" }),
            new((int)GameEventType.LandedOn,           nameof(GameEventType.LandedOn),           new[] { "seat", "tile" }),
            new((int)GameEventType.PropertyBought,     nameof(GameEventType.PropertyBought),     new[] { "seat", "tile", "price" }),
            new((int)GameEventType.PropertyPassed,     nameof(GameEventType.PropertyPassed),     new[] { "seat", "tile" }),
            new((int)GameEventType.RentPaid,           nameof(GameEventType.RentPaid),           new[] { "payerSeat", "ownerSeat", "tile", "amount" }),
            new((int)GameEventType.TaxPaid,            nameof(GameEventType.TaxPaid),            new[] { "seat", "tile", "amount" }),
            new((int)GameEventType.CardDrawn,          nameof(GameEventType.CardDrawn),          new[] { "seat", "cardId" }),
            new((int)GameEventType.CardCash,           nameof(GameEventType.CardCash),           new[] { "seat", "amountSigned" }),
            new((int)GameEventType.SentToDetention,    nameof(GameEventType.SentToDetention),    new[] { "seat", "reason" }),          // 0 tile, 1 card, 2 three doubles
            new((int)GameEventType.LeftDetention,      nameof(GameEventType.LeftDetention),      new[] { "seat", "method" }),          // 0 doubles, 1 fine, 2 card, 3 forced fine
            new((int)GameEventType.DetentionFailed,    nameof(GameEventType.DetentionFailed),    new[] { "seat", "attempt" }),
            new((int)GameEventType.DebtIncurred,       nameof(GameEventType.DebtIncurred),       new[] { "seat", "creditorSeat", "amount" }),
            new((int)GameEventType.DebtPaid,           nameof(GameEventType.DebtPaid),           new[] { "seat", "creditorSeat", "amount" }),
            new((int)GameEventType.Built,              nameof(GameEventType.Built),              new[] { "seat", "tile", "housesAfter", "cost" }),
            new((int)GameEventType.BuildingSold,       nameof(GameEventType.BuildingSold),       new[] { "seat", "tile", "housesAfter", "refund" }),
            new((int)GameEventType.Mortgaged,          nameof(GameEventType.Mortgaged),          new[] { "seat", "tile", "amount" }),
            new((int)GameEventType.Unmortgaged,        nameof(GameEventType.Unmortgaged),        new[] { "seat", "tile", "cost" }),
            new((int)GameEventType.TradeProposed,      nameof(GameEventType.TradeProposed),      new[] { "proposerSeat", "recipientSeat", "offerCash", "requestCash", "offeredCount", "requestedCount" }),
            new((int)GameEventType.TradeAccepted,      nameof(GameEventType.TradeAccepted),      new[] { "proposerSeat", "recipientSeat" }),
            new((int)GameEventType.TradeDeclined,      nameof(GameEventType.TradeDeclined),      new[] { "proposerSeat", "recipientSeat" }),
            new((int)GameEventType.TradeCancelled,     nameof(GameEventType.TradeCancelled),     new[] { "proposerSeat", "recipientSeat" }),
            new((int)GameEventType.Bankrupt,           nameof(GameEventType.Bankrupt),           new[] { "seat", "creditorSeat" }),
            new((int)GameEventType.GameWon,            nameof(GameEventType.GameWon),            new[] { "seat" }),
            new((int)GameEventType.SeatTakenOverByBot, nameof(GameEventType.SeatTakenOverByBot), new[] { "seat", "reason" }),          // 0 left the game, 1 missed turns
            new((int)GameEventType.TurnTimedOut,       nameof(GameEventType.TurnTimedOut),       new[] { "seat" }),
            new((int)GameEventType.GameAbandoned,      nameof(GameEventType.GameAbandoned),      Array.Empty<string>()),
        };
    }
}
