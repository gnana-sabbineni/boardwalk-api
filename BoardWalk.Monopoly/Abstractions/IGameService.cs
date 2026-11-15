using BoardWalk.Monopoly.Contracts;

namespace BoardWalk.Monopoly.Abstractions
{
    public interface IGameService
    {
        // ----- Lifecycle -----

        /// <summary>
        /// Builds a game (players, turn order, cash, property rows, shuffled deck, dice seed, first snapshot) and adds it
        /// to the host's DbContext WITHOUT saving, so the host commits it together with its own changes. Returns the game id.
        /// </summary>
        Task<Guid> CreateGameAsync(CreateGameRequest request);

        /// <summary>
        /// A human left the room mid-game: their seat is handed to a bot. If no human is left the game is abandoned.
        /// Does nothing if the user has no human-controlled seat in a game.
        /// </summary>
        Task HandlePlayerLeftAsync(Guid userId);

        // ----- Queries -----

        BoardResponse GetBoard();
        Task<GameStateResponse?> GetCurrentGameAsync(Guid userId);
        Task<EventPageResponse> GetEventsAsync(Guid callerUserId, Guid gameId, int afterSequence, int limit);
        Task<List<TurnSummaryResponse>> GetSummariesAsync(Guid callerUserId, Guid gameId, int? fromTurn, int? toTurn);
        Task<PlayerPropertiesResponse> GetPlayerPropertiesAsync(Guid callerUserId, Guid targetUserId);
        Task<List<TradeResponse>> GetTradesAsync(Guid userId);

        // ----- Turn actions -----

        Task<RollResultResponse> RollAsync(Guid userId);
        Task<GameStateResponse> BuyPropertyAsync(Guid userId);
        Task<GameStateResponse> PassPropertyAsync(Guid userId);
        Task<GameStateResponse> EndTurnAsync(Guid userId);
        Task<GameStateResponse> PayJailFineAsync(Guid userId);
        Task<GameStateResponse> UseJailCardAsync(Guid userId);
        Task<GameStateResponse> PayDebtAsync(Guid userId);
        Task<GameStateResponse> DeclareBankruptcyAsync(Guid userId);

        // ----- Property management (own turn only) -----

        Task<GameStateResponse> BuildAsync(Guid userId, int tileIndex);
        Task<GameStateResponse> SellBuildingAsync(Guid userId, int tileIndex);
        Task<GameStateResponse> MortgageAsync(Guid userId, int tileIndex);
        Task<GameStateResponse> UnmortgageAsync(Guid userId, int tileIndex);

        // ----- Trades (any player, any time) -----

        Task<TradeResponse> ProposeTradeAsync(Guid userId, ProposeTradeRequest request);
        Task<GameStateResponse> AcceptTradeAsync(Guid userId, Guid tradeId);
        Task<TradeResponse> DeclineTradeAsync(Guid userId, Guid tradeId);
        Task<TradeResponse> CancelTradeAsync(Guid userId, Guid tradeId);

        // ----- Bot service support -----

        /// <summary>Every bot-controlled seat that needs to act now (its turn, or a trade offer waiting).</summary>
        Task<List<BotTurnResponse>> GetBotTurnsAsync();

        // ----- Turn clock -----

        /// <summary>Ids of in-progress games whose turn clock has run out.</summary>
        Task<List<Guid>> GetOverdueGameIdsAsync();

        /// <summary>
        /// Plays out an overdue game's turn (re-checks the deadline under the game lock). Call with a fresh
        /// DI scope per game — see TurnTimerService.
        /// </summary>
        Task ExpireTurnAsync(Guid gameId);
    }
}
