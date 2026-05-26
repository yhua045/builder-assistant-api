# Issue #9 — Backend BFF APIs: Phase 1 Design

**Date:** 14 May 2026  
**Author:** Architect (GitHub Copilot)  
**Status:** Implemented — awaiting review

---

## 1. Context & Goals

Issue #9 requires the backend to expose a **Backend-for-Frontend (BFF)** API layer so the React Native mobile client can:

1. Proxy AI requests (speech-to-text, document parsing) to the Groq LLM service without embedding API keys in the mobile app.
2. Forward frontend telemetry (error reports, analytics events) to backend logging infrastructure without coupling the mobile app to any specific monitoring vendor.

Phase 1 establishes the minimal, extensible contracts (interfaces + DTOs), the thin HTTP controller surface, and stub infrastructure implementations that unblock frontend integration and allow full TDD coverage at the controller layer.

---

## 2. Architectural Principles

The project follows **Clean Architecture** with three layers inside `src/`:

```
Domain  ←  Application  ←  Infrastructure
                ↑
              Api (HTTP entry point)
```

| Layer | Responsibility |
|---|---|
| `Domain` | Entities, repository interfaces — no external dependencies |
| `Application` | Service interfaces (`IGroqService`, `ITelemetryService`), DTOs/response models |
| `Infrastructure` | Concrete service implementations, EF Core, HTTP client wiring |
| `Api` | ASP.NET Core controllers, middleware, `Program.cs` composition root |

Phase 1 additions touch **Application** (interfaces + DTOs) and **Infrastructure** (service stubs) only, keeping `Domain` untouched.

---

## 3. API Structure

### 3.1 Document Processing API — `POST /api/*`

All endpoints are grouped under `DocumentProcessingController` (`src/Api/Controllers/DocumentProcessingController.cs`).

| Method | Route | Content-Type | Purpose |
|---|---|---|---|
| `POST` | `/api/audio/stt` | `multipart/form-data` | Speech-to-text: upload audio file + metadata |
| `POST` | `/api/text/task-draft` | `application/json` | Parse a voice transcript into a structured task |
| `POST` | `/api/ocr/invoices/parse-text` | `application/json` | Parse invoice from OCR text |
| `POST` | `/api/ocr/invoices/parse-image` | `multipart/form-data` | Parse invoice from image file |
| `POST` | `/api/ocr/quotations/parse-text` | `application/json` | Parse quotation from OCR text |
| `POST` | `/api/ocr/quotations/parse-image` | `multipart/form-data` | Parse quotation from image file |
| `POST` | `/api/ocr/receipts/parse-text` | `application/json` | Parse receipt from OCR text |
| `POST` | `/api/ocr/receipts/parse-image` | `multipart/form-data` | Parse receipt from image file |

All endpoints accept a `CancellationToken` threaded from the HTTP request, enabling cooperative cancellation.

### 3.2 Telemetry Sink — `POST /api/telemetry/*`

All endpoints are grouped under `TelemetryController` (`src/Api/Controllers/TelemetryController.cs`).

| Method | Route | Content-Type | Purpose |
|---|---|---|---|
| `POST` | `/api/telemetry/errors` | `application/json` | Receive frontend error/crash reports |
| `POST` | `/api/telemetry/analytics/events` | `application/json` | Receive frontend analytics events |

Both return **`202 Accepted`** with `{ "accepted": true }` — the fire-and-forget pattern decouples the frontend from backend processing latency.

---

## 4. DTOs & Response Models

All DTOs live in `src/Application/Interfaces/` co-located with the service interface they belong to. This keeps the Application layer self-contained.

### 4.1 Groq DTOs (`IGroqService.cs`)

#### Request DTOs (defined in `Api` layer — thin wrappers)

| Class | Fields | Used By |
|---|---|---|
| `TaskDraftRequest` | `Transcript: string` | `POST /groq/task-draft` |
| `TextParseRequest` | `OcrText: string` | All `parse-text` endpoints |

#### Response DTOs

**`AiSttResponse`**
```
Transcript: string
Model: string
Language: string
DurationMs: long
```

**`TaskDraftResponse`**
```
Title: string
Notes: string
DueDate: string        // ISO 8601
Priority: string       // low | medium | high
Trade: string          // builder trade category
DurationEstimate: int  // minutes
```

**`InvoiceParseResponse`**
```
Vendor?: string
InvoiceNumber?: string
InvoiceDate?: string
DueDate?: string
Subtotal: decimal
Tax: decimal
Total: decimal
Currency: string
LineItems: InvoiceLineItem[]
Confidence: InvoiceConfidence
SuggestedCorrections: string[]
```

> `InvoiceLineItem`: `{ Description, Quantity, UnitPrice, Total, Tax }`  
> `InvoiceConfidence`: `{ Overall, Vendor, InvoiceNumber, InvoiceDate, Total }` — all `decimal` (0–1 scale)

**`QuotationParseResponse`**
```
Reference?: string
Vendor?: string
VendorEmail?: string
VendorPhone?: string
VendorAddress?: string
TaxId?: string
Date?: string
ExpiryDate?: string
Currency: string
Subtotal: decimal
Tax: decimal
Total: decimal
LineItems: QuotationLineItem[]
PaymentTerms?: string
Scope?: string
Exclusions?: string
Notes?: string
Confidence: QuotationConfidence
SuggestedCorrections: string[]
```

> `QuotationLineItem`: `{ Description, Quantity, Unit?, UnitPrice, Total, Tax }`  
> `QuotationConfidence`: `{ Overall, Vendor, Reference, Date, Total }`

**`ReceiptParseResponse`**
```
Vendor?: string
Date?: string
Total: decimal
Subtotal: decimal
Tax: decimal
Currency: string
PaymentMethod?: string
ReceiptNumber?: string
LineItems: ReceiptLineItem[]
Notes?: string
Confidence: ReceiptConfidence
SuggestedCorrections: string[]
```

> `ReceiptLineItem`: `{ Description, Quantity, UnitPrice, Total }`  
> `ReceiptConfidence`: `{ Overall, Vendor, Date, Total }`

### 4.2 Telemetry DTOs (`ITelemetryService.cs`)

**`ErrorTelemetryRequest`**
```
Source: string                  // e.g. "sentry", "custom"
Message?: string
Error?: ErrorDetail
Level: string                   // error | warning | info
Tags: Dictionary<string,string>
Context: Dictionary<string,string>
UserId?: string
```

> `ErrorDetail`: `{ Name?, Message, Stack? }`

**`AnalyticsEventRequest`**
```
Source: string                  // e.g. "firebase", "custom"
EventName: string
Properties: Dictionary<string,string>
UserId?: string
ScreenName?: string
```

---

## 5. Service Abstractions

### 5.1 `IGroqService` (Application layer)

```csharp
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
```

**Design decisions:**
- Accepts `IFormFile` directly: the Infrastructure implementation is responsible for reading the stream, which avoids double-buffering in the controller.
- `model` is optional for STT — the default model can be configured in the Infrastructure layer without touching the interface.

### 5.2 `ITelemetryService` (Application layer)

```csharp
public interface ITelemetryService
{
    Task<bool> ReportErrorAsync(ErrorTelemetryRequest request, CancellationToken cancellationToken = default);
    Task<bool> ReportAnalyticsEventAsync(AnalyticsEventRequest request, CancellationToken cancellationToken = default);
}
```

**Design decisions:**
- Returns `bool` so callers can detect backend-side failures if needed, even though the HTTP response is always `202`.
- The `Dictionary<string, string>` shapes for `Tags`, `Context`, and `Properties` keep the DTO vendor-neutral — the implementation decides how to forward or transform these fields.

---

## 6. Infrastructure Implementation

### 6.1 Current State (Phase 1 — Stub)

Both `GroqService` and `TelemetryService` (`src/Infrastructure/Services/`) are **in-memory stubs** that log the call and return hard-coded placeholder responses. This approach:

- Unblocks the mobile frontend from integrating against real endpoints immediately.
- Allows controller-layer tests to be green without any external service dependency.
- Establishes the correct wiring: constructor-injected `IHttpClientFactory` and `ILogger<T>` are already present, ready for Phase 2 real implementations.

### 6.2 Dependency Injection (`DependencyInjection.cs`)

Services are registered as **`Scoped`** (per-request lifetime):

```csharp
services.AddScoped<IGroqService, GroqService>();
services.AddScoped<ITelemetryService, TelemetryService>();
```

The `propagatingClient` named `HttpClient` (registered in `Program.cs`) is available for injection via `IHttpClientFactory` and will automatically forward the `X-Correlation-ID` header (see §7 below).

### 6.3 Phase 2 Roadmap (Infrastructure only — interfaces unchanged)

| Service | Phase 2 Implementation |
|---|---|
| `GroqService.ProcessSttAsync` | Multipart upload to `https://api.groq.com/openai/v1/audio/transcriptions` |
| `GroqService.Parse*Async` | Chat completions call with structured JSON output schema |
| `TelemetryService.ReportErrorAsync` | Forward to Sentry / Azure Monitor via HTTP or SDK |
| `TelemetryService.ReportAnalyticsEventAsync` | Forward to Firebase Analytics / App Insights |

Because the interfaces are fixed, Phase 2 involves only replacing the stub implementations — **zero changes to controllers or tests**.

---

## 7. Cross-Cutting: Correlation ID Propagation

All outbound HTTP calls from `GroqService` and `TelemetryService` must use the `propagatingClient` named client:

```csharp
var client = _httpClientFactory.CreateClient("propagatingClient");
```

`CorrelationIdPropagationHandler` copies the `X-Correlation-ID` from the inbound `HttpContext` into every outgoing request automatically. The correlation ID is also added to the Serilog logging scope, so logs across the request chain share the same trace ID.

---

## 8. TDD Coverage

### 8.1 Controller Tests (`tests/Api.Tests/Controllers/`)

Tests use `Moq` to mock the service interfaces, isolating controller logic from infrastructure.

| Test Class | Tests |
|---|---|
| `DocumentProcessingControllerTests` | `ProcessStt_ReturnsOkResult`, `ParseTaskDraft_ReturnsOkResult` |
| `TelemetryControllerTests` | `ReportError_ReturnsAccepted`, `ReportAnalyticsEvent_ReturnsAccepted` |

**Pattern applied:**
1. Mock `IGroqService` / `ITelemetryService`.
2. Set up the mock return value for the specific method under test.
3. Call the controller action directly (no HTTP stack).
4. Assert the correct `IActionResult` type and value.

### 8.2 Coverage Gaps (Phase 2 work)

- Integration tests hitting real endpoints (ASP.NET Core `WebApplicationFactory`).
- Infrastructure-level tests for `GroqService` with mock `HttpMessageHandler`.
- Input validation tests (null/empty bodies, oversized files).
- Error/exception propagation tests (service throws → controller returns 5xx).

---

## 9. UI Alignment

The BFF API surface was designed to match the mobile client's existing service layer expectations:

| Mobile Action | BFF Endpoint | Notes |
|---|---|---|
| Record voice → create task | `POST /api/audio/stt` then `POST /api/text/task-draft` | Two-step: transcribe then parse |
| Photograph invoice | `POST /api/ocr/invoices/parse-image` | Single call; confidence scores allow UI validation prompts |
| Scan invoice text (OCR pre-done) | `POST /api/ocr/invoices/parse-text` | Faster path when device-side OCR runs first |
| Same patterns for quotations/receipts | See §3.1 table | |
| Crash/error reporting | `POST /api/telemetry/errors` | Fire-and-forget; 202 keeps UI unblocked |
| Screen analytics | `POST /api/telemetry/analytics/events` | `ScreenName` field maps directly to RN screen names |

**Key design alignment decisions:**
- `SuggestedCorrections` lists on parse responses allow the mobile UI to surface correction hints to the user when confidence is low.
- `Confidence` sub-objects expose per-field confidence so the UI can highlight low-confidence fields for manual review.
- All monetary values use `decimal` on the backend — the mobile client should treat these as strings during JSON parsing to avoid floating-point rounding issues.
- `DueDate` and `Date` fields are `string` (ISO 8601) rather than typed `DateTimeOffset` — this avoids timezone confusion when the backend and device are in different zones.

---

## 10. File Inventory (Phase 1)

| File | Layer | Purpose |
|---|---|---|
| `src/Application/Interfaces/IGroqService.cs` | Application | Interface + all Groq DTOs |
| `src/Application/Interfaces/ITelemetryService.cs` | Application | Interface + telemetry DTOs |
| `src/Api/Controllers/DocumentProcessingController.cs` | Api | 8 generic AI document-processing endpoints |
| `src/Api/Controllers/TelemetryController.cs` | Api | 2 telemetry sink endpoints |
| `src/Infrastructure/Services/GroqService.cs` | Infrastructure | Stub implementation of `IGroqService` |
| `src/Infrastructure/Services/TelemetryService.cs` | Infrastructure | Stub implementation of `ITelemetryService` |
| `src/Infrastructure/DependencyInjection.cs` | Infrastructure | Scoped DI registration for both services |
| `tests/Api.Tests/Controllers/DocumentProcessingControllerTests.cs` | Tests | Controller unit tests for document processing |
| `tests/Api.Tests/Controllers/TelemetryControllerTests.cs` | Tests | Controller unit tests for telemetry |

---

## 11. Open Questions / Phase 2 Prerequisites

1. **Groq API key management** — should be injected via `IOptions<GroqOptions>` and sourced from Azure Key Vault / environment variable, never hardcoded.
2. **File size limits** — `multipart/form-data` endpoints need `[RequestSizeLimit]` attributes or Kestrel configuration to cap audio/image upload sizes.
3. **Input validation** — consider `FluentValidation` or `DataAnnotations` on request DTOs to return `400` before reaching the service layer.
4. **Rate limiting** — Groq API has per-minute token limits; a sliding-window rate limiter on the BFF routes may be needed.
5. **Telemetry fan-out** — `TelemetryService` may need to forward to multiple backends (Sentry + App Insights); a composite pattern or middleware pipeline would be cleaner than a single service doing both.
