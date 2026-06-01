using System.Security.Claims;
using BuilderAssistantApi.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace BuilderAssistantApi.Api.Filters;

/// <summary>
/// Action filter that enforces feature-flag-based access control using the caller's roles.
/// Returns HTTP 403 when <see cref="IFeatureFlagService.IsEnabledAsync"/> returns false.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireFeatureAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _featureKey;

    public RequireFeatureAttribute(string featureKey)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
            throw new ArgumentException("Feature key must not be empty.", nameof(featureKey));

        _featureKey = featureKey;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var service = context.HttpContext.RequestServices.GetRequiredService<IFeatureFlagService>();
        var userRoles = GetUserRoles(context.HttpContext.User);

        if (!await service.IsEnabledAsync(userRoles, _featureKey, context.HttpContext.RequestAborted))
        {
            context.Result = new ObjectResult(new { error = $"Feature '{_featureKey}' is not enabled for this caller." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }

    private static IReadOnlyList<string> GetUserRoles(ClaimsPrincipal user) =>
        user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
}
