using BuilderAssistantApi.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace BuilderAssistantApi.Infrastructure.Services;

public class TelemetryService : ITelemetryService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TelemetryService> _logger;

    public TelemetryService(IHttpClientFactory httpClientFactory, ILogger<TelemetryService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<bool> ReportErrorAsync(ErrorTelemetryRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reporting error telemetry from {Source}", request.Source);
        return Task.FromResult(true);
    }

    public Task<bool> ReportAnalyticsEventAsync(AnalyticsEventRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reporting analytics event {EventName} from {Source}", request.EventName, request.Source);
        return Task.FromResult(true);
    }
}
