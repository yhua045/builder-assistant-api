using System.ComponentModel.DataAnnotations;

namespace BuilderAssistantApi.Infrastructure.Options;

public class AuthOptions
{
    public const string SectionName = "Auth";

    [Required]
    public string JwtSigningKey { get; set; } = string.Empty;

    public string JwtIssuer { get; set; } = "builder-assistant-api";
    public string JwtAudience { get; set; } = "builder-assistant-client";
    public int AccessTokenExpiryMinutes { get; set; } = 60;
    public int RefreshTokenExpiryDays { get; set; } = 7;
    public int AuthCodeExpiryMinutes { get; set; } = 5;
}
