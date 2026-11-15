namespace BoardWalk.Api.Data.Models
{
    public enum NotificationType
    {
        FriendRequest,
        LobbyInvite
    }

    public enum NotificationOutcome
    {
        Accepted,
        Declined
    }

    public class Notification
    {
        public Guid Id { get; set; }

        // Who SEES this notification
        public Guid RecipientUserId { get; set; }
        public User Recipient { get; set; } = null!;

        // Who TRIGGERED it (used to build the display message, e.g. "Alex invited you...")
        public Guid ActorUserId { get; set; }
        public User Actor { get; set; } = null!;

        public NotificationType Type { get; set; }

        // Points to the FriendRequest.Id or (later) LobbyInvite.Id this notification
        // is about. No DB-level foreign key — which table it points to depends on
        // Type, resolved in code by whichever service handles that type.
        public Guid ReferenceId { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }

        // null = pending (show Accept/Decline). Set once, at the moment the user
        // acts, by the same transaction that updates the underlying FriendRequest/
        // LobbyInvite — never updated independently, so it can't drift out of sync.
        public NotificationOutcome? Outcome { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? LastModifiedAt { get; set; }
    }
}