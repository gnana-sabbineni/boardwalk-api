using Microsoft.AspNetCore.SignalR;
using BoardWalk.Api.Hubs;
using BoardWalk.Api.Services.Interfaces;

namespace BoardWalk.Api.Services.Implementations
{
    public class LobbyRealtimeNotifier : ILobbyRealtimeNotifier
    {
        private readonly IHubContext<LobbyHub> _hubContext;
        private readonly IUserConnectionTracker _connectionTracker;
        private readonly ILogger<LobbyRealtimeNotifier> _logger;

        public LobbyRealtimeNotifier(IHubContext<LobbyHub> hubContext, IUserConnectionTracker connectionTracker, ILogger<LobbyRealtimeNotifier> logger)
        {
            _hubContext = hubContext;
            _connectionTracker = connectionTracker;
            _logger = logger;
        }

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

        public async Task AddUserToLobbyGroupAsync(Guid lobbyId, Guid userId)
        {
            var connections = _connectionTracker.GetConnections(userId);
            _logger.LogWarning("Adding user {UserId} to group {LobbyId} — found {Count} connection(s)", userId, lobbyId, connections.Count);
            foreach (var connectionId in connections)
            {
                await _hubContext.Groups.AddToGroupAsync(connectionId, Group(lobbyId));
            }
        }

        public async Task RemoveUserFromLobbyGroupAsync(Guid lobbyId, Guid userId)
        {
            foreach (var connectionId in _connectionTracker.GetConnections(userId))
            {
                await _hubContext.Groups.RemoveFromGroupAsync(connectionId, Group(lobbyId));
            }
        }

        public Task NotifyLobbyClosedAsync(Guid lobbyId) =>
    _hubContext.Clients.Group(Group(lobbyId)).SendAsync("LobbyClosed");
    }
}