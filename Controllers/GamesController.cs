using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BoardWalk.Api.Services.Interfaces;
using BoardWalk.Api.Services.Models.Requests;

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

        // ----- Player actions -----

        /// <summary>Owned properties of any player in the game, grouped by colour (for the properties panel).</summary>
        [HttpGet("players/{userId}/properties")]
        public Task<IActionResult> GetPlayerProperties(Guid userId) => Run(async () =>
            SuccessResponse(await _gameService.GetPlayerPropertiesAsync(CurrentUserId, userId), "Properties retrieved."));

        /// <summary>Builds a house (or hotel) on a street. Own turn only; needs the full colour group.</summary>
        [HttpPost("properties/{tileIndex}/build")]
        public Task<IActionResult> Build(int tileIndex) => Run(async () =>
            SuccessResponse(await _gameService.BuildAsync(CurrentUserId, tileIndex), "Built."));

        /// <summary>Sells one building back to the bank for half its cost.</summary>
        [HttpPost("properties/{tileIndex}/sell-building")]
        public Task<IActionResult> SellBuilding(int tileIndex) => Run(async () =>
            SuccessResponse(await _gameService.SellBuildingAsync(CurrentUserId, tileIndex), "Building sold."));

        /// <summary>Mortgages a property for half its price.</summary>
        [HttpPost("properties/{tileIndex}/mortgage")]
        public Task<IActionResult> Mortgage(int tileIndex) => Run(async () =>
            SuccessResponse(await _gameService.MortgageAsync(CurrentUserId, tileIndex), "Property mortgaged."));

        /// <summary>Lifts a mortgage (half the price plus 10% interest).</summary>
        [HttpPost("properties/{tileIndex}/unmortgage")]
        public Task<IActionResult> Unmortgage(int tileIndex) => Run(async () =>
            SuccessResponse(await _gameService.UnmortgageAsync(CurrentUserId, tileIndex), "Mortgage lifted."));

        /// <summary>Pending trades the caller proposed or received.</summary>
        [HttpGet("trades")]
        public Task<IActionResult> GetTrades() => Run(async () =>
            SuccessResponse(await _gameService.GetTradesAsync(CurrentUserId), "Trades retrieved."));

        /// <summary>Proposes a trade (properties + cash each way) to another player. Any player, any time.</summary>
        [HttpPost("trades")]
        public Task<IActionResult> ProposeTrade([FromBody] ProposeTradeRequest request) => Run(async () =>
            SuccessResponse(await _gameService.ProposeTradeAsync(CurrentUserId, request), "Trade proposed.", statusCode: 201));

        /// <summary>Recipient accepts: properties and cash swap immediately.</summary>
        [HttpPost("trades/{tradeId}/accept")]
        public Task<IActionResult> AcceptTrade(Guid tradeId) => Run(async () =>
            SuccessResponse(await _gameService.AcceptTradeAsync(CurrentUserId, tradeId), "Trade accepted."));

        /// <summary>Recipient declines.</summary>
        [HttpPost("trades/{tradeId}/decline")]
        public Task<IActionResult> DeclineTrade(Guid tradeId) => Run(async () =>
            SuccessResponse(await _gameService.DeclineTradeAsync(CurrentUserId, tradeId), "Trade declined."));

        /// <summary>Proposer withdraws a pending trade.</summary>
        [HttpPost("trades/{tradeId}/cancel")]
        public Task<IActionResult> CancelTrade(Guid tradeId) => Run(async () =>
            SuccessResponse(await _gameService.CancelTradeAsync(CurrentUserId, tradeId), "Trade cancelled."));

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
