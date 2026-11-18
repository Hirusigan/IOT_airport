namespace Luggage;
public sealed class DevtoolsGate { public SemaphoreSlim Mutex { get; } = new(1, 1); }
