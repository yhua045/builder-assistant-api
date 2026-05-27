namespace BuilderAssistantApi.Application.Exceptions;

/// <summary>Network or transport failure when calling the Groq API (e.g. after retries exhausted).</summary>
public sealed class GroqCommunicationException : Exception
{
    public GroqCommunicationException(string message) : base(message) { }
    public GroqCommunicationException(string message, Exception inner) : base(message, inner) { }
}
