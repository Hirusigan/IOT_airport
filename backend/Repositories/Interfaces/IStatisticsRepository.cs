namespace Luggage;

public interface IStatisticsRepository
{
    Task<StatisticsDTO> GetAsync();
}
