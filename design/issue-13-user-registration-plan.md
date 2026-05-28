# Design Plan — Issue #13: User Registration, Email Verification & 2FA

**Date:** 27 May 2026  
**Author:** Architect (GitHub Copilot)  
**Status:** Draft — Pending approval  
**Labels:** `enhancement`, `security`, `api`

---

## 1. Context & Goals

Issue #13 adds three related security features to the API:

1. **User Registration** — `POST /api/users/register` creates a new account with the `Owner` role.
2. **Email Verification** — Account is created in an unverified state; a confirmation token is sent via email and validated via `POST /api/users/confirm-email`.
3. **Two-Factor Authentication (2FA) via Email OTP** — After primary authentication succeeds, if 2FA is enabled for the account, an OTP is sent to the user's email and must be verified via `POST /api/users/verify-2fa` before a token is issued.

The project already has ASP.NET Core Identity (`User : IdentityUser<long>`), `IdentityRole<long>`, `BuilderAssistantDbContext : IdentityDbContext`, and OpenIddict configured for the password-grant flow. All three features build on these foundations without introducing new identity infrastructure.

---

## 2. Architectural Principles

```
Domain  ←  Application  ←  Infrastructure
                ↑
              Api (HTTP entry point)
```

| Layer | Responsibility for this issue |
|---|---|
| `Domain` | No changes — `User`, `ApplicationRoles` are already correct |
| `Application` | New service interfaces + DTOs (`IUserRegistrationService`, `IEmailSender`) |
| `Infrastructure` | Concrete service implementations (`UserRegistrationService`, `NullEmailSender`) |
| `Api` | New `UsersController`; modifications to `AuthorizationController` for 2FA gate |

---

## 3. API Surface

### 3.1 `POST /api/users/register`

| Property | Value |
|---|---|
| Auth | `[AllowAnonymous]` |
| Content-Type | `application/json` |
| Request | `RegisterRequest { string Email; string Password; }` |
| Success | `201 Created` + `Location: /api/users/{id}` + `RegisterResponse { long Id; string Email; }` |
| Duplicate email | `409 Conflict` + `ProblemDetails` |
| Invalid password / bad email format | `422 Unprocessable Entity` + `ProblemDetails` with Identity error list |

**Flow:**
1. Validate model state (email format, non-empty password).
2. Delegate to `IUserRegistrationService.RegisterAsync(request)`.
3. Service: normalize email → check for duplicate via `UserManager.FindByEmailAsync` → create user with `UserManager.CreateAsync` → assign `Owner` role via `RoleManager` → generate email confirmation token → call `IEmailSender.SendEmailConfirmationAsync`.
4. Return `201` with `RegisterResponse`.

### 3.2 `POST /api/users/confirm-email`

| Property | Value |
|---|---|
| Auth | `[AllowAnonymous]` |
| Content-Type | `application/json` |
| Request | `ConfirmEmailRequest { long UserId; string Token; }` |
| Success | `200 OK` + `{ "confirmed": true }` |
| Invalid token / user not found | `400 Bad Request` + `ProblemDetails` |

**Flow:**
1. Look up user by `UserId` via `UserManager.FindByIdAsync`.
2. Call `UserManager.ConfirmEmailAsync(user, token)`.
3. Return result.

### 3.3 `POST /api/users/verify-2fa`

| Property | Value |
|---|---|
| Auth | `[AllowAnonymous]` |
| Content-Type | `application/json` |
| Request | `Verify2faRequest { long UserId; string Token; }` |
| Success | `200 OK` + OpenIddict token response (same shape as `/connect/token`) |
| Invalid token | `400 Bad Request` + `ProblemDetails` |

**Flow:**
1. Look up user by `UserId`.
2. Verify OTP via `UserManager.VerifyTwoFactorTokenAsync(user, "Email", token)`.
3. On success, build OpenIddict `ClaimsPrincipal` and return `SignIn(principal, ...)`.

### 3.4 Modification to `AuthorizationController` — 2FA Gate

When the `/connect/token` password flow succeeds primary credential check, before issuing the token:

1. Check if `user.TwoFactorEnabled`.
2. If enabled: generate OTP via `UserManager.GenerateTwoFactorTokenAsync(user, "Email")` → call `IEmailSender.SendTwoFactorCodeAsync` → return `202 Accepted` with `{ "requires2fa": true, "userId": id }` instead of issuing token.
3. If not enabled: issue token as today.

> **Note:** No persistent challenge state (e.g., cache/DB) is needed for the MVP. The OTP itself is time-limited and HMAC-verified by Identity's default token provider. The mobile client stores `userId` from the `202` response and submits it with the OTP to `/api/users/verify-2fa`.

---

## 4. DTOs

All DTOs live in `src/Application/` alongside their owning interface.

```csharp
// src/Application/Services/IUserRegistrationService.cs

public record RegisterRequest(string Email, string Password);
public record RegisterResponse(long Id, string Email);
public record ConfirmEmailRequest(long UserId, string Token);
public record Verify2faRequest(long UserId, string Token);

public record RegistrationResult(bool Succeeded, RegisterResponse? User, IEnumerable<string> Errors);
public record ConfirmEmailResult(bool Succeeded, IEnumerable<string> Errors);
public record Verify2faResult(bool Succeeded, long UserId);
```

---

## 5. New Abstractions

### 5.1 `IUserRegistrationService` (Application layer)

```csharp
// src/Application/Services/IUserRegistrationService.cs

public interface IUserRegistrationService
{
    Task<RegistrationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<ConfirmEmailResult> ConfirmEmailAsync(long userId, string token, CancellationToken cancellationToken = default);
    Task<Verify2faResult> VerifyTwoFactorAsync(long userId, string token, CancellationToken cancellationToken = default);
}
```

Accepts `UserManager<User>` and `RoleManager<IdentityRole<long>>` via constructor injection. The interface boundary is what unit tests mock against.

### 5.2 `IEmailSender` (Application layer)

```csharp
// src/Application/Ports/IEmailSender.cs

public interface IEmailSender
{
    Task SendEmailConfirmationAsync(string toEmail, string confirmationToken, CancellationToken cancellationToken = default);
    Task SendTwoFactorCodeAsync(string toEmail, string code, CancellationToken cancellationToken = default);
}
```

Infrastructure provides `NullEmailSender` (no-op, logs token to structured log) for development. A real SMTP/SendGrid adapter can be swapped in later without changing Application or API layers.

> **Security:** The token/OTP is **never** included in API responses. It is written to structured logs (at `Debug` level) only in `NullEmailSender` in development, and delivered only via email in production.

---

## 6. Layer File Additions

```
src/
  Application/
    Services/
      IUserRegistrationService.cs   ← new interface + DTOs + result types
    Ports/
      IEmailSender.cs               ← new email abstraction

  Infrastructure/
    Services/
      UserRegistrationService.cs    ← concrete impl (UserManager + RoleManager + IEmailSender)
      NullEmailSender.cs            ← dev no-op impl (logs token)

  Api/
    Controllers/
      UsersController.cs            ← Register, ConfirmEmail, VerifyTwoFactor actions
    (modify)
      AuthorizationController.cs    ← add 2FA gate to password grant flow
```

**Unchanged:**
- `Domain/` — no changes
- `Infrastructure/DependencyInjection.cs` — register new services
- `Program.cs` — no structural changes

---

## 7. Dependency Injection

In `DependencyInjection.cs`:

```csharp
services.AddScoped<IUserRegistrationService, UserRegistrationService>();
services.AddScoped<IEmailSender, NullEmailSender>();  // replace with real impl via config later
```

---

## 8. Security Considerations

| Concern | Mitigation |
|---|---|
| Plaintext password in logs | `IUserRegistrationService` only receives `RegisterRequest` record; password never reaches log statements |
| Email enumeration via registration | Return generic `422` for all validation errors including duplicate. Or: per issue spec, return `409 Conflict` explicitly — acceptable for internal/controlled API |
| OTP brute-force | ASP.NET Core Identity's default `DataProtectorTokenProvider` is HMAC-signed and short-lived (1 hour default). Lockout on failure count applies via `UserManager` settings |
| Rate limiting | Mark endpoints with `[EnableRateLimiting("registration")]` policy (policy wiring out of scope for this issue — placeholder comment) |
| Email verification bypass | `EmailConfirmed=false` accounts should not be able to obtain tokens. The `AuthorizationController` must check `await _userManager.IsEmailConfirmedAsync(user)` before completing the password grant |

---

## 9. Test Plan (TDD)

### 9.1 Unit Tests — `IUserRegistrationService` (`tests/Infrastructure.Tests/`)

| Test | Scenario |
|---|---|
| `RegisterAsync_NewEmail_CreatesUserAndAssignsOwnerRole` | Happy path: user created, `Owner` role assigned, email sender called |
| `RegisterAsync_DuplicateEmail_ReturnsFailedResult` | `UserManager.FindByEmailAsync` returns existing user → `RegistrationResult.Succeeded = false` |
| `RegisterAsync_WeakPassword_ReturnsIdentityErrors` | `UserManager.CreateAsync` returns failure → errors propagated |
| `RegisterAsync_DoesNotLogPassword` | Assert log output does not contain the raw password string |
| `ConfirmEmailAsync_ValidToken_ReturnsSuccess` | `UserManager.ConfirmEmailAsync` succeeds |
| `ConfirmEmailAsync_InvalidToken_ReturnsFailure` | `UserManager.ConfirmEmailAsync` fails |
| `VerifyTwoFactorAsync_ValidOtp_ReturnsSuccess` | `UserManager.VerifyTwoFactorTokenAsync` returns true |
| `VerifyTwoFactorAsync_InvalidOtp_ReturnsFailure` | `UserManager.VerifyTwoFactorTokenAsync` returns false |

All `UserManager` / `RoleManager` calls are tested via Moq fakes (same pattern as existing `GroqServiceTests`).

### 9.2 Controller Unit Tests — `UsersController` (`tests/Api.Tests/`)

| Test | Scenario |
|---|---|
| `Register_ValidRequest_Returns201` | Service returns success → `201 Created` with `Location` header |
| `Register_DuplicateEmail_Returns409` | Service returns duplicate error → `409 Conflict` |
| `Register_InvalidPayload_Returns422` | Service returns validation errors → `422 Unprocessable Entity` |
| `ConfirmEmail_ValidToken_Returns200` | Service returns success → `200 OK` |
| `ConfirmEmail_InvalidToken_Returns400` | Service returns failure → `400 Bad Request` |
| `VerifyTwoFactor_ValidOtp_Returns200` | Service returns success → token issued |
| `VerifyTwoFactor_InvalidOtp_Returns400` | Service returns failure → `400 Bad Request` |

### 9.3 Integration Tests (`tests/Infrastructure.Tests/`)

| Test | Scenario |
|---|---|
| `Register_FullFlow_UserAndRolePersisted` | Register against in-memory DB, assert `AspNetUsers` row + `Owner` role assignment |
| `ConfirmEmail_AfterRegistration_EmailConfirmedTrue` | Register → confirm → assert `EmailConfirmed = true` |
| `PasswordGrantBlocked_UnconfirmedEmail` | Register (unconfirmed) → attempt `/connect/token` → should fail |

Integration tests use SQLite in-memory (`DataSource=:memory:`) consistent with existing `EfImageRepositoryTests`.

---

## 10. Mobile UI / API Contract Notes

> **No new UI screens are required from the backend.** The following is provided to inform the mobile-ui agent of the API contracts the mobile client will consume.

| Screen / Flow | Endpoint consumed | Expected mobile behaviour |
|---|---|---|
| Registration screen | `POST /api/users/register` | On `201` → show "Check your email" screen. On `409` → show "Email already registered". On `422` → show field-level password errors |
| Email verification screen | `POST /api/users/confirm-email` | User pastes/deep-links token. On `200` → navigate to login. On `400` → show "Invalid or expired link" |
| Login (2FA gate) | `POST /connect/token` | On `202 { requires2fa: true, userId }` → navigate to OTP entry screen |
| OTP entry screen | `POST /api/users/verify-2fa` | On `200` (token) → store token and navigate to home. On `400` → "Incorrect code, try again" |

The `requires2fa` field on the `202` from `/connect/token` is an addition to the existing token endpoint and must be agreed with the mobile team to avoid breaking existing login flows.

---

## 11. Out of Scope for This Issue

- SMTP / SendGrid email provider (wire `NullEmailSender`; replace later)
- 2FA enrollment / disable flow (enable 2FA on account settings)
- Rate limiting policy wiring
- Captcha / anti-bot measures
- Account lockout display on mobile
- Password reset flow

---

## 12. Pending Changes

The current implementation covers registration, email verification, and 2FA after a successful password-based login. One follow-up item is still being considered separately:

- **Option B passwordless login flow** — add a dedicated login endpoint that can trigger OTP delivery without requiring the user to submit a password first. This would be a separate flow from the existing `/connect/token` password grant and the 2FA continuation path.
- **Why it is pending** — this changes the user authentication model from "password first, then OTP" to "email identity first, then OTP", so it should be treated as an explicit product decision before implementation.
- **Current stance** — keep the existing password-based login and 2FA flow intact for Issue #13, and defer passwordless login to a separate approved change if we decide to support it.

---

## 13. Implementation Order (for TDD hand-off)

1. Add `IUserRegistrationService` + `IEmailSender` interfaces and DTOs (Application layer)
2. Write failing unit tests for `UserRegistrationService` (mocked `UserManager`)
3. Implement `UserRegistrationService` and `NullEmailSender` (Infrastructure layer)
4. Write failing unit tests for `UsersController`
5. Implement `UsersController`
6. Modify `AuthorizationController` to add 2FA gate (with tests)
7. Write integration tests
8. Register new services in `DependencyInjection.cs`

---

*Document location:* `design/issue-13-user-registration-plan.md`  
*For reference by:* `mobile-ui` agent, `developer` agent
