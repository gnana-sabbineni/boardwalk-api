namespace BoardWalk.Api.Data.Models
{
    public enum LobbyStatus
    {
        Open,
        InProgress,
        Closed
    }

    public class Lobby
    {
        public Guid Id { get; set; }

        public Guid HostUserId { get; set; }
        public User Host { get; set; } = null!;

        public LobbyStatus Status { get; set; } = LobbyStatus.Open;

        public int MaxPlayers { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        public List<LobbyMember> Members { get; set; } = new();
    }
}