namespace BuilderAssistantApi.Infrastructure.Http;

// Internal DTOs for Groq REST API responses — never exposed to API callers.
// Serialisation uses JsonNamingPolicy.SnakeCaseLower (matching Groq's snake_case API).

internal sealed class GroqChatResponse
{
    public string? Id { get; init; }
    public string? Model { get; init; }
    public List<GroqChoice>? Choices { get; init; }
}

internal sealed class GroqChoice
{
    public GroqResponseMessage? Message { get; init; }
    public string? FinishReason { get; init; }
}

internal sealed class GroqResponseMessage
{
    public string? Role { get; init; }
    public string? Content { get; init; }
}

internal sealed class GroqTranscriptionResponse
{
    public string? Text { get; init; }
    public string? Language { get; init; }
    public double Duration { get; init; }
}
