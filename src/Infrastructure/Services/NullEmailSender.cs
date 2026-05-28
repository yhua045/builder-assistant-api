using BuilderAssistantApi.Application.Ports;
using Microsoft.Extensions.Logging;

namespace BuilderAssistantApi.Infrastructure.Services;

public sealed class NullEmailSender : IEmailSender
{
    private readonly ILogger<NullEmailSender> _logger;

    public NullEmailSender(ILogger<NullEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending email to {To}: subject={Subject} body={Body}", to, subject, body);
        return Task.CompletedTask;
    }
}
