using Microsoft.AspNetCore.Mvc;

namespace BuilderAssistantApi.Api.Controllers;

[ApiController]
[Route("uploads")]
public class UploadsController : ControllerBase
{
    private readonly ILogger<UploadsController> _logger;

    public UploadsController(ILogger<UploadsController> logger)
    {
        _logger = logger;
    }

    [HttpPost("init")]
    public IActionResult Init()
    {
        _logger.LogInformation("Upload initialization requested");

        // Placeholder for upload initialization endpoint
        _logger.LogDebug("Upload initialization completed successfully");

        return NoContent();
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        _logger.LogInformation("Health check requested");

        var response = new
        {
            Status = "Healthy",
            Timestamp = DateTimeOffset.UtcNow,
            Version = "1.0.0"
        };

        _logger.LogInformation("Health check completed with status: {Status}", response.Status);

        return Ok(response);
    }
}
