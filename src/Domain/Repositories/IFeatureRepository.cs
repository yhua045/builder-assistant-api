using BuilderAssistantApi.Domain.Entities;

namespace BuilderAssistantApi.Domain.Repositories;

public interface IFeatureRepository
{
    Task<IReadOnlyList<Feature>> ListAllAsync(CancellationToken ct = default);

    Task<Feature?> GetByKeyAsync(string key, CancellationToken ct = default);

    /// <summary>Returns all non-expired entitlements for any of the given role names.</summary>
    Task<IReadOnlyList<RoleEntitlement>> ListEntitlementsForRolesAsync(
        IReadOnlyList<string> roleNames,
        CancellationToken ct = default);

    Task UpsertEntitlementAsync(RoleEntitlement entitlement, CancellationToken ct = default);

    Task DeleteEntitlementAsync(string roleName, string featureKey, CancellationToken ct = default);
}
