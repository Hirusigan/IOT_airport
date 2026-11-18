using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System.Text;
namespace Luggage;

public class MqttIngestion(IServiceScopeFactory scopes, IConfiguration config, BrokerHealth health, ILogger<MqttIngestion> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();
        client.DisconnectedAsync += _ => { health.Connected = false; return Task.CompletedTask; };
        client.ApplicationMessageReceivedAsync += async message =>
        {
            using var scope = scopes.CreateScope();
            try
            {
                if (message.ApplicationMessage.PayloadSegment.Count > 65536) throw new ArgumentException("Payload exceeds 64 KiB");
                var raw = Encoding.UTF8.GetString(message.ApplicationMessage.PayloadSegment);
                await scope.ServiceProvider.GetRequiredService<IEventProcessingService>().Process(message.ApplicationMessage.Topic, raw, config["Mqtt:Region"] ?? "EU");
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Rejected or failed MQTT message on {Topic}", message.ApplicationMessage.Topic);
                // A fresh scope avoids accidentally saving tracked changes from a failed transaction.
                using var diagnosticScope = scopes.CreateScope();
                try { await diagnosticScope.ServiceProvider.GetRequiredService<IDiagnosticService>().Record(message.ApplicationMessage.Topic, ex.Message); }
                catch (Exception failure) { log.LogError(failure, "Could not persist invalid-message diagnostic"); }
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
                    health.Connected = true; health.LastError = null; delay = 1;
                    log.LogInformation("Regional MQTT subscription ready");
                }
                await Task.Delay(1000, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                health.Connected = false; health.LastError = ex.Message;
                log.LogWarning("MQTT reconnect in {Delay}s: {Error}", delay, ex.Message);
                if (client.IsConnected) await client.DisconnectAsync();
                try { await Task.Delay(TimeSpan.FromSeconds(delay), token); } catch (OperationCanceledException) { break; }
                delay = Math.Min(delay * 2, 30);
            }
        }
    }
}

