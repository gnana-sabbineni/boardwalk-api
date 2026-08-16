using BoardWalk.Api.Data.Repositories;

namespace BoardWalk.Api.Data.Implementations
{
        public class UnitOfWork : IUnitOfWork
        {
            private readonly AppDbContext _context;
            private IUserRepository? _users;

            public UnitOfWork(AppDbContext context)
            {
                _context = context;
            }
            public IUserRepository Users => _users ??= new UserRepository(_context);

            public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

            public void Dispose() => _context.Dispose();
        }
}
