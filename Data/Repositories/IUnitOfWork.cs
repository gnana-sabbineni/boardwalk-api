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
        Task<int> SaveChangesAsync();
    }
}
