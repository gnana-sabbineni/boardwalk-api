using BoardWalk.Api.Data.Models;

namespace BoardWalk.Api.Data.Repositories
{
    public interface INotificationRepository : IRepository<Notification>
    {
        /// <summary>All notifications for a user, most recent first, with Actor loaded.</summary>
        Task<List<Notification>> GetByRecipientAsync(Guid recipientUserId);
    }
}