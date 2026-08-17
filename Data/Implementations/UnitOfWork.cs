using BoardWalk.Api.Data;
using BoardWalk.Api.Data.Implementations;
using BoardWalk.Api.Data.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IUserRepository? _users;
    private IFriendRequestRepository? _friendRequests;
    private INotificationRepository? _notifications;
    private IPasswordResetTokenRepository? _passwordResetTokens;

    public UnitOfWork(AppDbContext context) => _context = context;

    public IUserRepository Users => _users ??= new UserRepository(_context);
    public IFriendRequestRepository FriendRequests => _friendRequests ??= new FriendRequestRepository(_context);
    public INotificationRepository Notifications => _notifications ??= new NotificationRepository(_context);
    public IPasswordResetTokenRepository PasswordResetTokens => _passwordResetTokens ??= new PasswordResetTokenRepository(_context);

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();
    public void Dispose() => _context.Dispose();
}