# Progress on Issue 9

- Phase 1 completed: Implemented a generic document-processing controller for AI-backed API endpoints.
- Implemented TelemetryController for frontend metrics.
- Added necessary unit tests for these controllers.

## Security (Issue 11 - Phase 1)
- Added ASP.NET Core Identity mapping to the `User` domain entity.
- Configured OpenIddict as an OpenID Connect server to issue standard JWT bearer tokens.
- Add `AuthorizationController` endpoint (`/connect/token`) to handle login logic using Identity.
- Registered default fallback authorization policy so API controllers require authentication by default.
- Exempted telemetry endpoints using `[AllowAnonymous]`.

## Identity Seeding (Issue 12)
- Implemented standalone SQL seeding approach (supersedes C# `IIdentitySeeder` design).
- Rationale: keep seeding concern out of application binary; one-time ad-hoc development task.
- Created `scripts/seed-development-identity.sql` — idempotent T-SQL script for roles and users.
- Added `src/Domain/Constants/ApplicationRoles.cs` for role name constants (used by `[Authorize(Roles = ...)]`).
- Created design document `design/issue-12-identity-seeding-plan.md` (Revision 2, May 27, 2026).
- Script creates four roles (`Admin`, `SiteManager`, `ProjectManager`, `Owner`) + one user per role.
- All seed users share password `Dev1234!` (development only); script is safe to run multiple times.
- Updated README and `appsettings.Development.json` with seed documentation.

## Login Flow & Identity UI Integration (Issue 15 - Revised)
- **Direction Shift**: Pivoted from custom API-based authentication to ASP.NET Core Identity UI with Passwordless Login support.
- **OAuth 2.0 Stack**: OpenIddict (OIDC server) + ASP.NET Core Identity UI (passwordless login experience) + PKCE authorization flow.
- **Removed Custom Login Endpoints**:
  - Removed `POST /api/auth/login` (password or passwordless).
  - Removed `POST /api/auth/verify-otp` (OTP validation).
  - Removed `POST /api/auth/logout` (now handled by Identity UI).
- **OpenIddict OIDC Server**:
  - Registered OpenIddict server with ASP.NET Core Identity as backing user store.
  - Configured PKCE (Proof Key for Code Exchange) for native app authorization flows.
  - Token endpoint validates authorization codes and PKCE verifiers, issues JWT access tokens and refresh tokens.
- **ASP.NET Core Identity UI Integration**:
  - Added `Microsoft.AspNetCore.Identity.UI` NuGet package.
  - Registered `AddDefaultUI()`, `AddDefaultTokenProviders()`, and `AddRazorPages()` in DI container.
  - Scaffolded ASP.NET Core Identity UI Razor Pages into `src/Api/Areas/Identity/Pages/Account/`.
  - Configured passwordless login support: Login page initiates OTP (`UserManager.GenerateUserTokenAsync`), VerifyOtp page validates token (`UserManager.VerifyUserTokenAsync`).
- **Authorization Endpoint** (`/connect/authorize`):
  - Returns HTTP 302 Challenge (redirects to Identity UI login) for unauthenticated requests.
  - Captures original request URL (including PKCE query parameters: `client_id`, `response_type`, `redirect_uri`, `code_challenge`, `code_challenge_method`, `state`) as `ReturnUrl`.
  - After successful passwordless authentication in Razor Pages, user is seamlessly redirected back to authorization.
  - OpenIddict issues short-lived authorization code, redirects to `redirect_uri` with `code` and `state`.
- **Token Endpoint** (`POST /connect/token`): OpenIddict validates authorization code and PKCE verifier, issues JWT access token and refresh token.
- **Domain Entities**: `AuthorizationCode` and `RefreshToken` with unique indexes (unchanged from previous implementation).
- **Repositories**: `IAuthorizationCodeRepository` and `IRefreshTokenRepository` (EF Core implementations, unchanged).
- **AuthService**: Simplified to focus on authorization code & PKCE validation, JWT generation; OTP logic delegated to ASP.NET Core Identity.
- **AuthOptions**: Manages `JwtSigningKey` and token expiration settings (unchanged).
- **Database**: Existing `20260528015017_AddAuthorizationCodesAndRefreshTokens` migration intact.
- **Test Coverage**: Updated `AuthControllerTests` to validate new redirect behavior; `AuthServiceTests` focus on core OAuth exchange logic.
- **Design Document**: `design/issue-15-identity-ui-plan.md` specifies the revised architecture, passwordless flow, Challenge-based redirect pattern, and OpenIddict integration strategy.

## Final Status
- ✅ Build: Passed (0 warnings, 0 errors)
- ✅ Tests: All tests passing
- ✅ Issue #15 Implementation: Complete — ASP.NET Core Identity UI with Passwordless Login, PKCE OAuth flow, Challenge-based authorization redirect
- ✅ Code ready for PR submission

