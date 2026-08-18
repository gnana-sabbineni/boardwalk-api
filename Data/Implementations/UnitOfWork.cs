using BoardWalk.Api.Data;
using BoardWalk.Api.Data.Implementations;
using BoardWalk.Api.Data.Models;
using BoardWalk.Api.Data.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IUserRepository? _users;
    private IFriendRequestRepository? _friendRequests;
    private INotificationRepository? _notifications;
    private IPasswordResetTokenRepository? _passwordResetTokens;
    private ILobbyRepository? _lobbies;
    private ILobbyInviteRepository? _lobbyInvites;
    private IRepository<LobbyMember>? _lobbyMembers;

    public UnitOfWork(AppDbContext context) => _context = context;

    public IUserRepository Users => _users ??= new UserRepository(_context);
    public IFriendRequestRepository FriendRequests => _friendRequests ??= new FriendRequestRepository(_context);
    public INotificationRepository Notifications => _notifications ??= new NotificationRepository(_context);
    public IPasswordResetTokenRepository PasswordResetTokens => _passwordResetTokens ??= new PasswordResetTokenRepository(_context);
    public ILobbyRepository Lobbies => _lobbies ??= new LobbyRepository(_context);
    public ILobbyInviteRepository LobbyInvites => _lobbyInvites ??= new LobbyInviteRepository(_context);
    public IRepository<LobbyMember> LobbyMembers => _lobbyMembers ??= new Repository<LobbyMember>(_context);

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();
    public void Dispose() => _context.Dispose();
}