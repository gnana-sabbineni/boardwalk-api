using BoardWalk.Api.Services.GameRules;

namespace BoardWalk.Api.Services.Interfaces
{
    /// <summary>
    /// Pushes game events to every player of a game over the existing lobby SignalR group
    /// (players are already in "lobby-{id}" from the lobby phase, so no second hub is needed).
    /// Events carry no per-player data: clients re-fetch GET /api/games/current on "GameUpdated".
    /// </summary>
    public interface IGameRealtimeNotifier
    {
        Task NotifyGameUpdatedAsync(Guid lobbyId);
        Task NotifyDiceRolledAsync(Guid lobbyId, Guid userId, int die1, int die2);
        Task NotifyCardDrawnAsync(Guid lobbyId, Guid userId, ChanceCard card);
        Task NotifyGameFinishedAsync(Guid lobbyId, Guid? winnerUserId);
    }
}
