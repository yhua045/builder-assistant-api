using Microsoft.AspNetCore.Identity;

namespace BuilderAssistantApi.Domain.Entities;

public class User : IdentityUser<long>
{
    // Id, Email, and UserName are provided by IdentityUser<long>
    
    public string? DisplayName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public List<Project> Projects { get; set; } = new();
}
