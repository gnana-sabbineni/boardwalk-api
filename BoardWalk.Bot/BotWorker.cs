using Microsoft.Extensions.Options;
using BoardWalk.Monopoly.Contracts;

namespace BoardWalk.Bot
{
    /// <summary>
    /// The computer player's loop: ask the API which bot seats need to act, decide one action per seat, send it, pause
    /// (so humans can follow), repeat. It keeps no game state of its own — the API is the single source of truth — so it can
    /// be restarted at any time and simply carries on.
    /// </summary>
    public sealed class BotWorker : BackgroundService
    {
        private readonly BotApiClient _api;
        private readonly DumbStrategy _strategy;
        private readonly BotOptions _options;
        private readonly ILogger<BotWorker> _logger;

        public BotWorker(BotApiClient api, DumbStrategy strategy, IOptions<BotOptions> options, ILogger<BotWorker> logger)
        {
            _api = api;
            _strategy = strategy;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var board = await LoadBoardAsync(stoppingToken);
            if (board == null) return;

            _logger.LogInformation("Bot worker running against {Url}", _options.ApiBaseUrl);

            while (!stoppingToken.IsCancellationRequested)
            {
                var acted = false;

                try
                {
                    foreach (var turn in await _api.GetTurnsAsync(stoppingToken))
                    {
                        var action = _strategy.NextAction(turn, board);
                        if (action == null) continue;

                        var seat = turn.State.Players.FirstOrDefault(p => p.UserId == turn.UserId)?.FirstName ?? "Bot";
                        var result = await _api.PostAsync(turn.UserId, action.Path, stoppingToken);

                        if (result.Ok)
                            _logger.LogInformation("{Seat} is {Reason}", seat, action.Reason);
                        else
                            _logger.LogWarning("{Seat} tried {Path} ({Reason}) and was refused: {Message}", seat, action.Path, action.Reason, result.Message);

                        acted = true;
                        await Task.Delay(_options.ThinkDelayMs, stoppingToken);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
                {
                    _logger.LogWarning(ex, "Could not reach the API. Retrying shortly.");
                    await SafeDelayAsync(5000, stoppingToken);
                    continue;
                }

                if (!acted) await SafeDelayAsync(_options.PollIntervalMs, stoppingToken);
            }
        }

        private async Task<BoardResponse?> LoadBoardAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    return await _api.GetBoardAsync(ct);
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or System.Text.Json.JsonException)
                {
                    _logger.LogWarning("Waiting for the API to be ready ({Message})", ex.Message);
                    await SafeDelayAsync(3000, ct);
                }
            }
            return null;
        }

        private static async Task SafeDelayAsync(int ms, CancellationToken ct)
        {
            try { await Task.Delay(ms, ct); } catch (OperationCanceledException) { }
        }
    }
}
