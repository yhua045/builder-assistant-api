using BuilderAssistantApi.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BuilderAssistantApi.Api.Controllers;

[ApiController]
[Route("api/ai/groq")]
public class GroqController : ControllerBase
{
    private readonly IGroqService _groqService;

    public GroqController(IGroqService groqService)
    {
        _groqService = groqService;
    }

    [HttpPost("stt")]
    public async Task<IActionResult> ProcessStt([FromForm] IFormFile audio, [FromForm] string mimeType, [FromForm] string? filename, [FromForm] string? model, CancellationToken cancellationToken)
    {
        var result = await _groqService.ProcessSttAsync(audio, mimeType, filename, model, cancellationToken);
        return Ok(result);
    }

    [HttpPost("task-draft")]
    public async Task<IActionResult> ParseTaskDraft([FromBody] TaskDraftRequest request, CancellationToken cancellationToken)
    {
        var result = await _groqService.ParseTaskDraftAsync(request.Transcript, cancellationToken);
        return Ok(result);
    }

    [HttpPost("invoices/parse-text")]
    public async Task<IActionResult> ParseInvoiceText([FromBody] TextParseRequest request, CancellationToken cancellationToken)
    {
        var result = await _groqService.ParseInvoiceTextAsync(request.OcrText, cancellationToken);
        return Ok(result);
    }

    [HttpPost("invoices/parse-image")]
    public async Task<IActionResult> ParseInvoiceImage([FromForm] IFormFile image, [FromForm] string mimeType, CancellationToken cancellationToken)
    {
        var result = await _groqService.ParseInvoiceImageAsync(image, mimeType, cancellationToken);
        return Ok(result);
    }

    [HttpPost("quotations/parse-text")]
    public async Task<IActionResult> ParseQuotationText([FromBody] TextParseRequest request, CancellationToken cancellationToken)
    {
        var result = await _groqService.ParseQuotationTextAsync(request.OcrText, cancellationToken);
        return Ok(result);
    }

    [HttpPost("quotations/parse-image")]
    public async Task<IActionResult> ParseQuotationImage([FromForm] IFormFile image, [FromForm] string mimeType, CancellationToken cancellationToken)
    {
        var result = await _groqService.ParseQuotationImageAsync(image, mimeType, cancellationToken);
        return Ok(result);
    }

    [HttpPost("receipts/parse-text")]
    public async Task<IActionResult> ParseReceiptText([FromBody] TextParseRequest request, CancellationToken cancellationToken)
    {
        var result = await _groqService.ParseReceiptTextAsync(request.OcrText, cancellationToken);
        return Ok(result);
    }

    [HttpPost("receipts/parse-image")]
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
