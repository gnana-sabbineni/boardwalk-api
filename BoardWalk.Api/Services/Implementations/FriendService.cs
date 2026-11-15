using BoardWalk.Api.Data.Models;
using BoardWalk.Api.Data.Repositories;
using BoardWalk.Api.Services.Interfaces;
using BoardWalk.Api.Services.Models.Requests;
using BoardWalk.Api.Services.Models.Responses;

namespace BoardWalk.Api.Services.Implementations
{
    public class FriendService : IFriendService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<FriendService> _logger;
        private readonly ICurrentUserService _currentUserService;

        public FriendService(IUnitOfWork unitOfWork, ILogger<FriendService> logger, ICurrentUserService currentUserService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _currentUserService = currentUserService;
        }

        public async Task<List<FriendResponse>> GetFriendsAsync(Guid userId)
        {
            var relationships = await _unitOfWork.FriendRequests.GetAcceptedFriendshipsAsync(userId);

            // For each accepted row, figure out which side is "the friend"
            // (not the current user) and map that side's info.
            return relationships.Select(r =>
            {
                var friend = r.RequesterId == userId ? r.Addressee : r.Requester;
                return new FriendResponse
                {
                    UserId = friend.Id,
                    FirstName = friend.FirstName,
                    LastName = friend.LastName,
                    Email = friend.Email,
                    FriendsSince = r.RespondedAt ?? r.CreatedAt
                };
            }).ToList();
        }

        public async Task<List<FriendResponse>> SearchFriendsAsync(Guid userId, string query)
        {
            var friends = await GetFriendsAsync(userId);
            var lowered = query.ToLower();
            return friends.Where(f =>
                f.FirstName.ToLower().Contains(lowered) ||
                f.LastName.ToLower().Contains(lowered) ||
                f.Email.ToLower().Contains(lowered)).ToList();
        }

        public async Task<List<UserSearchResult>> SearchUsersAsync(Guid userId, string query)
        {
            var users = await _unitOfWork.Users.SearchAsync(query, userId);
            var userIds = users.Select(u => u.Id).ToList();

            // ONE query for all relationships, instead of one per result.
            var relationships = await _unitOfWork.FriendRequests.GetRelationshipsForUserAsync(userId, userIds);

            // Build a lookup: otherUserId -> the FriendRequest row involving them.
            var relationshipByUserId = relationships.ToDictionary(
                fr => fr.RequesterId == userId ? fr.AddresseeId : fr.RequesterId);

            var results = new List<UserSearchResult>();

            foreach (var user in users)
            {
                relationshipByUserId.TryGetValue(user.Id, out var relationship);

                if (relationship?.Status == FriendRequestStatus.Accepted)
                {
                    continue;
                }

                var status = relationship == null
                    ? RelationshipStatus.None
                    : relationship.RequesterId == userId
                        ? RelationshipStatus.PendingSentByMe
                        : RelationshipStatus.PendingReceivedByMe;

                results.Add(new UserSearchResult
                {
                    UserId = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Status = status
                });
            }

            return results;
        }

        public async Task<Guid> SendRequestAsync(Guid requesterId, SendFriendRequestRequest request)
        {
            var loggedUser =  _currentUserService.GetCurrentUser();
            if (loggedUser.Id == request.TargetUserId)
                throw new InvalidOperationException("You cannot send a friend request to yourself.");

            if (requesterId == request.TargetUserId)
                throw new InvalidOperationException("You cannot send a friend request to yourself.");

            var targetUser = await _unitOfWork.Users.GetByIdAsync(request.TargetUserId);
            if (targetUser == null)
                throw new InvalidOperationException("User not found.");

            var existing = await _unitOfWork.FriendRequests.GetRelationshipAsync(requesterId, request.TargetUserId);
            if (existing != null)
            {
                throw new InvalidOperationException(existing.Status switch
                {
                    FriendRequestStatus.Accepted => "You are already friends with this user.",
                    FriendRequestStatus.Pending => "A friend request already exists between you two.",
                    _ => "A previous request exists. Please try again later."
                });
            }

            var friendRequest = new FriendRequest
            {
                Id = Guid.NewGuid(),
                RequesterId = requesterId,
                AddresseeId = request.TargetUserId,
                Status = FriendRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.FriendRequests.AddAsync(friendRequest);
            // Notify the addressee
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                RecipientUserId = request.TargetUserId,
                ActorUserId = requesterId,
                Type = NotificationType.FriendRequest,
                ReferenceId = friendRequest.Id,
                IsRead = false,
                Outcome = null,
                Message = $"{loggedUser.FirstName} {loggedUser.LastName} wants to be friends",
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.Notifications.AddAsync(notification);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Friend request sent: {Requester} -> {Addressee}", requesterId, request.TargetUserId);
            return friendRequest.Id;
        }

        public async Task<List<FriendRequestResponse>> GetIncomingRequestsAsync(Guid userId)
        {
            var pending = await _unitOfWork.FriendRequests.GetIncomingPendingAsync(userId);
            return pending.Select(r => new FriendRequestResponse
            {
                RequestId = r.Id,
                FromUserId = r.RequesterId,
                FromFirstName = r.Requester.FirstName,
                FromLastName = r.Requester.LastName,
                FromEmail = r.Requester.Email,
                CreatedAt = r.CreatedAt
            }).ToList();
        }

        public async Task RespondToRequestAsync(Guid userId, Guid requestId, bool accept)
        {
            var request = await _unitOfWork.FriendRequests.GetByIdWithUsersAsync(requestId);
            if (request == null)
                throw new InvalidOperationException("Friend request not found.");

            if (request.AddresseeId != userId)
                throw new UnauthorizedAccessException("You are not authorized to respond to this request.");

            if (request.Status != FriendRequestStatus.Pending)
                throw new InvalidOperationException("This request has already been responded to.");

            if (accept)
            {
                request.Status = FriendRequestStatus.Accepted;
                request.RespondedAt = DateTime.UtcNow;
                _unitOfWork.FriendRequests.Update(request);
            }
            else
            {
                _unitOfWork.FriendRequests.Delete(request);
            }

            // NEW — this block was missing. Find the notification that was created when
            // this friend request was sent, and reflect the outcome on it.
            var notification = await _unitOfWork.Notifications.FindAsync(n =>
                n.Type == NotificationType.FriendRequest &&
                n.ReferenceId == requestId &&
                n.RecipientUserId == userId);

            if (notification != null)
            {
                notification.Outcome = accept ? NotificationOutcome.Accepted : NotificationOutcome.Declined;
                notification.IsRead = true;
                notification.LastModifiedAt = DateTime.UtcNow;
                notification.Message = accept
                    ? $"You accepted {request.Requester.FirstName}'s friend request"
                    : $"You declined {request.Requester.FirstName}'s friend request";
                _unitOfWork.Notifications.Update(notification);
            }
            else
            {
                _logger.LogWarning(
                    "No matching notification found for FriendRequest {RequestId} and recipient {UserId}",
                    requestId, userId);
            }

            await _unitOfWork.SaveChangesAsync(); // commits BOTH the FriendRequest and Notification changes together
        }

        public async Task RemoveFriendAsync(Guid userId, Guid friendUserId)
        {
            var relationship = await _unitOfWork.FriendRequests.GetRelationshipAsync(userId, friendUserId);
            if (relationship == null || relationship.Status != FriendRequestStatus.Accepted)
                throw new InvalidOperationException("You are not friends with this user.");

            _unitOfWork.FriendRequests.Delete(relationship);
            await _unitOfWork.SaveChangesAsync();
        }
    }
}