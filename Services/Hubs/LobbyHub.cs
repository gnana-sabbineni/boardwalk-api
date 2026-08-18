using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.IdentityModel.Tokens.Jwt;
using BoardWalk.Api.Data.Repositories;
using BoardWalk.Api.Services.Interfaces;
using BoardWalk.Api.Services.Implementations;

namespace BoardWalk.Api.Hubs
{
    [Authorize]
    public class LobbyHub : Hub
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPresenceService _presenceService;
        private readonly ILogger<LobbyHub> _logger;

        public LobbyHub(IUnitOfWork unitOfWork, IPresenceService presenceService, ILogger<LobbyHub> logger)
        {
            _unitOfWork = unitOfWork;
            _presenceService = presenceService;
            _logger = logger;
        }

        private Guid CurrentUserId =>
            Guid.Parse(Context.User!.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

        public override async Task OnConnectedAsync()
        {
            var userId = CurrentUserId;

            // Cancel any pending grace-period timer FIRST — this user just reconnected,
            // so whatever kick-countdown might be running for them needs to stop immediately,
            // before anything else runs.
            LobbyGracePeriodService.CancelGracePeriod(userId);

            await _presenceService.MarkOnlineAsync(userId);

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user?.CurrentLobbyId != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(user.CurrentLobbyId.Value));
                await Clients.OthersInGroup(GroupName(user.CurrentLobbyId.Value))
                    .SendAsync("MemberOnline", userId);
            }

            _logger.LogInformation("SignalR connected: {UserId} ({ConnectionId})", userId, Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = CurrentUserId;
            var remainingConnections = await _presenceService.MarkOfflineAsync(userId);

            if (remainingConnections == 0)
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user?.CurrentLobbyId != null)
                {
                    var lobbyId = user.CurrentLobbyId.Value;

                    await Clients.Group(GroupName(lobbyId)).SendAsync("MemberOffline", userId);

                    // Grace period — see LobbyGracePeriodService (§10)
                    LobbyGracePeriodService.StartGracePeriod(lobbyId, userId);
                }
            }

            _logger.LogInformation("SignalR disconnected: {UserId} ({ConnectionId})", userId, Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        public static string GroupName(Guid lobbyId) => $"lobby-{lobbyId}";
    }
}