using System.Text;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using Microsoft.EntityFrameworkCore;
namespace Luggage;

public class MqttIngestion(IServiceScopeFactory scopes, IConfiguration config, BrokerHealth health) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();
        client.DisconnectedAsync += _ =>
        {
            health.Connected = false;
            return Task.CompletedTask;
        };
        client.ApplicationMessageReceivedAsync += async message =>
        {
            try
            {
                if (message.ApplicationMessage.PayloadSegment.Count > 65536)
                    throw new ArgumentException("Payload exceeds 64 KiB");
                var raw = Encoding.UTF8.GetString(message.ApplicationMessage.PayloadSegment);
                await ProcessWithRetry(message.ApplicationMessage.Topic, raw, config["Mqtt:Region"] ?? "EU");
            }
            catch (Exception ex)
            {
                using var diagnosticScope = scopes.CreateScope();
                try
                {
                    await diagnosticScope.ServiceProvider.GetRequiredService<IDiagnosticService>().Record(message.ApplicationMessage.Topic, ex.Message);
                }
                catch (Exception)
                {
                }
            }
        };
        var options = new MqttClientOptionsBuilder().WithClientId("luggage-backend-eu")
            .WithTcpServer(config["Mqtt:Host"] ?? "localhost", config.GetValue("Mqtt:Port", 1883))
            .WithCleanSession(false).Build();
        int delay = 1;
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!client.IsConnected)
                {
                    await client.ConnectAsync(options, token);
                    await client.SubscribeAsync(factory.CreateSubscribeOptionsBuilder().WithTopicFilter(f => f.WithTopic("airport/#")
                        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)).Build(), token);
                    health.Connected = true;
                    health.LastError = null;
                    delay = 1;
                }
                await Task.Delay(1000, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                health.Connected = false;
                health.LastError = ex.Message;
                if (client.IsConnected)
                    await client.DisconnectAsync();
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delay), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                delay = Math.Min(delay * 2, 30);
            }
        }
    }

    private async Task ProcessWithRetry(string topic, string raw, string region)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            using var scope = scopes.CreateScope();
            try
            {
                await scope.ServiceProvider
                    .GetRequiredService<IEventProcessingService>()
                    .Process(topic, raw, region);
                return;
            }
            catch (Exception exception) when (
                attempt < maxAttempts &&
                exception is (DbUpdateConcurrencyException or PersistenceConflictException))
            {
                continue;
            }
        }
    }
}

