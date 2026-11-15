namespace BoardWalk.Api.Data.Models
{
    public enum LobbyInviteStatus
    {
        Pending,
        Accepted,
        Declined
    }

    public class LobbyInvite
    {
        public Guid Id { get; set; }

        public Guid LobbyId { get; set; }
        public Lobby Lobby { get; set; } = null!;

        public Guid InviterUserId { get; set; }
        public User Inviter { get; set; } = null!;

        public Guid InviteeUserId { get; set; }
        public User Invitee { get; set; } = null!;

        public LobbyInviteStatus Status { get; set; } = LobbyInviteStatus.Pending;

        public DateTime CreatedAt { get; set; }
        public DateTime? RespondedAt { get; set; }
    }
}