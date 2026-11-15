using Microsoft.EntityFrameworkCore;
using BoardWalk.Api.Data.Models;
using BoardWalk.Api.Data.Repositories;

namespace BoardWalk.Api.Data.Implementations
{
    public class FriendRequestRepository : Repository<FriendRequest>, IFriendRequestRepository
    {
        public FriendRequestRepository(AppDbContext context) : base(context) { }

        public async Task<FriendRequest?> GetRelationshipAsync(Guid userA, Guid userB)
        {
            return await ((IQueryable<FriendRequest>)_dbSet).FirstOrDefaultAsync(fr =>
                (fr.RequesterId == userA && fr.AddresseeId == userB) ||
                (fr.RequesterId == userB && fr.AddresseeId == userA));
        }

        public async Task<FriendRequest?> GetByIdWithUsersAsync(Guid requestId)
        {
            return await ((IQueryable<FriendRequest>)_dbSet)
                .Include(fr => fr.Requester)
                .Include(fr => fr.Addressee)
                .FirstOrDefaultAsync(fr => fr.Id == requestId);
        }

        public async Task<List<FriendRequest>> GetIncomingPendingAsync(Guid userId)
        {
            return await ((IQueryable<FriendRequest>)_dbSet)
                .Include(fr => fr.Requester)
                .Where(fr => fr.AddresseeId == userId && fr.Status == FriendRequestStatus.Pending)
                .ToListAsync();
        }

        public async Task<List<FriendRequest>> GetAcceptedFriendshipsAsync(Guid userId)
        {
            return await ((IQueryable<FriendRequest>)_dbSet)
                .Include(fr => fr.Requester)
                .Include(fr => fr.Addressee)
                .Where(fr => fr.Status == FriendRequestStatus.Accepted &&
                             (fr.RequesterId == userId || fr.AddresseeId == userId))
                .ToListAsync();
        }
        public async Task<List<FriendRequest>> GetRelationshipsForUserAsync(Guid userId, IEnumerable<Guid> otherUserIds)
        {
            var otherIds = otherUserIds.ToList();
            return await ((IQueryable<FriendRequest>)_dbSet)
                .Where(fr =>
                    (fr.RequesterId == userId && otherIds.Contains(fr.AddresseeId)) ||
                    (fr.AddresseeId == userId && otherIds.Contains(fr.RequesterId)))
                .ToListAsync();
        }
    }
}