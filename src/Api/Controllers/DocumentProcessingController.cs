using BuilderAssistantApi.Api.Filters;
using BuilderAssistantApi.Application.Interfaces;
using BuilderAssistantApi.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace BuilderAssistantApi.Api.Controllers;

[ApiController]
[Route("api")]
public class DocumentProcessingController : ControllerBase
{
    private readonly IGroqService _groqService;

    public DocumentProcessingController(IGroqService groqService)
    {
        _groqService = groqService;
    }

    [HttpPost("audio/stt")]
    public async Task<IActionResult> ProcessStt([FromForm] IFormFile audio, [FromForm] string mimeType, [FromForm] string? filename, [FromForm] string? model, CancellationToken cancellationToken)
    {
        var result = await _groqService.ProcessSttAsync(audio, mimeType, filename, model, cancellationToken);
        return Ok(result);
    }

    [HttpPost("text/task-draft")]
    public async Task<IActionResult> ParseTaskDraft([FromBody] TaskDraftRequest request, CancellationToken cancellationToken)
    {
        var result = await _groqService.ParseTaskDraftAsync(request.Transcript, cancellationToken);
        return Ok(result);
    }

    [HttpPost("ocr/invoices/parse-text")]
    [RequireFeature(FeatureKeys.OcrScan)]
    public async Task<IActionResult> ParseInvoiceText([FromBody] TextParseRequest request, CancellationToken cancellationToken)
    {
        var result = await _groqService.ParseInvoiceTextAsync(request.OcrText, cancellationToken);
        return Ok(result);
    }

    [HttpPost("ocr/invoices/parse-image")]
    [RequireFeature(FeatureKeys.OcrScan)]
    public async Task<IActionResult> ParseInvoiceImage([FromForm] IFormFile image, [FromForm] string mimeType, CancellationToken cancellationToken)
    {
        var result = await _groqService.ParseInvoiceImageAsync(image, mimeType, cancellationToken);
        return Ok(result);
    }

    [HttpPost("ocr/quotations/parse-text")]
    [RequireFeature(FeatureKeys.OcrScan)]
    public async Task<IActionResult> ParseQuotationText([FromBody] TextParseRequest request, CancellationToken cancellationToken)
    {
        var result = await _groqService.ParseQuotationTextAsync(request.OcrText, cancellationToken);
        return Ok(result);
    }

    [HttpPost("ocr/quotations/parse-image")]
    [RequireFeature(FeatureKeys.OcrScan)]
    public async Task<IActionResult> ParseQuotationImage([FromForm] IFormFile image, [FromForm] string mimeType, CancellationToken cancellationToken)
    {
        var result = await _groqService.ParseQuotationImageAsync(image, mimeType, cancellationToken);
        return Ok(result);
    }

    [HttpPost("ocr/receipts/parse-text")]
    [RequireFeature(FeatureKeys.OcrScan)]
    public async Task<IActionResult> ParseReceiptText([FromBody] TextParseRequest request, CancellationToken cancellationToken)
    {
        var result = await _groqService.ParseReceiptTextAsync(request.OcrText, cancellationToken);
        return Ok(result);
    }

    [HttpPost("ocr/receipts/parse-image")]
    [RequireFeature(FeatureKeys.OcrScan)]
    public async Task<IActionResult> ParseReceiptImage([FromForm] IFormFile image, [FromForm] string mimeType, CancellationToken cancellationToken)
    {
        var result = await _groqService.ParseReceiptImageAsync(image, mimeType, cancellationToken);
        return Ok(result);
    }
}

public class TaskDraftRequest
{
    public string Transcript { get; set; } = string.Empty;
}

public class TextParseRequest
{
    public string OcrText { get; set; } = string.Empty;
}