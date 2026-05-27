# GroqService Implementation Design

**Date:** 27 May 2026  
**Author:** Architect (GitHub Copilot)  
**Status:** Draft — awaiting approval before implementation

---

## 1. Context & Goals

The current `GroqService` in `src/Infrastructure/Services/GroqService.cs` is a stub that returns hardcoded data. This document describes the design for replacing it with a production implementation that:

1. Reads configuration (API key, model names, base URL) from the options system with validation.
2. Calls the real Groq REST API for speech-to-text (Whisper) and structured LLM/vision extraction.
3. Handles errors, timeouts, and transient failures robustly.
4. Propagates the `X-Correlation-ID` header on all outbound requests (per existing CLAUDE.md contract).
5. Is testable without real network calls.

No changes are made to `IGroqService`, its eight method signatures, or the public response DTOs — the BFF API contract with the mobile client is preserved.

---

## 2. Architecture Overview

```
src/Api (Program.cs)
    │  binds & validates GroqOptions
    │  configures named HttpClient "groq"
    ▼
src/Infrastructure/DependencyInjection.cs
    │  registers IOptions<GroqOptions>
    │  registers HttpClient "groq" (base URL, auth, timeout, resilience)
    ▼
src/Infrastructure/Services/GroqService.cs   ← replace stub
    │  IHttpClientFactory.CreateClient("groq")
    │  serialises prompts, deserialises Groq JSON
    ▼
src/Infrastructure/Http/GroqDtos.cs          ← new, internal only
    │  GroqChatRequest / GroqChatResponse
    │  GroqTranscriptionResponse
    ▼
src/Infrastructure/Options/GroqOptions.cs    ← new
```

The `Application` and `Domain` layers are **not modified** — clean-architecture boundaries hold.

---

## 3. Configuration Schema

### 3.1 `GroqOptions` Class

**New file:** `src/Infrastructure/Options/GroqOptions.cs`

```
namespace BuilderAssistantApi.Infrastructure.Options;

public sealed class GroqOptions
{
    public const string SectionName = "Groq";

    [Required]
    public string ApiKey { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = "https://api.groq.com";

    // Model used for speech-to-text (Whisper)
    public string SttModel { get; init; } = "whisper-large-v3";

    // Model used for text-only LLM extraction
    public string ChatModel { get; init; } = "llama-3.3-70b-versatile";

    // Model used for vision (image-based) extraction
    public string VisionModel { get; init; } = "meta-llama/llama-4-scout-17b-16e-instruct";

    // Per-request HTTP timeout in seconds
    public int TimeoutSeconds { get; init; } = 60;
}
```

Validation annotations: `[Required]` on `ApiKey`; remaining properties have safe defaults. Validation is enforced at startup via `ValidateDataAnnotations().ValidateOnStart()` — the app fails fast rather than producing runtime errors.

### 3.2 `appsettings.json` (non-sensitive skeleton)

Add to `src/Api/appsettings.json`:

```json
"Groq": {
  "BaseUrl": "https://api.groq.com",
  "SttModel": "whisper-large-v3",
  "ChatModel": "llama-3.3-70b-versatile",
  "VisionModel": "meta-llama/llama-4-scout-17b-16e-instruct",
  "TimeoutSeconds": 60
}
```

`ApiKey` is **not committed** to any appsettings file. See §7 (Security).

### 3.3 `appsettings.Development.json`

No `ApiKey` value added here — developers supply it via User Secrets or environment variable (see §7).

---

## 4. Groq API Endpoints & Protocols

| Use Case | HTTP Method | Endpoint | Auth | Body |
|---|---|---|---|---|
| Speech-to-text | `POST` | `/openai/v1/audio/transcriptions` | Bearer | `multipart/form-data`: `file`, `model`, `response_format=verbose_json` |
| Text LLM (task draft, invoice/quotation/receipt text) | `POST` | `/openai/v1/chat/completions` | Bearer | JSON; `response_format: { type: "json_object" }` |
| Vision LLM (invoice/quotation/receipt image) | `POST` | `/openai/v1/chat/completions` | Bearer | JSON; image content embedded as base64 `image_url` |

All requests include `Authorization: Bearer {ApiKey}` and `X-Correlation-ID` (via the existing `CorrelationIdPropagationHandler`).

---

## 5. File-by-File Implementation Plan

### 5.1 `src/Infrastructure/Options/GroqOptions.cs` — **New**

Defines `GroqOptions` as described in §3.1.

### 5.2 `src/Infrastructure/Http/GroqDtos.cs` — **New** (internal, never serialised to API responses)

Internal DTOs for talking to Groq REST endpoints:

```
// Chat completions
GroqChatRequest  { model, messages: GroqMessage[], response_format?, temperature? }
GroqMessage      { role: "system"|"user"|"assistant", content: string | GroqContentPart[] }
GroqContentPart  { type: "text"|"image_url", text?, image_url?: GroqImageUrl }
GroqImageUrl     { url: string }   // "data:{mimeType};base64,{base64Data}"
GroqChatResponse { id, model, choices: GroqChoice[] }
GroqChoice       { message: GroqMessage, finish_reason }

// Audio transcriptions
GroqTranscriptionResponse { text, language, duration }
```

Serialisation uses `System.Text.Json` with `JsonNamingPolicy.SnakeCaseLower` (matching Groq's snake_case API). Deserialisation must be tolerant of unknown fields (`JsonUnknownTypeHandling.JsonElement` / `UnknownTypeHandling` is not needed — just ensure `PropertyNameCaseInsensitive = true`).

### 5.3 `src/Infrastructure/Services/GroqService.cs` — **Replace stub**

#### Constructor & Dependencies

```
public GroqService(
    IHttpClientFactory httpClientFactory,
    IOptions<GroqOptions> options,
    ILogger<GroqService> logger)
```

`IOptions<GroqOptions>` (not `IOptionsSnapshot` — `GroqOptions` are stable for the process lifetime).

#### HttpClient Usage

```csharp
using var client = _httpClientFactory.CreateClient("groq");
```

The named client `"groq"` has base address, auth header, and timeout pre-configured (see §5.5). The service does **not** set the auth header per-call — it is set once on the named client's default request headers at registration time.

#### Method Implementation Strategy

| Method | Groq Call | Notes |
|---|---|---|
| `ProcessSttAsync` | `POST /openai/v1/audio/transcriptions` | Stream `IFormFile` into `MultipartFormDataContent`; set `response_format=verbose_json` to get language + duration |
| `ParseTaskDraftAsync` | Chat completions, text model | System prompt describes JSON schema for `TaskDraftResponse`; user message is the transcript |
| `ParseInvoiceTextAsync` | Chat completions, text model | System prompt describes `InvoiceParseResponse` schema; user message is OCR text |
| `ParseInvoiceImageAsync` | Chat completions, vision model | Image uploaded as base64 `image_url` in user message content |
| `ParseQuotationTextAsync` | Chat completions, text model | Same pattern as invoice text |
| `ParseQuotationImageAsync` | Chat completions, vision model | Same pattern as invoice image |
| `ParseReceiptTextAsync` | Chat completions, text model | Same pattern |
| `ParseReceiptImageAsync` | Chat completions, vision model | Same pattern |

**LLM prompt strategy:** Each text-extraction method sends a system prompt that:
- Instructs the model to respond with a single JSON object matching the exact DTO shape.
- Lists every field name, type, and whether it is nullable.
- Instructs the model to set confidence scores (0.0–1.0) per field where applicable.
- Instructs the model to populate `SuggestedCorrections` for fields it is uncertain about.

**JSON parsing:** Use `JsonSerializer.Deserialize<T>` with `PropertyNameCaseInsensitive = true`. If the model returns malformed JSON, log at `Warning` level and return a default (empty) response rather than throwing — callers currently expect a value, not an exception on parse failures.

**Image encoding:** Read `IFormFile` into a `byte[]`, `Convert.ToBase64String(bytes)`, assemble `data:{mimeType};base64,{base64}` URL. Validate `mimeType` is an accepted image type (`image/jpeg`, `image/png`, `image/webp`, `image/gif`) before sending.

#### Error Handling (within GroqService)

| Condition | Behaviour |
|---|---|
| `HttpRequestException` (network failure after retries exhausted) | Re-throw as `GroqCommunicationException` (see below) |
| Non-2xx HTTP status from Groq (400, 401, 429, 500) | Inspect status code; for 429 let resilience pipeline retry; for 401 log Error and throw `GroqAuthenticationException`; for 4xx other log Warning and throw `GroqRequestException`; for 5xx let retry pipeline handle |
| `TaskCanceledException` (timeout) | Re-throw with context message |
| JSON deserialisation failure on Groq response | Log Warning, return default DTO (graceful degradation) |

**Domain exceptions** (new, in `src/Application/Exceptions/`):

```
GroqCommunicationException : Exception   // network failure
GroqAuthenticationException : Exception  // 401 – misconfigured key
GroqRequestException : Exception         // 4xx client error
```

Placing these in `Application` keeps `Infrastructure` exceptions from leaking into the API layer while keeping the `Domain` clean.

#### Logging

All log statements include structured properties:

```csharp
_logger.LogInformation("GroqService.ProcessSttAsync start {Model} {MimeType}", options.SttModel, mimeType);
_logger.LogWarning("GroqService STT non-success {StatusCode} {CorrelationId}", ..., ...);
_logger.LogError(ex, "GroqService chat completions failed {Method}", nameof(ParseInvoiceTextAsync));
```

The correlation ID is already injected into the log scope by `CorrelationIdMiddleware` — no manual addition needed.

### 5.4 `src/Infrastructure/Http/GroqHttpClientFactory.cs` — Not needed

Configuration is done inline in `DependencyInjection.cs` following the existing `ImageStorageClient` pattern.

### 5.5 `src/Infrastructure/DependencyInjection.cs` — **Modify**

Add after the existing repository registrations:

```csharp
// Groq options — validated at startup
services.AddOptions<GroqOptions>()
    .BindConfiguration(GroqOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Groq HttpClient — named client "groq"
services.AddHttpClient("groq", (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<GroqOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);
})
.AddHttpMessageHandler<CorrelationIdPropagationHandler>()
.AddStandardResilienceHandler();   // see §6 Retry
```

Change the existing `IGroqService` registration to **Scoped** (already Scoped — no change needed):

```csharp
// Existing line — no change
services.AddScoped<IGroqService, GroqService>();
```

**Note:** `CorrelationIdPropagationHandler` is registered as `Transient` in `Program.cs`. The Infrastructure `DependencyInjection` method receives an `IServiceCollection` that already has the handler registered, so this works without additional registration.

If the Infrastructure layer is used in test contexts that do not register `CorrelationIdPropagationHandler`, the named client configuration should not add it via `AddHttpMessageHandler` in tests — pass a factory parameter or use a test-specific DI setup.

### 5.6 `src/Infrastructure/Infrastructure.csproj` — **Modify**

Add:

```xml
<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="8.*" />
```

This brings in `AddStandardResilienceHandler()` without pulling in the full Polly library directly.

### 5.7 `src/Api/appsettings.json` — **Modify**

Add the non-sensitive skeleton block (§3.2).

### 5.8 `tests/Infrastructure.Tests/Services/GroqServiceTests.cs` — **New**

See §8 Testing Strategy.

---

## 6. Retry, Timeout & Resilience

`AddStandardResilienceHandler()` from `Microsoft.Extensions.Http.Resilience` applies a standard pipeline:

| Layer | Default Behaviour |
|---|---|
| Rate limiter | Concurrency limiter (1000 concurrent) |
| Total request timeout | 30s (overridden by `client.Timeout`) |
| Retry | Exponential backoff, up to 3 retries on transient HTTP errors (408, 429, 5xx) |
| Circuit breaker | Trips after 10 consecutive failures in a 30s window; half-open after 5s |
| Attempt timeout | 10s per attempt |

The `TimeoutSeconds` option controls the outer `HttpClient.Timeout`; it should be set generously (default 60s) because Whisper STT on long audio files can take 10–20s.

For **429 Too Many Requests**, Groq returns a `Retry-After` header. The standard resilience handler does not automatically honour `Retry-After`. If Groq rate-limiting becomes a concern post-launch, a custom `RetryAfterHandler` can be layered in without changing `IGroqService`.

---

## 7. Security — Secrets Handling

### API Key — Never Committed

The `Groq:ApiKey` value **must never appear in any committed file** (`.gitignore` is not sufficient — the value must simply never be put there). The approved patterns, in priority order:

| Environment | Secret Source |
|---|---|
| Local development | .NET User Secrets: `dotnet user-secrets set "Groq:ApiKey" "<value>" --project src/Api` |
| CI/CD | Environment variable `Groq__ApiKey` injected by the pipeline secret store |
| Production (Azure) | Azure Key Vault reference via Azure App Configuration or environment variable sourced from Key Vault; **not** appsettings |

Because `ValidateOnStart()` is set, a missing or empty `ApiKey` will cause the app to fail immediately at startup with a clear message — not silently produce incorrect results at runtime.

### Other security notes

- The `Authorization` header is set once on `DefaultRequestHeaders` at `HttpClient` configuration time. This means the key lives in memory for the process lifetime — acceptable for a server-side service. The key is **not** echoed in any API response or log line. Ensure `GroqOptions.ApiKey` is never logged (the logger caller code must not reference it; the structured log template must not include it).
- The image bytes POSTed to Groq are user-supplied. Validate `mimeType` against an allowlist before forwarding to avoid sending unexpected content types.
- No SSRF risk: the Groq base URL is controlled by configuration, not by user input.

---

## 8. Testing Strategy (TDD)

All tests are written **before** the implementation (Red → Green → Refactor).

### 8.1 Unit Tests — `tests/Infrastructure.Tests/Services/GroqServiceTests.cs`

**Test double approach:** A custom `FakeHttpMessageHandler : HttpMessageHandler` that captures requests and returns pre-programmed `HttpResponseMessage` objects. This avoids external dependencies (WireMock, RichardSzalay.MockHttp) but is explicit and readable.

Key test cases:

| Test | Asserts |
|---|---|
| `ProcessSttAsync_ValidAudio_ReturnsTranscript` | Correct multipart fields sent; response mapped to `AiSttResponse` |
| `ProcessSttAsync_GroqReturns401_ThrowsGroqAuthenticationException` | 401 → `GroqAuthenticationException` |
| `ProcessSttAsync_GroqReturnsNonSuccess_ThrowsGroqCommunicationException` | 500 (after retries) → `GroqCommunicationException` |
| `ParseTaskDraftAsync_ValidTranscript_ReturnsStructuredDraft` | Prompt contains transcript; JSON response deserialised |
| `ParseTaskDraftAsync_GroqReturnsInvalidJson_ReturnsDefault` | Malformed JSON → returns default `TaskDraftResponse`, no exception |
| `ParseInvoiceTextAsync_ValidOcrText_ReturnsInvoice` | Correct model sent; line items populated |
| `ParseInvoiceImageAsync_ValidImage_SendsBase64ImageUrl` | Request body contains `data:image/jpeg;base64,...` |
| `ParseQuotationTextAsync_ValidOcrText_ReturnsQuotation` | Happy path |
| `ParseReceiptTextAsync_ValidOcrText_ReturnsReceipt` | Happy path |
| `CorrelationId_PropagatedOnAllRequests` | `X-Correlation-ID` header present in captured request |
| `Timeout_ExceededOnStt_ThrowsTaskCanceledException` | Short timeout triggers cancellation |

### 8.2 Integration Tests — `tests/Infrastructure.Tests/`

Not in-scope for the first TDD cycle. When added, they should use WireMock.Net to stub the Groq API and verify the full DI pipeline (options binding, named client, handler chain).

### 8.3 Controller Tests (existing) — No change

`DocumentProcessingControllerTests` already mocks `IGroqService` via Moq — these tests continue to pass unchanged because `IGroqService` and its DTOs are not modified.

### 8.4 New Packages Required for Tests

```xml
<!-- tests/Infrastructure.Tests/Infrastructure.Tests.csproj -->
<PackageReference Include="Microsoft.Extensions.Http" Version="8.*" />
<PackageReference Include="Microsoft.Extensions.Options" Version="8.*" />
```

No additional mocking library — the `FakeHttpMessageHandler` is hand-rolled (20–30 lines).

---

## 9. Backwards-Compatibility / API-Contract Impact

| Surface | Change | Impact |
|---|---|---|
| `IGroqService` interface | **None** | Controller tests unaffected |
| Response DTOs (in `Application/Interfaces/IGroqService.cs`) | **None** | Mobile client API contract preserved |
| `DocumentProcessingController` routes | **None** | No URL or HTTP method changes |
| `appsettings.json` | Adds `"Groq"` section | Purely additive; existing deployments without `Groq:ApiKey` will fail fast at startup — this is intentional and correct (was already broken: stub returned fake data) |
| `IHttpClientFactory` usage | Named client `"groq"` added | No conflict with existing `"propagatingClient"` or `ImageStorageClient` |

The only **breaking change** is the startup-time validation failure if `Groq:ApiKey` is not configured. This is a deliberate improvement over silent stub behaviour.

---

## 10. Mobile-UI Agent Coordination

Consultation with the `mobile-ui` agent was requested. Since `GroqService` is a backend infrastructure component with no UI surface, the coordination concern narrows to **API contract stability** — the shapes of the BFF response DTOs that the React Native mobile app receives via `DocumentProcessingController`.

**Result:** The `mobile-ui` agent reviewed the DTO structures (`AiSttResponse`, `TaskDraftResponse`, `InvoiceParseResponse`, `QuotationParseResponse`, `ReceiptParseResponse`) and confirmed no changes to field names, nullability, or types are proposed. The mobile UI components that consume these endpoints will continue to work without modification.

One forward-looking note flagged by `mobile-ui`: the `DurationMs` field on `AiSttResponse` will now be populated from the real Groq response (currently always `1500`). The mobile UI already renders this field optionally — no code change needed.

---

## 11. Rollout & Verification Checklist

- [ ] **Secret configured:** `Groq:ApiKey` set in User Secrets (dev) or env var (CI/prod) before running.
- [ ] **Build passes:** `dotnet build` with no errors after adding `Microsoft.Extensions.Http.Resilience` package.
- [ ] **Unit tests pass:** `dotnet test tests/Infrastructure.Tests` — all new `GroqServiceTests` green.
- [ ] **Existing tests unaffected:** `dotnet test tests/Api.Tests` — `DocumentProcessingControllerTests` still pass.
- [ ] **Startup validation:** Launch without `Groq:ApiKey` → app exits with `OptionsValidationException` mentioning `Groq.ApiKey`.
- [ ] **STT smoke test:** `POST /api/audio/stt` with a real audio file → non-empty `transcript` returned from Groq.
- [ ] **Chat smoke test:** `POST /api/text/task-draft` with `{ "transcript": "Replace kitchen tap tomorrow morning" }` → structured `TaskDraftResponse` with plausible fields.
- [ ] **Vision smoke test:** `POST /api/ocr/invoices/parse-image` with a JPEG photo of an invoice → `InvoiceParseResponse.Vendor` populated.
- [ ] **Correlation ID:** Check logs to confirm `X-Correlation-ID` appears on outbound Groq requests.
- [ ] **Rate limit behaviour:** Simulate 429 response → app retries and eventually returns an error (does not crash).
- [ ] **No key in logs:** Grep application logs for the API key value — must not appear.

---

## 12. Open Questions (for approval)

1. **Vision model availability:** `meta-llama/llama-4-scout-17b-16e-instruct` is listed on Groq's supported vision models page as of May 2026. Confirm this is available on the target Groq account tier before implementation.
2. **`response_format: json_object`** requires the system prompt to explicitly instruct JSON output. The exact prompt wording (particularly field descriptions for invoice parsing) should be validated against real-world documents during the smoke test phase.
3. **File size limits:** Groq STT accepts up to 25 MB. The current `ProcessSttAsync` signature does not validate file size. A follow-up issue should add a 25 MB guard, but it is out of scope for this design.
4. **`CorrelationIdPropagationHandler` in Infrastructure tests:** The handler reads from `IHttpContextAccessor`. Tests that exercise `GroqService` in isolation will not have an active `HttpContext`. Options: (a) skip the handler in unit tests by building the `HttpClient` directly with `FakeHttpMessageHandler`; (b) register a no-op `IHttpContextAccessor` stub. Option (a) is recommended for unit tests.

---

## 13. Summary of Files Changed

| File | Action |
|---|---|
| `src/Infrastructure/Options/GroqOptions.cs` | **Create** |
| `src/Infrastructure/Http/GroqDtos.cs` | **Create** |
| `src/Infrastructure/Services/GroqService.cs` | **Replace** (stub → real) |
| `src/Infrastructure/DependencyInjection.cs` | **Modify** (options + named client) |
| `src/Infrastructure/Infrastructure.csproj` | **Modify** (add resilience package) |
| `src/Application/Exceptions/GroqCommunicationException.cs` | **Create** |
| `src/Application/Exceptions/GroqAuthenticationException.cs` | **Create** |
| `src/Application/Exceptions/GroqRequestException.cs` | **Create** |
| `src/Api/appsettings.json` | **Modify** (add `"Groq"` skeleton) |
| `tests/Infrastructure.Tests/Services/GroqServiceTests.cs` | **Create** |
| `tests/Infrastructure.Tests/Infrastructure.Tests.csproj` | **Modify** (test helpers) |

Files **not changed:** `IGroqService.cs`, all response DTOs, `DocumentProcessingController.cs`, controller tests, Domain entities, migrations.

---

*Design ready for build.*

**LGTB**
