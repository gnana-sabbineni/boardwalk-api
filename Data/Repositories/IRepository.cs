using System.Linq.Expressions;

namespace BoardWalk.Api.Data.Repositories
{
    public interface IRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(Guid id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<T?> FindAsync(Expression<Func<T, bool>> predicate);
        Task AddAsync(T entity);
        /// <summary>Synchronous add (marks the entity Added). Use when the caller is not async.</summary>
        void Add(T entity);
        void Update(T entity);
        void Delete(T entity);
    }
}