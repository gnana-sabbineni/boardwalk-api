namespace BoardWalk.Api.Services.Interfaces
{
    public interface IPresenceService
    {
        Task MarkOnlineAsync(Guid userId);

        /// <summary>Marks a user offline. Returns the new connection count (0 if they have no more connections).</summary>
        Task<int> MarkOfflineAsync(Guid userId);

        Task<bool> IsOnlineAsync(Guid userId);

        /// <summary>
        /// Atomically checks whether the user is still offline and, if so, removes their
        /// presence key. Returns true only if they were confirmed offline at the exact
        /// instant of the check — prevents the reconnect-vs-kick race (see design doc §6).
        /// </summary>
        Task<bool> ConfirmStillOfflineAndClearAsync(Guid userId);
    }
}