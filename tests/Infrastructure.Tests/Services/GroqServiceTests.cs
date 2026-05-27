using System.Net;
using System.Text;
using System.Text.Json;
using BuilderAssistantApi.Application.Exceptions;
using BuilderAssistantApi.Infrastructure.Options;
using BuilderAssistantApi.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MsOptions = Microsoft.Extensions.Options.Options;
using Xunit;

namespace BuilderAssistantApi.Infrastructure.Tests.Services;

public sealed class GroqServiceTests
{
    // ── Test helpers ─────────────────────────────────────────────────────────

    private static GroqService CreateService(FakeHttpMessageHandler handler, GroqOptions? options = null)
    {
        var opts = options ?? new GroqOptions { ApiKey = "test-key" };
        // disposeHandler: false — prevents HttpClient.Dispose from killing the shared fake handler.
        var client = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri(opts.BaseUrl)
        };
        var factory = new TestHttpClientFactory(client);
        return new GroqService(factory, MsOptions.Create(opts), NullLogger<GroqService>.Instance);
    }

    private static IFormFile CreateFakeAudio(string content = "fake audio data") =>
        new FakeFormFile(Encoding.UTF8.GetBytes(content), "audio/mpeg", "test.mp3");

    private static IFormFile CreateFakeImage(byte[]? bytes = null, string contentType = "image/jpeg") =>
        new FakeFormFile(bytes ?? new byte[] { 0xFF, 0xD8, 0xFF }, contentType, "test.jpg");

    private static HttpResponseMessage OkJson(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    /// <summary>Wraps an inner JSON string as a Groq chat completion envelope.</summary>
    private static string GroqChatEnvelope(string innerContentJson) =>
        $$"""
        {
          "id": "chatcmpl-test",
          "model": "llama-3.3-70b-versatile",
          "choices": [
            {
              "message": { "role": "assistant", "content": {{JsonSerializer.Serialize(innerContentJson)}} },
              "finish_reason": "stop"
            }
          ]
        }
        """;

    private static string SttEnvelope(string text = "Hello world", string language = "en", double duration = 2.5) =>
        $$"""{"text":"{{text}}","language":"{{language}}","duration":{{duration}}}""";

    // ── STT tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessSttAsync_ValidAudio_ReturnsTranscript()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpMessageHandler((req, _) =>
        {
            captured = req;
            return Task.FromResult(OkJson(SttEnvelope("Transcribed audio text", "en", 3.0)));
        });
        var service = CreateService(handler);

        var result = await service.ProcessSttAsync(CreateFakeAudio(), "audio/mpeg", "test.mp3", null);

        Assert.Equal("Transcribed audio text", result.Transcript);
        Assert.Equal("en", result.Language);
        Assert.Equal(3000, result.DurationMs);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Contains("/openai/v1/audio/transcriptions", captured.RequestUri!.PathAndQuery);
        Assert.IsType<MultipartFormDataContent>(captured.Content);
    }

    [Fact]
    public async Task ProcessSttAsync_CustomModelParam_UsesProvidedModel()
    {
        var handler = new FakeHttpMessageHandler((_, _) =>
            Task.FromResult(OkJson(SttEnvelope("text"))));
        var service = CreateService(handler);

        var result = await service.ProcessSttAsync(CreateFakeAudio(), "audio/mpeg", null, "whisper-custom");

        Assert.Equal("whisper-custom", result.Model);
    }

    [Fact]
    public async Task ProcessSttAsync_GroqReturns401_ThrowsGroqAuthenticationException()
    {
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":\"invalid_api_key\"}")
            });
        var service = CreateService(handler);

        await Assert.ThrowsAsync<GroqAuthenticationException>(
            () => service.ProcessSttAsync(CreateFakeAudio(), "audio/mpeg", null, null));
    }

    [Fact]
    public async Task ProcessSttAsync_GroqReturns500_ThrowsGroqCommunicationException()
    {
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("{\"error\":\"server_error\"}")
            });
        var service = CreateService(handler);

        await Assert.ThrowsAsync<GroqCommunicationException>(
            () => service.ProcessSttAsync(CreateFakeAudio(), "audio/mpeg", null, null));
    }

    [Fact]
    public async Task ProcessSttAsync_GroqReturns400_ThrowsGroqRequestException()
    {
        var handler = new FakeHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"invalid_model\"}")
            });
        var service = CreateService(handler);

        await Assert.ThrowsAsync<GroqRequestException>(
            () => service.ProcessSttAsync(CreateFakeAudio(), "audio/mpeg", null, null));
    }

    [Fact]
    public async Task ProcessSttAsync_Timeout_ThrowsTaskCanceledException()
    {
        var handler = new FakeHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK); // unreachable
        });
        var opts = new GroqOptions { ApiKey = "test-key" };
        var client = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri(opts.BaseUrl),
            Timeout = TimeSpan.FromMilliseconds(100)
        };
        var service = new GroqService(
            new TestHttpClientFactory(client),
            MsOptions.Create(opts),
            NullLogger<GroqService>.Instance);

        await Assert.ThrowsAnyAsync<TaskCanceledException>(
            () => service.ProcessSttAsync(CreateFakeAudio(), "audio/mpeg", null, null));
    }

    // ── Task draft tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task ParseTaskDraftAsync_ValidTranscript_ReturnsStructuredDraft()
    {
        const string innerJson =
            """{"title":"Fix kitchen tap","notes":"Replace kitchen tap tomorrow morning","dueDate":"2026-05-28","priority":"medium","trade":"plumbing","durationEstimate":120}""";

        // Capture the body inside the handler — before the service disposes StringContent.
        string? capturedBody = null;
        var handler = new FakeHttpMessageHandler(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync();
            return OkJson(GroqChatEnvelope(innerJson));
        });
        var service = CreateService(handler);

        var result = await service.ParseTaskDraftAsync("Replace kitchen tap tomorrow morning");

        Assert.Equal("Fix kitchen tap", result.Title);
        Assert.Equal("medium", result.Priority);
        Assert.Equal("plumbing", result.Trade);
        Assert.Equal(120, result.DurationEstimate);
        // Verify transcript reached the request body
        Assert.NotNull(capturedBody);
        Assert.Contains("Replace kitchen tap tomorrow morning", capturedBody);
    }

    [Fact]
    public async Task ParseTaskDraftAsync_GroqReturnsInvalidJson_ReturnsDefault()
    {
        // The LLM content is malformed JSON — service should degrade gracefully.
        var handler = new FakeHttpMessageHandler(
            OkJson(GroqChatEnvelope("INVALID JSON {{{")));
        var service = CreateService(handler);

        var result = await service.ParseTaskDraftAsync("some transcript");

        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Title);
        Assert.Equal(0, result.DurationEstimate);
    }

    // ── Invoice text tests ───────────────────────────────────────────────────

    [Fact]
    public async Task ParseInvoiceTextAsync_ValidOcrText_ReturnsInvoice()
    {
        const string innerJson =
            """{"vendor":"ACME Plumbing","invoiceNumber":"INV-001","invoiceDate":"2026-05-01","dueDate":"2026-05-31","subtotal":100,"tax":10,"total":110,"currency":"AUD","lineItems":[{"description":"Labour","quantity":2,"unitPrice":50,"total":100,"tax":10}],"confidence":{"overall":0.95,"vendor":0.98,"invoiceNumber":0.9,"invoiceDate":0.95,"total":0.99},"suggestedCorrections":[]}""";

        var handler = new FakeHttpMessageHandler(OkJson(GroqChatEnvelope(innerJson)));
        var service = CreateService(handler);

        var result = await service.ParseInvoiceTextAsync("Invoice OCR text content");

        Assert.Equal("ACME Plumbing", result.Vendor);
        Assert.Equal("INV-001", result.InvoiceNumber);
        Assert.Equal(110m, result.Total);
        Assert.Single(result.LineItems);
        Assert.Equal("Labour", result.LineItems[0].Description);
        Assert.Equal(2m, result.LineItems[0].Quantity);
    }

    // ── Invoice image tests ──────────────────────────────────────────────────

    [Fact]
    public async Task ParseInvoiceImageAsync_ValidImage_SendsBase64ImageUrl()
    {
        const string innerJson = """{"vendor":"IMG Corp","total":50,"currency":"AUD"}""";
        // Capture the body inside the handler — before the service disposes StringContent.
        string? capturedBody = null;
        var handler = new FakeHttpMessageHandler(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync();
            return OkJson(GroqChatEnvelope(innerJson));
        });
        var service = CreateService(handler);
        var image = CreateFakeImage(new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg");

        var result = await service.ParseInvoiceImageAsync(image, "image/jpeg");

        Assert.Equal("IMG Corp", result.Vendor);
        Assert.NotNull(capturedBody);
        Assert.Contains("data:image/jpeg;base64,", capturedBody);
    }

    [Fact]
    public async Task ParseInvoiceImageAsync_UnsupportedMimeType_ThrowsGroqRequestException()
    {
        var handler = new FakeHttpMessageHandler(OkJson("{}"));
        var service = CreateService(handler);

        await Assert.ThrowsAsync<GroqRequestException>(
            () => service.ParseInvoiceImageAsync(CreateFakeImage(), "image/bmp"));
    }

    // ── Quotation tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task ParseQuotationTextAsync_ValidOcrText_ReturnsQuotation()
    {
        const string innerJson =
            """{"reference":"QTE-2026-001","vendor":"Build Co","total":5000,"currency":"AUD","lineItems":[],"confidence":{"overall":0.9,"vendor":0.95,"reference":0.88,"date":0.7,"total":0.92},"suggestedCorrections":[]}""";

        var handler = new FakeHttpMessageHandler(OkJson(GroqChatEnvelope(innerJson)));
        var service = CreateService(handler);

        var result = await service.ParseQuotationTextAsync("Quotation OCR text");

        Assert.Equal("Build Co", result.Vendor);
        Assert.Equal("QTE-2026-001", result.Reference);
        Assert.Equal(5000m, result.Total);
    }

    // ── Receipt tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task ParseReceiptTextAsync_ValidOcrText_ReturnsReceipt()
    {
        const string innerJson =
            """{"vendor":"Bunnings","date":"2026-05-27","total":89.99,"subtotal":81.81,"tax":8.18,"currency":"AUD","paymentMethod":"card","receiptNumber":"REC-001","lineItems":[],"confidence":{"overall":0.97,"vendor":0.99,"date":0.95,"total":0.98},"suggestedCorrections":[]}""";

        var handler = new FakeHttpMessageHandler(OkJson(GroqChatEnvelope(innerJson)));
        var service = CreateService(handler);

        var result = await service.ParseReceiptTextAsync("Receipt OCR text");

        Assert.Equal("Bunnings", result.Vendor);
        Assert.Equal(89.99m, result.Total);
        Assert.Equal("card", result.PaymentMethod);
        Assert.Equal("REC-001", result.ReceiptNumber);
    }

    // ── Test doubles ─────────────────────────────────────────────────────────

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public FakeHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
            => _send = send;

        public FakeHttpMessageHandler(HttpResponseMessage fixedResponse)
            : this((_, _) => Task.FromResult(fixedResponse)) { }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => _send(request, cancellationToken);
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public TestHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class FakeFormFile : IFormFile
    {
        private readonly byte[] _data;

        public FakeFormFile(byte[] data, string contentType, string fileName)
        {
            _data = data;
            ContentType = contentType;
            FileName = fileName;
        }

        public string ContentType { get; }
        public string ContentDisposition => $"form-data; name=\"file\"; filename=\"{FileName}\"";
        public IHeaderDictionary Headers => new HeaderDictionary();
        public long Length => _data.Length;
        public string Name => "file";
        public string FileName { get; }

        public void CopyTo(Stream target) => target.Write(_data);

        public async Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
            => await target.WriteAsync(_data, cancellationToken);

        public Stream OpenReadStream() => new MemoryStream(_data);
    }
}
