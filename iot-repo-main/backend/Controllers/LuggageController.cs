using Microsoft.AspNetCore.Mvc;
namespace Luggage;
[ApiController, Route("api/luggage")]
public class LuggageController(ILuggageService luggage) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id)
    {
        var result = await luggage.Get(id);
        return result == null ? NotFound() : Ok(result);
    }
}
