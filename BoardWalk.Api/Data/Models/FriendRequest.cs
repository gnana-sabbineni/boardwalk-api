namespace BoardWalk.Api.Data.Models
{
    public enum FriendRequestStatus
    {
        Pending,
        Accepted,
        Declined
    }

    public class FriendRequest
    {
        public Guid Id { get; set; }

        public Guid RequesterId { get; set; }
        public User Requester { get; set; } = null!; // navigation property — lets EF Core load the related User

        public Guid AddresseeId { get; set; }
        public User Addressee { get; set; } = null!;

        public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending;

        public DateTime CreatedAt { get; set; }
        public DateTime? RespondedAt { get; set; } // null until accepted/declined
    }
}