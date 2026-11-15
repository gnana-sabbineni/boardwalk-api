using BoardWalk.Api.Data.Models;

namespace BoardWalk.Api.Data.Repositories
{
    public interface IGameRepository : IRepository<Game>
    {
        /// <summary>Loads a game with its players (and their users) and property rows.</summary>
        Task<Game?> GetFullAsync(Guid gameId);

        /// <summary>Id only (cheap) — used to take the per-game lock before loading the full game.</summary>
        Task<Guid?> GetActiveGameIdForUserAsync(Guid userId);

        /// <summary>The in-progress game the user is a (non-bankrupt or bankrupt) player in, if any.</summary>
        Task<Game?> GetActiveForUserAsync(Guid userId);

        /// <summary>Most recent events for the game, newest first.</summary>
        Task<List<GameEvent>> GetRecentEventsAsync(Guid gameId, int count);
    }
}
