using Microsoft.AspNetCore.SignalR;
using BoardWalk.Api.Hubs;
using BoardWalk.Api.Services.GameRules;
using BoardWalk.Api.Services.Interfaces;

namespace BoardWalk.Api.Services.Implementations
{
    public class GameRealtimeNotifier : IGameRealtimeNotifier
    {
        private readonly IHubContext<LobbyHub> _hubContext;

        public GameRealtimeNotifier(IHubContext<LobbyHub> hubContext) => _hubContext = hubContext;

        private static string Group(Guid lobbyId) => LobbyHub.GroupName(lobbyId);

        public Task NotifyGameUpdatedAsync(Guid lobbyId) =>
            _hubContext.Clients.Group(Group(lobbyId)).SendAsync("GameUpdated");

        public Task NotifyDiceRolledAsync(Guid lobbyId, Guid userId, int die1, int die2) =>
            _hubContext.Clients.Group(Group(lobbyId)).SendAsync("DiceRolled", userId, die1, die2);

        public Task NotifyCardDrawnAsync(Guid lobbyId, Guid userId, ChanceCard card) =>
            _hubContext.Clients.Group(Group(lobbyId)).SendAsync("CardDrawn", userId, new
            {
                id = card.Id,
                title = card.Title,
                description = card.Description
            });

        public Task NotifyGameFinishedAsync(Guid lobbyId, Guid? winnerUserId) =>
            _hubContext.Clients.Group(Group(lobbyId)).SendAsync("GameFinished", winnerUserId);
    }
}
