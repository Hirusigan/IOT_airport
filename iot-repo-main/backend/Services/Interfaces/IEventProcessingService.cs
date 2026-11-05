namespace Luggage;
public interface IEventProcessingService { Task<bool> Process(string topic, string raw, string region); }
