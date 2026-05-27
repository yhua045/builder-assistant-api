using System.ComponentModel.DataAnnotations;

namespace BuilderAssistantApi.Infrastructure.Options;

public sealed class GroqOptions
{
    public const string SectionName = "Groq";

    [Required]
    public string ApiKey { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = "https://api.groq.com";

    /// <summary>Model used for speech-to-text (Whisper).</summary>
    public string SttModel { get; init; } = "whisper-large-v3";

    /// <summary>Model used for text-only LLM extraction.</summary>
    public string ChatModel { get; init; } = "llama-3.3-70b-versatile";

    /// <summary>Model used for vision (image-based) extraction.</summary>
    public string VisionModel { get; init; } = "meta-llama/llama-4-scout-17b-16e-instruct";

    /// <summary>Per-request HTTP timeout in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 60;
}
