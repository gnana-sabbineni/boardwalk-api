namespace BoardWalk.Monopoly.Abstractions
{
    /// <summary>
    /// Source of dice rolls. Injected so tests and replays can use fixed or seeded dice instead of real randomness.
    /// The default implementation is a pure function of (game seed, roll counter): the same inputs always give the same roll.
    /// </summary>
    public interface IDiceRoller
    {
        (int Die1, int Die2) Roll(int gameSeed, int rollCounter);
    }

    public sealed class SeededDiceRoller : IDiceRoller
    {
        public (int Die1, int Die2) Roll(int gameSeed, int rollCounter)
        {
            unchecked
            {
                // SplitMix64: a tiny, well-known integer mixer. Stable on every platform and .NET version
                // (unlike System.Random's seeded sequence, which is only stable per runtime implementation).
                var x = ((ulong)(uint)gameSeed << 32) | (uint)rollCounter;
                x += 0x9E3779B97F4A7C15UL;
                x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
                x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
                x ^= x >> 31;

                var die1 = (int)(x % 6UL) + 1;
                var die2 = (int)((x >> 32) % 6UL) + 1;
                return (die1, die2);
            }
        }
    }
}
