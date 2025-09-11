namespace BuilderAssistantApi.Domain.Entities;

public class Prompt
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project? Project { get; set; }

    // The raw prompt text sent to the LLM
    public string Text { get; set; } = string.Empty;

    // Optional metadata of the prompt
    public string? Model { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Note: Prompt no longer maintains a direct collection of images.
}

