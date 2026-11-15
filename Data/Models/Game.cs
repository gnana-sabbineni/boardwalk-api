namespace BoardWalk.Api.Data.Models
{
    public enum GameStatus
    {
        InProgress,
        Finished
    }

    /// <summary>One running match. Created from a Lobby when the host starts the game.</summary>
    public class Game
    {
        public Guid Id { get; set; }

        public Guid LobbyId { get; set; }
        public Lobby Lobby { get; set; } = null!;

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
        /// Tile index of an unowned property the current player landed on and has not yet
        /// bought or passed on. Null when there is no purchase decision outstanding.
        /// </summary>
        public int? PendingPurchaseTileIndex { get; set; }

        /// <summary>Money the current player owes but could not pay from cash yet (they must raise funds or go bankrupt).</summary>
        public int PendingDebtAmount { get; set; }
        /// <summary>Who is owed the debt. Null means the bank.</summary>
        public Guid? PendingDebtCreditorUserId { get; set; }

        /// <summary>Shuffled chance-card ids as a comma separated list, drawn in order and cycled.</summary>
        public string ChanceDeckOrder { get; set; } = string.Empty;
        public int ChanceDeckPosition { get; set; }

        /// <summary>The most recently drawn chance card id (for the UI card modal), if any this turn.</summary>
        public int? LastChanceCardId { get; set; }

        public Guid? WinnerUserId { get; set; }

        /// <summary>Counter used to give every GameEvent a strictly increasing Sequence (stable log ordering).</summary>
        public int EventCounter { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? FinishedAt { get; set; }

        public List<GamePlayer> Players { get; set; } = new();
        public List<GameProperty> Properties { get; set; } = new();
        public List<GameEvent> Events { get; set; } = new();
        public List<Trade> Trades { get; set; } = new();
    }

    public class GamePlayer
    {
        public Guid Id { get; set; }

        public Guid GameId { get; set; }
        public Game Game { get; set; } = null!;

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        /// <summary>0-based position in the turn order.</summary>
        public int TurnOrder { get; set; }

        public int Cash { get; set; }

        /// <summary>Tile index 0-39 the player's token is on.</summary>
        public int Position { get; set; }

        public bool InJail { get; set; }
        /// <summary>Failed attempts to roll doubles while in jail (3rd failure forces the fine).</summary>
        public int JailTurns { get; set; }
        public int GetOutOfJailCards { get; set; }

        public bool IsBankrupt { get; set; }
    }

    /// <summary>
    /// One row per ownable tile per game (created when the game starts), so ownership,
    /// buildings and mortgage state all live in one place. OwnerUserId == null means the bank owns it.
    /// </summary>
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

    /// <summary>Append-only action log shown in the UI feed.</summary>
    public class GameEvent
    {
        public Guid Id { get; set; }

        public Guid GameId { get; set; }
        public Game Game { get; set; } = null!;

        public int Sequence { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
