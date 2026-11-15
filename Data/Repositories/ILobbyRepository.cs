using BoardWalk.Api.Data.Models;

namespace BoardWalk.Api.Data.Repositories
{
    public interface ILobbyRepository : IRepository<Lobby>
    {
        Task<Lobby?> GetWithMembersAsync(Guid lobbyId);
        Task<Lobby?> GetCurrentLobbyForUserAsync(Guid userId);
    }
}