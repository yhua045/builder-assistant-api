using BuilderAssistantApi.Domain.Entities;
using BuilderAssistantApi.Domain.Constants;
using BuilderAssistantApi.Infrastructure;
using BuilderAssistantApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BuilderAssistantApi.Infrastructure.Tests.Repositories;

public class EfFeatureRepositoryTests
{
    private static BuilderAssistantDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<BuilderAssistantDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BuilderAssistantDbContext(options);
    }

    // ── ListAllAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAllAsync_ReturnsAllFeatures()
    {
        // Arrange
        await using var ctx = CreateInMemoryContext();
        ctx.Features.AddRange(
            new Feature { Key = FeatureKeys.OcrScan,      DefaultEnabled = false, Description = "OCR" },
            new Feature { Key = "basic_feature", DefaultEnabled = true }
        );
        await ctx.SaveChangesAsync();
        var repo = new EfFeatureRepository(ctx);

        // Act
        var result = await repo.ListAllAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, f => f.Key == FeatureKeys.OcrScan);
        Assert.Contains(result, f => f.Key == "basic_feature");
    }

    [Fact]
    public async Task ListAllAsync_EmptyDatabase_ReturnsEmpty()
    {
        // Arrange
        await using var ctx = CreateInMemoryContext();
        var repo = new EfFeatureRepository(ctx);

        // Act
        var result = await repo.ListAllAsync();

        // Assert
        Assert.Empty(result);
    }

    // ── GetByKeyAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByKeyAsync_ExistingKey_ReturnsFeature()
    {
        // Arrange
        await using var ctx = CreateInMemoryContext();
        ctx.Features.Add(new Feature { Key = FeatureKeys.OcrScan, DefaultEnabled = false, Description = "OCR" });
        await ctx.SaveChangesAsync();
        var repo = new EfFeatureRepository(ctx);

        // Act
        var result = await repo.GetByKeyAsync(FeatureKeys.OcrScan);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(FeatureKeys.OcrScan, result.Key);
    }

    [Fact]
    public async Task GetByKeyAsync_MissingKey_ReturnsNull()
    {
        // Arrange
        await using var ctx = CreateInMemoryContext();
        var repo = new EfFeatureRepository(ctx);

        // Act
        var result = await repo.GetByKeyAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    // ── ListEntitlementsForRolesAsync ────────────────────────────────────────

    [Fact]
    public async Task ListEntitlementsForRoles_FiltersExpiredEntitlements()
    {
        // Arrange
        await using var ctx = CreateInMemoryContext();
        ctx.RoleEntitlements.AddRange(
            new RoleEntitlement { RoleName = "Premium", FeatureKey = FeatureKeys.OcrScan,  Enabled = true,  ExpiresAt = null,                             CreatedAt = DateTimeOffset.UtcNow }, // active
            new RoleEntitlement { RoleName = "Premium", FeatureKey = "high_rate", Enabled = true,  ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1), CreatedAt = DateTimeOffset.UtcNow }  // expired
        );
        await ctx.SaveChangesAsync();
        var repo = new EfFeatureRepository(ctx);

        // Act
        var result = await repo.ListEntitlementsForRolesAsync(["Premium"]);

        // Assert
        Assert.Single(result);
        Assert.Equal(FeatureKeys.OcrScan, result[0].FeatureKey);
    }

    [Fact]
    public async Task ListEntitlementsForRoles_MultipleRoles_ReturnsAll()
    {
        // Arrange
        await using var ctx = CreateInMemoryContext();
        ctx.RoleEntitlements.AddRange(
            new RoleEntitlement { RoleName = "Admin",   FeatureKey = FeatureKeys.OcrScan,      Enabled = true, CreatedAt = DateTimeOffset.UtcNow },
            new RoleEntitlement { RoleName = "Premium", FeatureKey = FeatureKeys.OcrScan,      Enabled = true, CreatedAt = DateTimeOffset.UtcNow },
            new RoleEntitlement { RoleName = "Basic",   FeatureKey = "basic_feature", Enabled = true, CreatedAt = DateTimeOffset.UtcNow }
        );
        await ctx.SaveChangesAsync();
        var repo = new EfFeatureRepository(ctx);

        // Act
        var result = await repo.ListEntitlementsForRolesAsync(["Admin", "Premium"]);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.Contains(e.RoleName, new[] { "Admin", "Premium" }));
    }

    [Fact]
    public async Task ListEntitlementsForRoles_EmptyRoleList_ReturnsEmpty()
    {
        // Arrange
        await using var ctx = CreateInMemoryContext();
        ctx.RoleEntitlements.Add(new RoleEntitlement { RoleName = "Premium", FeatureKey = FeatureKeys.OcrScan, Enabled = true, CreatedAt = DateTimeOffset.UtcNow });
        await ctx.SaveChangesAsync();
        var repo = new EfFeatureRepository(ctx);

        // Act
        var result = await repo.ListEntitlementsForRolesAsync([]);

        // Assert
        Assert.Empty(result);
    }

    // ── UpsertEntitlementAsync ───────────────────────────────────────────────

    [Fact]
    public async Task UpsertEntitlementAsync_NewRow_Inserts()
    {
        // Arrange
        await using var ctx = CreateInMemoryContext();
        var repo = new EfFeatureRepository(ctx);
        var entitlement = new RoleEntitlement
        {
            RoleName   = "Premium",
            FeatureKey = FeatureKeys.OcrScan,
            Enabled    = true,
            ExpiresAt  = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt  = DateTimeOffset.UtcNow
        };

        // Act
        await repo.UpsertEntitlementAsync(entitlement);

        // Assert
        var saved = await ctx.RoleEntitlements
            .FirstOrDefaultAsync(e => e.RoleName == "Premium" && e.FeatureKey == FeatureKeys.OcrScan);
        Assert.NotNull(saved);
        Assert.True(saved.Id > 0);
        Assert.True(saved.Enabled);
        Assert.NotNull(saved.ExpiresAt);
    }

    [Fact]
    public async Task UpsertEntitlementAsync_ExistingRow_Updates()
    {
        // Arrange — seed an existing row
        await using var ctx = CreateInMemoryContext();
        ctx.RoleEntitlements.Add(new RoleEntitlement
        {
            RoleName   = "Premium",
            FeatureKey = FeatureKeys.OcrScan,
            Enabled    = false,
            CreatedAt  = DateTimeOffset.UtcNow
        });
        await ctx.SaveChangesAsync();
        var countBefore = await ctx.RoleEntitlements.CountAsync();
        var repo = new EfFeatureRepository(ctx);

        // Act — upsert to flip Enabled
        var update = new RoleEntitlement
        {
            RoleName   = "Premium",
            FeatureKey = FeatureKeys.OcrScan,
            Enabled    = true,
            CreatedAt  = DateTimeOffset.UtcNow
        };
        await repo.UpsertEntitlementAsync(update);

        // Assert — row count unchanged, value updated
        Assert.Equal(countBefore, await ctx.RoleEntitlements.CountAsync());
        var saved = await ctx.RoleEntitlements.SingleAsync(e => e.RoleName == "Premium" && e.FeatureKey == FeatureKeys.OcrScan);
        Assert.True(saved.Enabled);
    }

    // ── DeleteEntitlementAsync ───────────────────────────────────────────────

    [Fact]
    public async Task DeleteEntitlementAsync_RemovesRow()
    {
        // Arrange
        await using var ctx = CreateInMemoryContext();
        ctx.RoleEntitlements.Add(new RoleEntitlement
        {
            RoleName   = "Premium",
            FeatureKey = FeatureKeys.OcrScan,
            Enabled    = true,
            CreatedAt  = DateTimeOffset.UtcNow
        });
        await ctx.SaveChangesAsync();
        var repo = new EfFeatureRepository(ctx);

        // Act
        await repo.DeleteEntitlementAsync("Premium", FeatureKeys.OcrScan);

        // Assert
        Assert.Empty(ctx.RoleEntitlements.ToList());
    }

    [Fact]
    public async Task DeleteEntitlementAsync_NonExistentRow_DoesNotThrow()
    {
        // Arrange
        await using var ctx = CreateInMemoryContext();
        var repo = new EfFeatureRepository(ctx);

        // Act & Assert — should not throw
        await repo.DeleteEntitlementAsync("NonExistent", FeatureKeys.OcrScan);
    }
}
