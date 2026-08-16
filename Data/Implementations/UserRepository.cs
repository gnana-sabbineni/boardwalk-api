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
    }
}
