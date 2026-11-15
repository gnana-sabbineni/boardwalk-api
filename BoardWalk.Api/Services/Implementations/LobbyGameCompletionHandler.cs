using BoardWalk.Api.Data.Models;
using BoardWalk.Api.Data.Repositories;
using BoardWalk.Monopoly.Abstractions;

namespace BoardWalk.Api.Services.Implementations
{
    /// <summary>
    /// When a game ends (won or abandoned) the lobby it ran in is closed and every player is freed to create or join
    /// another. Runs inside the game's own save, so the final game state and the lobby cleanup commit together.
    /// Idempotent: a player who already left, or a lobby already closed, is simply skipped.
    /// </summary>
    public class LobbyGameCompletionHandler : IGameCompletionHandler
    {
        private readonly IUnitOfWork _unitOfWork;

        public LobbyGameCompletionHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

        public async Task OnGameEndedAsync(GameEndedInfo info)
        {
            foreach (var userId in info.PlayerUserIds)
            {
                var user = await _unitOfWork.Users.GetByIdAsync(userId);
                if (user != null && user.CurrentLobbyId == info.RoomId)
                {
                    user.CurrentLobbyId = null;
                    _unitOfWork.Users.Update(user);
                }
            }

            var lobby = await _unitOfWork.Lobbies.GetWithMembersAsync(info.RoomId);
            if (lobby == null) return;

            foreach (var member in lobby.Members.ToList())
                _unitOfWork.LobbyMembers.Delete(member);

            if (lobby.Status != LobbyStatus.Closed)
            {
                lobby.Status = LobbyStatus.Closed;
                lobby.ClosedAt = DateTime.UtcNow;
            }
            _unitOfWork.Lobbies.Update(lobby);
        }
    }
}
