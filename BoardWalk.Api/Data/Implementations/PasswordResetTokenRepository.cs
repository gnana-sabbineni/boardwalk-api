using Microsoft.EntityFrameworkCore;
using BoardWalk.Api.Data.Models;
using BoardWalk.Api.Data.Repositories;

namespace BoardWalk.Api.Data.Implementations
{
    public class PasswordResetTokenRepository : Repository<PasswordResetToken>, IPasswordResetTokenRepository
    {
        public PasswordResetTokenRepository(AppDbContext context) : base(context) { }

        public async Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash)
        {
            return await ((IQueryable<PasswordResetToken>)_dbSet)
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
        }
    }
}