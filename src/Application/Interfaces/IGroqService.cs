using Microsoft.AspNetCore.Http;

namespace BuilderAssistantApi.Application.Interfaces;

public interface IGroqService
{
    Task<AiSttResponse> ProcessSttAsync(IFormFile audio, string mimeType, string? filename, string? model, CancellationToken cancellationToken = default);
    Task<TaskDraftResponse> ParseTaskDraftAsync(string transcript, CancellationToken cancellationToken = default);
    Task<InvoiceParseResponse> ParseInvoiceTextAsync(string ocrText, CancellationToken cancellationToken = default);
    Task<InvoiceParseResponse> ParseInvoiceImageAsync(IFormFile image, string mimeType, CancellationToken cancellationToken = default);
    Task<QuotationParseResponse> ParseQuotationTextAsync(string ocrText, CancellationToken cancellationToken = default);
    Task<QuotationParseResponse> ParseQuotationImageAsync(IFormFile image, string mimeType, CancellationToken cancellationToken = default);
    Task<ReceiptParseResponse> ParseReceiptTextAsync(string ocrText, CancellationToken cancellationToken = default);
    Task<ReceiptParseResponse> ParseReceiptImageAsync(IFormFile image, string mimeType, CancellationToken cancellationToken = default);
}

public class AiSttResponse
{
    public string Transcript { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public long DurationMs { get; set; }
}

public class TaskDraftResponse
{
    public string Title { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Trade { get; set; } = string.Empty;
    public int DurationEstimate { get; set; }
}

public class InvoiceParseResponse
{
    public string? Vendor { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? InvoiceDate { get; set; }
    public string? DueDate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = string.Empty;
    public List<InvoiceLineItem> LineItems { get; set; } = new();
    public InvoiceConfidence Confidence { get; set; } = new();
    public List<string> SuggestedCorrections { get; set; } = new();
}

public class InvoiceLineItem
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public decimal Tax { get; set; }
}

public class InvoiceConfidence
{
    public decimal Overall { get; set; }
    public decimal Vendor { get; set; }
    public decimal InvoiceNumber { get; set; }
    public decimal InvoiceDate { get; set; }
    public decimal Total { get; set; }
}

public class QuotationParseResponse
{
    public string? Reference { get; set; }
    public string? Vendor { get; set; }
    public string? VendorEmail { get; set; }
    public string? VendorPhone { get; set; }
    public string? VendorAddress { get; set; }
    public string? TaxId { get; set; }
    public string? Date { get; set; }
    public string? ExpiryDate { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public List<QuotationLineItem> LineItems { get; set; } = new();
    public string? PaymentTerms { get; set; }
    public string? Scope { get; set; }
    public string? Exclusions { get; set; }
    public string? Notes { get; set; }
    public QuotationConfidence Confidence { get; set; } = new();
    public List<string> SuggestedCorrections { get; set; } = new();
}

public class QuotationLineItem
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public decimal Tax { get; set; }
}

public class QuotationConfidence
{
    public decimal Overall { get; set; }
    public decimal Vendor { get; set; }
    public decimal Reference { get; set; }
    public decimal Date { get; set; }
    public decimal Total { get; set; }
}

public class ReceiptParseResponse
{
    public string? Vendor { get; set; }
    public string? Date { get; set; }
    public decimal Total { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
    public string? ReceiptNumber { get; set; }
    public List<ReceiptLineItem> LineItems { get; set; } = new();
    public string? Notes { get; set; }
    public ReceiptConfidence Confidence { get; set; } = new();
    public List<string> SuggestedCorrections { get; set; } = new();
}

public class ReceiptLineItem
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
}

public class ReceiptConfidence
{
    public decimal Overall { get; set; }
    public decimal Vendor { get; set; }
    public decimal Date { get; set; }
    public decimal Total { get; set; }
}
