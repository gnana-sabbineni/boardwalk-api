using Microsoft.AspNetCore.SignalR;
using BoardWalk.Api.Hubs;
using BoardWalk.Monopoly.Abstractions;
using BoardWalk.Monopoly.Rules;

namespace BoardWalk.Api.Services.Implementations
{
    /// <summary>
    /// Delivers game events over the existing lobby SignalR group (players are already in "lobby-{id}" from the lobby phase,
    /// and the game's room id IS the lobby id), so no second hub or connection is needed.
    /// </summary>
    public class MonopolyRealtimeNotifier : IGameRealtimeNotifier
    {
        private readonly IHubContext<LobbyHub> _hubContext;

        public MonopolyRealtimeNotifier(IHubContext<LobbyHub> hubContext) => _hubContext = hubContext;

        private static string Group(Guid roomId) => LobbyHub.GroupName(roomId);

        public Task NotifyGameUpdatedAsync(Guid roomId) =>
            _hubContext.Clients.Group(Group(roomId)).SendAsync("GameUpdated");

        public Task NotifyDiceRolledAsync(Guid roomId, Guid userId, int die1, int die2) =>
            _hubContext.Clients.Group(Group(roomId)).SendAsync("DiceRolled", userId, die1, die2);

        public Task NotifyCardDrawnAsync(Guid roomId, Guid userId, ChanceCard card) =>
            _hubContext.Clients.Group(Group(roomId)).SendAsync("CardDrawn", userId, new
            {
                id = card.Id,
                title = card.Title,
                description = card.Description
            });

        public Task NotifyTradeUpdatedAsync(Guid roomId) =>
            _hubContext.Clients.Group(Group(roomId)).SendAsync("TradeUpdated");

        public Task NotifyPlayerReplacedByBotAsync(Guid roomId, Guid userId, int seat) =>
            _hubContext.Clients.Group(Group(roomId)).SendAsync("PlayerReplacedByBot", userId, seat);

        public Task NotifyGameFinishedAsync(Guid roomId, Guid? winnerUserId, bool abandoned) =>
            _hubContext.Clients.Group(Group(roomId)).SendAsync("GameFinished", winnerUserId, abandoned);
    }
}
