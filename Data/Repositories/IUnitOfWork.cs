namespace BoardWalk.Api.Data.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        IUserRepository Users { get; }
 
        Task<int> SaveChangesAsync();
    }
}
