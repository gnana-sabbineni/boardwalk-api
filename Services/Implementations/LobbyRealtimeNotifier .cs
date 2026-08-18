using Microsoft.AspNetCore.SignalR;
using BoardWalk.Api.Hubs;
using BoardWalk.Api.Services.Interfaces;

namespace BoardWalk.Api.Services.Implementations
{
    public class LobbyRealtimeNotifier : ILobbyRealtimeNotifier
    {
        private readonly IHubContext<LobbyHub> _hubContext;

        public LobbyRealtimeNotifier(IHubContext<LobbyHub> hubContext) => _hubContext = hubContext;

        private string Group(Guid lobbyId) => LobbyHub.GroupName(lobbyId);

        public Task NotifyMemberJoinedAsync(Guid lobbyId, Guid userId) =>
            _hubContext.Clients.Group(Group(lobbyId)).SendAsync("MemberJoined", userId);

        public Task NotifyMemberLeftAsync(Guid lobbyId, Guid userId) =>
            _hubContext.Clients.Group(Group(lobbyId)).SendAsync("MemberLeft", userId);

        public Task NotifyMemberKickedAsync(Guid lobbyId, Guid userId) =>
            _hubContext.Clients.Group(Group(lobbyId)).SendAsync("MemberKicked", userId);

        public Task NotifyMemberRemovedForDisconnectAsync(Guid lobbyId, Guid userId) =>
            _hubContext.Clients.Group(Group(lobbyId)).SendAsync("MemberRemovedForDisconnect", userId);

        public Task NotifyGameStartingAsync(Guid lobbyId) =>
            _hubContext.Clients.Group(Group(lobbyId)).SendAsync("GameStarting");
    }
}