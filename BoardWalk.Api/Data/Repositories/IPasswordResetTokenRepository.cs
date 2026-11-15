using BoardWalk.Api.Data.Models;

namespace BoardWalk.Api.Data.Repositories
{
    public interface IPasswordResetTokenRepository : IRepository<PasswordResetToken>
    {
        Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash);
    }
}