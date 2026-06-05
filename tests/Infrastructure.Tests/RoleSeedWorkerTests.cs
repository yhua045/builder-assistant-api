using BuilderAssistantApi.Domain.Constants;
using BuilderAssistantApi.Domain.Entities;
using BuilderAssistantApi.Infrastructure.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BuilderAssistantApi.Infrastructure.Tests;

public sealed class RoleSeedWorkerTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Mock<UserManager<User>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<User>>();
#pragma warning disable CS8625
        return new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625
    }

        private static Mock<RoleManager<BuilderAssistantApi.Domain.Entities.UserRole>> CreateRoleManagerMock()
        {
        var store = new Mock<IRoleStore<BuilderAssistantApi.Domain.Entities.UserRole>>();
    #pragma warning disable CS8625
        return new Mock<RoleManager<BuilderAssistantApi.Domain.Entities.UserRole>>(store.Object, null, null, null, null);
    #pragma warning restore CS8625
        }

    private static RoleSeedWorker CreateWorker(
        Mock<UserManager<User>> userManager,
        Mock<RoleManager<BuilderAssistantApi.Domain.Entities.UserRole>> roleManager,
        SeedOptions? seedOptions = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(userManager.Object);
        services.AddSingleton(roleManager.Object);
        services.AddSingleton<IOptions<SeedOptions>>(
            new OptionsWrapper<SeedOptions>(seedOptions ?? new SeedOptions()));

        var sp = services.BuildServiceProvider();
        return new RoleSeedWorker(sp, NullLogger<RoleSeedWorker>.Instance);
    }

    // ── Role seeding ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SeedAsync_CreatesAllDefaultRoles_WhenNoneExist()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();

        roleManager.Setup(rm => rm.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(false);
        roleManager.Setup(rm => rm.CreateAsync(It.IsAny<BuilderAssistantApi.Domain.Entities.UserRole>()))
                   .ReturnsAsync(IdentityResult.Success);

        var worker = CreateWorker(userManager, roleManager);
        await worker.StartAsync(CancellationToken.None);

        foreach (var role in ApplicationRoles.All)
        {
            roleManager.Verify(rm => rm.CreateAsync(
                It.Is<BuilderAssistantApi.Domain.Entities.UserRole>(r => r.Name == role)), Times.Once);
        }
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_WhenRolesAlreadyExist()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();

        roleManager.Setup(rm => rm.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        var worker = CreateWorker(userManager, roleManager);
        await worker.StartAsync(CancellationToken.None);

        roleManager.Verify(rm => rm.CreateAsync(It.IsAny<BuilderAssistantApi.Domain.Entities.UserRole>()), Times.Never);
    }

    // ── Admin user seeding ───────────────────────────────────────────────────

    [Fact]
    public async Task SeedAsync_CreatesAdminUser_WhenSeedOptionsConfigured()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();

        roleManager.Setup(rm => rm.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        userManager.Setup(m => m.FindByEmailAsync("admin@example.com")).ReturnsAsync((User?)null);
        userManager.Setup(m => m.CreateAsync(It.IsAny<User>(), "Admin@1234!"))
                   .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.AddToRoleAsync(It.IsAny<User>(), ApplicationRoles.Admin))
                   .ReturnsAsync(IdentityResult.Success);

        var worker = CreateWorker(userManager, roleManager, new SeedOptions
        {
            AdminEmail = "admin@example.com",
            AdminPassword = "Admin@1234!"
        });

        await worker.StartAsync(CancellationToken.None);

        userManager.Verify(m => m.CreateAsync(
            It.Is<User>(u => u.Email == "admin@example.com"), "Admin@1234!"), Times.Once);
        userManager.Verify(m => m.AddToRoleAsync(
            It.IsAny<User>(), ApplicationRoles.Admin), Times.Once);
    }

    [Fact]
    public async Task SeedAsync_SkipsAdminUserSeed_WhenEmailNotConfigured()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();

        roleManager.Setup(rm => rm.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        var worker = CreateWorker(userManager, roleManager, new SeedOptions
        {
            AdminEmail = null,
            AdminPassword = null
        });

        await worker.StartAsync(CancellationToken.None);

        userManager.Verify(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SeedAsync_SkipsAdminUserSeed_WhenAlreadyExists()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();

        roleManager.Setup(rm => rm.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(true);

        var existingAdmin = new User { Email = "admin@example.com", UserName = "admin@example.com" };
        userManager.Setup(m => m.FindByEmailAsync("admin@example.com")).ReturnsAsync(existingAdmin);

        var worker = CreateWorker(userManager, roleManager, new SeedOptions
        {
            AdminEmail = "admin@example.com",
            AdminPassword = "Admin@1234!"
        });

        await worker.StartAsync(CancellationToken.None);

        userManager.Verify(m => m.CreateAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }
}
