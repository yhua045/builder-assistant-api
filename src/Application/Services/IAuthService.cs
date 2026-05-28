namespace BuilderAssistantApi.Application.Services;

public record LoginRequest(string Email, string? Password);
public record LoginResult(bool Succeeded, bool RequiresOtp, long? AuthenticatedUserId, string? Next, IEnumerable<string> Errors);

public record VerifyOtpRequest(string Email, string Otp);
public record VerifyOtpResult(bool Succeeded, long? AuthenticatedUserId, string? Next, IEnumerable<string> Errors);

public record GenerateAuthCodeRequest(long UserId, string ClientId, string RedirectUri, string CodeChallenge, string CodeChallengeMethod, string? State);
public record GenerateAuthCodeResult(bool Succeeded, string? RedirectUrl, IEnumerable<string> Errors);

public record ExchangeCodeRequest(string Code, string CodeVerifier, string ClientId, string RedirectUri);
public record TokenResponse(string AccessToken, string TokenType, int ExpiresIn, string RefreshToken);

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<VerifyOtpResult> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default);
    Task<GenerateAuthCodeResult> GenerateAuthCodeAsync(GenerateAuthCodeRequest request, CancellationToken cancellationToken = default);
    Task<(TokenResponse? Response, IEnumerable<string> Errors)> ExchangeCodeAsync(ExchangeCodeRequest request, CancellationToken cancellationToken = default);
}
