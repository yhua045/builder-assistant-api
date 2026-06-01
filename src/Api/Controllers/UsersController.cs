using System.Security.Claims;
using BuilderAssistantApi.Application.Dtos;
using BuilderAssistantApi.Application.Interfaces;
using BuilderAssistantApi.Application.Services;
using BuilderAssistantApi.Domain.Constants;
using BuilderAssistantApi.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace BuilderAssistantApi.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserRegistrationService _registrationService;
    private readonly UserManager<User> _userManager;
    private readonly IRoleService _roleService;

    public UsersController(
        IUserRegistrationService registrationService,
        UserManager<User> userManager,
        IRoleService roleService)
    {
        _registrationService = registrationService;
        _userManager = userManager;
        _roleService = roleService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _registrationService.RegisterAsync(request, cancellationToken);

        if (!result.Succeeded)
        {
            var errors = result.Errors.ToList();

            if (errors.Count == 1 && errors[0].Contains("already registered", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new ProblemDetails
                {
                    Title = "Conflict",
                    Detail = errors[0],
                    Status = StatusCodes.Status409Conflict
                });
            }

            var pd = new ProblemDetails
            {
                Title = "Validation failed",
                Status = StatusCodes.Status422UnprocessableEntity
            };
            pd.Extensions["errors"] = errors;
            return UnprocessableEntity(pd);
        }

        return CreatedAtAction(
            actionName: nameof(Register),
            routeValues: new { id = result.User!.Id },
            value: result.User);
    }

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        var result = await _registrationService.ConfirmEmailAsync(request.UserId, request.Token, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Email confirmation failed",
                Detail = string.Join("; ", result.Errors),
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new { confirmed = true });
    }

    // NOTE: For this endpoint to issue OpenIddict tokens end-to-end, "api/users/verify-2fa"
    // must be added to OpenIddict's SetTokenEndpointUris in DependencyInjection.cs.
    // With EnableTokenEndpointPassthrough(), OpenIddict will process the SignIn result
    // and return a token response even when the request body is JSON (not form-urlencoded).
    [HttpPost("verify-2fa")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] Verify2faRequest request, CancellationToken cancellationToken)
    {
        var result = await _registrationService.VerifyTwoFactorAsync(request.UserId, request.Token, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Two-factor verification failed",
                Detail = "The provided OTP is invalid or has expired.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var user = await _userManager.FindByIdAsync(result.UserId.ToString());
        if (user == null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "User not found",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var identity = new ClaimsIdentity(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: ClaimsIdentity.DefaultNameClaimType,
            roleType: ClaimsIdentity.DefaultRoleClaimType);

        identity.SetClaim(OpenIddictConstants.Claims.Subject, await _userManager.GetUserIdAsync(user))
                .SetClaim(OpenIddictConstants.Claims.Email, await _userManager.GetEmailAsync(user))
                .SetClaim(OpenIddictConstants.Claims.Name, await _userManager.GetUserNameAsync(user));

        foreach (var role in await _userManager.GetRolesAsync(user))
        {
            identity.AddClaim(new Claim(ClaimsIdentity.DefaultRoleClaimType, role)
                                      .SetDestinations(OpenIddictConstants.Destinations.AccessToken));
        }

        foreach (var claim in identity.Claims)
        {
            claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);
        }

        var principal = new ClaimsPrincipal(identity);
        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // ── Role Management ───────────────────────────────────────────────────────

    [HttpGet("/api/roles")]
    [Authorize]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await _roleService.ListAllRolesAsync(cancellationToken);
        return Ok(new { roles });
    }

    [HttpGet("{userId:long}/roles")]
    [Authorize(Roles = ApplicationRoles.Admin)]
    public async Task<IActionResult> GetUserRoles(long userId, CancellationToken cancellationToken)
    {
        var dto = await _roleService.GetUserRolesAsync(userId, cancellationToken);
        if (dto is null)
            return NotFound(new ProblemDetails
            {
                Title    = "Not Found",
                Detail   = $"User '{userId}' was not found.",
                Status   = StatusCodes.Status404NotFound
            });

        return Ok(dto);
    }

    [HttpPost("{userId:long}/roles")]
    [Authorize(Roles = ApplicationRoles.Admin)]
    public async Task<IActionResult> AssignRole(long userId, [FromBody] AssignRoleRequest request, CancellationToken cancellationToken)
    {
        // Check user existence first so we can distinguish 404 vs 400
        var userDto = await _roleService.GetUserRolesAsync(userId, cancellationToken);
        if (userDto is null)
            return NotFound(new ProblemDetails
            {
                Title  = "Not Found",
                Detail = $"User '{userId}' was not found.",
                Status = StatusCodes.Status404NotFound
            });

        // Check if already assigned
        if (userDto.Roles.Contains(request.RoleName, StringComparer.OrdinalIgnoreCase))
            return Conflict(new ProblemDetails
            {
                Title  = "Conflict",
                Detail = $"User '{userId}' already has role '{request.RoleName}'.",
                Status = StatusCodes.Status409Conflict
            });

        var assigned = await _roleService.AssignRoleAsync(userId, request.RoleName, cancellationToken);
        if (!assigned)
            return BadRequest(new ProblemDetails
            {
                Title  = "Bad Request",
                Detail = $"Role '{request.RoleName}' does not exist.",
                Status = StatusCodes.Status400BadRequest
            });

        return NoContent();
    }

    [HttpDelete("{userId:long}/roles/{roleName}")]
    [Authorize(Roles = ApplicationRoles.Admin)]
    public async Task<IActionResult> RemoveRole(long userId, string roleName, CancellationToken cancellationToken)
    {
        var removed = await _roleService.RemoveRoleAsync(userId, roleName, cancellationToken);
        if (!removed)
            return NotFound(new ProblemDetails
            {
                Title  = "Not Found",
                Detail = $"User '{userId}' was not found or does not have role '{roleName}'.",
                Status = StatusCodes.Status404NotFound
            });

        return NoContent();
    }
}
