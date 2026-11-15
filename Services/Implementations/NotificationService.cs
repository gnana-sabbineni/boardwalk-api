using BoardWalk.Api.Data.Models;
using BoardWalk.Api.Data.Repositories;
using BoardWalk.Api.Services.Interfaces;
using BoardWalk.Api.Services.Models.Responses;

namespace BoardWalk.Api.Services.Implementations
{
    public class NotificationService : INotificationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFriendService _friendService;
        private readonly ILobbyService _lobbyService;

        private readonly ILogger<NotificationService> _logger;

        public NotificationService(IUnitOfWork unitOfWork, IFriendService friendService, ILobbyService lobbyService, ILogger<NotificationService> logger)
        {
            _unitOfWork = unitOfWork;
            _friendService = friendService;
            _lobbyService = lobbyService;
            _logger = logger;
        }

        public async Task<List<NotificationResponse>> GetNotificationsAsync(Guid userId)
        {
            var notifications = await _unitOfWork.Notifications.GetByRecipientAsync(userId);
            return notifications.Select(MapToResponse).ToList();
        }

        public async Task<NotificationResponse> RespondAsync(Guid userId, Guid notificationId, bool accept)
        {
            var notification = await _unitOfWork.Notifications.GetByIdAsync(notificationId);
            if (notification == null)
                throw new InvalidOperationException("Notification not found.");

            if (notification.RecipientUserId != userId)
                throw new UnauthorizedAccessException("You are not authorized to respond to this notification.");

            if (notification.Outcome != null)
                throw new InvalidOperationException("This notification has already been responded to.");

            switch (notification.Type)
            {
                case NotificationType.FriendRequest:
                    await _friendService.RespondToRequestAsync(userId, notification.ReferenceId, accept);
                    break;

                case NotificationType.LobbyInvite:
                    await _lobbyService.RespondToInviteAsync(userId, notification.ReferenceId, accept);
                    break;

                default:
                    throw new NotSupportedException($"Unknown notification type: {notification.Type}");
            }

            // No re-query needed — EF Core's identity map means `notification` already
            // reflects the Outcome/IsRead changes RespondToRequestAsync just saved,
            // since both services share the same AppDbContext for this request.
            _logger.LogInformation("Notification {NotificationId} responded to: {Outcome}", notificationId, notification.Outcome);

            return MapToResponse(notification);
        }

        public async Task MarkAsReadAsync(Guid userId, Guid notificationId)
        {
            var notification = await _unitOfWork.Notifications.GetByIdAsync(notificationId);
            if (notification == null)
                throw new InvalidOperationException("Notification not found.");

            if (notification.RecipientUserId != userId)
                throw new UnauthorizedAccessException("You are not authorized to modify this notification.");

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.LastModifiedAt = DateTime.UtcNow;
                _unitOfWork.Notifications.Update(notification);
                await _unitOfWork.SaveChangesAsync();
            }
        }

        private static NotificationResponse MapToResponse(Notification n) => new()
        {
            Id = n.Id,
            Message = n.Message,
            IsRead = n.IsRead,
            Outcome = n.Outcome?.ToString(),
            Actions = n.Outcome == null ? new List<string> { "accept", "decline" } : new List<string>(),
            CreatedAt = n.CreatedAt
        };
    }
}