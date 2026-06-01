namespace BuilderAssistantApi.Application.Interfaces;

public interface IFeatureCacheInvalidator
{
    /// <summary>Removes all cached flag sets whose key includes <paramref name="roleName"/>.</summary>
    void InvalidateRole(string roleName);

    /// <summary>Removes all feature flag cache entries (admin purge).</summary>
    void InvalidateAll();
}
