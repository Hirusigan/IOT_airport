namespace Luggage;
public interface IEventValidationService { Task Validate(string topic, Envelope envelope, string region); }
