using BuilderAssistantApi.Application.Dtos;

namespace BuilderAssistantApi.Application.Interfaces;

public interface IFeatureFlagService
{
    /// <summary>
    /// Returns the full flag set for the caller.
    /// <paramref name="userId"/> is used only to populate the response DTO (may be null for anonymous callers).
    /// <paramref name="userRoles"/> drives entitlement resolution; pass an empty list or null for anonymous.
    /// </summary>
    Task<FeatureFlagDto> GetEffectiveFlagsAsync(
        long? userId,
        IReadOnlyList<string>? userRoles,
        CancellationToken ct = default);

    /// <summary>Single-flag check used by RequireFeatureAttribute and service-level enforcement.</summary>
    Task<bool> IsEnabledAsync(
        IReadOnlyList<string>? userRoles,
        string featureKey,
        CancellationToken ct = default);
}
