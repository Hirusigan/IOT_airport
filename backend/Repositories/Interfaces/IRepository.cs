using Microsoft.EntityFrameworkCore.Storage;

namespace Luggage;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(params object[] keys);
    Task<IReadOnlyList<T>> GetAllAsync();
    void Add(T entity);
    void AddRange(IEnumerable<T> entities);
    void Update(T entity);
    void Delete(T entity);
    Task SaveChangesAsync();
    Task<IDbContextTransaction> BeginTransactionAsync();
}
