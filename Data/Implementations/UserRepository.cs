using BoardWalk.Api.Data.Models;
using BoardWalk.Api.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BoardWalk.Api.Data.Implementations
{
    public class UserRepository : Repository<User> , IUserRepository
    {
        public UserRepository(AppDbContext context) : base(context) { }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _dbSet.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<List<User>> SearchAsync(string query, Guid excludeUserId)
        {
            var lowered = query.ToLower();
            return await ((IQueryable<User>)_dbSet)
                .Where(u => u.Id != excludeUserId &&
                       (u.Email.ToLower().Contains(lowered) ||
                        u.FirstName.ToLower().Contains(lowered) ||
                        u.LastName.ToLower().Contains(lowered)))
                .Take(20) 
                .ToListAsync();
        }
    }
}
