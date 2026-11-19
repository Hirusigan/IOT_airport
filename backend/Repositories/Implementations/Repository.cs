using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace Luggage;

public class Repository<T>(TrackingDbContext db) : IRepository<T> where T : class
{
    protected DbSet<T> Set => db.Set<T>();
    public async Task<T?> GetByIdAsync(params object[] keys) => await Set.FindAsync(keys);
    public Task<IReadOnlyList<T>> GetAllAsync() => ListAsync(_ => true);
    public void Add(T entity) => Set.Add(entity);
    public void AddRange(IEnumerable<T> entities) => Set.AddRange(entities);
    public void Update(T entity) => Set.Update(entity);
    public void Delete(T entity) => Set.Remove(entity);
    public async Task SaveChangesAsync()
    {
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw;
        }
        catch (DbUpdateException ex)
        {
            throw new PersistenceConflictException("Data conflicts with stored identities", ex);
        }
    }
    public Task<IDbContextTransaction> BeginTransactionAsync() => db.Database.BeginTransactionAsync();
    protected Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate) => Set.AnyAsync(predicate);
    protected async Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>> predicate) => await Set.Where(predicate).ToListAsync();
}
