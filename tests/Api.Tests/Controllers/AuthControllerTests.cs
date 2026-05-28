using System.Security.Claims;
using BuilderAssistantApi.Api.Controllers;
using BuilderAssistantApi.Application.Services;
using BuilderAssistantApi.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BuilderAssistantApi.Api.Tests.Controllers;

public sealed class AuthControllerTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Mock<UserManager<User>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<User>>();
#pragma warning disable CS8625
        return new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625
    }

    private static Mock<SignInManager<User>> CreateSignInManagerMock(Mock<UserManager<User>> userManager)
    {
        return new Mock<SignInManager<User>>(
            userManager.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<User>>().Object,
            new Mock<IOptions<IdentityOptions>>().Object,
            new Mock<ILogger<SignInManager<User>>>().Object,
            new Mock<IAuthenticationSchemeProvider>().Object,
            new Mock<IUserConfirmation<User>>().Object);
    }

    private static DefaultHttpContext CreateHttpContextWithCookieAuth(AuthenticateResult authResult)
    {
        var authServiceMock = new Mock<IAuthenticationService>();
        authServiceMock
            .Setup(s => s.AuthenticateAsync(It.IsAny<HttpContext>(), IdentityConstants.ApplicationScheme))
            .ReturnsAsync(authResult);
        authServiceMock
            .Setup(s => s.SignOutAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        var sp = new ServiceCollection()
            .AddSingleton(authServiceMock.Object)
            .BuildServiceProvider();

        return new DefaultHttpContext { RequestServices = sp };
    }

    private static AuthController CreateController(
        Mock<IAuthService> authService,
        Mock<SignInManager<User>>? signInManager = null,
        Mock<UserManager<User>>? userManager = null,
        HttpContext? httpContext = null)
    {
        var um = userManager ?? CreateUserManagerMock();
        var sm = signInManager ?? CreateSignInManagerMock(um);
        var controller = new AuthController(authService.Object, sm.Object, um.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext ?? new DefaultHttpContext()
        };
        return controller;
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_PasswordFlow_ValidCredentials_Returns200WithNextAuthorize()
    {
        var authServiceMock = new Mock<IAuthService>();
        authServiceMock
            .Setup(s => s.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResult(true, false, 42L, "/authorize", []));

        var umMock = CreateUserManagerMock();
        umMock.Setup(m => m.FindByIdAsync("42"))
              .ReturnsAsync(new User { Id = 42, Email = "user@example.com" });

        var smMock = CreateSignInManagerMock(umMock);
        smMock.Setup(m => m.SignInAsync(It.IsAny<User>(), false, null))
              .Returns(Task.CompletedTask);

        var controller = CreateController(authServiceMock, smMock, umMock);

        var result = await controller.Login(new LoginRequest("user@example.com", "Password123!"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        var body = ok.Value!.ToString();
        Assert.Contains("/authorize", body);
    }

    [Fact]
    public async Task Login_PasswordlessFlow_Returns200WithNextVerifyOtp()
    {
        var authServiceMock = new Mock<IAuthService>();
        authServiceMock
            .Setup(s => s.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResult(true, true, null, "/verify-otp", []));

        var controller = CreateController(authServiceMock);

        var result = await controller.Login(new LoginRequest("user@example.com", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        var body = ok.Value!.ToString();
        Assert.Contains("/verify-otp", body);
    }

    [Fact]
    public async Task Login_InvalidCredentials_Returns401()
    {
        var authServiceMock = new Mock<IAuthService>();
        authServiceMock
            .Setup(s => s.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResult(false, false, null, null, ["Invalid credentials."]));

        var controller = CreateController(authServiceMock);

        var result = await controller.Login(new LoginRequest("user@example.com", "wrong"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ── VerifyOtp ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyOtp_ValidOtp_Returns200WithNextAuthorize()
    {
        var authServiceMock = new Mock<IAuthService>();
        authServiceMock
            .Setup(s => s.VerifyOtpAsync(It.IsAny<VerifyOtpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifyOtpResult(true, 42L, "/authorize", []));

        var umMock = CreateUserManagerMock();
        umMock.Setup(m => m.FindByIdAsync("42"))
              .ReturnsAsync(new User { Id = 42, Email = "user@example.com" });

        var smMock = CreateSignInManagerMock(umMock);
        smMock.Setup(m => m.SignInAsync(It.IsAny<User>(), false, null))
              .Returns(Task.CompletedTask);

        var controller = CreateController(authServiceMock, smMock, umMock);

        var result = await controller.VerifyOtp(new VerifyOtpRequest("user@example.com", "123456"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        var body = ok.Value!.ToString();
        Assert.Contains("/authorize", body);
    }

    [Fact]
    public async Task VerifyOtp_InvalidOtp_Returns401()
    {
        var authServiceMock = new Mock<IAuthService>();
        authServiceMock
            .Setup(s => s.VerifyOtpAsync(It.IsAny<VerifyOtpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifyOtpResult(false, null, null, ["Invalid or expired OTP."]));

        var controller = CreateController(authServiceMock);

        var result = await controller.VerifyOtp(new VerifyOtpRequest("user@example.com", "000000"), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ── Authorize ─────────────────────────────────────────────────────────────

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
    public async Task Authorize_NoCookie_Returns401()
    {
        var authServiceMock = new Mock<IAuthService>();
        var httpContext = CreateHttpContextWithCookieAuth(AuthenticateResult.NoResult());
        var controller = CreateController(authServiceMock, httpContext: httpContext);

        var result = await controller.Authorize(
            "my-client", "https://app.example.com/callback",
            "code", "challenge123", "S256", null, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
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

    // ── Logout ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_AuthenticatedUser_Returns200()
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "42") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme));
        var ticket = new AuthenticationTicket(principal, IdentityConstants.ApplicationScheme);

        var authServiceMock = new Mock<IAuthService>();
        var umMock = CreateUserManagerMock();
        var smMock = CreateSignInManagerMock(umMock);
        smMock.Setup(m => m.SignOutAsync()).Returns(Task.CompletedTask);

        var httpContext = CreateHttpContextWithCookieAuth(AuthenticateResult.Success(ticket));
        var controller = CreateController(authServiceMock, smMock, umMock, httpContext);

        var result = await controller.Logout();

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Logout_NoCookie_Returns401()
    {
        var authServiceMock = new Mock<IAuthService>();
        var httpContext = CreateHttpContextWithCookieAuth(AuthenticateResult.NoResult());
        var controller = CreateController(authServiceMock, httpContext: httpContext);

        var result = await controller.Logout();

        Assert.IsType<UnauthorizedResult>(result);
    }
}
