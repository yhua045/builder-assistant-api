using BuilderAssistantApi.Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace BuilderAssistantApi.Api.Authorization;

public sealed class ManageUserRolesHandler
    : AuthorizationHandler<ManageUserRolesRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ManageUserRolesRequirement requirement)
    {
        if (context.User.IsInRole(ApplicationRoles.Admin))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
