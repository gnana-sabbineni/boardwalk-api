using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BoardWalk.Api.Services.Auth;
using BoardWalk.Monopoly.Abstractions;

namespace BoardWalk.Api.Controllers
{
    /// <summary>Endpoints for the bot service only. A person's JWT is rejected here.</summary>
    [Authorize(AuthenticationSchemes = BotAuth.Scheme)]
    [Route("api/bot")]
    public class BotController : ApiControllerBase
    {
        private readonly IGameService _gameService;

        public BotController(IGameService gameService) => _gameService = gameService;

        /// <summary>
        /// Every computer-controlled seat that needs to act right now (its turn, or a trade offer waiting), each with the
        /// game as that seat sees it. The bot then acts through the normal /api/games endpoints with X-Act-As set to that seat.
        /// </summary>
        [HttpGet("turns")]
        public async Task<IActionResult> GetTurns() =>
            SuccessResponse(await _gameService.GetBotTurnsAsync(), "Bot turns retrieved.");
    }
}
