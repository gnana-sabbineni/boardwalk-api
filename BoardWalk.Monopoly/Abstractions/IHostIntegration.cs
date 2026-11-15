using BoardWalk.Monopoly.Rules;

namespace BoardWalk.Monopoly.Abstractions
{
    /// <summary>One player handed over by the host when it creates a game.</summary>
    public sealed record PlayerSeat(Guid UserId, string FirstName, string LastName, bool IsBot);

    public sealed record CreateGameRequest(Guid RoomId, IReadOnlyList<PlayerSeat> Players);

    /// <summary>What the host learns when a game stops, so it can clean up its own data (e.g. close the lobby).</summary>
    public sealed record GameEndedInfo(Guid RoomId, Guid GameId, bool Abandoned, Guid? WinnerUserId, IReadOnlyList<Guid> PlayerUserIds);

    /// <summary>
    /// Implemented by the host. Called while the game's changes are still unsaved and in the same DbContext,
    /// so the host's cleanup and the game's final state are committed together or not at all.
    /// </summary>
    public interface IGameCompletionHandler
    {
        Task OnGameEndedAsync(GameEndedInfo info);
    }

    /// <summary>Implemented by the host: who is making the current request.</summary>
    public interface IGameActorContext
    {
        /// <summary>True when the request comes from the trusted bot service acting on behalf of a bot-controlled seat.</summary>
        bool IsBotService { get; }
    }

    /// <summary>
    /// Implemented by the host (SignalR in BoardWalk). Events carry no per-player data: clients re-fetch their own
    /// view on "updated". The room id is whatever the host passed in CreateGameRequest.
    /// </summary>
    public interface IGameRealtimeNotifier
    {
        Task NotifyGameUpdatedAsync(Guid roomId);
        Task NotifyDiceRolledAsync(Guid roomId, Guid userId, int die1, int die2);
        Task NotifyCardDrawnAsync(Guid roomId, Guid userId, ChanceCard card);
        Task NotifyTradeUpdatedAsync(Guid roomId);
        Task NotifyPlayerReplacedByBotAsync(Guid roomId, Guid userId, int seat);
        Task NotifyGameFinishedAsync(Guid roomId, Guid? winnerUserId, bool abandoned);
    }
}
