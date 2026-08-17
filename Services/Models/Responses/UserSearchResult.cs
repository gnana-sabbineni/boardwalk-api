namespace BoardWalk.Api.Services.Models.Responses
{
    // Tells the frontend what button to show next to a search result:
    // "Add Friend" vs "Request Sent" vs "Already Friends" vs "Respond to Request".
    public enum RelationshipStatus
    {
        None,
        Friends,
        PendingSentByMe,
        PendingReceivedByMe
    }

    public class UserSearchResult
    {
        public Guid UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public RelationshipStatus Status { get; set; }
    }
}