namespace BoardWalk.Api.Data.Models
{
    public class LobbyMember
    {
        public Guid Id { get; set; }

        public Guid LobbyId { get; set; }
        public Lobby Lobby { get; set; } = null!;

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public DateTime JoinedAt { get; set; }
    }
}