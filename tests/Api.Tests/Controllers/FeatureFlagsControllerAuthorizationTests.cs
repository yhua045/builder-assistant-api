using System.Reflection;
using BuilderAssistantApi.Api.Controllers;
using BuilderAssistantApi.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace BuilderAssistantApi.Api.Tests.Controllers;

public class FeatureFlagsControllerAuthorizationTests
{
    private static MethodInfo GetMethod(string name)
        => typeof(FeatureFlagsController).GetMethod(name)
           ?? throw new InvalidOperationException($"Method '{name}' not found on FeatureFlagsController.");

    [Fact]
    public void UpsertEntitlement_HasManageFeatureFlagsPolicy()
    {
        var method = GetMethod(nameof(FeatureFlagsController.UpsertEntitlement));
        var attr = method.GetCustomAttributes<AuthorizeAttribute>()
                         .SingleOrDefault(a => a.Policy == ApplicationPolicies.ManageFeatureFlags);
        Assert.NotNull(attr);
    }

    [Fact]
    public void DeleteEntitlement_HasManageFeatureFlagsPolicy()
    {
        var method = GetMethod(nameof(FeatureFlagsController.DeleteEntitlement));
        var attr = method.GetCustomAttributes<AuthorizeAttribute>()
                         .SingleOrDefault(a => a.Policy == ApplicationPolicies.ManageFeatureFlags);
        Assert.NotNull(attr);
    }

    [Fact]
    public void UpsertEntitlement_NoRoleBasedAuthorize()
    {
        var method = GetMethod(nameof(FeatureFlagsController.UpsertEntitlement));
        var roleAttrs = method.GetCustomAttributes<AuthorizeAttribute>()
                              .Where(a => a.Roles != null);
        Assert.Empty(roleAttrs);
    }

    [Fact]
    public void DeleteEntitlement_NoRoleBasedAuthorize()
    {
        var method = GetMethod(nameof(FeatureFlagsController.DeleteEntitlement));
        var roleAttrs = method.GetCustomAttributes<AuthorizeAttribute>()
                              .Where(a => a.Roles != null);
        Assert.Empty(roleAttrs);
    }

    [Fact]
    public void GetFeatures_AllowAnonymousUnchanged()
    {
        var method = GetMethod(nameof(FeatureFlagsController.GetFeatures));
        var attr = method.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void Controller_NoClassLevelRoleBasedAuthorize()
    {
        var roleAttrs = typeof(FeatureFlagsController)
            .GetCustomAttributes<AuthorizeAttribute>()
            .Where(a => a.Roles != null);
        Assert.Empty(roleAttrs);
    }
}
