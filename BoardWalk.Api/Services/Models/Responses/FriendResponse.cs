namespace BoardWalk.Api.Services.Models.Responses
{
    public class FriendResponse
    {
        public Guid UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime FriendsSince { get; set; }
    }
}