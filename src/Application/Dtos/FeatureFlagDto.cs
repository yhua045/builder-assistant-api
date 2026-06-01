namespace BuilderAssistantApi.Application.Dtos;

/// <summary>Effective feature flags returned by GET /api/features.</summary>
/// <param name="UserId">Authenticated user ID as string, or null for anonymous callers.</param>
/// <param name="AsAnonymous">True when no user identity was resolved.</param>
/// <param name="Flags">Ordered list of feature flag states for the caller.</param>
public sealed record FeatureFlagDto(string? UserId, bool AsAnonymous, IReadOnlyList<FeatureItemDto> Flags);
