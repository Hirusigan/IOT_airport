namespace Luggage;
public sealed class ProcessingGate { public SemaphoreSlim Mutex { get; } = new(1, 1); }

