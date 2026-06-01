using BuilderAssistantApi.Application.Dtos;
using BuilderAssistantApi.Application.Interfaces;
using BuilderAssistantApi.Domain.Entities;
using BuilderAssistantApi.Domain.Constants;
using BuilderAssistantApi.Domain.Repositories;
using BuilderAssistantApi.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using BuilderAssistantApi.Infrastructure.Options;
using Moq;
using Xunit;

namespace BuilderAssistantApi.Infrastructure.Tests.Services;

public class FeatureFlagServiceTests
{
    private readonly Mock<IFeatureRepository> _repoMock;
    private readonly IMemoryCache _cache;
    private readonly FeatureFlagService _service;

    public FeatureFlagServiceTests()
    {
        _repoMock = new Mock<IFeatureRepository>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        var options = new OptionsWrapper<FeatureFlagCacheOptions>(new FeatureFlagCacheOptions());
        _service = new FeatureFlagService(_repoMock.Object, _cache, options);
    }

    // ── GetEffectiveFlagsAsync — defaults ────────────────────────────────────

    [Fact]
    public async Task GetEffectiveFlags_NoRoles_UsesDefaultEnabled_True()
    {
        // Arrange
        var features = new List<Feature>
        {
            new() { Key = "basic_feature", DefaultEnabled = true, Description = "Basic" }
        };
        _repoMock.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(features);
        _repoMock.Setup(r => r.ListEntitlementsForRolesAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);

        // Act
        var result = await _service.GetEffectiveFlagsAsync(null, null);

        // Assert
        Assert.True(result.AsAnonymous);
        var flag = result.Flags.Single(f => f.Key == "basic_feature");
        Assert.True(flag.Enabled);
        Assert.Equal("default_on", flag.Reason);
        Assert.Null(flag.ExpiresAt);
    }

    [Fact]
    public async Task GetEffectiveFlags_NoRoles_UsesDefaultEnabled_False()
    {
        // Arrange
        var features = new List<Feature>
        {
            new() { Key = FeatureKeys.OcrScan, DefaultEnabled = false, Description = "OCR" }
        };
        _repoMock.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
                 .ReturnsAsync(features);
        _repoMock.Setup(r => r.ListEntitlementsForRolesAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);

        // Act
        var result = await _service.GetEffectiveFlagsAsync(null, null);

        // Assert
        var flag = result.Flags.Single(f => f.Key == FeatureKeys.OcrScan);
        Assert.False(flag.Enabled);
        Assert.Equal("default_off", flag.Reason);
    }

    // ── GetEffectiveFlagsAsync — role entitlements ───────────────────────────

    [Fact]
    public async Task GetEffectiveFlags_RoleHasEntitlement_OverridesDefaultDisabled()
    {
        // Arrange
        var features = new List<Feature> { new() { Key = FeatureKeys.OcrScan, DefaultEnabled = false } };
        var entitlements = new List<RoleEntitlement>
        {
            new() { RoleName = "Premium", FeatureKey = FeatureKeys.OcrScan, Enabled = true, ExpiresAt = null }
        };
        _repoMock.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(features);
        _repoMock.Setup(r => r.ListEntitlementsForRolesAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(entitlements);

        // Act
        var result = await _service.GetEffectiveFlagsAsync(42, ["Premium"]);

        // Assert
        Assert.False(result.AsAnonymous);
        Assert.Equal("42", result.UserId);
        var flag = result.Flags.Single(f => f.Key == FeatureKeys.OcrScan);
        Assert.True(flag.Enabled);
        Assert.Equal("role:Premium", flag.Reason);
    }

    [Fact]
    public async Task GetEffectiveFlags_MultipleRoles_AnyEnabledWins()
    {
        // Arrange – one role disables, another enables; enabled wins
        var features = new List<Feature> { new() { Key = FeatureKeys.OcrScan, DefaultEnabled = false } };
        var entitlements = new List<RoleEntitlement>
        {
            new() { RoleName = "Basic",   FeatureKey = FeatureKeys.OcrScan, Enabled = false },
            new() { RoleName = "Premium", FeatureKey = FeatureKeys.OcrScan, Enabled = true  }
        };
        _repoMock.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(features);
        _repoMock.Setup(r => r.ListEntitlementsForRolesAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(entitlements);

        // Act
        var result = await _service.GetEffectiveFlagsAsync(null, ["Basic", "Premium"]);

        // Assert
        var flag = result.Flags.Single(f => f.Key == FeatureKeys.OcrScan);
        Assert.True(flag.Enabled);
    }

    [Fact]
    public async Task GetEffectiveFlags_RoleEntitlementExplicitlyDisabled_ReasonReflectsDisabled()
    {
        // Arrange – entitlement exists but Enabled=false; no other role enables it
        var features = new List<Feature> { new() { Key = FeatureKeys.OcrScan, DefaultEnabled = true } };
        var entitlements = new List<RoleEntitlement>
        {
            new() { RoleName = "Basic", FeatureKey = FeatureKeys.OcrScan, Enabled = false }
        };
        _repoMock.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(features);
        _repoMock.Setup(r => r.ListEntitlementsForRolesAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(entitlements);

        // Act
        var result = await _service.GetEffectiveFlagsAsync(null, ["Basic"]);

        // Assert
        var flag = result.Flags.Single(f => f.Key == FeatureKeys.OcrScan);
        Assert.False(flag.Enabled);
        Assert.Equal("role:Basic:disabled", flag.Reason);
    }

    [Fact]
    public async Task GetEffectiveFlags_ExpiredRoleEntitlement_FallsBackToDefault()
    {
        // Arrange — repo already filters expired rows; returning empty simulates that
        var features = new List<Feature> { new() { Key = FeatureKeys.OcrScan, DefaultEnabled = false } };
        _repoMock.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(features);
        _repoMock.Setup(r => r.ListEntitlementsForRolesAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);

        // Act
        var result = await _service.GetEffectiveFlagsAsync(null, ["Premium"]);

        // Assert
        var flag = result.Flags.Single(f => f.Key == FeatureKeys.OcrScan);
        Assert.False(flag.Enabled);
        Assert.Equal("default_off", flag.Reason);
    }

    // ── Cache behaviour ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetEffectiveFlags_CacheMiss_PopulatesCache()
    {
        // Arrange
        _repoMock.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repoMock.Setup(r => r.ListEntitlementsForRolesAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);

        // Act — call twice
        await _service.GetEffectiveFlagsAsync(null, null);
        await _service.GetEffectiveFlagsAsync(null, null);

        // Assert — repo only called once
        _repoMock.Verify(r => r.ListAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetEffectiveFlags_CacheKeyIncludesRoles_HitOnSameRolesInDifferentOrder()
    {
        // Arrange
        _repoMock.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repoMock.Setup(r => r.ListEntitlementsForRolesAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);

        // Act — ["B","A"] and ["A","B"] should share the same cache entry
        await _service.GetEffectiveFlagsAsync(null, ["B", "A"]);
        await _service.GetEffectiveFlagsAsync(null, ["A", "B"]);

        // Assert — only one DB hit
        _repoMock.Verify(r => r.ListAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidateRole_RemovesCacheEntriesForRole()
    {
        // Arrange — populate cache for "Premium"
        _repoMock.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repoMock.Setup(r => r.ListEntitlementsForRolesAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);

        await _service.GetEffectiveFlagsAsync(null, ["Premium"]);

        // Act — invalidate the Premium role
        ((IFeatureCacheInvalidator)_service).InvalidateRole("Premium");

        // Next call should hit repo again
        await _service.GetEffectiveFlagsAsync(null, ["Premium"]);

        // Assert — exactly 2 repo calls
        _repoMock.Verify(r => r.ListAllAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task InvalidateAll_PurgesAllCacheEntries()
    {
        // Arrange — populate cache for anonymous and "Premium"
        _repoMock.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repoMock.Setup(r => r.ListEntitlementsForRolesAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);

        await _service.GetEffectiveFlagsAsync(null, null);
        await _service.GetEffectiveFlagsAsync(null, ["Premium"]);

        // Act — invalidate all
        ((IFeatureCacheInvalidator)_service).InvalidateAll();

        await _service.GetEffectiveFlagsAsync(null, null);
        await _service.GetEffectiveFlagsAsync(null, ["Premium"]);

        // Assert — repo called 4 times total (2 before + 2 after invalidation)
        _repoMock.Verify(r => r.ListAllAsync(It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    // ── IsEnabledAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task IsEnabled_DelegatesToGetEffectiveFlags()
    {
        // Arrange
        var features = new List<Feature>
        {
            new() { Key = FeatureKeys.OcrScan,      DefaultEnabled = false },
            new() { Key = "basic_feature", DefaultEnabled = true  }
        };
        var entitlements = new List<RoleEntitlement>
        {
            new() { RoleName = "Premium", FeatureKey = FeatureKeys.OcrScan, Enabled = true }
        };
        _repoMock.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(features);
        _repoMock.Setup(r => r.ListEntitlementsForRolesAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(entitlements);

        // Act
        var ocrEnabled   = await _service.IsEnabledAsync(["Premium"], FeatureKeys.OcrScan);
        var basicEnabled = await _service.IsEnabledAsync(["Premium"], "basic_feature");

        // Assert
        Assert.True(ocrEnabled);
        Assert.True(basicEnabled);
    }

    [Fact]
    public async Task IsEnabled_UnknownFeature_ReturnsFalse()
    {
        // Arrange
        _repoMock.Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repoMock.Setup(r => r.ListEntitlementsForRolesAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync([]);

        // Act
        var result = await _service.IsEnabledAsync(null, "nonexistent_feature");

    }
}
