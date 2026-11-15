namespace BoardWalk.Monopoly.Models
{
    public enum GameStatus
    {
        InProgress,
        Finished,
        /// <summary>Every human left, so the game was stopped (only computer players remained).</summary>
        Abandoned
    }

    /// <summary>Who decides this seat's moves.</summary>
    public enum SeatControl
    {
        Human,
        Bot
    }

    /// <summary>
    /// One running match. The host creates it for a "room" (for BoardWalk, a lobby) and hands over only
    /// opaque ids and display names, so this module never depends on the host's user or lobby tables.
    /// </summary>
    public class Game
    {
        public Guid Id { get; set; }

        /// <summary>The host's room id (a lobby id for BoardWalk). Opaque to this module; used to address realtime events.</summary>
        public Guid RoomId { get; set; }

        public GameStatus Status { get; set; } = GameStatus.InProgress;

        /// <summary>Whose turn it is right now.</summary>
        public Guid CurrentTurnUserId { get; set; }
        public int TurnNumber { get; set; } = 1;

        /// <summary>True once the current player has rolled and may not roll again (no doubles pending).</summary>
        public bool HasRolled { get; set; }

        public int LastDie1 { get; set; }
        public int LastDie2 { get; set; }

        /// <summary>Consecutive doubles rolled this turn. Three in a row sends the player to jail.</summary>
        public int DoublesCount { get; set; }

        /// <summary>
        /// Seed for this game's dice. Together with RollCounter it makes every roll reproducible
        /// (replays, tests). Server-side only: exposing it would let anyone predict future rolls.
        /// </summary>
        public int DiceSeed { get; set; }
        public int RollCounter { get; set; }

        /// <summary>Tile index of an unowned property the current player may still buy or pass on.</summary>
        public int? PendingPurchaseTileIndex { get; set; }

        /// <summary>Money the current player owes but could not pay from cash yet.</summary>
        public int PendingDebtAmount { get; set; }
        /// <summary>Who is owed the debt. Null means the bank.</summary>
        public Guid? PendingDebtCreditorUserId { get; set; }

        /// <summary>Shuffled chance-card ids as a comma separated list, drawn in order and cycled.</summary>
        public string ChanceDeckOrder { get; set; } = string.Empty;
        public int ChanceDeckPosition { get; set; }
        public int? LastChanceCardId { get; set; }

        public Guid? WinnerUserId { get; set; }

        /// <summary>When the current player's clock runs out. The turn timer acts on games past this time.</summary>
        public DateTime? TurnDeadlineAt { get; set; }

        /// <summary>Counter giving every GameEvent a strictly increasing Sequence (stable log ordering).</summary>
        public int EventCounter { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? FinishedAt { get; set; }

        public List<GamePlayer> Players { get; set; } = new();
        public List<GameProperty> Properties { get; set; } = new();
        public List<Trade> Trades { get; set; } = new();
    }

    public class GamePlayer
    {
        public Guid Id { get; set; }

        public Guid GameId { get; set; }
        public Game Game { get; set; } = null!;

        /// <summary>The host's user id. Opaque here (no foreign key), so the game never reads the host's user table.</summary>
        public Guid UserId { get; set; }

        /// <summary>Copied from the host at game start so the game can show names on its own.</summary>
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        /// <summary>0-based seat number in the turn order. Activity-log events refer to players by seat.</summary>
        public int TurnOrder { get; set; }

        public SeatControl ControlledBy { get; set; } = SeatControl.Human;

        /// <summary>Consecutive turns this human let the clock run out. Too many hands the seat to a bot.</summary>
        public int MissedTurns { get; set; }

        public int Cash { get; set; }

        /// <summary>Tile index 0-39 the player's token is on.</summary>
        public int Position { get; set; }

        public bool InJail { get; set; }
        public int JailTurns { get; set; }
        public int GetOutOfJailCards { get; set; }

        public bool IsBankrupt { get; set; }
    }

    /// <summary>One row per ownable tile per game. OwnerUserId == null means the bank owns it.</summary>
    public class GameProperty
    {
        public Guid Id { get; set; }

        public Guid GameId { get; set; }
        public Game Game { get; set; } = null!;

        public int TileIndex { get; set; }

        public Guid? OwnerUserId { get; set; }

        /// <summary>0-4 houses, 5 = hotel.</summary>
        public int Houses { get; set; }
        public bool IsMortgaged { get; set; }
    }

    /// <summary>
    /// Append-only activity log entry. Stored compactly: a type number plus a positional list of integers
    /// (seats, tile indexes, amounts). What each position means is defined by GameEventSchema in the contracts.
    /// </summary>
    public class GameEvent
    {
        public Guid Id { get; set; }

        public Guid GameId { get; set; }
        public Game Game { get; set; } = null!;

        public int Sequence { get; set; }
        public int Turn { get; set; }
        public int Type { get; set; }
        public int[] Args { get; set; } = Array.Empty<int>();
    }

    /// <summary>
    /// One player's numbers at the end of one turn (turn 0 = game start). Lets the UI show "how was everyone doing
    /// at turn N" with one cheap read, and lets a bot take over without replaying the log. The dice and the
    /// activity-log position are stored too so the moment can be reconstructed exactly.
    /// </summary>
    public class GameSnapshot
    {
        public Guid Id { get; set; }

        public Guid GameId { get; set; }
        public Game Game { get; set; } = null!;

        public int Turn { get; set; }
        public int Seat { get; set; }
        public int EventSequence { get; set; }

        public int Die1 { get; set; }
        public int Die2 { get; set; }
        public int RollCounter { get; set; }

        public int Cash { get; set; }
        public int NetWorth { get; set; }
        public int Position { get; set; }
        public int Properties { get; set; }
        public int Houses { get; set; }
        public int Hotels { get; set; }
        public int Mortgaged { get; set; }
        public bool InJail { get; set; }
        public bool IsBankrupt { get; set; }
        public SeatControl ControlledBy { get; set; }
    }
}
