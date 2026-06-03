using BuilderAssistantApi.Application.Dtos;
using BuilderAssistantApi.Application.Interfaces;
using BuilderAssistantApi.Domain.Entities;
using BuilderAssistantApi.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit;

namespace BuilderAssistantApi.Infrastructure.Tests.Services;

public sealed class RoleServiceTests
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

    private static RoleService CreateService(
        Mock<UserManager<User>> userManager,
        Mock<RoleManager<BuilderAssistantApi.Domain.Entities.UserRole>> roleManager)
        => new(userManager.Object, roleManager.Object);

    private static User MakeUser(long id = 1, string email = "test@example.com") =>
        new() { Id = id, Email = email, UserName = email };

    // ── ListAllRolesAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ListAllRolesAsync_ReturnsAllRoles()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();
        var expectedRoles = new List<BuilderAssistantApi.Domain.Entities.UserRole>
        {
            new() { Name = "Admin" },
            new() { Name = "Owner" }
        };

        roleManager.Setup(rm => rm.Roles).Returns(expectedRoles.AsQueryable());

        var service = CreateService(userManager, roleManager);
        var result = await service.ListAllRolesAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains("Admin", result);
        Assert.Contains("Owner", result);
    }

    // ── GetUserRolesAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserRolesAsync_ExistingUser_ReturnsRoles()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();
        var user = MakeUser(42);

        userManager.Setup(m => m.FindByIdAsync("42")).ReturnsAsync(user);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(["Owner", "SiteManager"]);

        var service = CreateService(userManager, roleManager);
        var result = await service.GetUserRolesAsync(42);

        Assert.NotNull(result);
        Assert.Equal(42, result.UserId);
        Assert.Contains("Owner", result.Roles);
        Assert.Contains("SiteManager", result.Roles);
    }

    [Fact]
    public async Task GetUserRolesAsync_UnknownUser_ReturnsNull()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();

        userManager.Setup(m => m.FindByIdAsync("999")).ReturnsAsync((User?)null);

        var service = CreateService(userManager, roleManager);
        var result = await service.GetUserRolesAsync(999);

        Assert.Null(result);
    }

    // ── AssignRoleAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task AssignRoleAsync_ValidRoleAndUser_ReturnsTrue()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();
        var user = MakeUser(1);

        userManager.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        roleManager.Setup(rm => rm.RoleExistsAsync("Admin")).ReturnsAsync(true);
        userManager.Setup(m => m.AddToRoleAsync(user, "Admin")).ReturnsAsync(IdentityResult.Success);

        var service = CreateService(userManager, roleManager);
        var result = await service.AssignRoleAsync(1, "Admin");

        Assert.True(result);
        userManager.Verify(m => m.AddToRoleAsync(user, "Admin"), Times.Once);
    }

    [Fact]
    public async Task AssignRoleAsync_UnknownRole_ReturnsFalse()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();
        var user = MakeUser(1);

        userManager.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        roleManager.Setup(rm => rm.RoleExistsAsync("Ghost")).ReturnsAsync(false);

        var service = CreateService(userManager, roleManager);
        var result = await service.AssignRoleAsync(1, "Ghost");

        Assert.False(result);
        userManager.Verify(m => m.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AssignRoleAsync_UnknownUser_ReturnsFalse()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();

        userManager.Setup(m => m.FindByIdAsync("99")).ReturnsAsync((User?)null);

        var service = CreateService(userManager, roleManager);
        var result = await service.AssignRoleAsync(99, "Admin");

        Assert.False(result);
        userManager.Verify(m => m.AddToRoleAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    // ── RemoveRoleAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveRoleAsync_UserHasRole_ReturnsTrue()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();
        var user = MakeUser(1);

        userManager.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        userManager.Setup(m => m.IsInRoleAsync(user, "Owner")).ReturnsAsync(true);
        userManager.Setup(m => m.RemoveFromRoleAsync(user, "Owner")).ReturnsAsync(IdentityResult.Success);

        var service = CreateService(userManager, roleManager);
        var result = await service.RemoveRoleAsync(1, "Owner");

        Assert.True(result);
        userManager.Verify(m => m.RemoveFromRoleAsync(user, "Owner"), Times.Once);
    }

    [Fact]
    public async Task RemoveRoleAsync_UserLacksRole_ReturnsFalse()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();
        var user = MakeUser(1);

        userManager.Setup(m => m.FindByIdAsync("1")).ReturnsAsync(user);
        userManager.Setup(m => m.IsInRoleAsync(user, "Admin")).ReturnsAsync(false);

        var service = CreateService(userManager, roleManager);
        var result = await service.RemoveRoleAsync(1, "Admin");

        Assert.False(result);
        userManager.Verify(m => m.RemoveFromRoleAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RemoveRoleAsync_UnknownUser_ReturnsFalse()
    {
        var userManager = CreateUserManagerMock();
        var roleManager = CreateRoleManagerMock();

        userManager.Setup(m => m.FindByIdAsync("99")).ReturnsAsync((User?)null);

        var service = CreateService(userManager, roleManager);
        var result = await service.RemoveRoleAsync(99, "Owner");

        Assert.False(result);
        userManager.Verify(m => m.RemoveFromRoleAsync(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }
}
