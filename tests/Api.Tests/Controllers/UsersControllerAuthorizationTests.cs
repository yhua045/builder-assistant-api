using System.Reflection;
using BuilderAssistantApi.Api.Controllers;
using BuilderAssistantApi.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace BuilderAssistantApi.Api.Tests.Controllers;

public class UsersControllerAuthorizationTests
{
    private static MethodInfo GetMethod(string name)
        => typeof(UsersController).GetMethod(name)
           ?? throw new InvalidOperationException($"Method '{name}' not found on UsersController.");

    [Fact]
    public void GetUserRoles_HasManageUserRolesPolicy()
    {
        var method = GetMethod(nameof(UsersController.GetUserRoles));
        var attr = method.GetCustomAttributes<AuthorizeAttribute>()
                         .SingleOrDefault(a => a.Policy == ApplicationPolicies.ManageUserRoles);
        Assert.NotNull(attr);
    }

    [Fact]
    public void AssignRole_HasManageUserRolesPolicy()
    {
        var method = GetMethod(nameof(UsersController.AssignRole));
        var attr = method.GetCustomAttributes<AuthorizeAttribute>()
                         .SingleOrDefault(a => a.Policy == ApplicationPolicies.ManageUserRoles);
        Assert.NotNull(attr);
    }

    [Fact]
    public void RemoveRole_HasManageUserRolesPolicy()
    {
        var method = GetMethod(nameof(UsersController.RemoveRole));
        var attr = method.GetCustomAttributes<AuthorizeAttribute>()
                         .SingleOrDefault(a => a.Policy == ApplicationPolicies.ManageUserRoles);
        Assert.NotNull(attr);
    }

    [Fact]
    public void GetUserRoles_NoRoleBasedAuthorize()
    {
        var method = GetMethod(nameof(UsersController.GetUserRoles));
        var roleAttrs = method.GetCustomAttributes<AuthorizeAttribute>()
                              .Where(a => a.Roles != null);
        Assert.Empty(roleAttrs);
    }

    [Fact]
    public void AssignRole_NoRoleBasedAuthorize()
    {
        var method = GetMethod(nameof(UsersController.AssignRole));
        var roleAttrs = method.GetCustomAttributes<AuthorizeAttribute>()
                              .Where(a => a.Roles != null);
        Assert.Empty(roleAttrs);
    }

    [Fact]
    public void RemoveRole_NoRoleBasedAuthorize()
    {
        var method = GetMethod(nameof(UsersController.RemoveRole));
        var roleAttrs = method.GetCustomAttributes<AuthorizeAttribute>()
                              .Where(a => a.Roles != null);
        Assert.Empty(roleAttrs);
    }

    [Fact]
    public void Register_AllowAnonymousUnchanged()
    {
        var method = GetMethod(nameof(UsersController.Register));
        var attr = method.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void ConfirmEmail_AllowAnonymousUnchanged()
    {
        var method = GetMethod(nameof(UsersController.ConfirmEmail));
        var attr = method.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void VerifyTwoFactor_AllowAnonymousUnchanged()
    {
        var method = GetMethod(nameof(UsersController.VerifyTwoFactor));
        var attr = method.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void GetRoles_PlainAuthorizeUnchanged()
    {
        var method = GetMethod(nameof(UsersController.GetRoles));
        var attr = method.GetCustomAttributes<AuthorizeAttribute>()
                         .SingleOrDefault(a => a.Policy == null && a.Roles == null);
        Assert.NotNull(attr);
    }

    [Fact]
    public void Controller_NoClassLevelRoleBasedAuthorize()
    {
        var roleAttrs = typeof(UsersController)
            .GetCustomAttributes<AuthorizeAttribute>()
            .Where(a => a.Roles != null);
        Assert.Empty(roleAttrs);
    }
}
