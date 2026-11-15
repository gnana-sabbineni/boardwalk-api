using System.Collections.Concurrent;

namespace BoardWalk.Monopoly.Abstractions
{
    /// <summary>
    /// Mutual exclusion per game, so two requests for the same game (double-click, two tabs, the turn timer
    /// firing at the same moment as a player) never interleave. The host decides the implementation:
    /// the in-memory one below works for a single server; a Redis one works across several.
    /// </summary>
    public interface IGameLock
    {
        /// <summary>Returns a handle to dispose when done, or null if the lock could not be taken within <paramref name="wait"/>.</summary>
        Task<IAsyncDisposable?> TryAcquireAsync(Guid gameId, TimeSpan wait, CancellationToken cancellationToken = default);
    }

    public sealed class InMemoryGameLock : IGameLock
    {
        private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();

        public async Task<IAsyncDisposable?> TryAcquireAsync(Guid gameId, TimeSpan wait, CancellationToken cancellationToken = default)
        {
            var gate = Gates.GetOrAdd(gameId, _ => new SemaphoreSlim(1, 1));
            if (!await gate.WaitAsync(wait, cancellationToken)) return null;
            return new Releaser(gate);
        }

        private sealed class Releaser : IAsyncDisposable
        {
            private readonly SemaphoreSlim _gate;
            private int _released;
            public Releaser(SemaphoreSlim gate) => _gate = gate;

            public ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref _released, 1) == 0) _gate.Release();
                return ValueTask.CompletedTask;
            }
        }
    }
}
