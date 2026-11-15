// Services/Models/Responses/LobbyResponse.cs
namespace BoardWalk.Api.Services.Models.Responses
{
    public class LobbyMemberResponse
    {
        public Guid UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsHost { get; set; }
        public bool IsOnline { get; set; }
    }

    public class LobbyResponse
    {
        public Guid Id { get; set; }
        public Guid HostUserId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int MaxPlayers { get; set; }
        public List<LobbyMemberResponse> Members { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}