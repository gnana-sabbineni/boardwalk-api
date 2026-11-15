namespace BoardWalk.Api.Services.Interfaces
{
    /// <summary>
    /// Tracks which SignalR ConnectionIds belong to which user, in-memory. Lets the
    /// server add/remove a user's already-open connection(s) to/from a lobby group
    /// in response to a REST action, without waiting for them to reconnect.
    /// </summary>
    public interface IUserConnectionTracker
    {
        void AddConnection(Guid userId, string connectionId);
        void RemoveConnection(Guid userId, string connectionId);
        IReadOnlyCollection<string> GetConnections(Guid userId);
    }
}