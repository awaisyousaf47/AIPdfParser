using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IVectorStore vectorStore, ILogger<HealthController> logger)
    {
        _vectorStore = vectorStore;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetHealth()
    {
        try
        {
            var info = await _vectorStore.GetCollectionInfoAsync();
            return Ok(new
            {
                Status = "Healthy",
                Collection = info,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(503, new
            {
                Status = "Unhealthy",
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}