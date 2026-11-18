namespace Luggage;
public class DiagnosticService(IDiagnosticRepository diagnostics, IUnitOfWork work, TimeProvider clock) : IDiagnosticService
{
    public async Task Record(string topic, string reason)
    {
        diagnostics.Add(new Diagnostic { ReceivedAt = clock.GetUtcNow(), Topic = topic, Reason = reason.Length > 500 ? reason[..500] : reason });
        await work.SaveChangesAsync();
    }
}
