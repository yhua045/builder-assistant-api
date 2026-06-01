using BuilderAssistantApi.Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace BuilderAssistantApi.Api.Authorization;

public sealed class ManageFeatureFlagsHandler
    : AuthorizationHandler<ManageFeatureFlagsRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ManageFeatureFlagsRequirement requirement)
    {
        if (context.User.IsInRole(ApplicationRoles.Admin))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
