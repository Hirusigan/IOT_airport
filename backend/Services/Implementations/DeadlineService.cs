namespace Luggage;
public class DeadlineService(IRuleService rules, IUnitOfWork work, ProcessingGate gate, IDashboardNotifier notifications, TimeProvider clock) : IDeadlineService
{
    public async Task Evaluate()
    {
        bool changed;
        await gate.Mutex.WaitAsync();
        try
        {
            await using var transaction = await work.BeginTransactionAsync();
            await rules.Evaluate(clock.GetUtcNow());
            changed = work.HasChanges;
            if (changed) await work.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        finally { gate.Mutex.Release(); }
        if (changed) await notifications.Send();
    }
}
