using BoardWalk.Api.Services.Models.Requests;
using BoardWalk.Api.Services.Models.Responses;

namespace BoardWalk.Api.Services.Interfaces
{
    public interface IFriendService
    {
        Task<List<FriendResponse>> GetFriendsAsync(Guid userId);
        Task<List<FriendResponse>> SearchFriendsAsync(Guid userId, string query);
        Task<List<UserSearchResult>> SearchUsersAsync(Guid userId, string query);
        Task<Guid> SendRequestAsync(Guid requesterId, SendFriendRequestRequest request);
        Task<List<FriendRequestResponse>> GetIncomingRequestsAsync(Guid userId);
        Task RespondToRequestAsync(Guid userId, Guid requestId, bool accept);
        Task RemoveFriendAsync(Guid userId, Guid friendUserId);
    }
}