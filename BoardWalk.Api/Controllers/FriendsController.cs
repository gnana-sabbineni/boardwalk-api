using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BoardWalk.Api.Services.Interfaces;
using BoardWalk.Api.Services.Models.Requests;

namespace BoardWalk.Api.Controllers
{
    [Authorize]
    [Route("api/friends")]
    public class FriendsController : ApiControllerBase
    {
        private readonly IFriendService _friendService;

        public FriendsController(IFriendService friendService)
        {
            _friendService = friendService;
        }

        /// <summary>Returns the current user's full friends list.</summary>
        /// <returns>200 OK with a list of friends.</returns>
        [HttpGet]
        public async Task<IActionResult> GetFriends()
        {
            var friends = await _friendService.GetFriendsAsync(CurrentUserId);
            return SuccessResponse(friends, "Friends retrieved.");
        }

        /// <summary>Searches the current user's existing friends by name or email.</summary>
        /// <param name="query">Text to match against friend name or email.</param>
        /// <returns>200 OK with matching friends.</returns>
        [HttpGet("search")]
        public async Task<IActionResult> SearchFriends([FromQuery] string query)
        {
            var results = await _friendService.SearchFriendsAsync(CurrentUserId, query);
            return SuccessResponse(results, "Search complete.");
        }

        /// <summary>Searches all users (not just existing friends) by name or email, to find people to add.</summary>
        /// <param name="query">Text to match against user name or email.</param>
        /// <returns>200 OK with matching users and their relationship status relative to the current user.</returns>
        [HttpGet("users/search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string query)
        {
            var results = await _friendService.SearchUsersAsync(CurrentUserId, query);
            return SuccessResponse(results, "Search complete.");
        }

        /// <summary>Sends a friend request to another user.</summary>
        /// <param name="request">The target user's ID.</param>
        /// <returns>
        /// 201 Created with the new request's ID on success.
        /// 409 Conflict if a relationship already exists between the two users.
        /// </returns>
        [HttpPost("requests")]
        public async Task<IActionResult> SendRequest([FromBody] SendFriendRequestRequest request)
        {
            try
            {
                var requestId = await _friendService.SendRequestAsync(CurrentUserId, request);
                return SuccessResponse(new { requestId }, "Friend request sent.", statusCode: 201);
            }
            catch (InvalidOperationException ex)
            {
                return FailResponse(ex.Message, statusCode: 409);
            }
        }

        /// <summary>Returns all pending friend requests sent TO the current user.</summary>
        /// <returns>200 OK with a list of incoming requests.</returns>
        [HttpGet("requests")]
        public async Task<IActionResult> GetIncomingRequests()
        {
            var requests = await _friendService.GetIncomingRequestsAsync(CurrentUserId);
            return SuccessResponse(requests, "Incoming requests retrieved.");
        }

        /// <summary>Accepts a pending friend request sent to the current user.</summary>
        /// <param name="requestId">The friend request's ID.</param>
        /// <returns>200 OK on success. 403 if the request wasn't sent to this user. 400 if it's not pending.</returns>
        [HttpPost("requests/{requestId}/accept")]
        public Task<IActionResult> AcceptRequest(Guid requestId) => RespondToRequest(requestId, accept: true);

        /// <summary>Declines a pending friend request sent to the current user.</summary>
        /// <param name="requestId">The friend request's ID.</param>
        /// <returns>200 OK on success. 403 if the request wasn't sent to this user. 400 if it's not pending.</returns>
        [HttpPost("requests/{requestId}/decline")]
        public Task<IActionResult> DeclineRequest(Guid requestId) => RespondToRequest(requestId, accept: false);

        private async Task<IActionResult> RespondToRequest(Guid requestId, bool accept)
        {
            try
            {
                await _friendService.RespondToRequestAsync(CurrentUserId, requestId, accept);
                return SuccessResponse<object>(null, accept ? "Friend request accepted." : "Friend request declined.");
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

        /// <summary>Removes an existing friend (deletes the accepted relationship).</summary>
        /// <param name="friendUserId">The user ID of the friend to remove.</param>
        /// <returns>200 OK on success. 400 if the two users are not currently friends.</returns>
        [HttpDelete("{friendUserId}")]
        public async Task<IActionResult> RemoveFriend(Guid friendUserId)
        {
            try
            {
                await _friendService.RemoveFriendAsync(CurrentUserId, friendUserId);
                return SuccessResponse<object>(null, "Friend removed.");
            }
            catch (InvalidOperationException ex)
            {
                return FailResponse(ex.Message, statusCode: 400);
            }
        }
    }
}