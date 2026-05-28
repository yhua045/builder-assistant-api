using System.Security.Claims;
using BuilderAssistantApi.Api.Controllers;
using BuilderAssistantApi.Application.Services;
using BuilderAssistantApi.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace BuilderAssistantApi.Api.Tests.Controllers;

public sealed class AuthControllerTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DefaultHttpContext CreateHttpContextWithCookieAuth(AuthenticateResult authResult)
    {
        var authServiceMock = new Mock<IAuthenticationService>();
        authServiceMock
            .Setup(s => s.AuthenticateAsync(It.IsAny<HttpContext>(), IdentityConstants.ApplicationScheme))
            .ReturnsAsync(authResult);
        authServiceMock
            .Setup(s => s.ChallengeAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        var sp = new ServiceCollection()
            .AddSingleton(authServiceMock.Object)
            .BuildServiceProvider();

        return new DefaultHttpContext { RequestServices = sp };
    }

    private static AuthController CreateController(
        Mock<IAuthService> authService,
        HttpContext? httpContext = null)
    {
        var controller = new AuthController(authService.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext ?? new DefaultHttpContext()
        };
        return controller;
    }

    [Fact]
    public async Task Authorize_AuthenticatedUser_ValidRequest_ReturnsRedirect()
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "42") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme));
        var ticket = new AuthenticationTicket(principal, IdentityConstants.ApplicationScheme);

        var authServiceMock = new Mock<IAuthService>();
        authServiceMock
            .Setup(s => s.GenerateAuthCodeAsync(It.IsAny<GenerateAuthCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerateAuthCodeResult(true, "https://app.example.com/callback?code=abc123", []));

        var httpContext = CreateHttpContextWithCookieAuth(AuthenticateResult.Success(ticket));
        var controller = CreateController(authServiceMock, httpContext: httpContext);

        var result = await controller.Authorize(
            "my-client", "https://app.example.com/callback",
            "code", "challenge123", "S256", "state-xyz", CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Contains("code=abc123", redirect.Url);
    }

    [Fact]
    public async Task Authorize_NoCookie_ReturnsChallenge()
    {
        var authServiceMock = new Mock<IAuthService>();
        var httpContext = CreateHttpContextWithCookieAuth(AuthenticateResult.NoResult());
        var controller = CreateController(authServiceMock, httpContext: httpContext);

        var result = await controller.Authorize(
            "my-client", "https://app.example.com/callback",
            "code", "challenge123", "S256", null, CancellationToken.None);

        var challenge = Assert.IsType<ChallengeResult>(result);
        Assert.Contains(IdentityConstants.ApplicationScheme, challenge.AuthenticationSchemes);
    }

    [Fact]
    public async Task Authorize_InvalidResponseType_Returns400()
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "42") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme));
        var ticket = new AuthenticationTicket(principal, IdentityConstants.ApplicationScheme);

        var authServiceMock = new Mock<IAuthService>();
        var httpContext = CreateHttpContextWithCookieAuth(AuthenticateResult.Success(ticket));
        var controller = CreateController(authServiceMock, httpContext: httpContext);

        var result = await controller.Authorize(
            "my-client", "https://app.example.com/callback",
            "token", "challenge123", "S256", null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── Token ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Token_ValidCode_Returns200WithTokenResponse()
    {
        var authServiceMock = new Mock<IAuthService>();
        authServiceMock
            .Setup(s => s.ExchangeCodeAsync(It.IsAny<ExchangeCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new TokenResponse("jwt-access-token", "Bearer", 3600, "opaque-refresh-token"), Enumerable.Empty<string>()));

        var controller = CreateController(authServiceMock);

        var result = await controller.Token(
            "authorization_code", "my-client", "authcode123",
            "https://app.example.com/callback", "verifier123", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        var body = ok.Value!.ToString();
        Assert.Contains("jwt-access-token", body);
    }

    [Fact]
    public async Task Token_InvalidCode_Returns400()
    {
        var authServiceMock = new Mock<IAuthService>();
        authServiceMock
            .Setup(s => s.ExchangeCodeAsync(It.IsAny<ExchangeCodeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, new[] { "Invalid or expired authorization code." }));

        var controller = CreateController(authServiceMock);

        var result = await controller.Token(
            "authorization_code", "my-client", "bad-code",
            "https://app.example.com/callback", "verifier123", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Token_InvalidGrantType_Returns400()
    {
        var authServiceMock = new Mock<IAuthService>();
        var controller = CreateController(authServiceMock);

        var result = await controller.Token(
            "password", "my-client", "code123",
            "https://app.example.com/callback", "verifier123", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
