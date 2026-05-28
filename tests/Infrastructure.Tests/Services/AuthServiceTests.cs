using System.Security.Cryptography;
using System.Text;
using BuilderAssistantApi.Application.Services;
using BuilderAssistantApi.Domain.Entities;
using BuilderAssistantApi.Domain.Repositories;
using BuilderAssistantApi.Infrastructure.Options;
using BuilderAssistantApi.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BuilderAssistantApi.Infrastructure.Tests.Services;

public sealed class AuthServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static AuthOptions DefaultOptions => new()
    {
        JwtSigningKey = "test-signing-key-that-is-long-enough-for-hmac256-alg!!",
        JwtIssuer = "test-issuer",
        JwtAudience = "test-audience",
        AccessTokenExpiryMinutes = 60,
        RefreshTokenExpiryDays = 7,
        AuthCodeExpiryMinutes = 5
    };

    private static Mock<UserManager<User>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<User>>();
#pragma warning disable CS8625
        return new Mock<UserManager<User>>(store.Object, null, null, null, null, null, null, null, null);
#pragma warning restore CS8625
    }

    private static AuthService CreateService(
        Mock<UserManager<User>>? userManager = null,
        Mock<IAuthorizationCodeRepository>? authCodeRepo = null,
        Mock<IRefreshTokenRepository>? refreshTokenRepo = null,
        AuthOptions? options = null)
    {
        var um = userManager ?? CreateUserManagerMock();
        var acr = authCodeRepo ?? new Mock<IAuthorizationCodeRepository>();
        var rtr = refreshTokenRepo ?? new Mock<IRefreshTokenRepository>();
        var opts = new OptionsWrapper<AuthOptions>(options ?? DefaultOptions);
        return new AuthService(um.Object, acr.Object, rtr.Object, opts);
    }

    private static string ComputeS256Challenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", string.Empty);
    }

    // ── GenerateAuthCodeAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAuthCodeAsync_ValidRequest_StoresCodeAndReturnsRedirectUrl()
    {
        var acrMock = new Mock<IAuthorizationCodeRepository>();
        acrMock.Setup(r => r.AddAsync(It.IsAny<AuthorizationCode>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        acrMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        var service = CreateService(authCodeRepo: acrMock);

        var request = new GenerateAuthCodeRequest(
            UserId: 10L,
            ClientId: "my-client",
            RedirectUri: "https://app.example.com/callback",
            CodeChallenge: "abc123",
            CodeChallengeMethod: "S256",
            State: "state-xyz");

        var result = await service.GenerateAuthCodeAsync(request);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.RedirectUrl);
        Assert.Contains("https://app.example.com/callback", result.RedirectUrl);
        Assert.Contains("code=", result.RedirectUrl);
        Assert.Contains("state=state-xyz", result.RedirectUrl);
        acrMock.Verify(r => r.AddAsync(It.IsAny<AuthorizationCode>(), It.IsAny<CancellationToken>()), Times.Once);
        acrMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAuthCodeAsync_UnsupportedMethod_ReturnsFailed()
    {
        var service = CreateService();

        var request = new GenerateAuthCodeRequest(
            UserId: 10L,
            ClientId: "my-client",
            RedirectUri: "https://app.example.com/callback",
            CodeChallenge: "abc123",
            CodeChallengeMethod: "plain",
            State: null);

        var result = await service.GenerateAuthCodeAsync(request);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
    }

    // ── ExchangeCodeAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExchangeCodeAsync_CodeNotFound_ReturnsFailed()
    {
        var acrMock = new Mock<IAuthorizationCodeRepository>();
        acrMock.Setup(r => r.FindValidByCodeAsync("bad-code", It.IsAny<CancellationToken>()))
               .ReturnsAsync((AuthorizationCode?)null);

        var service = CreateService(authCodeRepo: acrMock);

        var (response, errors) = await service.ExchangeCodeAsync(
            new ExchangeCodeRequest("bad-code", "verifier", "client", "https://redirect.example.com"));

        Assert.Null(response);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ClientIdMismatch_ReturnsFailed()
    {
        var authCode = new AuthorizationCode
        {
            Code = "valid-code",
            UserId = 1,
            ClientId = "correct-client",
            RedirectUri = "https://redirect.example.com",
            CodeChallenge = "challenge",
            CodeChallengeMethod = "S256",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        var acrMock = new Mock<IAuthorizationCodeRepository>();
        acrMock.Setup(r => r.FindValidByCodeAsync("valid-code", It.IsAny<CancellationToken>()))
               .ReturnsAsync(authCode);

        var service = CreateService(authCodeRepo: acrMock);

        var (response, errors) = await service.ExchangeCodeAsync(
            new ExchangeCodeRequest("valid-code", "verifier", "wrong-client", "https://redirect.example.com"));

        Assert.Null(response);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task ExchangeCodeAsync_RedirectUriMismatch_ReturnsFailed()
    {
        var authCode = new AuthorizationCode
        {
            Code = "valid-code",
            UserId = 1,
            ClientId = "my-client",
            RedirectUri = "https://redirect.example.com",
            CodeChallenge = "challenge",
            CodeChallengeMethod = "S256",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        var acrMock = new Mock<IAuthorizationCodeRepository>();
        acrMock.Setup(r => r.FindValidByCodeAsync("valid-code", It.IsAny<CancellationToken>()))
               .ReturnsAsync(authCode);

        var service = CreateService(authCodeRepo: acrMock);

        var (response, errors) = await service.ExchangeCodeAsync(
            new ExchangeCodeRequest("valid-code", "verifier", "my-client", "https://wrong.example.com"));

        Assert.Null(response);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task ExchangeCodeAsync_InvalidPkce_ReturnsFailed()
    {
        var authCode = new AuthorizationCode
        {
            Code = "valid-code",
            UserId = 1,
            ClientId = "my-client",
            RedirectUri = "https://redirect.example.com",
            CodeChallenge = ComputeS256Challenge("correct-verifier"),
            CodeChallengeMethod = "S256",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        var acrMock = new Mock<IAuthorizationCodeRepository>();
        acrMock.Setup(r => r.FindValidByCodeAsync("valid-code", It.IsAny<CancellationToken>()))
               .ReturnsAsync(authCode);

        var service = CreateService(authCodeRepo: acrMock);

        var (response, errors) = await service.ExchangeCodeAsync(
            new ExchangeCodeRequest("valid-code", "wrong-verifier", "my-client", "https://redirect.example.com"));

        Assert.Null(response);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ValidRequest_ReturnsTokenResponse()
    {
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        var challenge = ComputeS256Challenge(verifier);

        var user = new User { Id = 7, Email = "user@example.com" };

        var authCode = new AuthorizationCode
        {
            Code = "valid-code",
            UserId = 7,
            ClientId = "my-client",
            RedirectUri = "https://redirect.example.com",
            CodeChallenge = challenge,
            CodeChallengeMethod = "S256",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        var acrMock = new Mock<IAuthorizationCodeRepository>();
        acrMock.Setup(r => r.FindValidByCodeAsync("valid-code", It.IsAny<CancellationToken>()))
               .ReturnsAsync(authCode);
        acrMock.Setup(r => r.MarkUsedAsync(authCode, It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        acrMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        var umMock = CreateUserManagerMock();
        umMock.Setup(m => m.FindByIdAsync("7")).ReturnsAsync(user);

        var rtrMock = new Mock<IRefreshTokenRepository>();
        rtrMock.Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);
        rtrMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        var service = CreateService(userManager: umMock, authCodeRepo: acrMock, refreshTokenRepo: rtrMock);

        var (response, errors) = await service.ExchangeCodeAsync(
            new ExchangeCodeRequest("valid-code", verifier, "my-client", "https://redirect.example.com"));

        Assert.NotNull(response);
        Assert.Empty(errors);
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal(3600, response.ExpiresIn);
        Assert.False(string.IsNullOrEmpty(response.AccessToken));
        Assert.False(string.IsNullOrEmpty(response.RefreshToken));

        acrMock.Verify(r => r.MarkUsedAsync(authCode, It.IsAny<CancellationToken>()), Times.Once);
        rtrMock.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ValidPkce_AccessTokenIsJwt()
    {
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        var challenge = ComputeS256Challenge(verifier);
        var user = new User { Id = 9, Email = "jwt@example.com" };

        var authCode = new AuthorizationCode
        {
            Code = "code-jwt-test",
            UserId = 9,
            ClientId = "client",
            RedirectUri = "https://app.test",
            CodeChallenge = challenge,
            CodeChallengeMethod = "S256",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5)
        };

        var acrMock = new Mock<IAuthorizationCodeRepository>();
        acrMock.Setup(r => r.FindValidByCodeAsync("code-jwt-test", It.IsAny<CancellationToken>())).ReturnsAsync(authCode);
        acrMock.Setup(r => r.MarkUsedAsync(It.IsAny<AuthorizationCode>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        acrMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var umMock = CreateUserManagerMock();
        umMock.Setup(m => m.FindByIdAsync("9")).ReturnsAsync(user);

        var rtrMock = new Mock<IRefreshTokenRepository>();
        rtrMock.Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        rtrMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var service = CreateService(userManager: umMock, authCodeRepo: acrMock, refreshTokenRepo: rtrMock);

        var (response, _) = await service.ExchangeCodeAsync(
            new ExchangeCodeRequest("code-jwt-test", verifier, "client", "https://app.test"));

        Assert.NotNull(response);
        // JWT has three base64url-encoded sections separated by dots
        Assert.Equal(3, response!.AccessToken.Split('.').Length);
    }
}
