namespace BoardWalk.Api.Data.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        IUserRepository Users { get; }
        IFriendRequestRepository FriendRequests { get; }
        INotificationRepository Notifications { get; }
        IPasswordResetTokenRepository PasswordResetTokens { get; }
        Task<int> SaveChangesAsync();
    }
}
