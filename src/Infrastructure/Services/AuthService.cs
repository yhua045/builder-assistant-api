using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BuilderAssistantApi.Application.Services;
using BuilderAssistantApi.Domain.Entities;
using BuilderAssistantApi.Domain.Repositories;
using BuilderAssistantApi.Infrastructure.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BuilderAssistantApi.Infrastructure.Services;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IUserRegistrationService _userRegistrationService;
    private readonly IAuthorizationCodeRepository _authCodeRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly AuthOptions _authOptions;

    public AuthService(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IUserRegistrationService userRegistrationService,
        IAuthorizationCodeRepository authCodeRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IOptions<AuthOptions> authOptions)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _userRegistrationService = userRegistrationService;
        _authCodeRepository = authCodeRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _authOptions = authOptions.Value;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return new LoginResult(false, false, null, null, ["Invalid email or credentials."]);
        }

        bool hasPassword = await _userManager.HasPasswordAsync(user);
        bool passwordProvided = !string.IsNullOrEmpty(request.Password);

        if (hasPassword && passwordProvided)
        {
            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password!, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                return new LoginResult(false, false, null, null, ["Invalid email or credentials."]);
            }
            return new LoginResult(true, false, user.Id, "/authorize", []);
        }

        // Passwordless / OTP flow
        await _userRegistrationService.SendTwoFactorCodeAsync(user.Id, cancellationToken);
        return new LoginResult(true, true, null, "/verify-otp", []);
    }

    public async Task<VerifyOtpResult> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return new VerifyOtpResult(false, null, null, ["User not found."]);
        }

        var result = await _userRegistrationService.VerifyTwoFactorAsync(user.Id, request.Otp, cancellationToken);
        if (!result.Succeeded)
        {
            return new VerifyOtpResult(false, null, null, ["Invalid or expired OTP."]);
        }

        return new VerifyOtpResult(true, user.Id, "/authorize", []);
    }

    public async Task<GenerateAuthCodeResult> GenerateAuthCodeAsync(GenerateAuthCodeRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.CodeChallengeMethod, "S256", StringComparison.OrdinalIgnoreCase))
        {
            return new GenerateAuthCodeResult(false, null, ["Only S256 code_challenge_method is supported."]);
        }

        var code = GenerateSecureToken();
        var authCode = new AuthorizationCode
        {
            Code = code,
            UserId = request.UserId,
            ClientId = request.ClientId,
            RedirectUri = request.RedirectUri,
            CodeChallenge = request.CodeChallenge,
            CodeChallengeMethod = request.CodeChallengeMethod,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(_authOptions.AuthCodeExpiryMinutes),
            IsUsed = false
        };

        await _authCodeRepository.AddAsync(authCode, cancellationToken);
        await _authCodeRepository.SaveChangesAsync(cancellationToken);

        var redirectUrl = BuildRedirectUrl(request.RedirectUri, code, request.State);
        return new GenerateAuthCodeResult(true, redirectUrl, []);
    }

    public async Task<(TokenResponse? Response, IEnumerable<string> Errors)> ExchangeCodeAsync(ExchangeCodeRequest request, CancellationToken cancellationToken = default)
    {
        var authCode = await _authCodeRepository.FindValidByCodeAsync(request.Code, cancellationToken);
        if (authCode == null)
        {
            return (null, ["Invalid or expired authorization code."]);
        }

        if (!string.Equals(authCode.ClientId, request.ClientId, StringComparison.Ordinal))
        {
            return (null, ["client_id mismatch."]);
        }

        if (!string.Equals(authCode.RedirectUri, request.RedirectUri, StringComparison.Ordinal))
        {
            return (null, ["redirect_uri mismatch."]);
        }

        if (!VerifyPkceS256(request.CodeVerifier, authCode.CodeChallenge))
        {
            return (null, ["PKCE verification failed."]);
        }

        await _authCodeRepository.MarkUsedAsync(authCode, cancellationToken);
        await _authCodeRepository.SaveChangesAsync(cancellationToken);

        var user = await _userManager.FindByIdAsync(authCode.UserId.ToString());
        if (user == null)
        {
            return (null, ["User not found."]);
        }

        var accessToken = GenerateJwt(user);
        var refreshToken = await GenerateAndStoreRefreshTokenAsync(user.Id, cancellationToken);

        return (new TokenResponse(accessToken, "Bearer", _authOptions.AccessTokenExpiryMinutes * 60, refreshToken), []);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private string GenerateJwt(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_authOptions.JwtSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", user.Id.ToString()),
                new Claim("email", user.Email ?? string.Empty),
                new Claim("jti", Guid.NewGuid().ToString()),
            }),
            Expires = DateTime.UtcNow.AddMinutes(_authOptions.AccessTokenExpiryMinutes),
            Issuer = _authOptions.JwtIssuer,
            Audience = _authOptions.JwtAudience,
            SigningCredentials = creds
        };

        return new JsonWebTokenHandler().CreateToken(tokenDescriptor);
    }

    private async Task<string> GenerateAndStoreRefreshTokenAsync(long userId, CancellationToken cancellationToken)
    {
        var tokenValue = GenerateSecureToken();
        var refreshToken = new RefreshToken
        {
            Token = tokenValue,
            UserId = userId,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_authOptions.RefreshTokenExpiryDays),
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return tokenValue;
    }

    private static string GenerateSecureToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", string.Empty);
    }

    private static bool VerifyPkceS256(string codeVerifier, string codeChallenge)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var computed = Convert.ToBase64String(hash)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", string.Empty);
        return string.Equals(computed, codeChallenge, StringComparison.Ordinal);
    }

    private static string BuildRedirectUrl(string redirectUri, string code, string? state)
    {
        var separator = redirectUri.Contains('?') ? "&" : "?";
        var url = $"{redirectUri}{separator}code={Uri.EscapeDataString(code)}";
        if (!string.IsNullOrEmpty(state))
        {
            url += $"&state={Uri.EscapeDataString(state)}";
        }
        return url;
    }
}
