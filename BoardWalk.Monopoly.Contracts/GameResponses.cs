namespace BoardWalk.Monopoly.Contracts
{
    // ---------- Static board description (same for every game) ----------

    public class TileResponse
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        /// <summary>Start, Property, Station, Utility, Tax, Chance, Detention, RestStop, GoToDetention.</summary>
        public string Type { get; set; } = string.Empty;
        /// <summary>sand, seafoam, coral, amber, orchid, sky, rose, indigo — null for non-street tiles.</summary>
        public string? ColorGroup { get; set; }
        public int Price { get; set; }
        public int HouseCost { get; set; }
        public int TaxAmount { get; set; }
        /// <summary>[base, 1 house, 2, 3, 4, hotel]. Empty for non-street tiles.</summary>
        public int[] RentTiers { get; set; } = Array.Empty<int>();
    }

    public class BoardResponse
    {
        public List<TileResponse> Tiles { get; set; } = new();
        public int PassStartSalary { get; set; }
        public int StartingCash { get; set; }
        public int DetentionFine { get; set; }
    }

    // ---------- Live game state ----------

    public class GamePlayerResponse
    {
        public Guid UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        /// <summary>Seat number (0-based turn order). Event args refer to players by this number.</summary>
        public int Seat { get; set; }
        public int Cash { get; set; }
        public int Position { get; set; }
        public bool InJail { get; set; }
        public int GetOutOfJailCards { get; set; }
        public bool IsBankrupt { get; set; }
        public bool IsCurrentTurn { get; set; }
        /// <summary>"Human" or "Bot". A seat flips to Bot when its player leaves or keeps missing turns.</summary>
        public string ControlledBy { get; set; } = "Human";
        /// <summary>Tile indexes of every property this player owns (always visible to everyone).</summary>
        public List<int> OwnedTileIndexes { get; set; } = new();
    }

    public class GamePropertyResponse
    {
        public int TileIndex { get; set; }
        public Guid? OwnerUserId { get; set; }
        /// <summary>0-4 houses, 5 = hotel.</summary>
        public int Houses { get; set; }
        public bool IsMortgaged { get; set; }
    }

    public class ChanceCardResponse
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class PendingDebtResponse
    {
        public int Amount { get; set; }
        /// <summary>Null means the bank.</summary>
        public Guid? CreditorUserId { get; set; }
    }

    public class GameStateResponse
    {
        public Guid Id { get; set; }
        /// <summary>InProgress, Finished, or Abandoned (every human left).</summary>
        public string Status { get; set; } = string.Empty;
        public Guid CurrentTurnUserId { get; set; }
        public int TurnNumber { get; set; }
        public bool HasRolled { get; set; }
        public int Die1 { get; set; }
        public int Die2 { get; set; }
        public int DoublesCount { get; set; }
        /// <summary>Tile the current player may buy or pass on, if any.</summary>
        public int? PendingPurchaseTileIndex { get; set; }
        public PendingDebtResponse? PendingDebt { get; set; }
        public ChanceCardResponse? LastChanceCard { get; set; }
        public Guid? WinnerUserId { get; set; }

        /// <summary>When the current player's clock runs out (UTC). Null once the game is over.</summary>
        public DateTime? TurnDeadlineAt { get; set; }
        /// <summary>Server clock at response time, so the UI can correct for the browser clock being off.</summary>
        public DateTime ServerTime { get; set; }

        public List<GamePlayerResponse> Players { get; set; } = new();
        public List<GamePropertyResponse> Properties { get; set; } = new();

        /// <summary>
        /// The most recent events, NEWEST FIRST, each as [sequence, turn, type, ...args]
        /// (see GameEventSchema for what the args mean for each type).
        /// </summary>
        public List<int[]> RecentEvents { get; set; } = new();

        /// <summary>
        /// What the CALLING player may do right now — lets the UI enable/disable buttons without
        /// re-implementing the rules. Values: roll, buy, pass, endTurn, payJailFine, useJailCard,
        /// payDebt, declareBankruptcy, manageProperties, proposeTrade.
        /// </summary>
        public List<string> AvailableActions { get; set; } = new();
    }

    /// <summary>Result of rolling the dice, returned alongside the new state so the UI can animate.</summary>
    public class RollResultResponse
    {
        public int Die1 { get; set; }
        public int Die2 { get; set; }
        public bool IsDoubles { get; set; }
        public GameStateResponse State { get; set; } = null!;
    }

    // ---------- Activity log (full history) ----------

    /// <summary>
    /// A page of the full activity log, oldest first. Each event is [sequence, turn, type, ...args].
    /// Ask for the next page with afterSequence = NextAfterSequence while HasMore is true.
    /// </summary>
    public class EventPageResponse
    {
        public List<int[]> Events { get; set; } = new();
        public int NextAfterSequence { get; set; }
        public bool HasMore { get; set; }
    }

    // ---------- Player summaries (snapshots) ----------

    /// <summary>One player's numbers at the end of a turn.</summary>
    public class PlayerSnapshotResponse
    {
        public int Seat { get; set; }
        public Guid UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public int Cash { get; set; }
        /// <summary>Cash + property prices (mortgaged count half) + buildings at cost.</summary>
        public int NetWorth { get; set; }
        public int Position { get; set; }
        public int Properties { get; set; }
        public int Houses { get; set; }
        public int Hotels { get; set; }
        public int Mortgaged { get; set; }
        public bool InJail { get; set; }
        public bool IsBankrupt { get; set; }
        public string ControlledBy { get; set; } = "Human";
    }

    /// <summary>The whole table at the end of one turn.</summary>
    public class TurnSummaryResponse
    {
        public int Turn { get; set; }
        /// <summary>Activity-log position at that moment: events up to this sequence had happened.</summary>
        public int EventSequence { get; set; }
        public int Die1 { get; set; }
        public int Die2 { get; set; }
        public List<PlayerSnapshotResponse> Players { get; set; } = new();
    }

    // ---------- Bot service ----------

    /// <summary>One seat a bot must act for right now, with the game as that seat sees it.</summary>
    public class BotTurnResponse
    {
        public Guid GameId { get; set; }
        public Guid UserId { get; set; }
        public GameStateResponse State { get; set; } = null!;
        /// <summary>Pending trades proposed TO this seat (bots decline them).</summary>
        public List<Guid> IncomingTradeIds { get; set; } = new();
    }
}
