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
    private IGameRepository? _games;
    private IRepository<GamePlayer>? _gamePlayers;
    private IRepository<GameProperty>? _gameProperties;
    private IRepository<GameEvent>? _gameEvents;
    private IRepository<Trade>? _trades;

    public UnitOfWork(AppDbContext context) => _context = context;

    public IUserRepository Users => _users ??= new UserRepository(_context);
    public IFriendRequestRepository FriendRequests => _friendRequests ??= new FriendRequestRepository(_context);
    public INotificationRepository Notifications => _notifications ??= new NotificationRepository(_context);
    public IPasswordResetTokenRepository PasswordResetTokens => _passwordResetTokens ??= new PasswordResetTokenRepository(_context);
    public ILobbyRepository Lobbies => _lobbies ??= new LobbyRepository(_context);
    public ILobbyInviteRepository LobbyInvites => _lobbyInvites ??= new LobbyInviteRepository(_context);
    public IRepository<LobbyMember> LobbyMembers => _lobbyMembers ??= new Repository<LobbyMember>(_context);

    public IGameRepository Games => _games ??= new GameRepository(_context);
    public IRepository<GamePlayer> GamePlayers => _gamePlayers ??= new Repository<GamePlayer>(_context);
    public IRepository<GameProperty> GameProperties => _gameProperties ??= new Repository<GameProperty>(_context);
    public IRepository<GameEvent> GameEvents => _gameEvents ??= new Repository<GameEvent>(_context);

    public IRepository<Trade> Trades => _trades ??= new Repository<Trade>(_context);

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();
    public void Dispose() => _context.Dispose();
}