namespace BoardWalk.Monopoly.Rules
{
    public enum TileType
    {
        Start,
        Property,
        Station,
        Utility,
        Tax,
        Chance,
        Detention,      // the "Jail" corner (just visiting / locked up)
        RestStop,       // the "Free Parking" corner
        GoToDetention   // sends the player to Detention
    }

    /// <summary>
    /// A single tile on the Boardwalk board. This is an ORIGINAL layout: the shape (a 40-tile
    /// square loop with four corners) is the genre convention, but all names, groups and
    /// numbers here are invented for this game.
    /// </summary>
    public sealed record Tile(
        int Index,
        string Name,
        TileType Type,
        string? ColorGroup = null,
        int Price = 0,
        int HouseCost = 0,
        int TaxAmount = 0)
    {
        public bool IsOwnable => Type is TileType.Property or TileType.Station or TileType.Utility;

        /// <summary>
        /// Rent tiers for a street property: [base, 1 house, 2, 3, 4, hotel].
        /// Derived from price so the whole table stays consistent and easy to tune.
        /// </summary>
        public int[] RentTiers()
        {
            if (Type != TileType.Property) return Array.Empty<int>();
            var unit = Math.Max(1, (int)Math.Round(Price / 30.0));
            int[] multipliers = { 1, 5, 15, 45, 80, 125 };
            return multipliers.Select(m => m * unit).ToArray();
        }
    }

    public static class BoardDefinition
    {
        public const int TileCount = 40;
        public const int StartIndex = 0;
        public const int DetentionIndex = 10;
        public const int GoToDetentionIndex = 30;
        public const int PassStartSalary = 200;
        public const int StartingCash = 1500;
        public const int DetentionFine = 50;
        public const int MortgageInterestPercent = 10;

        // Original colour-group keys. The frontend maps each key to a colour from the design tokens.
        public const string Sand = "sand";
        public const string Seafoam = "seafoam";
        public const string Coral = "coral";
        public const string Amber = "amber";
        public const string Orchid = "orchid";
        public const string Sky = "sky";
        public const string Rose = "rose";
        public const string Indigo = "indigo";

        public static readonly IReadOnlyList<Tile> Tiles = new List<Tile>
        {
            new(0,  "Departure Gate",        TileType.Start),
            new(1,  "Driftwood Lane",        TileType.Property, Sand,    60,  50),
            new(2,  "Fortune Card",          TileType.Chance),
            new(3,  "Kelp Court",            TileType.Property, Sand,    60,  50),
            new(4,  "Harbor Levy",           TileType.Tax, TaxAmount: 200),
            new(5,  "North Pier Tram",       TileType.Station, Price: 200),
            new(6,  "Lantern Row",           TileType.Property, Seafoam, 100, 50),
            new(7,  "Fortune Card",          TileType.Chance),
            new(8,  "Harbor Steps",          TileType.Property, Seafoam, 100, 50),
            new(9,  "Gull Street",           TileType.Property, Seafoam, 120, 50),
            new(10, "Detention Cove",        TileType.Detention),
            new(11, "Saltmarsh Road",        TileType.Property, Coral,   140, 100),
            new(12, "Tidal Power Works",     TileType.Utility, Price: 150),
            new(13, "Cobble Quay",           TileType.Property, Coral,   140, 100),
            new(14, "Anchor Walk",           TileType.Property, Coral,   160, 100),
            new(15, "East Pier Tram",        TileType.Station, Price: 200),
            new(16, "Marigold Close",        TileType.Property, Amber,   180, 100),
            new(17, "Fortune Card",          TileType.Chance),
            new(18, "Citrus Terrace",        TileType.Property, Amber,   180, 100),
            new(19, "Sundial Square",        TileType.Property, Amber,   200, 100),
            new(20, "Rest Stop Meadow",      TileType.RestStop),
            new(21, "Heather Hill",          TileType.Property, Orchid,  220, 150),
            new(22, "Fortune Card",          TileType.Chance),
            new(23, "Violet Arcade",         TileType.Property, Orchid,  220, 150),
            new(24, "Plum Orchard",          TileType.Property, Orchid,  240, 150),
            new(25, "South Pier Tram",       TileType.Station, Price: 200),
            new(26, "Breaker Bay",           TileType.Property, Sky,     260, 150),
            new(27, "Cloudline Avenue",      TileType.Property, Sky,     260, 150),
            new(28, "Windmill Waterworks",   TileType.Utility, Price: 150),
            new(29, "Skyview Terrace",       TileType.Property, Sky,     280, 150),
            new(30, "Go To Detention",       TileType.GoToDetention),
            new(31, "Rosewater Parade",      TileType.Property, Rose,    300, 200),
            new(32, "Petal Promenade",       TileType.Property, Rose,    300, 200),
            new(33, "Fortune Card",          TileType.Chance),
            new(34, "Blossom Boulevard",     TileType.Property, Rose,    320, 200),
            new(35, "West Pier Tram",        TileType.Station, Price: 200),
            new(36, "Fortune Card",          TileType.Chance),
            new(37, "Midnight Esplanade",    TileType.Property, Indigo,  350, 200),
            new(38, "Luxury Levy",           TileType.Tax, TaxAmount: 100),
            new(39, "Boardwalk Crown",       TileType.Property, Indigo,  400, 200),
        };

        public static Tile Get(int index) => Tiles[index];

        public static IEnumerable<Tile> OwnableTiles => Tiles.Where(t => t.IsOwnable);

        public static IEnumerable<Tile> InGroup(string colorGroup) =>
            Tiles.Where(t => t.Type == TileType.Property && t.ColorGroup == colorGroup);

        public static int StationRent(int stationsOwned) => stationsOwned switch
        {
            1 => 25,
            2 => 50,
            3 => 100,
            >= 4 => 200,
            _ => 0
        };

        /// <summary>Utility rent is a multiple of the dice total: 4x with one utility, 10x with both.</summary>
        public static int UtilityRent(int utilitiesOwned, int diceTotal) =>
            utilitiesOwned >= 2 ? diceTotal * 10 : utilitiesOwned == 1 ? diceTotal * 4 : 0;
    }
}
