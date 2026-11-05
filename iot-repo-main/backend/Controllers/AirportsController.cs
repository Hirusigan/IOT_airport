using Microsoft.AspNetCore.Mvc;
namespace Luggage;
[ApiController, Route("api/airports")]
public class AirportsController(IAirportService airports) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await airports.GetAll());
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var result = await airports.GetDetails(id);
        return result == null ? NotFound() : Ok(result);
    }
    [HttpPost]
    public async Task<IActionResult> Register(Airport airport)
    {
        try { return Created($"/api/airports/{airport.Id}", await airports.Register(airport)); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (ConflictException ex) { return Conflict(ex.Message); }
    }
}
