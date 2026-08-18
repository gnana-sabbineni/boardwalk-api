using BoardWalk.Api.Data.Models;
using BoardWalk.Api.Data.Repositories;
using BoardWalk.Api.Services.Interfaces;
using BoardWalk.Api.Services.Models.Requests;
using BoardWalk.Api.Services.Models.Responses;
using Microsoft.EntityFrameworkCore;

namespace BoardWalk.Api.Services.Implementations
{
    public class LobbyService : ILobbyService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPresenceService _presenceService;
        private readonly ILobbyRealtimeNotifier _realtimeNotifier; // wraps IHubContext, see §8
        private readonly ILogger<LobbyService> _logger;

        public LobbyService(
            IUnitOfWork unitOfWork,
            IPresenceService presenceService,
            ILobbyRealtimeNotifier realtimeNotifier,
            ILogger<LobbyService> logger)
        {
            _unitOfWork = unitOfWork;
            _presenceService = presenceService;
            _realtimeNotifier = realtimeNotifier;
            _logger = logger;
        }

        /// <summary>Creates a new lobby with the current user as host and sole member.</summary>
        /// <exception cref="InvalidOperationException">User is already locked to another active lobby.</exception>
        public async Task<LobbyResponse> CreateLobbyAsync(Guid userId, CreateLobbyRequest request)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            if (user.CurrentLobbyId != null)
                throw new InvalidOperationException("You are already in a lobby. Leave it before creating a new one.");

            var lobby = new Lobby
            {
                Id = Guid.NewGuid(),
                HostUserId = userId,
                Status = LobbyStatus.Open,
                MaxPlayers = request.MaxPlayers,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Lobbies.AddAsync(lobby);

            var member = new LobbyMember
            {
                Id = Guid.NewGuid(),
                LobbyId = lobby.Id,
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            };
            await _unitOfWork.LobbyMembers.AddAsync(member);

            user.CurrentLobbyId = lobby.Id;
            _unitOfWork.Users.Update(user);

            await SaveWithConcurrencyRetryAsync();

            _logger.LogInformation("Lobby {LobbyId} created by {UserId}", lobby.Id, userId);

            var fullLobby = await _unitOfWork.Lobbies.GetWithMembersAsync(lobby.Id);
            return await MapToResponseAsync(fullLobby!);
        }

        /// <summary>Host invites another user to the lobby.</summary>
        public async Task SendInviteAsync(Guid hostUserId, Guid lobbyId, InviteToLobbyRequest request)
        {
            var lobby = await _unitOfWork.Lobbies.GetWithMembersAsync(lobbyId)
                ?? throw new InvalidOperationException("Lobby not found.");

            if (lobby.HostUserId != hostUserId)
                throw new UnauthorizedAccessException("Only the host can send invites.");

            if (lobby.Status != LobbyStatus.Open)
                throw new InvalidOperationException("This lobby is not open for invites.");

            if (lobby.Members.Count >= lobby.MaxPlayers)
                throw new InvalidOperationException("This lobby is full.");

            if (lobby.Members.Any(m => m.UserId == request.InviteeUserId))
                throw new InvalidOperationException("That user is already in this lobby.");

            var existingInvite = await _unitOfWork.LobbyInvites.GetPendingAsync(lobbyId, request.InviteeUserId);
            if (existingInvite != null)
                throw new InvalidOperationException("An invite is already pending for that user.");

            var inviterUser = await _unitOfWork.Users.GetByIdAsync(hostUserId);

            var invite = new LobbyInvite
            {
                Id = Guid.NewGuid(),
                LobbyId = lobbyId,
                InviterUserId = hostUserId,
                InviteeUserId = request.InviteeUserId,
                Status = LobbyInviteStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.LobbyInvites.AddAsync(invite);

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                RecipientUserId = request.InviteeUserId,
                ActorUserId = hostUserId,
                Type = NotificationType.LobbyInvite,
                ReferenceId = invite.Id,
                IsRead = false,
                Outcome = null,
                Message = $"{inviterUser!.FirstName} invited you to a Monopoly game",
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.Notifications.AddAsync(notification);

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Lobby invite sent: {Lobby} -> {Invitee}", lobbyId, request.InviteeUserId);
        }

        /// <summary>
        /// Dispatched from NotificationService when a LobbyInvite notification is responded to.
        /// </summary>
        /// <exception cref="InvalidOperationException">Invite not found, already responded to, lobby full/closed, or user already in another lobby.</exception>
        /// <exception cref="UnauthorizedAccessException">Responder is not the invitee.</exception>
        public async Task RespondToInviteAsync(Guid userId, Guid inviteId, bool accept)
        {
            var invite = await _unitOfWork.LobbyInvites.GetByIdWithDetailsAsync(inviteId)
                ?? throw new InvalidOperationException("Invite not found.");

            if (invite.InviteeUserId != userId)
                throw new UnauthorizedAccessException("You are not authorized to respond to this invite.");

            if (invite.Status != LobbyInviteStatus.Pending)
                throw new InvalidOperationException("This invite has already been responded to.");

            if (!accept)
            {
                _unitOfWork.LobbyInvites.Delete(invite);

                // NEW — reflect the decline on the notification, same as friend requests.
                await UpdateLobbyInviteNotificationAsync(invite, userId, accept: false);

                await _unitOfWork.SaveChangesAsync();
                return;
            }

            // Re-check lobby state at ACCEPT time, not just invite time (FR3) — the lobby
            // may have filled up or closed while this invite was pending.
            var lobby = await _unitOfWork.Lobbies.GetWithMembersAsync(invite.LobbyId)
                ?? throw new InvalidOperationException("This lobby no longer exists.");

            if (lobby.Status != LobbyStatus.Open)
                throw new InvalidOperationException("This lobby is no longer open.");

            if (lobby.Members.Count >= lobby.MaxPlayers)
                throw new InvalidOperationException("This lobby is now full.");

            var user = await _unitOfWork.Users.GetByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            if (user.CurrentLobbyId != null)
                throw new InvalidOperationException("You are already in another lobby.");

            var member = new LobbyMember
            {
                Id = Guid.NewGuid(),
                LobbyId = lobby.Id,
                UserId = userId,
                JoinedAt = DateTime.UtcNow
            };
            await _unitOfWork.LobbyMembers.AddAsync(member);

            user.CurrentLobbyId = lobby.Id;
            _unitOfWork.Users.Update(user);

            invite.Status = LobbyInviteStatus.Accepted;
            invite.RespondedAt = DateTime.UtcNow;
            _unitOfWork.LobbyInvites.Update(invite);

            // NEW — reflect the acceptance on the notification, same as friend requests.
            await UpdateLobbyInviteNotificationAsync(invite, userId, accept: true);

            await SaveWithConcurrencyRetryAsync();

            await _realtimeNotifier.NotifyMemberJoinedAsync(lobby.Id, userId);

            _logger.LogInformation("User {UserId} joined lobby {LobbyId} via invite", userId, lobby.Id);
        }

        /// <summary>
        /// Finds the Notification created when this lobby invite was sent, and updates its
        /// Outcome/IsRead/Message to reflect the response — mirrors FriendService's equivalent
        /// block exactly, so both notification types stay consistent.
        /// </summary>
        private async Task UpdateLobbyInviteNotificationAsync(LobbyInvite invite, Guid userId, bool accept)
        {
            var notification = await _unitOfWork.Notifications.FindAsync(n =>
                n.Type == NotificationType.LobbyInvite &&
                n.ReferenceId == invite.Id &&
                n.RecipientUserId == userId);

            if (notification != null)
            {
                notification.Outcome = accept ? NotificationOutcome.Accepted : NotificationOutcome.Declined;
                notification.IsRead = true;
                notification.LastModifiedAt = DateTime.UtcNow;
                notification.Message = accept
                    ? $"You joined {invite.Inviter.FirstName}'s Monopoly game"
                    : $"You declined {invite.Inviter.FirstName}'s Monopoly invite";
                _unitOfWork.Notifications.Update(notification);
            }
            else
            {
                _logger.LogWarning(
                    "No matching notification found for LobbyInvite {InviteId} and recipient {UserId}",
                    invite.Id, userId);
            }
        }

        /// <summary>Removes the current user from their lobby. Transfers host or closes the lobby as needed.</summary>
        public async Task LeaveLobbyAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId)
                ?? throw new InvalidOperationException("User not found.");

            if (user.CurrentLobbyId == null)
                throw new InvalidOperationException("You are not in a lobby.");

            var lobby = await _unitOfWork.Lobbies.GetWithMembersAsync(user.CurrentLobbyId.Value)
                ?? throw new InvalidOperationException("Lobby not found.");

            if (lobby.Status == LobbyStatus.InProgress)
                throw new InvalidOperationException("Cannot leave a lobby once the game has started.");

            await RemoveMemberInternalAsync(lobby, userId);

            await _realtimeNotifier.NotifyMemberLeftAsync(lobby.Id, userId);
        }

        /// <summary>Host removes another member from the lobby.</summary>
        public async Task KickMemberAsync(Guid hostUserId, Guid targetUserId)
        {
            if (hostUserId == targetUserId)
                throw new InvalidOperationException("Use leave, not kick, to remove yourself.");

            var host = await _unitOfWork.Users.GetByIdAsync(hostUserId)
                ?? throw new InvalidOperationException("User not found.");

            if (host.CurrentLobbyId == null)
                throw new InvalidOperationException("You are not in a lobby.");

            var lobby = await _unitOfWork.Lobbies.GetWithMembersAsync(host.CurrentLobbyId.Value)
                ?? throw new InvalidOperationException("Lobby not found.");

            if (lobby.HostUserId != hostUserId)
                throw new UnauthorizedAccessException("Only the host can kick members.");

            if (lobby.Status == LobbyStatus.InProgress)
                throw new InvalidOperationException("Cannot kick once the game has started.");

            if (!lobby.Members.Any(m => m.UserId == targetUserId))
                throw new InvalidOperationException("That user is not in this lobby.");

            await RemoveMemberInternalAsync(lobby, targetUserId);

            await _realtimeNotifier.NotifyMemberKickedAsync(lobby.Id, targetUserId);
        }

        /// <summary>Called by the grace-period expiry path after presence confirms the user is still offline.</summary>
        public async Task RemoveDisconnectedMemberAsync(Guid lobbyId, Guid userId)
        {
            var lobby = await _unitOfWork.Lobbies.GetWithMembersAsync(lobbyId);
            if (lobby == null || lobby.Status != LobbyStatus.Open) return;
            if (!lobby.Members.Any(m => m.UserId == userId)) return;

            await RemoveMemberInternalAsync(lobby, userId);
            await _realtimeNotifier.NotifyMemberRemovedForDisconnectAsync(lobby.Id, userId);
        }

        public async Task StartGameAsync(Guid hostUserId)
        {
            var host = await _unitOfWork.Users.GetByIdAsync(hostUserId)
                ?? throw new InvalidOperationException("User not found.");

            if (host.CurrentLobbyId == null)
                throw new InvalidOperationException("You are not in a lobby.");

            var lobby = await _unitOfWork.Lobbies.GetWithMembersAsync(host.CurrentLobbyId.Value)
                ?? throw new InvalidOperationException("Lobby not found.");

            if (lobby.HostUserId != hostUserId)
                throw new UnauthorizedAccessException("Only the host can start the game.");

            if (lobby.Status != LobbyStatus.Open)
                throw new InvalidOperationException("This lobby cannot be started.");

            if (lobby.Members.Count < 2)
                throw new InvalidOperationException("At least 2 players are required to start.");

            foreach (var member in lobby.Members)
            {
                if (!await _presenceService.IsOnlineAsync(member.UserId))
                    throw new InvalidOperationException("All players must be online to start.");
            }

            lobby.Status = LobbyStatus.InProgress;
            lobby.StartedAt = DateTime.UtcNow;
            _unitOfWork.Lobbies.Update(lobby);
            await _unitOfWork.SaveChangesAsync();

            await _realtimeNotifier.NotifyGameStartingAsync(lobby.Id);

            _logger.LogInformation("Lobby {LobbyId} started by host {UserId}", lobby.Id, hostUserId);
        }

        public async Task<LobbyResponse?> GetCurrentLobbyAsync(Guid userId)
        {
            var lobby = await _unitOfWork.Lobbies.GetCurrentLobbyForUserAsync(userId);
            return lobby == null ? null : await MapToResponseAsync(lobby);
        }

        // --- Shared internals ---

        private async Task RemoveMemberInternalAsync(Lobby lobby, Guid userId)
        {
            var member = lobby.Members.First(m => m.UserId == userId);
            _unitOfWork.LobbyMembers.Delete(member);

            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            user!.CurrentLobbyId = null;
            _unitOfWork.Users.Update(user);

            var remainingMembers = lobby.Members.Where(m => m.UserId != userId).ToList();

            if (remainingMembers.Count == 0)
            {
                lobby.Status = LobbyStatus.Closed;
                lobby.ClosedAt = DateTime.UtcNow;
            }
            else if (lobby.HostUserId == userId)
            {
                // Transfer to earliest-joined remaining member.
                var newHost = remainingMembers.OrderBy(m => m.JoinedAt).First();
                lobby.HostUserId = newHost.UserId;
            }

            _unitOfWork.Lobbies.Update(lobby);
            await SaveWithConcurrencyRetryAsync();
        }

        /// <summary>
        /// Saves changes, retrying once on a concurrency conflict (DbUpdateConcurrencyException) —
        /// this is what actually enforces the RowVersion protection on User.CurrentLobbyId (INV5).
        /// A caught conflict here means another request modified the same User row first.
        /// </summary>
        private async Task SaveWithConcurrencyRetryAsync()
        {
            try
            {
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException(
                    "This action conflicted with another change happening at the same time. Please try again.");
            }
        }

        private async Task<LobbyResponse> MapToResponseAsync(Lobby lobby)
        {
            var members = new List<LobbyMemberResponse>();
            foreach (var m in lobby.Members)
            {
                members.Add(new LobbyMemberResponse
                {
                    UserId = m.UserId,
                    FirstName = m.User.FirstName,
                    LastName = m.User.LastName,
                    IsHost = m.UserId == lobby.HostUserId,
                    IsOnline = await _presenceService.IsOnlineAsync(m.UserId)
                });
            }

            return new LobbyResponse
            {
                Id = lobby.Id,
                HostUserId = lobby.HostUserId,
                Status = lobby.Status.ToString(),
                MaxPlayers = lobby.MaxPlayers,
                Members = members,
                CreatedAt = lobby.CreatedAt
            };
        }
    }
}