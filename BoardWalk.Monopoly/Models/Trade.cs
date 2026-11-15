namespace BoardWalk.Monopoly.Models
{
    public enum TradeStatus
    {
        Pending,
        Accepted,
        Declined,
        Cancelled
    }

    /// <summary>
    /// A proposal between two players in the same game: "I give you X (properties + cash) and you give me Y".
    /// Nothing moves until the recipient accepts, and everything is re-validated at that moment.
    /// </summary>
    public class Trade
    {
        public Guid Id { get; set; }

        public Guid GameId { get; set; }
        public Game Game { get; set; } = null!;

        public Guid ProposerUserId { get; set; }
        public Guid RecipientUserId { get; set; }

        /// <summary>Cash the proposer gives.</summary>
        public int OfferCash { get; set; }
        /// <summary>Cash the proposer asks for from the recipient.</summary>
        public int RequestCash { get; set; }

        public TradeStatus Status { get; set; } = TradeStatus.Pending;

        public DateTime CreatedAt { get; set; }
        public DateTime? RespondedAt { get; set; }

        public List<TradeItem> Items { get; set; } = new();
    }

    /// <summary>One property inside a trade. FromProposer = true means the proposer is giving it away.</summary>
    public class TradeItem
    {
        public Guid Id { get; set; }

        public Guid TradeId { get; set; }
        public Trade Trade { get; set; } = null!;

        public int TileIndex { get; set; }
        public bool FromProposer { get; set; }
    }
}
