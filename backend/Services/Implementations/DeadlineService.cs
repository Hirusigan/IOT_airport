namespace Luggage;

public class DeadlineService(IRuleService rules, IAlertRepository alerts, IDashboardNotifier notifications, TimeProvider clock) : IDeadlineService
{
    public async Task Evaluate()
    {
        bool changed;
        await using (var transaction = await alerts.BeginTransactionAsync())
        {
            changed = await rules.Evaluate(clock.GetUtcNow());
            if (changed)
            {
                await alerts.SaveChangesAsync();
            }

            await transaction.CommitAsync();
        }

        if (changed)
        {
            await notifications.Send();
        }
    }
}
