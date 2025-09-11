using Microsoft.AspNetCore.Mvc;

namespace BuilderAssistantApi.Api.Controllers;

[ApiController]
[Route("uploads")]
public class UploadsController : ControllerBase
{
    [HttpPost("init")]
    public IActionResult Init()
    {
        // Placeholder for upload initialization endpoint
        return NoContent();
    }
}
