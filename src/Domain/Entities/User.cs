namespace BuilderAssistantApi.Domain.Entities;

public class User
{
    public long Id { get; set; }
    // Unique identifier for the user (email is the business key)
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public List<Project> Projects { get; set; } = new();
}
