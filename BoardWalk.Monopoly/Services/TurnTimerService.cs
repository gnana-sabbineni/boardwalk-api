using BoardWalk.Monopoly.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BoardWalk.Monopoly.Services
{
    /// <summary>
    /// Background loop that enforces the turn clock: every second or so it asks for games past their deadline and
    /// plays out each overdue turn. Every game gets its own DI scope (and so its own DbContext) so a failure in one
    /// game can never corrupt another. It is safe to run on several servers at once: the per-game lock and the
    /// deadline re-check inside ExpireTurnAsync mean only one of them acts.
    /// </summary>
    public sealed class TurnTimerService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly GameOptions _options;
        private readonly ILogger<TurnTimerService> _logger;

        public TurnTimerService(IServiceScopeFactory scopes, IOptions<GameOptions> options, ILogger<TurnTimerService> logger)
        {
            _scopes = scopes;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var delay = TimeSpan.FromSeconds(Math.Max(1, _options.TimerPollSeconds));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    List<Guid> overdue;
                    using (var scope = _scopes.CreateScope())
                    {
                        overdue = await scope.ServiceProvider.GetRequiredService<IGameService>().GetOverdueGameIdsAsync();
                    }

                    foreach (var gameId in overdue)
                    {
                        stoppingToken.ThrowIfCancellationRequested();
                        try
                        {
                            using var scope = _scopes.CreateScope();
                            await scope.ServiceProvider.GetRequiredService<IGameService>().ExpireTurnAsync(gameId);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            // Another server got there first, or the game moved on. The next poll re-checks.
                            _logger.LogWarning(ex, "Could not expire the turn of game {GameId}", gameId);
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Turn timer scan failed");
                }

                try { await Task.Delay(delay, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
