using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
namespace Luggage;

public abstract class BaseRepository<T>(Store db) : IRepository<T> where T : class
{
    protected DbSet<T> Set => db.Set<T>();
    public void Add(T entity) => Set.Add(entity);
    public void AddRange(IEnumerable<T> entities) => Set.AddRange(entities);
    protected async Task<T?> FindAsync(params object[] keys) => await Set.FindAsync(keys);
    protected Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate) => Set.AnyAsync(predicate);
    protected async Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>> predicate) => await Set.Where(predicate).ToListAsync();
}
