using System.Text.RegularExpressions;
using BuilderAssistantApi.Application.Ports;
using Microsoft.Extensions.Logging;

namespace BuilderAssistantApi.Api.Services;

public sealed partial class DevTestEmailSender : IEmailSender
{
    private readonly ILogger<DevTestEmailSender> _logger;
    private readonly LoginOtpStore _otpStore;

    public DevTestEmailSender(ILogger<DevTestEmailSender> logger, LoginOtpStore otpStore)
    {
        _logger = logger;
        _otpStore = otpStore;
    }

    public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Sending email to {To}: subject={Subject} body={Body}", to, subject, body);

        var token = ExtractPasswordlessLoginToken(body);
        if (!string.IsNullOrWhiteSpace(token))
        {
            _otpStore.Store(to, token);
        }

        return Task.CompletedTask;
    }

    private static string? ExtractPasswordlessLoginToken(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var match = PasswordlessLoginTokenRegex().Match(body);
        return match.Success ? match.Groups["token"].Value : null;
    }

    [GeneratedRegex(@"Your one-time login code is:\s*(?<token>.+?)(?:\r?\n|$)", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex PasswordlessLoginTokenRegex();
}