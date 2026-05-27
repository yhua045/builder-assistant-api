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

## Final Status
- ✅ Build: Passed (0 warnings, 0 errors)
- ✅ Tests: 17/17 passed (Api.Tests: 7, Infrastructure.Tests: 10)
- ✅ Code ready for PR submission
