using Microsoft.EntityFrameworkCore;
using BoardWalk.Api.Data.Models;
using BoardWalk.Api.Data.Repositories;

namespace BoardWalk.Api.Data.Implementations
{
    public class LobbyInviteRepository : Repository<LobbyInvite>, ILobbyInviteRepository
    {
        public LobbyInviteRepository(AppDbContext context) : base(context) { }

        public async Task<LobbyInvite?> GetPendingAsync(Guid lobbyId, Guid inviteeUserId)
        {
            return await ((IQueryable<LobbyInvite>)_dbSet)
                .FirstOrDefaultAsync(i => i.LobbyId == lobbyId &&
                                          i.InviteeUserId == inviteeUserId &&
                                          i.Status == LobbyInviteStatus.Pending);
        }

        public async Task<LobbyInvite?> GetByIdWithDetailsAsync(Guid inviteId)
        {
            return await ((IQueryable<LobbyInvite>)_dbSet)
                .Include(i => i.Lobby)
                .Include(i => i.Inviter)
                .Include(i => i.Invitee)
                .FirstOrDefaultAsync(i => i.Id == inviteId);
        }
    }
}