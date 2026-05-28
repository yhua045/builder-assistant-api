namespace BuilderAssistantApi.Application.Services;

public record RegisterRequest(string Email, string Password);
public record RegisterResponse(long Id, string Email);
public record ConfirmEmailRequest(long UserId, string Token);
public record Verify2faRequest(long UserId, string Token);

public record RegistrationResult(bool Succeeded, RegisterResponse? User, IEnumerable<string> Errors);
public record ConfirmEmailResult(bool Succeeded, IEnumerable<string> Errors);
public record Verify2faResult(bool Succeeded, long UserId);

public interface IUserRegistrationService
{
    Task<RegistrationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<ConfirmEmailResult> ConfirmEmailAsync(long userId, string token, CancellationToken cancellationToken = default);
    Task<Verify2faResult> VerifyTwoFactorAsync(long userId, string token, CancellationToken cancellationToken = default);
    Task SendTwoFactorCodeAsync(long userId, CancellationToken cancellationToken = default);
}
