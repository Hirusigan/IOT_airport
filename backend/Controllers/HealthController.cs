using Microsoft.AspNetCore.Mvc;
namespace Luggage;

[ApiController, Route("health")]
public class HealthController(IHealthService health) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var status = await health.Get();
        return StatusCode(status.Database && status.Mqtt ? 200 : 503, status);
    }
}
