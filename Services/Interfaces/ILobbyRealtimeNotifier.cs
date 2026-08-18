namespace BoardWalk.Api.Services.Interfaces
{
    public interface ILobbyRealtimeNotifier
    {
        Task NotifyMemberJoinedAsync(Guid lobbyId, Guid userId);
        Task NotifyMemberLeftAsync(Guid lobbyId, Guid userId);
        Task NotifyMemberKickedAsync(Guid lobbyId, Guid userId);
        Task NotifyMemberRemovedForDisconnectAsync(Guid lobbyId, Guid userId);
        Task NotifyGameStartingAsync(Guid lobbyId);
    }
}