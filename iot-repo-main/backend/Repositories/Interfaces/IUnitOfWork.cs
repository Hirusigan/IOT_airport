namespace Luggage;

public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync();
}
public interface IUnitOfWork
{
    Task<ITransaction> BeginTransactionAsync();
    Task SaveChangesAsync();
    bool HasChanges { get; }
}
public class PersistenceConflictException(string message, Exception inner) : Exception(message, inner);
