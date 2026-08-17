namespace BoardWalk.Api.Services.Models.Responses
{
    public class FriendRequestResponse
    {
        public Guid RequestId { get; set; }
        public Guid FromUserId { get; set; }
        public string FromFirstName { get; set; } = string.Empty;
        public string FromLastName { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}