namespace IotDeviceSimulator;

public sealed class MqttEventPublisher : IMqttEventPublisher
{
    public async Task Publish(string host, int port, string topic, string payload, CancellationToken token)
    {
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithClientId($"device-{Guid.NewGuid():N}")
            .WithTcpServer(host, port)
            .Build();
        await client.ConnectAsync(options, token);
        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(false)
                .Build();
            await client.PublishAsync(message, token);
        }
        finally
        {
            if (client.IsConnected) await client.DisconnectAsync();
        }
    }
}
