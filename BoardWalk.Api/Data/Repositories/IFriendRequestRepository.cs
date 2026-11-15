using BoardWalk.Api.Data.Models;

namespace BoardWalk.Api.Data.Repositories
{
    public interface IFriendRequestRepository : IRepository<FriendRequest>
    {
        // Any row (any status) between these two users, in EITHER direction —
        // used to block duplicate requests / duplicate friendships.
        Task<FriendRequest?> GetRelationshipAsync(Guid userA, Guid userB);

        Task<FriendRequest?> GetByIdWithUsersAsync(Guid requestId);

        Task<List<FriendRequest>> GetIncomingPendingAsync(Guid userId);

        Task<List<FriendRequest>> GetAcceptedFriendshipsAsync(Guid userId);
        Task<List<FriendRequest>> GetRelationshipsForUserAsync(Guid userId, IEnumerable<Guid> otherUserIds);
    }
}