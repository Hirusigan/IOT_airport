namespace Luggage;
public class HealthService(IHealthRepository database, BrokerHealth broker) : IHealthService
{
    public async Task<HealthDTO> Get() => new(await database.CanConnectAsync(), broker.Connected);
}
