using BoardWalk.Api.Data.Models;

namespace BoardWalk.Api.Data.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        IUserRepository Users { get; }
        IFriendRequestRepository FriendRequests { get; }
        INotificationRepository Notifications { get; }
        IPasswordResetTokenRepository PasswordResetTokens { get; }
        ILobbyRepository Lobbies { get; }
        ILobbyInviteRepository LobbyInvites { get; }
        IRepository<LobbyMember> LobbyMembers { get; }
        IGameRepository Games { get; }
        IRepository<GamePlayer> GamePlayers { get; }
        IRepository<GameProperty> GameProperties { get; }
        IRepository<GameEvent> GameEvents { get; }
        IRepository<Trade> Trades { get; }
        Task<int> SaveChangesAsync();
    }
}
