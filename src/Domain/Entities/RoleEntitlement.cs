namespace BuilderAssistantApi.Domain.Entities;

/// <summary>Per-role override for a feature flag. A row means the role's access is explicitly set.</summary>
public class RoleEntitlement
{
    public long Id { get; set; }

    /// <summary>Role name (e.g. "Admin", "Premium"). No FK to AspNetRoles — soft coupling.</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>References <see cref="Feature.Key"/>.</summary>
    public string FeatureKey { get; set; } = string.Empty;

    /// <summary>Whether the feature is enabled for this role.</summary>
    public bool Enabled { get; set; }

    /// <summary>When null the entitlement does not expire.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
