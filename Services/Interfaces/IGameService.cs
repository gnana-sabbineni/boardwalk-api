using BoardWalk.Api.Data.Models;
using BoardWalk.Api.Services.Models.Requests;
using BoardWalk.Api.Services.Models.Responses;

namespace BoardWalk.Api.Services.Interfaces
{
    public interface IGameService
    {
        /// <summary>
        /// Builds a Game (players, turn order, cash, property rows, shuffled deck) from a lobby and adds it
        /// to the unit of work WITHOUT saving, so LobbyService can commit it together with the lobby's
        /// status change in one transaction.
        /// </summary>
        Task CreateGameFromLobbyAsync(Lobby lobby);

        /// <summary>The static 40-tile board (names, groups, prices, rent tiers).</summary>
        BoardResponse GetBoard();

        /// <summary>The caller's in-progress game, or null if they are not in one.</summary>
        Task<GameStateResponse?> GetCurrentGameAsync(Guid userId);

        Task<RollResultResponse> RollAsync(Guid userId);
        Task<GameStateResponse> BuyPropertyAsync(Guid userId);
        Task<GameStateResponse> PassPropertyAsync(Guid userId);
        Task<GameStateResponse> EndTurnAsync(Guid userId);
        Task<GameStateResponse> PayJailFineAsync(Guid userId);
        Task<GameStateResponse> UseJailCardAsync(Guid userId);
        Task<GameStateResponse> PayDebtAsync(Guid userId);
        Task<GameStateResponse> DeclareBankruptcyAsync(Guid userId);

        // ----- Player actions (feat/player-actions) -----

        /// <summary>Properties of any player in the caller's game, grouped by colour, with buildings and mortgage state.</summary>
        Task<PlayerPropertiesResponse> GetPlayerPropertiesAsync(Guid callerUserId, Guid targetUserId);

        /// <summary>Adds a house (or a hotel on the 5th level) to a street the caller owns. Own turn only.</summary>
        Task<GameStateResponse> BuildAsync(Guid userId, int tileIndex);
        /// <summary>Sells one building back to the bank for half its cost. Own turn only.</summary>
        Task<GameStateResponse> SellBuildingAsync(Guid userId, int tileIndex);
        /// <summary>Mortgages a property for half its price. Own turn only.</summary>
        Task<GameStateResponse> MortgageAsync(Guid userId, int tileIndex);
        /// <summary>Lifts a mortgage by paying half the price plus 10% interest. Own turn only.</summary>
        Task<GameStateResponse> UnmortgageAsync(Guid userId, int tileIndex);

        /// <summary>Pending trades the caller proposed or received.</summary>
        Task<List<TradeResponse>> GetTradesAsync(Guid userId);
        Task<TradeResponse> ProposeTradeAsync(Guid userId, ProposeTradeRequest request);
        Task<GameStateResponse> AcceptTradeAsync(Guid userId, Guid tradeId);
        Task<TradeResponse> DeclineTradeAsync(Guid userId, Guid tradeId);
        Task<TradeResponse> CancelTradeAsync(Guid userId, Guid tradeId);
    }
}
