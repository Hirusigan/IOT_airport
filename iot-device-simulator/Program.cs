using MQTTnet;
using MQTTnet.Protocol;
var app = WebApplication.Create(args);
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/publish", async (PublishRequest request, IConfiguration config, CancellationToken token) =>
{
    try
    {
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder().WithClientId($"device-{request.AirportId}-{Guid.NewGuid():N}")
            .WithTcpServer(request.Host, request.Port).Build();
        await client.ConnectAsync(options, token);
        var message = new MqttApplicationMessageBuilder().WithTopic(request.Topic).WithPayload(request.Payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).WithRetainFlag(false).Build();
        await client.PublishAsync(message, token);
        await client.DisconnectAsync();
        return Results.Accepted();
    }
    catch (Exception e) { return Results.Problem(e.Message); }
});
app.Run();
record PublishRequest(string AirportId, string Host, int Port, string Topic, string Payload);
