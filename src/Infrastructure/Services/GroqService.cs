using BuilderAssistantApi.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuilderAssistantApi.Infrastructure.Services;

public class GroqService : IGroqService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GroqService> _logger;

    public GroqService(IHttpClientFactory httpClientFactory, ILogger<GroqService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<AiSttResponse> ProcessSttAsync(IFormFile audio, string mimeType, string? filename, string? model, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing STT");
        return Task.FromResult(new AiSttResponse
        {
            Transcript = "Transcribed text",
            Language = "en",
            Model = model ?? "Whisper",
            DurationMs = 1500
        });
    }

    public Task<TaskDraftResponse> ParseTaskDraftAsync(string transcript, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parsing task draft");
        return Task.FromResult(new TaskDraftResponse
        {
            Title = "Task",
            Notes = transcript,
            DueDate = DateTime.UtcNow.ToString("O"),
            Priority = "medium",
            Trade = "general",
            DurationEstimate = 60
        });
    }

    public Task<InvoiceParseResponse> ParseInvoiceTextAsync(string ocrText, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parsing invoice text");
        return Task.FromResult(new InvoiceParseResponse { Vendor = "ACME" });
    }

    public Task<InvoiceParseResponse> ParseInvoiceImageAsync(IFormFile image, string mimeType, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parsing invoice image");
        return Task.FromResult(new InvoiceParseResponse { Vendor = "ACME" });
    }

    public Task<QuotationParseResponse> ParseQuotationTextAsync(string ocrText, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parsing quotation text");
        return Task.FromResult(new QuotationParseResponse { Vendor = "Vendor" });
    }

    public Task<QuotationParseResponse> ParseQuotationImageAsync(IFormFile image, string mimeType, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parsing quotation image");
        return Task.FromResult(new QuotationParseResponse { Vendor = "Vendor" });
    }

    public Task<ReceiptParseResponse> ParseReceiptTextAsync(string ocrText, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parsing receipt text");
        return Task.FromResult(new ReceiptParseResponse { Vendor = "Vendor" });
    }

    public Task<ReceiptParseResponse> ParseReceiptImageAsync(IFormFile image, string mimeType, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parsing receipt image");
        return Task.FromResult(new ReceiptParseResponse { Vendor = "Vendor" });
    }
}
