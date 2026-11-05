using System.Text;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace Showcase;

public class BrokerStatus
{
    public volatile bool Connected;
}

public class MqttIngestion(IServiceScopeFactory scopes, IConfiguration config,
    BrokerStatus status, ILogger<MqttIngestion> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();
        client.DisconnectedAsync += _ => { status.Connected = false; return Task.CompletedTask; };
        client.ApplicationMessageReceivedAsync += async args =>
        {
            try
            {
                if (args.ApplicationMessage.PayloadSegment.Count > 4096)
                    throw new ArgumentException("Event exceeds 4 KiB");
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ObservationService>().Ingest(
                    args.ApplicationMessage.Topic, Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment), token);
            }
            catch (Exception ex) { log.LogWarning(ex, "Rejected or failed tracking event"); }
        };
        var options = new MqttClientOptionsBuilder().WithClientId("showcase-backend")
            .WithTcpServer(config["Mqtt:Host"] ?? "localhost", config.GetValue("Mqtt:Port", 1883))
            .WithCleanSession(false).Build();
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!client.IsConnected)
                {
                    await client.ConnectAsync(options, token);
                    await client.SubscribeAsync(factory.CreateSubscribeOptionsBuilder()
                        .WithTopicFilter(f => f.WithTopic(ObservationService.Subscription)
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)).Build(), token);
                    status.Connected = true;
                }
                await Task.Delay(1000, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                status.Connected = false;
                log.LogWarning(ex, "MQTT unavailable; retrying in two seconds");
                try { await Task.Delay(2000, token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            }
        }
        status.Connected = false;
    }
}
