using BuilderAssistantApi.Application.Dtos;
using BuilderAssistantApi.Application.Interfaces;
using BuilderAssistantApi.Domain.Entities;
using BuilderAssistantApi.Domain.Repositories;
using BuilderAssistantApi.Infrastructure.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BuilderAssistantApi.Infrastructure.Services;

/// <summary>
/// Resolves effective feature flags by merging global defaults with per-role entitlements.
/// Results are cached in <see cref="IMemoryCache"/> keyed by sorted role names.
/// Implements <see cref="IFeatureCacheInvalidator"/> to expose cache management.
/// </summary>
public class FeatureFlagService : IFeatureFlagService, IFeatureCacheInvalidator
{
    private const string CachePrefix = "features:";

    private readonly IFeatureRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly FeatureFlagCacheOptions _options;

    // Tracks all cache keys that contain a given role name so InvalidateRole can clean them up.
    // key = roleName, value = set of cache keys that include that role.
    private readonly Dictionary<string, HashSet<string>> _roleKeyIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _indexLock = new();

    public FeatureFlagService(
        IFeatureRepository repository,
        IMemoryCache cache,
        IOptions<FeatureFlagCacheOptions> options)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<FeatureFlagDto> GetEffectiveFlagsAsync(
        long? userId,
        IReadOnlyList<string>? userRoles,
        CancellationToken ct = default)
    {
        var roles = NormaliseRoles(userRoles);
        var cacheKey = BuildCacheKey(roles);

        if (_cache.TryGetValue(cacheKey, out FeatureFlagDto? cached) && cached is not null)
            return cached;

        var allFeatures = await _repository.ListAllAsync(ct);
        var entitlements = roles.Count > 0
            ? await _repository.ListEntitlementsForRolesAsync(roles, ct)
            : (IReadOnlyList<RoleEntitlement>)[];

        var flags = allFeatures
            .Select(f => BuildFlag(f, entitlements))
            .ToList();

        var userIdStr = userId.HasValue ? userId.Value.ToString() : null;
        var dto = new FeatureFlagDto(userIdStr, !userId.HasValue, flags);

        var ttl = TimeSpan.FromMinutes(_options.CacheTtlMinutes);
        _cache.Set(cacheKey, dto, ttl);

        // Update the role→cacheKey index for later invalidation
        lock (_indexLock)
        {
            foreach (var role in roles)
            {
                if (!_roleKeyIndex.TryGetValue(role, out var keys))
                    _roleKeyIndex[role] = keys = [];
                keys.Add(cacheKey);
            }
        }

        return dto;
    }

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(
        IReadOnlyList<string>? userRoles,
        string featureKey,
        CancellationToken ct = default)
    {
        var flags = await GetEffectiveFlagsAsync(null, userRoles, ct);
        return flags.Flags.FirstOrDefault(f => f.Key == featureKey)?.Enabled ?? false;
    }

    /// <inheritdoc />
    public void InvalidateRole(string roleName)
    {
        HashSet<string> keysToRemove;
        lock (_indexLock)
        {
            if (!_roleKeyIndex.TryGetValue(roleName, out var keys))
                return;
            keysToRemove = new HashSet<string>(keys);
            _roleKeyIndex.Remove(roleName);
        }

        foreach (var key in keysToRemove)
            _cache.Remove(key);
    }

    /// <inheritdoc />
    public void InvalidateAll()
    {
        List<string> allKeys;
        lock (_indexLock)
        {
            allKeys = _roleKeyIndex.Values.SelectMany(s => s).Distinct().ToList();
            _roleKeyIndex.Clear();
        }

        // Also remove the anonymous key (not in the role index)
        allKeys.Add(CachePrefix + "anonymous");

        foreach (var key in allKeys)
            _cache.Remove(key);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IReadOnlyList<string> NormaliseRoles(IReadOnlyList<string>? roles) =>
        roles is { Count: > 0 }
            ? [.. roles.Select(r => r.Trim()).Where(r => r.Length > 0).Order(StringComparer.OrdinalIgnoreCase)]
            : [];

    private static string BuildCacheKey(IReadOnlyList<string> sortedRoles) =>
        sortedRoles.Count == 0
            ? CachePrefix + "anonymous"
            : CachePrefix + "roles:" + string.Join("+", sortedRoles);

    private static FeatureItemDto BuildFlag(Feature feature, IReadOnlyList<RoleEntitlement> entitlements)
    {
        // Collect all matching entitlements for this feature
        var matching = entitlements
            .Where(e => string.Equals(e.FeatureKey, feature.Key, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matching.Count == 0)
        {
            // No role entitlement — fall back to global default
            return feature.DefaultEnabled
                ? new FeatureItemDto(feature.Key, true,  "default_on",  null)
                : new FeatureItemDto(feature.Key, false, "default_off", null);
        }

        // Any-enabled wins: if at least one role has Enabled=true, the feature is on
        var enabledEntry = matching.FirstOrDefault(e => e.Enabled);
        if (enabledEntry is not null)
            return new FeatureItemDto(feature.Key, true, $"role:{enabledEntry.RoleName}", enabledEntry.ExpiresAt);

        // All matching roles explicitly disable this feature
        var disabledEntry = matching[0];
        return new FeatureItemDto(feature.Key, false, $"role:{disabledEntry.RoleName}:disabled", disabledEntry.ExpiresAt);
    }
}
