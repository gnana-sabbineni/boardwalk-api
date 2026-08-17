using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BoardWalk.Api.Services.Interfaces;
using BoardWalk.Api.Services.Models.Requests;

namespace BoardWalk.Api.Controllers
{
    [Authorize]
    [Route("api/notifications")]
    public class NotificationsController : ApiControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        /// <summary>Returns all notifications for the current user, most recent first.</summary>
        /// <returns>200 OK with the notification list.</returns>
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            var notifications = await _notificationService.GetNotificationsAsync(CurrentUserId);
            return SuccessResponse(notifications, "Notifications retrieved.");
        }

        /// <summary>
        /// Accepts or declines a notification. Dispatches internally to the correct
        /// handler based on the notification's type — the caller doesn't need to know
        /// which underlying endpoint would normally handle it.
        /// </summary>
        /// <param name="notificationId">The notification's ID.</param>
        /// <param name="request">Action must be "accept" or "decline".</param>
        /// <returns>
        /// 200 OK with the updated notification (empty Actions, Outcome set) on success.
        /// 400 if already responded to, or the action string is invalid.
        /// 403 if this notification doesn't belong to the current user.
        /// </returns>
        [HttpPost("{notificationId}/respond")]
        public async Task<IActionResult> Respond(Guid notificationId, [FromBody] RespondToNotificationRequest request)
        {
            bool accept;
            switch (request.Action.Trim().ToLower())
            {
                case "accept": accept = true; break;
                case "decline": accept = false; break;
                default: return FailResponse("Action must be 'accept' or 'decline'.", statusCode: 400);
            }

            try
            {
                var result = await _notificationService.RespondAsync(CurrentUserId, notificationId, accept);
                return SuccessResponse(result, $"Notification {(accept ? "accepted" : "declined")}.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return FailResponse(ex.Message, statusCode: 403);
            }
            catch (InvalidOperationException ex)
            {
                return FailResponse(ex.Message, statusCode: 400);
            }
            catch (NotSupportedException ex)
            {
                return FailResponse(ex.Message, statusCode: 400);
            }
        }

        /// <summary>Marks a notification as read without taking any action on it.</summary>
        /// <param name="notificationId">The notification's ID.</param>
        /// <returns>200 OK on success. 403 if it doesn't belong to the current user.</returns>
        [HttpPost("{notificationId}/read")]
        public async Task<IActionResult> MarkAsRead(Guid notificationId)
        {
            try
            {
                await _notificationService.MarkAsReadAsync(CurrentUserId, notificationId);
                return SuccessResponse<object>(null, "Notification marked as read.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return FailResponse(ex.Message, statusCode: 403);
            }
            catch (InvalidOperationException ex)
            {
                return FailResponse(ex.Message, statusCode: 400);
            }
        }
    }
}