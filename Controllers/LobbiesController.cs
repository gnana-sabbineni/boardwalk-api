using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BoardWalk.Api.Services.Interfaces;
using BoardWalk.Api.Services.Models.Requests;

namespace BoardWalk.Api.Controllers
{
    [Authorize]
    [Route("api/lobbies")]
    public class LobbiesController : ApiControllerBase
    {
        private readonly ILobbyService _lobbyService;

        public LobbiesController(ILobbyService lobbyService) => _lobbyService = lobbyService;

        /// <summary>Creates a new lobby with the current user as host.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateLobbyRequest request)
        {
            try
            {
                var result = await _lobbyService.CreateLobbyAsync(CurrentUserId, request);
                return SuccessResponse(result, "Lobby created.", statusCode: 201);
            }
            catch (InvalidOperationException ex)
            {
                return FailResponse(ex.Message, statusCode: 400);
            }
        }

        /// <summary>Returns the current user's active lobby, if any.</summary>
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrent()
        {
            var result = await _lobbyService.GetCurrentLobbyAsync(CurrentUserId);
            return result == null
                ? FailResponse("You are not currently in a lobby.", statusCode: 404)
                : SuccessResponse(result, "Current lobby retrieved.");
        }

        /// <summary>Host invites another user to the lobby.</summary>
        [HttpPost("invites")]
        public async Task<IActionResult> Invite([FromBody] InviteToLobbyRequest request)
        {
            try
            {
                var lobby = await _lobbyService.GetCurrentLobbyAsync(CurrentUserId)
                    ?? throw new InvalidOperationException("You are not in a lobby.");
                await _lobbyService.SendInviteAsync(CurrentUserId, lobby.Id, request);
                return SuccessResponse<object>(null, "Invite sent.", statusCode: 201);
            }
            catch (UnauthorizedAccessException ex) { return FailResponse(ex.Message, statusCode: 403); }
            catch (InvalidOperationException ex) { return FailResponse(ex.Message, statusCode: 400); }
        }

        /// <summary>Leaves the current lobby.</summary>
        [HttpPost("leave")]
        public async Task<IActionResult> Leave()
        {
            try
            {
                await _lobbyService.LeaveLobbyAsync(CurrentUserId);
                return SuccessResponse<object>(null, "Left the lobby.");
            }
            catch (InvalidOperationException ex) { return FailResponse(ex.Message, statusCode: 400); }
        }

        /// <summary>Host kicks a member from the lobby.</summary>
        [HttpPost("kick/{targetUserId}")]
        public async Task<IActionResult> Kick(Guid targetUserId)
        {
            try
            {
                await _lobbyService.KickMemberAsync(CurrentUserId, targetUserId);
                return SuccessResponse<object>(null, "Member kicked.");
            }
            catch (UnauthorizedAccessException ex) { return FailResponse(ex.Message, statusCode: 403); }
            catch (InvalidOperationException ex) { return FailResponse(ex.Message, statusCode: 400); }
        }

        /// <summary>Host starts the game, handing off to the game engine (Phase 3).</summary>
        [HttpPost("start")]
        public async Task<IActionResult> Start()
        {
            try
            {
                await _lobbyService.StartGameAsync(CurrentUserId);
                return SuccessResponse<object>(null, "Game starting.");
            }
            catch (UnauthorizedAccessException ex) { return FailResponse(ex.Message, statusCode: 403); }
            catch (InvalidOperationException ex) { return FailResponse(ex.Message, statusCode: 400); }
        }

        /// <summary>Host-only. Closes the lobby entirely, removing all members. Different from
        /// Leave, which transfers host to the next member instead of ending the lobby.</summary>
        [HttpPost("close")]
        public async Task<IActionResult> Close()
        {
            try
            {
                await _lobbyService.CloseLobbyAsync(CurrentUserId);
                return SuccessResponse<object>(null, "Lobby closed.");
            }
            catch (UnauthorizedAccessException ex) { return FailResponse(ex.Message, statusCode: 403); }
            catch (InvalidOperationException ex) { return FailResponse(ex.Message, statusCode: 400); }
        }
    }
}