using BoardWalk.Monopoly.Models;
using Microsoft.EntityFrameworkCore;

namespace BoardWalk.Monopoly.Data
{
    /// <summary>
    /// All database access for the module, over whatever DbContext the host registered. It shares the host's
    /// DbContext instance (scoped per request), so the host and the game can commit in one transaction.
    /// </summary>
    public class GameStore
    {
        private readonly DbContext _db;

        public GameStore(DbContext db) => _db = db;

        // ----- Writes (registered as Added explicitly so EF always INSERTs them) -----

        public void AddGame(Game game) => _db.Set<Game>().Add(game);
        public void AddEvent(GameEvent gameEvent) => _db.Set<GameEvent>().Add(gameEvent);
        public void AddSnapshot(GameSnapshot snapshot) => _db.Set<GameSnapshot>().Add(snapshot);
        public void AddTrade(Trade trade) => _db.Set<Trade>().Add(trade);

        public Task<int> SaveChangesAsync() => _db.SaveChangesAsync();

        // ----- Loading a game -----

        /// <summary>Loads the game with its players, property rows and PENDING trades (with items).</summary>
        public Task<Game?> GetFullAsync(Guid gameId) =>
            _db.Set<Game>()
                .Include(g => g.Players)
                .Include(g => g.Properties)
                .Include(g => g.Trades.Where(t => t.Status == TradeStatus.Pending)).ThenInclude(t => t.Items)
                .AsSplitQuery()
                .FirstOrDefaultAsync(g => g.Id == gameId);

        /// <summary>Id only (cheap) — used to take the per-game lock before loading the full game.</summary>
        public Task<Guid?> GetActiveGameIdForUserAsync(Guid userId, SeatControl control) =>
            _db.Set<Game>()
                .Where(g => g.Status == GameStatus.InProgress &&
                            g.Players.Any(p => p.UserId == userId && p.ControlledBy == control))
                .Select(g => (Guid?)g.Id)
                .FirstOrDefaultAsync();

        public async Task<Game?> GetActiveForUserAsync(Guid userId, SeatControl control)
        {
            var id = await GetActiveGameIdForUserAsync(userId, control);
            return id == null ? null : await GetFullAsync(id.Value);
        }

        // ----- Turn clock and bots -----

        public Task<List<Guid>> GetOverdueGameIdsAsync(DateTime now, int take) =>
            _db.Set<Game>()
                .Where(g => g.Status == GameStatus.InProgress && g.TurnDeadlineAt != null && g.TurnDeadlineAt <= now)
                .OrderBy(g => g.TurnDeadlineAt)
                .Select(g => g.Id)
                .Take(take)
                .ToListAsync();

        /// <summary>In-progress games that contain at least one active bot seat.</summary>
        public Task<List<Guid>> GetGamesWithBotSeatsAsync() =>
            _db.Set<Game>()
                .Where(g => g.Status == GameStatus.InProgress &&
                            g.Players.Any(p => p.ControlledBy == SeatControl.Bot && !p.IsBankrupt))
                .Select(g => g.Id)
                .ToListAsync();

        // ----- History -----

        public Task<bool> IsParticipantAsync(Guid gameId, Guid userId) =>
            _db.Set<GamePlayer>().AnyAsync(p => p.GameId == gameId && p.UserId == userId);

        public Task<List<GamePlayer>> GetPlayersAsync(Guid gameId) =>
            _db.Set<GamePlayer>().Where(p => p.GameId == gameId).OrderBy(p => p.TurnOrder).ToListAsync();

        /// <summary>Newest first.</summary>
        public Task<List<GameEvent>> GetRecentEventsAsync(Guid gameId, int count) =>
            _db.Set<GameEvent>().Where(e => e.GameId == gameId).OrderByDescending(e => e.Sequence).Take(count).ToListAsync();

        /// <summary>Oldest first, strictly after <paramref name="afterSequence"/>.</summary>
        public Task<List<GameEvent>> GetEventsAfterAsync(Guid gameId, int afterSequence, int take) =>
            _db.Set<GameEvent>().Where(e => e.GameId == gameId && e.Sequence > afterSequence).OrderBy(e => e.Sequence).Take(take).ToListAsync();

        public Task<List<GameSnapshot>> GetSnapshotsAsync(Guid gameId, int? fromTurn, int? toTurn) =>
            _db.Set<GameSnapshot>()
                .Where(s => s.GameId == gameId &&
                            (fromTurn == null || s.Turn >= fromTurn) &&
                            (toTurn == null || s.Turn <= toTurn))
                .OrderBy(s => s.Turn).ThenBy(s => s.Seat)
                .ToListAsync();
    }
}
