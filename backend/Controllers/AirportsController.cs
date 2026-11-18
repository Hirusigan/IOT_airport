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
    [HttpPost("schedule")]
    public async Task<IActionResult> Schedule(FlightSchedule schedule)
    {
        try { await airports.RegisterSchedule(schedule); return Accepted(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (ConflictException ex) { return Conflict(ex.Message); }
    }
    [HttpPost("flights/{flightId}/departure")]
    public async Task<IActionResult> Departure(string flightId, FlightDepartureUpdate departure)
    {
        try { await airports.RecordDeparture(flightId, departure); return Accepted(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (ConflictException ex) { return Conflict(ex.Message); }
    }
    [HttpPost("flights/{flightId}/arrival")]
    public async Task<IActionResult> Arrival(string flightId, FlightArrivalUpdate arrival)
    {
        try { await airports.RecordArrival(flightId, arrival); return Accepted(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (ConflictException ex) { return Conflict(ex.Message); }
    }
}
