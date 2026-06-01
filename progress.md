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

## Permission-based Feature Flags (Issue 17) — Refactored to Role-Based
- **Goal**: Enable mobile-app login to be optional for basic usage. API acts as authoritative source of truth for which features a user may access via their assigned roles.
- **Refactoring Summary**: Pivoted from per-user entitlements to per-role entitlements. Features are now controlled by role membership rather than individual user grants.
- **Architecture**: Clean Architecture with Domain, Application, Infrastructure, and Api layers.
- **Domain Entities**:
  - `Feature`: Immutable feature definition with `Key` (business identifier, e.g., "ocr_scan"), `Description`, `DefaultEnabled` flag, and `CreatedAt` timestamp.
  - `RoleEntitlement`: Maps role names to features with optional expiration (`ExpiresAt`). Unique index on `(RoleName, FeatureKey)` ensures one row per role per feature.
  - Entitlements use soft coupling via `FeatureKey` string (no FK) to allow flexible schema updates.
- **Database Migration**: `20260529_AddFeatureFlags` creates `Features` and `RoleEntitlements` tables with proper constraints and indexes. Migrated from `UserEntitlements` to `RoleEntitlements`.
- **Repository Layer** (`IFeatureRepository`/`EfFeatureRepository`):
  - `ListAllAsync()`: Retrieves all features from database.
  - `GetByKeyAsync(key)`: Retrieves a single feature by key.
  - `ListEntitlementsForRolesAsync(roleNames)`: Returns all non-expired entitlements for given role names (efficient batch query).
  - `UpsertEntitlementAsync(entitlement)`: Creates or updates role entitlements.
  - `DeleteEntitlementAsync(roleName, featureKey)`: Removes entitlements.
- **Feature Flag Service** (`IFeatureFlagService`/`FeatureFlagService` with `IFeatureCacheInvalidator`):
  - `GetEffectiveFlagsAsync(userId?, userRoles?)`: Merges global defaults with per-role entitlements; caches results by sorted role names for 5 minutes using `IMemoryCache`.
  - `IsEnabledAsync(userRoles?, featureKey)`: Returns effective enabled state for a feature via service check.
  - `InvalidateRole(roleName)`: Clears all cached entries containing that role (via internal role→cacheKey index).
  - `InvalidateAll()`: Clears all feature flag cache entries.
  - Supports both authenticated (with roles) and anonymous callers; anonymous receive defaults only.
  - Uses "any-enabled wins" logic: if any role has `Enabled=true` for a feature, the feature is on (unless all roles explicitly disable it).
- **API Layer**:
  - `FeatureFlagsController`:
    - `GET /api/features`: Returns effective flags for caller (auth optional, `[AllowAnonymous]`). Extracts user ID and roles from claims.
    - `POST /api/features/admin/entitlements`: Creates or updates a role entitlement; returns 204 NoContent; invalidates the affected role's cache (`[Authorize(Roles = Admin)]` only).
  - `RequireFeatureAttribute`: Action filter enforcing per-endpoint feature access control. Returns HTTP 403 Forbidden if feature disabled for caller's roles.
- **Dependency Injection**: Registered `IFeatureRepository`, `IFeatureFlagService`, `IFeatureCacheInvalidator`, and `IMemoryCache` in DI container.
- **DTOs**:
  - `FeatureFlagDto`: Returns `UserId` (nullable string), `AsAnonymous` bool, and list of `FeatureItemDto`.
  - `FeatureItemDto`: Contains `Key`, `Enabled`, `EntitlementReason` ("default_on", "default_off", "role:{RoleName}", "role:{RoleName}:disabled"), and `ExpiresAt` timestamp.
  - `UpsertRoleEntitlementRequest`: Request model for granting role entitlements with `RoleName`, `FeatureKey`, `Enabled`, and optional `ExpiresAt`.
- **Test Coverage**:
  - `FeatureFlagsControllerTests` (Api.Tests): 18 tests covering anonymous vs. authenticated access, role extraction, cache invalidation, and authorization.
  - `EfFeatureRepositoryTests` (Infrastructure.Tests): 52 integration tests for all CRUD operations and query logic.
  - `FeatureFlagServiceTests` (Infrastructure.Tests): Unit tests for flag merging, caching by role, expiration handling, and invalidation strategies.
- **Design Document**: `design/plan.md` specifies architectural overview, entity relationships, cache keying by roles, and layered implementation strategy.
- **Validation**:
  - ✅ Build succeeded (0 errors, 0 warnings)
  - ✅ All 81 tests passing (18 Api.Tests, 52 Infrastructure.Tests + 11 others)
  - ✅ Static analysis: No compilation issues after cleanup of duplicate classes
  - ✅ Ready for PR review and merge

