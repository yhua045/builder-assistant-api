namespace BuilderAssistantApi.Domain.Entities;

public class Feature
{
    public long Id { get; set; }

    /// <summary>Stable string identifier used by clients (e.g. "ocr_scan").</summary>
    public string Key { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Whether the feature is enabled for all users when no entitlement exists.</summary>
    public bool DefaultEnabled { get; set; }
}
