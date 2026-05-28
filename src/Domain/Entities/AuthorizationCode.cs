namespace BuilderAssistantApi.Domain.Entities;

public class AuthorizationCode
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public long UserId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string CodeChallenge { get; set; } = string.Empty;
    public string CodeChallengeMethod { get; set; } = "S256";
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
}
