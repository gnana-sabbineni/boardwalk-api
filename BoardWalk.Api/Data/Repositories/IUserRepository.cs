using BoardWalk.Api.Data.Models;

namespace BoardWalk.Api.Data.Repositories
{
    public interface IUserRepository : IRepository<User>
    {
        Task<User?> GetByEmailAsync(string email);
        Task<List<User>> SearchAsync(string query, Guid excludeUserId);

        /// <summary>A seeded computer-player account that is not currently in any lobby.</summary>
        Task<User?> FindFreeBotAsync();
    }
}
