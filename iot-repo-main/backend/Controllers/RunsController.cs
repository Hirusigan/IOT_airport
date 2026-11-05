using Microsoft.AspNetCore.Mvc;
namespace Luggage;
[ApiController, Route("api/runs")]
public class RunsController(IRunImportService imports) : ControllerBase
{
    [HttpPost("import")]
    public async Task<IActionResult> Import(RunImport run)
    {
        try { await imports.Import(run); return Ok(new { run.SimulationRunId }); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (ConflictException ex) { return Conflict(ex.Message); }
    }
}
