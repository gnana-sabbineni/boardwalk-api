using BoardWalk.Api.Data.Models;
using BoardWalk.Api.Services.Models.Requests;
using BoardWalk.Api.Services.Models.Responses;

namespace BoardWalk.Api.Services.Interfaces
{
    public interface ILobbyService
    {
        Task<LobbyResponse> CreateLobbyAsync(Guid userId, CreateLobbyRequest request);
        Task SendInviteAsync(Guid hostUserId, Guid lobbyId, InviteToLobbyRequest request);
        Task RespondToInviteAsync(Guid userId, Guid inviteId, bool accept);
        Task LeaveLobbyAsync(Guid userId);
        Task KickMemberAsync(Guid hostUserId, Guid targetUserId);
        Task RemoveDisconnectedMemberAsync(Guid lobbyId, Guid userId);
        Task StartGameAsync(Guid hostUserId);
        Task<LobbyResponse?> GetCurrentLobbyAsync(Guid userId);
    }
}
