using BoardWalk.Api.Services.Models.Responses;

namespace BoardWalk.Api.Services.Interfaces
{
    public interface INotificationService
    {
        /// <summary>Returns all notifications for the current user, most recent first.</summary>
        Task<List<NotificationResponse>> GetNotificationsAsync(Guid userId);

        /// <summary>
        /// Accepts or declines a notification by dispatching to the correct underlying
        /// service based on the notification's Type (e.g. IFriendService for FriendRequest).
        /// </summary>
        /// <exception cref="InvalidOperationException">Notification not found, or already responded to.</exception>
        /// <exception cref="UnauthorizedAccessException">Notification doesn't belong to this user.</exception>
        /// <exception cref="NotSupportedException">Notification type has no handler wired up yet.</exception>
        Task<NotificationResponse> RespondAsync(Guid userId, Guid notificationId, bool accept);

        /// <summary>Marks a notification as read without taking any accept/decline action.</summary>
        Task MarkAsReadAsync(Guid userId, Guid notificationId);
    }
}