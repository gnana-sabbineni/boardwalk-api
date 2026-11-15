using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BoardWalk.Api.Services.Interfaces;

namespace BoardWalk.Api.Controllers
{
    /// <summary>
    /// In-game actions. Every action applies to the caller's current game, so none need a game id.
    /// Rule violations (not your turn, can't afford it, etc.) come back as 400 with the reason in the message.
    /// </summary>
    [Authorize]
    [Route("api/games")]
    public class GamesController : ApiControllerBase
    {
        private readonly IGameService _gameService;

        public GamesController(IGameService gameService) => _gameService = gameService;

        /// <summary>The static board: 40 tiles with names, colour groups, prices and rent tiers.</summary>
        [HttpGet("board")]
        public IActionResult GetBoard() => SuccessResponse(_gameService.GetBoard(), "Board retrieved.");

        /// <summary>The caller's in-progress game (players, cash, positions, ownership, log, available actions).</summary>
        [HttpGet("current")]
        public async Task<IActionResult> GetCurrent()
        {
            var state = await _gameService.GetCurrentGameAsync(CurrentUserId);
            return state == null
                ? FailResponse("You are not currently in a game.", statusCode: 404)
                : SuccessResponse(state, "Current game retrieved.");
        }

        /// <summary>Rolls the dice for the current player and resolves the move.</summary>
        [HttpPost("roll")]
        public Task<IActionResult> Roll() => Run(async () =>
            SuccessResponse(await _gameService.RollAsync(CurrentUserId), "Dice rolled."));

        /// <summary>Buys the unowned property the player just landed on.</summary>
        [HttpPost("buy")]
        public Task<IActionResult> Buy() => Run(async () =>
            SuccessResponse(await _gameService.BuyPropertyAsync(CurrentUserId), "Property bought."));

        /// <summary>Declines to buy the property the player just landed on.</summary>
        [HttpPost("pass")]
        public Task<IActionResult> Pass() => Run(async () =>
            SuccessResponse(await _gameService.PassPropertyAsync(CurrentUserId), "Passed on property."));

        /// <summary>Ends the current player's turn.</summary>
        [HttpPost("end-turn")]
        public Task<IActionResult> EndTurn() => Run(async () =>
            SuccessResponse(await _gameService.EndTurnAsync(CurrentUserId), "Turn ended."));

        /// <summary>Pays the Detention Cove fine to leave before rolling.</summary>
        [HttpPost("jail/pay")]
        public Task<IActionResult> PayJailFine() => Run(async () =>
            SuccessResponse(await _gameService.PayJailFineAsync(CurrentUserId), "Fine paid."));

        /// <summary>Uses a Harbour Pass card to leave Detention Cove.</summary>
        [HttpPost("jail/card")]
        public Task<IActionResult> UseJailCard() => Run(async () =>
            SuccessResponse(await _gameService.UseJailCardAsync(CurrentUserId), "Card used."));

        /// <summary>Settles a pending debt (rent, tax or fee) once the player has enough cash.</summary>
        [HttpPost("debt/pay")]
        public Task<IActionResult> PayDebt() => Run(async () =>
            SuccessResponse(await _gameService.PayDebtAsync(CurrentUserId), "Debt paid."));

        /// <summary>Declares bankruptcy: assets go to the creditor (or the bank) and the player is out.</summary>
        [HttpPost("bankruptcy")]
        public Task<IActionResult> DeclareBankruptcy() => Run(async () =>
            SuccessResponse(await _gameService.DeclareBankruptcyAsync(CurrentUserId), "Bankruptcy declared."));

        private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
        {
            try
            {
                return await action();
            }
            catch (InvalidOperationException ex)
            {
                return FailResponse(ex.Message, statusCode: 400);
            }
        }
    }
}
