namespace BuilderAssistantApi.Application.Exceptions;

/// <summary>Groq API returned HTTP 401 Unauthorized — the API key is missing or invalid.</summary>
public sealed class GroqAuthenticationException : Exception
{
    public GroqAuthenticationException(string message) : base(message) { }
    public GroqAuthenticationException(string message, Exception inner) : base(message, inner) { }
}
