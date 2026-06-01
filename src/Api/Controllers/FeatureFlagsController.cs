using System.Security.Claims;
using BuilderAssistantApi.Application.Dtos;
using BuilderAssistantApi.Application.Interfaces;
using BuilderAssistantApi.Domain.Constants;
using BuilderAssistantApi.Domain.Entities;
using BuilderAssistantApi.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BuilderAssistantApi.Api.Controllers;

[ApiController]
[Route("api/features")]
public class FeatureFlagsController : ControllerBase
{
    private readonly IFeatureFlagService _featureFlagService;
    private readonly IFeatureCacheInvalidator _cacheInvalidator;
    private readonly IFeatureRepository _featureRepository;

    public FeatureFlagsController(
        IFeatureFlagService featureFlagService,
        IFeatureCacheInvalidator cacheInvalidator,
        IFeatureRepository featureRepository)
    {
        _featureFlagService = featureFlagService;
        _cacheInvalidator = cacheInvalidator;
        _featureRepository = featureRepository;
    }

    /// <summary>
    /// Returns effective feature flags for the caller.
    /// Auth is optional: anonymous callers receive default flags; authenticated callers also receive
    /// any role-based entitlements.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetFeatures(CancellationToken ct)
    {
        var userId = GetUserIdLong();
        var userRoles = GetUserRoles();
        var response = await _featureFlagService.GetEffectiveFlagsAsync(userId, userRoles, ct);
        return Ok(response);
    }

    /// <summary>
    /// Creates or updates a role entitlement for a feature key.
    /// Invalidates the affected role's cached flags.
    /// </summary>
    [HttpPost("admin/entitlements")]
    [Authorize(Roles = ApplicationRoles.Admin)]
    public async Task<IActionResult> UpsertEntitlement(
        [FromBody] UpsertRoleEntitlementRequest request,
        CancellationToken ct)
    {
        var entitlement = new RoleEntitlement
        {
            RoleName   = request.RoleName,
            FeatureKey = request.FeatureKey,
            Enabled    = request.Enabled,
            ExpiresAt  = request.ExpiresAt,
            CreatedAt  = DateTimeOffset.UtcNow
        };

        await _featureRepository.UpsertEntitlementAsync(entitlement, ct);
        _cacheInvalidator.InvalidateRole(request.RoleName);

        return Created(string.Empty, null);
    }

    /// <summary>
    /// Deletes a role entitlement for a feature key.
    /// Invalidates the affected role's cached flags.
    /// </summary>
    [HttpDelete("admin/entitlements/{roleName}/{featureKey}")]
    [Authorize(Roles = ApplicationRoles.Admin)]
    public async Task<IActionResult> DeleteEntitlement(
        string roleName,
        string featureKey,
        CancellationToken ct)
    {
        await _featureRepository.DeleteEntitlementAsync(roleName, featureKey, ct);
        _cacheInvalidator.InvalidateRole(roleName);

        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private long? GetUserIdLong()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(sub, out var id) ? id : null;
    }

    private IReadOnlyList<string> GetUserRoles() =>
        User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
}
