using BoardWalk.Api.Data.Models;
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
    }
}
