namespace BuilderAssistantApi.Application.Exceptions;

/// <summary>Groq API returned a 4xx client error (other than 401).</summary>
public sealed class GroqRequestException : Exception
{
    public GroqRequestException(string message) : base(message) { }
    public GroqRequestException(string message, Exception inner) : base(message, inner) { }
}
