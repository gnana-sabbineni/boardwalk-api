using System.ComponentModel.DataAnnotations;

namespace BoardWalk.Api.Services.Models.Requests
{
    public class ProposeTradeRequest
    {
        [Required]
        public Guid RecipientUserId { get; set; }

        /// <summary>Cash the proposer gives to the recipient.</summary>
        [Range(0, 1_000_000)]
        public int OfferCash { get; set; }

        /// <summary>Cash the proposer asks the recipient to give.</summary>
        [Range(0, 1_000_000)]
        public int RequestCash { get; set; }

        /// <summary>Tile indexes of properties the proposer gives.</summary>
        public List<int> OfferedTileIndexes { get; set; } = new();

        /// <summary>Tile indexes of properties the proposer asks for.</summary>
        public List<int> RequestedTileIndexes { get; set; } = new();
    }
}
