using Microsoft.AspNetCore.Mvc;
namespace Luggage;

[ApiController, Route("api/statistics")]
public class StatisticsController(IStatisticsService statistics) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await statistics.Get());
}
