using imagehub.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace imagehub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GeneralController(IOptions<CacheSettings> cacheSettings) : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return Ok("Pong");
    }
    
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok("Healthy");
    }

    [HttpGet("settings")]
    public IActionResult Settings()
    {
        return new OkObjectResult(cacheSettings.Value);
    }
}
