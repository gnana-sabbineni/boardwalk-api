namespace BoardWalk.Monopoly.Abstractions
{
    /// <summary>Tunable rules of play and timing. Bind from configuration section "Monopoly".</summary>
    public class GameOptions
    {
        /// <summary>Clock for the current player; re-armed at the start of each turn and after each of their actions.</summary>
        public int TurnSeconds { get; set; } = 60;

        /// <summary>Consecutive turns a human may let the clock run out before the seat is handed to a bot.</summary>
        public int MissedTurnsBeforeBot { get; set; } = 2;

        /// <summary>How often the turn timer looks for games past their deadline.</summary>
        public int TimerPollSeconds { get; set; } = 1;

        /// <summary>How long a request waits for another request on the same game to finish.</summary>
        public int LockWaitSeconds { get; set; } = 5;
    }
}
