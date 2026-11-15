using Microsoft.EntityFrameworkCore;
using BoardWalk.Api.Data.Models;
using BoardWalk.Api.Data.Repositories;

namespace BoardWalk.Api.Data.Implementations
{
    public class GameRepository : Repository<Game>, IGameRepository
    {
        public GameRepository(AppDbContext context) : base(context) { }

        public async Task<Game?> GetFullAsync(Guid gameId)
        {
            return await ((IQueryable<Game>)_dbSet)
                .Include(g => g.Players).ThenInclude(p => p.User)
                .Include(g => g.Properties)
                .AsSplitQuery()
                .FirstOrDefaultAsync(g => g.Id == gameId);
        }

        public async Task<Guid?> GetActiveGameIdForUserAsync(Guid userId)
        {
            return await ((IQueryable<Game>)_dbSet)
                .Where(g => g.Status == GameStatus.InProgress && g.Players.Any(p => p.UserId == userId))
                .Select(g => (Guid?)g.Id)
                .FirstOrDefaultAsync();
        }

        public async Task<Game?> GetActiveForUserAsync(Guid userId)
        {
            return await ((IQueryable<Game>)_dbSet)
                .Include(g => g.Players).ThenInclude(p => p.User)
                .Include(g => g.Properties)
                .AsSplitQuery()
                .Where(g => g.Status == GameStatus.InProgress && g.Players.Any(p => p.UserId == userId))
                .FirstOrDefaultAsync();
        }

        public async Task<List<GameEvent>> GetRecentEventsAsync(Guid gameId, int count)
        {
            return await _context.Set<GameEvent>()
                .Where(e => e.GameId == gameId)
                .OrderByDescending(e => e.Sequence)
                .Take(count)
                .ToListAsync();
        }
    }
}
