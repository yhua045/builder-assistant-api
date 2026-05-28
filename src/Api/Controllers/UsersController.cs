using System.Security.Claims;
using BuilderAssistantApi.Application.Services;
using BuilderAssistantApi.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BuilderAssistantApi.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserRegistrationService _registrationService;
    private readonly UserManager<User> _userManager;

    public UsersController(IUserRegistrationService registrationService, UserManager<User> userManager)
    {
        _registrationService = registrationService;
        _userManager = userManager;
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

    // Users will need to obtain a token via the custom OAuth flow after verification.
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

        return Ok(new { verified = true, userId = user.Id });
    }
}
