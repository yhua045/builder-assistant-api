namespace BuilderAssistantApi.Domain.Entities;

public class Image
{
    public long Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Optional category for the uploaded image. If not specified, defaults to Other.
    public ImageCategory Category { get; set; } = ImageCategory.Other;

    // Associations
    // An image may belong to a project.
    public long? ProjectId { get; set; }
    public Project? Project { get; set; }
}
