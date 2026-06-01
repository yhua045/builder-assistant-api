namespace BuilderAssistantApi.Application.Dtos;

/// <summary>Represents the effective state of a single feature flag for the current caller.</summary>
/// <param name="Key">Stable feature identifier (e.g. "ocr_scan").</param>
/// <param name="Enabled">Whether the feature may be used.</param>
/// <param name="Reason">
/// Debugging metadata: "default_on" | "default_off" | "role:{roleName}" | "role:{roleName}:disabled".
/// </param>
/// <param name="ExpiresAt">ISO-8601 expiry date, or null when the entitlement does not expire.</param>
public sealed record FeatureItemDto(string Key, bool Enabled, string Reason, DateTimeOffset? ExpiresAt);
