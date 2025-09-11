namespace BuilderAssistantApi.Domain.Entities;

public class Project
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long OwnerId { get; set; }
    public User? Owner { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Collections
    public List<Prompt> Prompts { get; set; } = new();
    public List<Image> Images { get; set; } = new();
}
