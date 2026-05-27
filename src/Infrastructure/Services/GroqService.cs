using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BuilderAssistantApi.Application.Exceptions;
using BuilderAssistantApi.Application.Interfaces;
using BuilderAssistantApi.Infrastructure.Http;
using BuilderAssistantApi.Infrastructure.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuilderAssistantApi.Infrastructure.Services;

public class GroqService : IGroqService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GroqOptions _options;
    private readonly ILogger<GroqService> _logger;

    // Snake_case naming for the Groq REST API envelope; case-insensitive for flexibility.
    private static readonly JsonSerializerOptions s_groqApiOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    // Case-insensitive only for deserialising the inner LLM content JSON.
    private static readonly JsonSerializerOptions s_llmContentOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> s_allowedImageMimeTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/webp", "image/gif"
        };

    public GroqService(
        IHttpClientFactory httpClientFactory,
        IOptions<GroqOptions> options,
        ILogger<GroqService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    // ── Public interface ─────────────────────────────────────────────────────

    public async Task<AiSttResponse> ProcessSttAsync(
        IFormFile audio,
        string mimeType,
        string? filename,
        string? model,
        CancellationToken cancellationToken = default)
    {
        var sttModel = model ?? _options.SttModel;
        _logger.LogInformation("GroqService.ProcessSttAsync start {Model} {MimeType}", sttModel, mimeType);

        using var client = _httpClientFactory.CreateClient("groq");
        using var form = new MultipartFormDataContent();

        var audioContent = new StreamContent(audio.OpenReadStream());
        audioContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        form.Add(audioContent, "file", filename ?? audio.FileName);
        form.Add(new StringContent(sttModel), "model");
        form.Add(new StringContent("verbose_json"), "response_format");

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync("/openai/v1/audio/transcriptions", form, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GroqService.ProcessSttAsync network failure");
            throw new GroqCommunicationException("Network failure calling Groq STT API.", ex);
        }

        await HandleNonSuccessAsync(response, nameof(ProcessSttAsync), cancellationToken);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        GroqTranscriptionResponse? result = null;
        try
        {
            result = JsonSerializer.Deserialize<GroqTranscriptionResponse>(json, s_groqApiOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "GroqService.ProcessSttAsync failed to deserialize transcription response");
        }

        return new AiSttResponse
        {
            Transcript = result?.Text ?? string.Empty,
            Language = result?.Language ?? string.Empty,
            Model = sttModel,
            DurationMs = (long)((result?.Duration ?? 0.0) * 1000)
        };
    }

    public Task<TaskDraftResponse> ParseTaskDraftAsync(
        string transcript,
        CancellationToken cancellationToken = default)
    {
        const string systemPrompt = """
            You are a task extraction assistant for a builder/tradesperson app.
            Extract task information from the voice transcript below.
            Respond with a single JSON object with these fields:
            - title: string (short descriptive task title, max 100 chars)
            - notes: string (full details from transcript)
            - dueDate: string (ISO 8601 date YYYY-MM-DD if mentioned, otherwise empty string)
            - priority: string ("low", "medium", or "high")
            - trade: string (trade type e.g. "plumbing", "electrical", "carpentry", "painting", "roofing", "tiling", "general")
            - durationEstimate: number (estimated minutes to complete, 0 if unknown)
            Output only the JSON object, no markdown or explanation.
            """;

        return SendTextChatCompletionAsync<TaskDraftResponse>(
            systemPrompt, transcript, _options.ChatModel, nameof(ParseTaskDraftAsync), cancellationToken);
    }

    public Task<InvoiceParseResponse> ParseInvoiceTextAsync(
        string ocrText,
        CancellationToken cancellationToken = default)
    {
        const string systemPrompt = """
            You are a document parsing assistant. Extract invoice data from the OCR text below.
            Respond with a single JSON object:
            - vendor: string | null
            - invoiceNumber: string | null
            - invoiceDate: string | null (ISO 8601 YYYY-MM-DD)
            - dueDate: string | null (ISO 8601 YYYY-MM-DD)
            - subtotal: number
            - tax: number
            - total: number
            - currency: string (ISO 4217 code e.g. "AUD", "USD")
            - lineItems: array of { description: string, quantity: number, unitPrice: number, total: number, tax: number }
            - confidence: { overall: number, vendor: number, invoiceNumber: number, invoiceDate: number, total: number } (values 0.0–1.0)
            - suggestedCorrections: string[] (list any fields you are uncertain about)
            Output only the JSON object, no markdown or explanation.
            """;

        return SendTextChatCompletionAsync<InvoiceParseResponse>(
            systemPrompt, ocrText, _options.ChatModel, nameof(ParseInvoiceTextAsync), cancellationToken);
    }

    public async Task<InvoiceParseResponse> ParseInvoiceImageAsync(
        IFormFile image,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        const string systemPrompt = """
            You are a document parsing assistant. Analyse the invoice image and extract all data.
            Respond with a single JSON object:
            - vendor: string | null
            - invoiceNumber: string | null
            - invoiceDate: string | null (ISO 8601 YYYY-MM-DD)
            - dueDate: string | null (ISO 8601 YYYY-MM-DD)
            - subtotal: number
            - tax: number
            - total: number
            - currency: string (ISO 4217 code e.g. "AUD", "USD")
            - lineItems: array of { description: string, quantity: number, unitPrice: number, total: number, tax: number }
            - confidence: { overall: number, vendor: number, invoiceNumber: number, invoiceDate: number, total: number } (values 0.0–1.0)
            - suggestedCorrections: string[] (list any fields you are uncertain about)
            Output only the JSON object, no markdown or explanation.
            """;

        var base64Url = await BuildBase64ImageUrlAsync(image, mimeType);
        return await SendVisionChatCompletionAsync<InvoiceParseResponse>(
            systemPrompt, base64Url, _options.VisionModel, nameof(ParseInvoiceImageAsync), cancellationToken);
    }

    public Task<QuotationParseResponse> ParseQuotationTextAsync(
        string ocrText,
        CancellationToken cancellationToken = default)
    {
        const string systemPrompt = """
            You are a document parsing assistant. Extract quotation data from the OCR text below.
            Respond with a single JSON object:
            - reference: string | null
            - vendor: string | null
            - vendorEmail: string | null
            - vendorPhone: string | null
            - vendorAddress: string | null
            - taxId: string | null
            - date: string | null (ISO 8601 YYYY-MM-DD)
            - expiryDate: string | null (ISO 8601 YYYY-MM-DD)
            - currency: string (ISO 4217 code e.g. "AUD", "USD")
            - subtotal: number
            - tax: number
            - total: number
            - lineItems: array of { description: string, quantity: number, unit: string | null, unitPrice: number, total: number, tax: number }
            - paymentTerms: string | null
            - scope: string | null
            - exclusions: string | null
            - notes: string | null
            - confidence: { overall: number, vendor: number, reference: number, date: number, total: number } (values 0.0–1.0)
            - suggestedCorrections: string[]
            Output only the JSON object, no markdown or explanation.
            """;

        return SendTextChatCompletionAsync<QuotationParseResponse>(
            systemPrompt, ocrText, _options.ChatModel, nameof(ParseQuotationTextAsync), cancellationToken);
    }

    public async Task<QuotationParseResponse> ParseQuotationImageAsync(
        IFormFile image,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        const string systemPrompt = """
            You are a document parsing assistant. Analyse the quotation image and extract all data.
            Respond with a single JSON object:
            - reference: string | null
            - vendor: string | null
            - vendorEmail: string | null
            - vendorPhone: string | null
            - vendorAddress: string | null
            - taxId: string | null
            - date: string | null (ISO 8601 YYYY-MM-DD)
            - expiryDate: string | null (ISO 8601 YYYY-MM-DD)
            - currency: string (ISO 4217 code e.g. "AUD", "USD")
            - subtotal: number
            - tax: number
            - total: number
            - lineItems: array of { description: string, quantity: number, unit: string | null, unitPrice: number, total: number, tax: number }
            - paymentTerms: string | null
            - scope: string | null
            - exclusions: string | null
            - notes: string | null
            - confidence: { overall: number, vendor: number, reference: number, date: number, total: number } (values 0.0–1.0)
            - suggestedCorrections: string[]
            Output only the JSON object, no markdown or explanation.
            """;

        var base64Url = await BuildBase64ImageUrlAsync(image, mimeType);
        return await SendVisionChatCompletionAsync<QuotationParseResponse>(
            systemPrompt, base64Url, _options.VisionModel, nameof(ParseQuotationImageAsync), cancellationToken);
    }

    public Task<ReceiptParseResponse> ParseReceiptTextAsync(
        string ocrText,
        CancellationToken cancellationToken = default)
    {
        const string systemPrompt = """
            You are a document parsing assistant. Extract receipt data from the OCR text below.
            Respond with a single JSON object:
            - vendor: string | null
            - date: string | null (ISO 8601 YYYY-MM-DD)
            - total: number
            - subtotal: number
            - tax: number
            - currency: string (ISO 4217 code e.g. "AUD", "USD")
            - paymentMethod: string | null
            - receiptNumber: string | null
            - lineItems: array of { description: string, quantity: number, unitPrice: number, total: number }
            - notes: string | null
            - confidence: { overall: number, vendor: number, date: number, total: number } (values 0.0–1.0)
            - suggestedCorrections: string[]
            Output only the JSON object, no markdown or explanation.
            """;

        return SendTextChatCompletionAsync<ReceiptParseResponse>(
            systemPrompt, ocrText, _options.ChatModel, nameof(ParseReceiptTextAsync), cancellationToken);
    }

    public async Task<ReceiptParseResponse> ParseReceiptImageAsync(
        IFormFile image,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        const string systemPrompt = """
            You are a document parsing assistant. Analyse the receipt image and extract all data.
            Respond with a single JSON object:
            - vendor: string | null
            - date: string | null (ISO 8601 YYYY-MM-DD)
            - total: number
            - subtotal: number
            - tax: number
            - currency: string (ISO 4217 code e.g. "AUD", "USD")
            - paymentMethod: string | null
            - receiptNumber: string | null
            - lineItems: array of { description: string, quantity: number, unitPrice: number, total: number }
            - notes: string | null
            - confidence: { overall: number, vendor: number, date: number, total: number } (values 0.0–1.0)
            - suggestedCorrections: string[]
            Output only the JSON object, no markdown or explanation.
            """;

        var base64Url = await BuildBase64ImageUrlAsync(image, mimeType);
        return await SendVisionChatCompletionAsync<ReceiptParseResponse>(
            systemPrompt, base64Url, _options.VisionModel, nameof(ParseReceiptImageAsync), cancellationToken);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private Task<T> SendTextChatCompletionAsync<T>(
        string systemPrompt,
        string userMessage,
        string modelName,
        string callerName,
        CancellationToken cancellationToken)
        where T : new()
    {
        _logger.LogInformation("GroqService.{Method} start {Model}", callerName, modelName);

        var requestBody = new
        {
            model = modelName,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            },
            response_format = new { type = "json_object" },
            temperature = 0.1
        };

        return SendChatCompletionCoreAsync<T>(requestBody, callerName, cancellationToken);
    }

    private Task<T> SendVisionChatCompletionAsync<T>(
        string systemPrompt,
        string base64ImageUrl,
        string modelName,
        string callerName,
        CancellationToken cancellationToken)
        where T : new()
    {
        _logger.LogInformation("GroqService.{Method} start {Model}", callerName, modelName);

        var requestBody = new
        {
            model = modelName,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "image_url",
                            image_url = new { url = base64ImageUrl }
                        }
                    }
                }
            },
            response_format = new { type = "json_object" },
            temperature = 0.1
        };

        return SendChatCompletionCoreAsync<T>(requestBody, callerName, cancellationToken);
    }

    private async Task<T> SendChatCompletionCoreAsync<T>(
        object requestBody,
        string callerName,
        CancellationToken cancellationToken)
        where T : new()
    {
        using var client = _httpClientFactory.CreateClient("groq");
        var json = JsonSerializer.Serialize(requestBody, s_groqApiOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync("/openai/v1/chat/completions", content, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "GroqService.{Method} network failure", callerName);
            throw new GroqCommunicationException($"Network failure calling Groq chat API in {callerName}.", ex);
        }

        await HandleNonSuccessAsync(response, callerName, cancellationToken);

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        GroqChatResponse? groqResponse = null;
        try
        {
            groqResponse = JsonSerializer.Deserialize<GroqChatResponse>(responseJson, s_groqApiOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "GroqService.{Method} failed to deserialize Groq response envelope", callerName);
            return new T();
        }

        var choiceContent = groqResponse?.Choices?.FirstOrDefault()?.Message?.Content ?? "{}";
        try
        {
            return JsonSerializer.Deserialize<T>(choiceContent, s_llmContentOptions) ?? new T();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "GroqService.{Method} failed to deserialize LLM content as {Type}", callerName, typeof(T).Name);
            return new T();
        }
    }

    private async Task HandleNonSuccessAsync(
        HttpResponseMessage response,
        string callerName,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning(
            "GroqService.{Method} received {StatusCode}: {Body}",
            callerName, (int)response.StatusCode, body);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new GroqAuthenticationException(
                $"Groq API returned 401 Unauthorized in {callerName}. Check Groq:ApiKey configuration.");
        }

        if ((int)response.StatusCode is >= 400 and < 500)
        {
            throw new GroqRequestException(
                $"Groq API returned {(int)response.StatusCode} in {callerName}: {body}");
        }

        // 5xx — the resilience pipeline retries; if we reach here, retries are exhausted.
        throw new GroqCommunicationException(
            $"Groq API returned {(int)response.StatusCode} in {callerName} after retries exhausted.");
    }

    private static async Task<string> BuildBase64ImageUrlAsync(IFormFile image, string mimeType)
    {
        if (!s_allowedImageMimeTypes.Contains(mimeType))
        {
            throw new GroqRequestException(
                $"Unsupported image MIME type '{mimeType}'. Accepted: image/jpeg, image/png, image/webp, image/gif.");
        }

        using var ms = new MemoryStream();
        await image.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        return $"data:{mimeType};base64,{base64}";
    }
}
