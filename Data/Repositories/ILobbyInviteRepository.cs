using BoardWalk.Api.Data.Models;

namespace BoardWalk.Api.Data.Repositories
{
    public interface ILobbyInviteRepository : IRepository<LobbyInvite>
    {
        Task<LobbyInvite?> GetPendingAsync(Guid lobbyId, Guid inviteeUserId);
        Task<LobbyInvite?> GetByIdWithDetailsAsync(Guid inviteId);
    }
}