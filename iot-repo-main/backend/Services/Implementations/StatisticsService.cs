namespace Luggage;
public class StatisticsService(IStatisticsRepository statistics) : IStatisticsService
{
    public Task<StatisticsDTO> Get() => statistics.GetAsync();
}
