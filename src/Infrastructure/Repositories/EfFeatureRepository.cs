using BuilderAssistantApi.Domain.Entities;
using BuilderAssistantApi.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BuilderAssistantApi.Infrastructure.Repositories;

public class EfFeatureRepository : IFeatureRepository
{
    private readonly BuilderAssistantDbContext _context;

    public EfFeatureRepository(BuilderAssistantDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<Feature>> ListAllAsync(CancellationToken ct = default)
    {
        return await _context.Features
            .AsNoTracking()
            .OrderBy(f => f.Key)
            .ToListAsync(ct);
    }

    public async Task<Feature?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        return await _context.Features
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Key == key, ct);
    }

    public async Task<IReadOnlyList<RoleEntitlement>> ListEntitlementsForRolesAsync(
        IReadOnlyList<string> roleNames,
        CancellationToken ct = default)
    {
        if (roleNames.Count == 0)
            return [];

        var now = DateTimeOffset.UtcNow;
        return await _context.RoleEntitlements
            .AsNoTracking()
            .Where(e => roleNames.Contains(e.RoleName)
                     && (e.ExpiresAt == null || e.ExpiresAt > now))
            .ToListAsync(ct);
    }

    public async Task UpsertEntitlementAsync(RoleEntitlement entitlement, CancellationToken ct = default)
    {
        if (entitlement is null) throw new ArgumentNullException(nameof(entitlement));

        var existing = await _context.RoleEntitlements
            .FirstOrDefaultAsync(e => e.RoleName == entitlement.RoleName
                                   && e.FeatureKey == entitlement.FeatureKey, ct);

        if (existing is null)
        {
            await _context.RoleEntitlements.AddAsync(entitlement, ct);
        }
        else
        {
            existing.Enabled   = entitlement.Enabled;
            existing.ExpiresAt = entitlement.ExpiresAt;
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteEntitlementAsync(string roleName, string featureKey, CancellationToken ct = default)
    {
        var existing = await _context.RoleEntitlements
            .FirstOrDefaultAsync(e => e.RoleName == roleName && e.FeatureKey == featureKey, ct);

        if (existing is not null)
        {
            _context.RoleEntitlements.Remove(existing);
            await _context.SaveChangesAsync(ct);
        }
    }
}
