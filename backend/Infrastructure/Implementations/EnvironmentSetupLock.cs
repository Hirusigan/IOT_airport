namespace Luggage;

public sealed class EnvironmentSetupLock : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<IDisposable> Acquire()
    {
        await gate.WaitAsync();
        return new Lease(gate);
    }

    public void Dispose() => gate.Dispose();

    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => gate.Release();
    }
}
