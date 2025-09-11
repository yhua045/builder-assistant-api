using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace BuilderAssistantApi.Api.Http;

public class CorrelationIdPropagationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public const string HeaderName = "X-Correlation-ID";

    public CorrelationIdPropagationHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context != null && context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            var correlationId = values.FirstOrDefault();
            if (!string.IsNullOrEmpty(correlationId))
            {
                // Ensure header is present on outbound request
                if (!request.Headers.Contains(HeaderName))
                {
                    request.Headers.Add(HeaderName, correlationId);
                }
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
