using BoardWalk.Api.Services.Interfaces;
using System.Collections.Concurrent;

namespace BoardWalk.Api.Services.Implementations
{
    public static class LobbyGracePeriodService
    {
        private static readonly ConcurrentDictionary<Guid, CancellationTokenSource> _timers = new();
        private static IServiceProvider? _serviceProvider;
        private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(10);

        public static void Configure(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

        public static void StartGracePeriod(Guid lobbyId, Guid userId)
        {
            var cts = new CancellationTokenSource();
            _timers[userId] = cts;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(GracePeriod, cts.Token);

                    using var scope = _serviceProvider!.CreateScope();
                    var presenceService = scope.ServiceProvider.GetRequiredService<IPresenceService>();
                    var lobbyService = scope.ServiceProvider.GetRequiredService<ILobbyService>();

                    // Atomic check — see design doc §6. Only proceeds if genuinely still offline.
                    var stillOffline = await presenceService.ConfirmStillOfflineAndClearAsync(userId);
                    if (stillOffline)
                    {
                        await lobbyService.RemoveDisconnectedMemberAsync(lobbyId, userId);
                    }
                }
                catch (TaskCanceledException)
                {
                    // Reconnected before the grace period expired — nothing to do.
                }
                finally
                {
                    _timers.TryRemove(userId, out _);
                }
            });
        }

        public static void CancelGracePeriod(Guid userId)
        {
            if (_timers.TryRemove(userId, out var cts))
            {
                cts.Cancel();
            }
        }
    }
}