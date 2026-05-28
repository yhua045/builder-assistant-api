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

## Login Flow & Internal OAuth (Issue 15)
- Implemented unified OAuth 2.0 authorization code flow with PKCE support.
- Created `AuthController` at `/api/auth/` with the following endpoints:
  - `POST /api/auth/login` — Initiates login (password or passwordless), sets HTTP-only Authentication Cookie.
  - `POST /api/auth/verify-otp` — Validates OTP for passwordless flow.
  - `GET /api/auth/authorize` — Issues authorization code (requires Authentication Cookie + PKCE params).
  - `POST /api/auth/connect/token` — Exchanges authorization code for JWT access token and refresh token (validates PKCE).
  - `POST /api/auth/logout` — Clears Authentication Cookie and invalidates tokens.
- Added domain entities: `AuthorizationCode` and `RefreshToken` with unique indexes for lookups.
- Created repositories: `IAuthorizationCodeRepository` and `IRefreshTokenRepository` (EF Core implementations).
- Implemented `AuthService` (Infrastructure layer) for OTP generation, authorization code validation, PKCE verification, and JWT token generation using `JsonWebTokenHandler`.
- Added `AuthOptions` configuration class to manage `JwtSigningKey` and token expiration settings.
- Database migration: `20260528015017_AddAuthorizationCodesAndRefreshTokens` adds necessary schema.
- Comprehensive test coverage: `AuthControllerTests` (11 tests) + `AuthServiceTests` (8 tests) validating all flows.
- Design document: `design/issue-15-login-flow-plan.md` specifying endpoint contracts and frontend integration.

## Final Status
- ✅ Build: Passed (0 warnings, 0 errors)
- ✅ Tests: 54/54 passed (Api.Tests: 21, Infrastructure.Tests: 33)
- ✅ Code ready for PR submission
