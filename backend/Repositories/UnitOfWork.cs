using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace Luggage;

public class UnitOfWork(TrackingDbContext db) : IUnitOfWork
{
    public bool HasChanges => db.ChangeTracker.HasChanges();
    public async Task<ITransaction> BeginTransactionAsync() => new Transaction(await db.Database.BeginTransactionAsync());
    public async Task SaveChangesAsync()
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) { throw new PersistenceConflictException("Data conflicts with stored identities", ex); }
    }
    private sealed class Transaction(IDbContextTransaction transaction) : ITransaction
    {
        public Task CommitAsync() => transaction.CommitAsync();
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
