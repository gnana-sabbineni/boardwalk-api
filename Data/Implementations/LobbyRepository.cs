using Microsoft.EntityFrameworkCore;
using BoardWalk.Api.Data.Models;
using BoardWalk.Api.Data.Repositories;

namespace BoardWalk.Api.Data.Implementations
{
    public class LobbyRepository : Repository<Lobby>, ILobbyRepository
    {
        public LobbyRepository(AppDbContext context) : base(context) { }

        public async Task<Lobby?> GetWithMembersAsync(Guid lobbyId)
        {
            return await ((IQueryable<Lobby>)_dbSet)
                .Include(l => l.Members).ThenInclude(m => m.User)
                .Include(l => l.Host)
                .FirstOrDefaultAsync(l => l.Id == lobbyId);
        }

        public async Task<Lobby?> GetCurrentLobbyForUserAsync(Guid userId)
        {
            return await ((IQueryable<Lobby>)_dbSet)
                .Include(l => l.Members).ThenInclude(m => m.User)
                .Include(l => l.Host)
                .Where(l => l.Members.Any(m => m.UserId == userId) && l.Status != LobbyStatus.Closed)
                .FirstOrDefaultAsync();
        }
    }
}