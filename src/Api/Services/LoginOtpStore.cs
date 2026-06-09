using System.Collections.Concurrent;

namespace BuilderAssistantApi.Api.Services;

public sealed class LoginOtpStore
{
    private readonly ConcurrentDictionary<string, OtpEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public void Store(string email, string otpToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(otpToken))
        {
            throw new ArgumentException("OTP token is required.", nameof(otpToken));
        }

        _entries[email.Trim()] = new OtpEntry(otpToken, DateTimeOffset.UtcNow);
    }

    public bool TryGetLatest(string email, out string? otpToken)
    {
        otpToken = null;

        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        if (!_entries.TryGetValue(email.Trim(), out var entry))
        {
            return false;
        }

        otpToken = entry.OtpToken;
        return true;
    }

    private sealed record OtpEntry(string OtpToken, DateTimeOffset SentAt);
}