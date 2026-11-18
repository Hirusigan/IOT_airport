using Microsoft.AspNetCore.Mvc;
namespace Luggage;

[ApiController, Route("api/alerts")]
public class AlertsController(IAlertService alerts) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string? airportId, bool? active, string? severity) => Ok(await alerts.Get(airportId, active, severity));
}
