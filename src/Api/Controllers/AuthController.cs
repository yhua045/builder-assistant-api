using System.Security.Claims;
using BuilderAssistantApi.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BuilderAssistantApi.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet("authorize")]
    [AllowAnonymous]
    public async Task<IActionResult> Authorize(
        [FromQuery] string client_id,
        [FromQuery] string redirect_uri,
        [FromQuery] string response_type,
        [FromQuery] string code_challenge,
        [FromQuery] string code_challenge_method,
        [FromQuery] string? state,
        CancellationToken cancellationToken)
    {
        var cookieResult = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!cookieResult.Succeeded)
        {
            var returnUrl = $"{Request.Path}{Request.QueryString}";
            return Challenge(
                new AuthenticationProperties { RedirectUri = returnUrl },
                IdentityConstants.ApplicationScheme);
        }

        if (!string.Equals(response_type, "code", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "unsupported_response_type" });
        }

        var userIdStr = cookieResult.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(userIdStr, out long userId))
        {
            return Unauthorized();
        }

        var request = new GenerateAuthCodeRequest(userId, client_id, redirect_uri, code_challenge, code_challenge_method, state);
        var result = await _authService.GenerateAuthCodeAsync(request, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { error = result.Errors.FirstOrDefault() });
        }

        return Redirect(result.RedirectUrl!);
    }

    [HttpPost("connect/token")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Token(
        [FromForm] string grant_type,
        [FromForm] string client_id,
        [FromForm] string code,
        [FromForm] string redirect_uri,
        [FromForm] string code_verifier,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(grant_type, "authorization_code", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "unsupported_grant_type" });
        }

        var request = new ExchangeCodeRequest(code, code_verifier, client_id, redirect_uri);
        var (response, errors) = await _authService.ExchangeCodeAsync(request, cancellationToken);

        if (response == null)
        {
            return BadRequest(new { error = "invalid_grant", error_description = errors.FirstOrDefault() });
        }

        return Ok(new
        {
            access_token = response.AccessToken,
            token_type = response.TokenType,
            expires_in = response.ExpiresIn,
            refresh_token = response.RefreshToken
        });
    }

}
