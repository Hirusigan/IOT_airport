using Microsoft.AspNetCore.Mvc;
namespace Luggage;

[ApiController, Route("api/devtools")]
public class DevtoolsController(IDevtoolsService service) : ControllerBase
{
    [HttpGet("scenarios")]
    public async Task<IActionResult> Scenarios()
    {
        return Ok(await service.GetScenarios());
    }

    [HttpGet("simulation")]
    public IActionResult Status()
    {
        return Ok(service.Status);
    }
    [HttpPost("simulation")]
    public async Task<IActionResult> Start(StartSimulationDTO request)
    {
        try
        {
            await service.Start(request.ScenarioId);
            return Accepted(service.Status);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                error = ex.Message
            });
        }
        catch (ConflictException ex)
        {
            return Conflict(new
            {
                error = ex.Message
            });
        }
    }
    [HttpPost("simulation/stop")]
    public async Task<IActionResult> Stop()
    {
        await service.Stop();
        return Ok(service.Status);
    }
    [HttpPost("database/clear")]
    public async Task<IActionResult> Clear()
    {
        try
        {
            await service.Clear();
            return NoContent();
        }
        catch (BrokerCleanupException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }
    [HttpPost("database/seed")]
    public async Task<IActionResult> Seed()
    {
        await service.Seed();
        return NoContent();
    }
}
