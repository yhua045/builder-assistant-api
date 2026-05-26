using Serilog.Context;

namespace BuilderAssistantApi.Api.Middleware;

public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrGenerateCorrelationId(context);

        // Add correlation ID to response headers
        context.Response.Headers[CorrelationIdHeaderName] = correlationId;

        // Add correlation ID to Serilog context for this request
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            _logger.LogInformation("Request started for {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await _next(context);

            _logger.LogInformation("Request completed for {Method} {Path} with status {StatusCode}",
                context.Request.Method, context.Request.Path, context.Response.StatusCode);
        }
    }

    private static string GetOrGenerateCorrelationId(HttpContext context)
    {
        // Check if correlation ID is already present in request headers
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var correlationId) &&
            !string.IsNullOrEmpty(correlationId))
        {
            return correlationId.ToString();
        }

        // Generate a new correlation ID
        return Guid.NewGuid().ToString("D")[..8]; // Use short 8-character ID
    }
}