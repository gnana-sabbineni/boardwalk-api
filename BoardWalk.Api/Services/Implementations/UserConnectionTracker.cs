using System.Collections.Concurrent;
using BoardWalk.Api.Services.Interfaces;

namespace BoardWalk.Api.Services.Implementations
{
    public class UserConnectionTracker : IUserConnectionTracker
    {
        private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> _connections = new();

        public void AddConnection(Guid userId, string connectionId)
        {
            var set = _connections.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
            set[connectionId] = 0;
        }

        public void RemoveConnection(Guid userId, string connectionId)
        {
            if (_connections.TryGetValue(userId, out var set))
            {
                set.TryRemove(connectionId, out _);
                if (set.IsEmpty) _connections.TryRemove(userId, out _);
            }
        }

        public IReadOnlyCollection<string> GetConnections(Guid userId) =>
            _connections.TryGetValue(userId, out var set) ? set.Keys.ToList() : Array.Empty<string>();
    }
}