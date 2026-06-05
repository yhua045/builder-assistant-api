using System.Security.Claims;
using BuilderAssistantApi.Api.Authorization;
using BuilderAssistantApi.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace BuilderAssistantApi.Api.Tests.Authorization;

public class ManageUserRolesHandlerTests
{
    private static ManageUserRolesHandler CreateHandler() => new();

    private static ClaimsPrincipal PrincipalWithRoles(params string[] roles)
    {
        var claims = roles.Select(r => new Claim(ClaimTypes.Role, r));
        var identity = new ClaimsIdentity(claims, authenticationType: "Test");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal AnonymousPrincipal()
        => new ClaimsPrincipal(new ClaimsIdentity());

    private static AuthorizationHandlerContext CreateContext(ClaimsPrincipal user)
    {
        var requirement = new ManageUserRolesRequirement();
        return new AuthorizationHandlerContext([requirement], user, resource: null);
    }

    [Fact]
    public async Task HandleRequirement_AdminRole_Succeeds()
    {
        var handler = CreateHandler();
        var context = CreateContext(PrincipalWithRoles(ApplicationRoles.Admin));

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirement_NonAdminRole_DoesNotSucceed()
    {
        var handler = CreateHandler();
        var context = CreateContext(PrincipalWithRoles(ApplicationRoles.SiteManager));

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirement_MultipleRolesNoAdmin_DoesNotSucceed()
    {
        var handler = CreateHandler();
        var context = CreateContext(PrincipalWithRoles(ApplicationRoles.SiteManager, ApplicationRoles.Owner));

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirement_Unauthenticated_DoesNotSucceed()
    {
        var handler = CreateHandler();
        var context = CreateContext(AnonymousPrincipal());

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirement_AdminPlusOtherRoles_Succeeds()
    {
        var handler = CreateHandler();
        var context = CreateContext(PrincipalWithRoles(ApplicationRoles.Admin, ApplicationRoles.SiteManager));

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }
}
