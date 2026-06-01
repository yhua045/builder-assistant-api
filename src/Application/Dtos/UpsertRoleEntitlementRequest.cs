namespace BuilderAssistantApi.Application.Dtos;

/// <summary>Request body for POST /api/features/admin/entitlements.</summary>
/// <param name="RoleName">The role to grant/revoke the entitlement for (e.g. "Premium").</param>
/// <param name="FeatureKey">Stable feature key (e.g. "ocr_scan").</param>
/// <param name="Enabled">Whether the feature should be enabled for this role.</param>
/// <param name="ExpiresAt">Optional expiry; null means the entitlement does not expire.</param>
public sealed record UpsertRoleEntitlementRequest(
    string RoleName,
    string FeatureKey,
    bool Enabled,
    DateTimeOffset? ExpiresAt);
