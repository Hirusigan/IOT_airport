namespace IotDeviceSimulator;

public interface IMqttEventPublisher
{
    Task Publish(string host, int port, string topic, string payload, CancellationToken token);
}
