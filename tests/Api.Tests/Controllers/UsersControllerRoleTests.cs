using BuilderAssistantApi.Api.Controllers;
using BuilderAssistantApi.Application.Dtos;
using BuilderAssistantApi.Application.Interfaces;
using BuilderAssistantApi.Application.Services;
using BuilderAssistantApi.Domain.Constants;
using BuilderAssistantApi.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace BuilderAssistantApi.Api.Tests.Controllers;

public sealed class UsersControllerRoleTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Mock<UserManager<User>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<User>>();
#pragma warning disable CS8625
        return new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625
    }

    private static UsersController CreateController(
        Mock<IRoleService> roleService,
        Mock<IUserRegistrationService>? registrationService = null,
        Mock<UserManager<User>>? userManager = null)
    {
        var regSvc = registrationService ?? new Mock<IUserRegistrationService>();
        var um     = userManager ?? CreateUserManagerMock();

        return new UsersController(regSvc.Object, um.Object, roleService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    // ── GET /api/roles ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetRoles_ReturnsAllSystemRoles()
    {
        var roleService = new Mock<IRoleService>();
        roleService.Setup(s => s.ListAllRolesAsync(It.IsAny<CancellationToken>()))
                   .ReturnsAsync(ApplicationRoles.All);

        var controller = CreateController(roleService);

        var result = await controller.GetRoles(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
    }

    // ── GET /api/users/{userId}/roles ────────────────────────────────────────

    [Fact]
    public async Task GetUserRoles_ExistingUser_Returns200WithRoles()
    {
        var roleService = new Mock<IRoleService>();
        roleService.Setup(s => s.GetUserRolesAsync(42, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new UserRolesDto(42, ["Owner"]));

        var controller = CreateController(roleService);

        var result = await controller.GetUserRoles(42, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        var dto = Assert.IsType<UserRolesDto>(ok.Value);
        Assert.Equal(42, dto.UserId);
        Assert.Contains("Owner", dto.Roles);
    }

    [Fact]
    public async Task GetUserRoles_UnknownUser_Returns404()
    {
        var roleService = new Mock<IRoleService>();
        roleService.Setup(s => s.GetUserRolesAsync(999, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((UserRolesDto?)null);

        var controller = CreateController(roleService);

        var result = await controller.GetUserRoles(999, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
    }

    // ── POST /api/users/{userId}/roles ───────────────────────────────────────

    [Fact]
    public async Task AssignRole_ValidRequest_Returns204()
    {
        var roleService = new Mock<IRoleService>();
        roleService.Setup(s => s.GetUserRolesAsync(1, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new UserRolesDto(1, []));
        roleService.Setup(s => s.AssignRoleAsync(1, "SiteManager", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var controller = CreateController(roleService);

        var result = await controller.AssignRole(1, new AssignRoleRequest("SiteManager"), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task AssignRole_UnknownRole_Returns400()
    {
        var roleService = new Mock<IRoleService>();
        roleService.Setup(s => s.GetUserRolesAsync(1, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new UserRolesDto(1, []));
        roleService.Setup(s => s.AssignRoleAsync(1, "Ghost", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

        var controller = CreateController(roleService);

        var result = await controller.AssignRole(1, new AssignRoleRequest("Ghost"), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
    }

    [Fact]
    public async Task AssignRole_UnknownUser_Returns404()
    {
        var roleService = new Mock<IRoleService>();
        roleService.Setup(s => s.GetUserRolesAsync(99, It.IsAny<CancellationToken>()))
                   .ReturnsAsync((UserRolesDto?)null);

        var controller = CreateController(roleService);

        var result = await controller.AssignRole(99, new AssignRoleRequest("Owner"), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
    }

    [Fact]
    public async Task AssignRole_AlreadyAssigned_Returns409()
    {
        var roleService = new Mock<IRoleService>();
        roleService.Setup(s => s.GetUserRolesAsync(1, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new UserRolesDto(1, ["Owner"]));

        var controller = CreateController(roleService);

        var result = await controller.AssignRole(1, new AssignRoleRequest("Owner"), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
    }

    // ── DELETE /api/users/{userId}/roles/{roleName} ──────────────────────────

    [Fact]
    public async Task RemoveRole_UserHasRole_Returns204()
    {
        var roleService = new Mock<IRoleService>();
        roleService.Setup(s => s.RemoveRoleAsync(1, "Owner", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(true);

        var controller = CreateController(roleService);

        var result = await controller.RemoveRole(1, "Owner", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task RemoveRole_UserLacksRole_Returns404()
    {
        var roleService = new Mock<IRoleService>();
        roleService.Setup(s => s.RemoveRoleAsync(1, "Admin", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(false);

        var controller = CreateController(roleService);

        var result = await controller.RemoveRole(1, "Admin", CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
    }
}
