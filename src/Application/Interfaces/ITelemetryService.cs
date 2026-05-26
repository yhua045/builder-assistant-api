namespace BuilderAssistantApi.Application.Interfaces;

public interface ITelemetryService
{
    Task<bool> ReportErrorAsync(ErrorTelemetryRequest request, CancellationToken cancellationToken = default);
    Task<bool> ReportAnalyticsEventAsync(AnalyticsEventRequest request, CancellationToken cancellationToken = default);
}

public class ErrorTelemetryRequest
{
    public string Source { get; set; } = string.Empty;
    public string? Message { get; set; }
    public ErrorDetail? Error { get; set; }
    public string Level { get; set; } = string.Empty;
    public Dictionary<string, string> Tags { get; set; } = new();
    public Dictionary<string, string> Context { get; set; } = new();
    public string? UserId { get; set; }
}

public class ErrorDetail
{
    public string? Name { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Stack { get; set; }
}

public class AnalyticsEventRequest
{
    public string Source { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public Dictionary<string, string> Properties { get; set; } = new();
    public string? UserId { get; set; }
    public string? ScreenName { get; set; }
}
