namespace BoardWalk.Monopoly.Rules
{
    public enum ChanceEffect
    {
        MoveTo,                 // Value = tile index (collects Start salary if it passes Start)
        MoveBack,               // Value = number of tiles to move backwards
        Gain,                   // Value = cash from the bank
        Pay,                    // Value = cash to the bank
        GoToDetention,
        GetOutOfDetentionFree,
        PayEachPlayer,          // Value = cash paid to every other active player
        CollectFromEachPlayer   // Value = cash received from every other active player
    }

    public sealed record ChanceCard(int Id, string Title, string Description, ChanceEffect Effect, int Value = 0);

    /// <summary>Original "Fortune" cards (the Chance equivalent). Card ids are stable and stored as the deck order.</summary>
    public static class ChanceDeck
    {
        public static readonly IReadOnlyList<ChanceCard> Cards = new List<ChanceCard>
        {
            new(1,  "Sunrise Departure",   "Head straight to Departure Gate and collect $200.", ChanceEffect.MoveTo, BoardDefinition.StartIndex),
            new(2,  "Seaside Stroll",      "Advance to Boardwalk Crown. Collect $200 if you pass Departure Gate.", ChanceEffect.MoveTo, 39),
            new(3,  "Market Day",          "Your stall sold out. Collect $150 from the bank.", ChanceEffect.Gain, 150),
            new(4,  "Lost Anchor",         "You replace a lost anchor. Pay $75 to the bank.", ChanceEffect.Pay, 75),
            new(5,  "Tide Turns",          "A strong tide pulls you back 3 tiles.", ChanceEffect.MoveBack, 3),
            new(6,  "Lighthouse Duty",     "Caught lighting the lighthouse late. Go to Detention Cove.", ChanceEffect.GoToDetention),
            new(7,  "Harbour Pass",        "Keep this card to leave Detention Cove for free.", ChanceEffect.GetOutOfDetentionFree),
            new(8,  "Festival Host",       "You host the harbour festival. Pay each player $50.", ChanceEffect.PayEachPlayer, 50),
            new(9,  "Lucky Catch",         "A record catch! Collect $50 from every player.", ChanceEffect.CollectFromEachPlayer, 50),
            new(10, "Pier Repairs",        "Storm damage at the pier. Pay $100 to the bank.", ChanceEffect.Pay, 100),
            new(11, "Tram Ride",           "Hop on the tram to North Pier Tram. Collect $200 if you pass Departure Gate.", ChanceEffect.MoveTo, 5),
            new(12, "Treasure Chest",      "You dig up a small chest on the beach. Collect $100.", ChanceEffect.Gain, 100),
        };

        public static ChanceCard Get(int id) => Cards.First(c => c.Id == id);

        public static string NewShuffledOrder(Random rng) =>
            string.Join(",", Cards.Select(c => c.Id).OrderBy(_ => rng.Next()));

        public static int[] ParseOrder(string order) =>
            order.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
    }
}
