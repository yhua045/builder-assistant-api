using BuilderAssistantApi.Api.Filters;
using BuilderAssistantApi.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BuilderAssistantApi.Api.Controllers;

[ApiController]
[Route("api/telemetry")]
[AllowAnonymous] // Bypasses the default JWT auth policy
[ApiKeyAuth]     // Enforces the API Key requirement
public class TelemetryController : ControllerBase
{
    private readonly ITelemetryService _telemetryService;

    public TelemetryController(ITelemetryService telemetryService)
    {
        _telemetryService = telemetryService;
    }

    [HttpPost("errors")]
    public async Task<IActionResult> ReportError([FromBody] ErrorTelemetryRequest request, CancellationToken cancellationToken)
    {
        await _telemetryService.ReportErrorAsync(request, cancellationToken);
        return Accepted(new { accepted = true });
    }

    [HttpPost("analytics/events")]
    public async Task<IActionResult> ReportAnalyticsEvent([FromBody] AnalyticsEventRequest request, CancellationToken cancellationToken)
    {
        await _telemetryService.ReportAnalyticsEventAsync(request, cancellationToken);
        return Accepted(new { accepted = true });
    }
}
