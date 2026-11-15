namespace BoardWalk.Api.Services.Models.Responses
{
    public class TradeResponse
    {
        public Guid Id { get; set; }
        public Guid ProposerUserId { get; set; }
        public string ProposerName { get; set; } = string.Empty;
        public Guid RecipientUserId { get; set; }
        public string RecipientName { get; set; } = string.Empty;
        public int OfferCash { get; set; }
        public int RequestCash { get; set; }
        public List<int> OfferedTileIndexes { get; set; } = new();
        public List<int> RequestedTileIndexes { get; set; } = new();
        /// <summary>Pending, Accepted, Declined, Cancelled.</summary>
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class OwnedPropertyResponse
    {
        public int TileIndex { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Price { get; set; }
        public int HouseCost { get; set; }
        /// <summary>0-4 houses, 5 = hotel.</summary>
        public int Houses { get; set; }
        public bool IsMortgaged { get; set; }
        /// <summary>Rent a visitor would pay right now (0 when mortgaged). Utilities show the per-dice-roll multiplier x dice total as 0 — see Type.</summary>
        public int CurrentRent { get; set; }
    }

    public class PropertyGroupResponse
    {
        /// <summary>A street colour key (sand, seafoam, ...), or "station" / "utility".</summary>
        public string Group { get; set; } = string.Empty;
        /// <summary>True when the player owns every street in this colour group (buildings allowed).</summary>
        public bool OwnsWholeGroup { get; set; }
        public List<OwnedPropertyResponse> Properties { get; set; } = new();
    }

    /// <summary>Everything the "player properties" panel needs for one player.</summary>
    public class PlayerPropertiesResponse
    {
        public Guid UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int Cash { get; set; }
        public bool IsBankrupt { get; set; }
        public int TotalHouses { get; set; }
        public int TotalHotels { get; set; }
        /// <summary>True only when the caller is this player AND it is their turn — shows manage / bankruptcy actions.</summary>
        public bool CanManage { get; set; }
        public List<PropertyGroupResponse> Groups { get; set; } = new();
    }
}
