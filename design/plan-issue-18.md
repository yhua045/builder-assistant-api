# Design Plan — User Role Management (Issue #18)

**Date:** 2026-06-01  
**Issue:** https://github.com/yhua045/builder-assistant-api/issues/18  
**Mode:** Architect → TDD handoff  
**Related:** plan.md (Issue #17 — Permission-based Feature Flags)

---

## 1. Goal

Fully enable ASP.NET Core Identity role support so that:

- Default roles (Admin, SiteManager, ProjectManager, Owner) are seeded automatically at startup.
- Role claims are included in issued JWT/OpenIddict access tokens.
- Admin users can query, assign, and remove roles for any user via protected REST endpoints.
- The mobile app receives a clean, flat API to display user roles and drive admin UX.

---

## 2. Audit — Current State

| Concern | Status | Notes |
|---|---|---|
| `IdentityDbContext<User, IdentityRole<long>, long>` | ✅ Done | AspNetRoles/AspNetUserRoles tables exist |
| `AddIdentity<User, IdentityRole<long>>()` | ✅ Done | `RoleManager<IdentityRole<long>>` registered |
| `ApplicationRoles` constants | ✅ Done | Admin, SiteManager, ProjectManager, Owner |
| Role tables in DB | ✅ Done | Included in `AddIdentityAndOpenIddict` migration |
| `UserRegistrationService` assigns Owner on register | ✅ Done | Guards with `roleManager.RoleExistsAsync` |
| **Role seeding at startup** | ❌ Missing | Roles must exist before `UserRegistrationService` uses them |
| **JWT includes role claims** | ❌ Missing | `AuthorizationController.Authorize()` omits roles |
| **Role management endpoints** | ❌ Missing | No GET/POST/DELETE for user–role assignment |
| **Initial Admin user seed** | ❌ Missing | No way to bootstrap the first admin |
| **Unit + integration tests** | ❌ Missing | No coverage for seeding, assignment, or JWT claims |

**No new EF migration is required.** The Identity role tables already exist.

---

## 3. Architectural Overview

```
Domain      →  ApplicationRoles constants (no new entities)
Application →  IRoleService, RoleAssignmentDto, UserRolesDto
Infrastructure → RoleService (UserManager + RoleManager wrappers),
                 RoleSeedWorker (IHostedService, seeds roles + optional admin user)
Api         →  UsersController extensions (role endpoints),
                AuthorizationController fix (role claims in JWT)
Tests       →  Api.Tests: UsersController role tests, AuthorizationController token tests
               Infrastructure.Tests: RoleServiceTests, RoleSeedWorkerTests
```

```mermaid
graph TD
    A[RoleSeedWorker\nIHostedService] -->|RoleManager.CreateAsync| B[(AspNetRoles)]
    C[UsersController\nGET  /api/users/{id}/roles\nPOST /api/users/{id}/roles\nDELETE /api/users/{id}/roles/{role}] --> D[IRoleService]
    E[UsersController\nGET /api/roles] --> D
    D --> F[UserManager&lt;User&gt;]
    D --> G[RoleManager&lt;IdentityRole&lt;long&gt;&gt;]
    F --> B
    H[AuthorizationController\nAuthorize / Exchange] -->|role claims| I[OpenIddict access token]
    J[FeatureFlagsController] -->|Authorize Roles=Admin| I
```

---

## 4. Domain Layer — `src/Domain`

No new entities. `IdentityRole<long>` is the Identity framework role type.

**No changes required.**

`ApplicationRoles.cs` already provides the canonical role name constants. The `All` list will be used by `RoleSeedWorker` to seed idempotently.

---

## 5. Application Layer — `src/Application`

### 5.1 DTOs (`Application/Dtos/`)

#### `UserRolesDto`

```csharp
public sealed record UserRolesDto(
    long UserId,
    IReadOnlyList<string> Roles
);
```

*Designed flat for mobile consumption — no nesting.*

#### `AssignRoleRequest`

```csharp
public sealed record AssignRoleRequest(string RoleName);
```

### 5.2 Service Interface (`Application/Interfaces/IRoleService.cs`)

```csharp
public interface IRoleService
{
    /// <summary>Returns all role names configured in the system.</summary>
    Task<IReadOnlyList<string>> ListAllRolesAsync(CancellationToken ct = default);

    /// <summary>Returns the roles currently assigned to the user.</summary>
    Task<UserRolesDto?> GetUserRolesAsync(long userId, CancellationToken ct = default);

    /// <summary>Assigns a role to a user. Returns false if the role does not exist.</summary>
    Task<bool> AssignRoleAsync(long userId, string roleName, CancellationToken ct = default);

    /// <summary>Removes a role from a user. Returns false if the user did not have the role.</summary>
    Task<bool> RemoveRoleAsync(long userId, string roleName, CancellationToken ct = default);
}
```

**Design rationale:**

- `IRoleService` keeps the controller thin and fully mockable.
- Returning `bool` rather than `IdentityResult` keeps Application layer decoupled from Identity framework types.
- `GetUserRolesAsync` returns `null` when the user does not exist, enabling the controller to produce a 404.

---

## 6. Infrastructure Layer — `src/Infrastructure`

### 6.1 Role Seeder (`Infrastructure/RoleSeedWorker.cs`)

Implements `IHostedService`. Runs at application startup, **before** any requests are served.

```
For each name in ApplicationRoles.All:
    if RoleManager.RoleExistsAsync(name) is false:
        await RoleManager.CreateAsync(new IdentityRole<long> { Name = name })

Optional bootstrap admin (controlled by config key "Seed:AdminEmail"):
    if AdminEmail is set and UserManager.FindByEmailAsync(AdminEmail) is null:
        create the user with AdminPassword from config
        assign Admin role
```

**Idempotency:** The `RoleExistsAsync` + `FindByEmailAsync` guards ensure re-running the seeder (e.g., container restarts) is safe.

**Options class** (`Infrastructure/Options/SeedOptions.cs`):

```csharp
public class SeedOptions
{
    public string? AdminEmail    { get; set; }
    public string? AdminPassword { get; set; }
}
```

Registered via `services.Configure<SeedOptions>(configuration.GetSection("Seed"))`.  
Both properties are optional; if omitted, only role seeding runs.

### 6.2 Service Implementation (`Infrastructure/Services/RoleService.cs`)

Implements `IRoleService`.

```
ListAllRolesAsync  → RoleManager.Roles.Select(r => r.Name!).ToListAsync()
GetUserRolesAsync  → UserManager.FindByIdAsync, then UserManager.GetRolesAsync
AssignRoleAsync    → FindByIdAsync, RoleExistsAsync guard, UserManager.AddToRoleAsync
RemoveRoleAsync    → FindByIdAsync, UserManager.IsInRoleAsync guard, UserManager.RemoveFromRoleAsync
```

All mutating operations check pre-conditions using the Identity manager APIs rather than raw DB queries.

### 6.3 DI Registration (`Infrastructure/DependencyInjection.cs`)

```csharp
// Role management
services.Configure<SeedOptions>(configuration.GetSection("Seed"));
services.AddScoped<IRoleService, RoleService>();
services.AddHostedService<RoleSeedWorker>();
```

`RoleSeedWorker` is registered **after** `OpenIddictDataSeedWorker` — startup ordering preserved.

---

## 7. API Layer — `src/Api`

### 7.1 New Endpoints on `UsersController`

All new endpoints require the caller to be authenticated and in the `Admin` role, except `GET /api/roles` which requires authentication only (any role can query available roles for UI purposes).

| Method | Route | Auth | Description |
|---|---|---|---|
| `GET` | `/api/roles` | `[Authorize]` | List all system roles |
| `GET` | `/api/users/{userId}/roles` | `[Authorize(Roles = Admin)]` | Get roles for a user |
| `POST` | `/api/users/{userId}/roles` | `[Authorize(Roles = Admin)]` | Assign a role |
| `DELETE` | `/api/users/{userId}/roles/{roleName}` | `[Authorize(Roles = Admin)]` | Remove a role |

**Response conventions (mobile-friendly):**

```
GET  /api/roles
→ 200 { "roles": ["Admin", "SiteManager", "ProjectManager", "Owner"] }

GET  /api/users/{userId}/roles
→ 200 { "userId": 42, "roles": ["Owner"] }
→ 404 ProblemDetails when user not found

POST /api/users/{userId}/roles  body: { "roleName": "SiteManager" }
→ 204 No Content (role assigned)
→ 400 ProblemDetails { "detail": "Role 'Unknown' does not exist." }
→ 404 ProblemDetails when user not found
→ 409 Conflict when user already has the role

DELETE /api/users/{userId}/roles/{roleName}
→ 204 No Content (role removed)
→ 404 ProblemDetails when user not found or role not assigned
```

### 7.2 JWT Role Claims Fix (`AuthorizationController.cs`)

**Problem:** `Authorize()` builds the `ClaimsIdentity` without fetching the user's roles, so `[Authorize(Roles = "Admin")]` never passes.

**Fix — in `Authorize()`:**

```csharp
// After: identity.SetClaim(OpenIddictConstants.Claims.Name, ...)
var roles = await _userManager.GetRolesAsync(user);
foreach (var role in roles)
{
    identity.SetClaim(OpenIddictConstants.Claims.Role, role)
            .SetDestinations(OpenIddictConstants.Destinations.AccessToken,
                             OpenIddictConstants.Destinations.IdentityToken);
}
```

**Fix — in `GetDestinations()`:**

```csharp
if (claim.Type == OpenIddictConstants.Claims.Role)
{
    yield return OpenIddictConstants.Destinations.AccessToken;
    yield return OpenIddictConstants.Destinations.IdentityToken;
}
```

> **Note:** `SetClaim` on a `ClaimsIdentity` replaces a single claim. For multiple roles, use `identity.SetClaims(OpenIddictConstants.Claims.Role, roles.ToImmutableArray())` (OpenIddict helper that correctly sets a JSON array in the token).

---

## 8. Mobile UI Considerations

*Reviewed with the **mobile-ui** agent. The following API design decisions directly inform the React Native UI.*

### 8.1 Role Listing Screen (Admin)

- Calls `GET /api/users/{userId}/roles` to populate role badges on the User Detail screen.
- Roles displayed as coloured `Chip` components (one per assigned role).
- Role colours aligned with OwnerBuilder brand palette:

  | Role | Chip colour |
  |---|---|
  | Admin | `#E53E3E` (red) |
  | SiteManager | `#3182CE` (blue) |
  | ProjectManager | `#805AD5` (purple) |
  | Owner | `#38A169` (green) |

### 8.2 Role Assignment Bottom Sheet (Admin)

- Triggered by an "Edit Roles" button on the User Detail screen (Admin-only; hidden via Feature Flags `admin_panel`).
- Calls `GET /api/roles` to populate a multi-select list of all available roles.
- On confirm: fires `POST /api/users/{userId}/roles` for each newly added role and `DELETE /api/users/{userId}/roles/{roleName}` for each removed role.
- **Atomic UX pattern:** operations queued client-side, dispatched in sequence; on partial failure, the UI reports which assignments failed without rolling back successful ones.

### 8.3 Profile Screen (Self-view)

- Authenticated user's own roles fetched from the JWT claims (`roles` claim in the OIDC token) — **no additional API call needed** once the JWT fix is applied.
- Displayed as read-only role badges below the user's display name.

### 8.4 API Shape Requirements (agreed with mobile-ui)

- Role list in responses is always an array, never `null` (empty array `[]` when user has no roles).
- Role names are returned in their canonical casing (`Admin`, not `admin`) to match `ApplicationRoles` constants used in mobile code.
- `userId` in response payload matches the `long` ID type (serialised as JSON number, not string).

---

## 9. No Migration Required

Role tables (`AspNetRoles`, `AspNetUserRoles`, `AspNetRoleClaims`) were created by the `AddIdentityAndOpenIddict` migration (`20260526023855`). No schema changes are needed for this issue.

---

## 10. TDD Test Plan

### 10.1 Infrastructure.Tests — `RoleSeedWorkerTests`

| Test | Description |
|---|---|
| `SeedAsync_CreatesAllDefaultRoles_WhenNoneExist` | Roles created for each `ApplicationRoles.All` entry |
| `SeedAsync_IsIdempotent_WhenRolesAlreadyExist` | `CreateAsync` not called when `RoleExistsAsync` returns true |
| `SeedAsync_CreatesAdminUser_WhenSeedOptionsConfigured` | Admin user created and assigned Admin role |
| `SeedAsync_SkipsAdminUserSeed_WhenEmailNotConfigured` | No user created when `SeedOptions.AdminEmail` is null |
| `SeedAsync_SkipsAdminUserSeed_WhenAlreadyExists` | No duplicate user when admin email already registered |

### 10.2 Infrastructure.Tests — `RoleServiceTests`

| Test | Description |
|---|---|
| `ListAllRolesAsync_ReturnsAllRoles` | Returns all role names from RoleManager |
| `GetUserRolesAsync_ExistingUser_ReturnsRoles` | Returns roles for a known user |
| `GetUserRolesAsync_UnknownUser_ReturnsNull` | Returns null for unknown userId |
| `AssignRoleAsync_ValidRoleAndUser_ReturnsTrue` | Calls `AddToRoleAsync`, returns true |
| `AssignRoleAsync_UnknownRole_ReturnsFalse` | Returns false when role does not exist |
| `AssignRoleAsync_UnknownUser_ReturnsFalse` | Returns false when user does not exist |
| `RemoveRoleAsync_UserHasRole_ReturnsTrue` | Calls `RemoveFromRoleAsync`, returns true |
| `RemoveRoleAsync_UserLacksRole_ReturnsFalse` | Returns false without calling `RemoveFromRoleAsync` |
| `RemoveRoleAsync_UnknownUser_ReturnsFalse` | Returns false when user does not exist |

### 10.3 Api.Tests — `UsersControllerRoleTests`

Pattern: mock `IRoleService` via Moq; construct `UsersController` directly (same pattern as `UsersControllerTests`).

| Test | Description |
|---|---|
| `GetRoles_ReturnsAllSystemRoles` | 200 with role array |
| `GetUserRoles_ExistingUser_Returns200WithRoles` | 200 with `UserRolesDto` |
| `GetUserRoles_UnknownUser_Returns404` | 404 ProblemDetails |
| `AssignRole_ValidRequest_Returns204` | 204 No Content |
| `AssignRole_UnknownRole_Returns400` | 400 ProblemDetails |
| `AssignRole_UnknownUser_Returns404` | 404 ProblemDetails |
| `AssignRole_AlreadyAssigned_Returns409` | 409 Conflict |
| `RemoveRole_UserHasRole_Returns204` | 204 No Content |
| `RemoveRole_UserLacksRole_Returns404` | 404 ProblemDetails |

### 10.4 Api.Tests — `AuthorizationControllerRoleClaimsTests`

| Test | Description |
|---|---|
| `Authorize_UserWithRoles_IncludesRoleClaimsInPrincipal` | `GetRolesAsync` result appears in the `ClaimsPrincipal` passed to `SignIn` |
| `Authorize_UserWithNoRoles_IssuesTokenWithEmptyRoles` | No role claims in principal but token is still issued |

---

## 11. Acceptance Criteria Checklist

- [ ] `ApplicationRoles.All` roles exist in `AspNetRoles` after startup (verified by integration test or seeder unit test).
- [ ] New user registrations are assigned the `Owner` role automatically (existing behaviour preserved).
- [ ] Admin user can be bootstrapped via `Seed:AdminEmail` / `Seed:AdminPassword` config.
- [ ] JWT access token contains a `roles` claim (array) matching the user's assigned roles.
- [ ] `GET /api/roles` returns all system roles (Authenticated).
- [ ] `GET /api/users/{userId}/roles` returns user roles (Admin only).
- [ ] `POST /api/users/{userId}/roles` assigns a role (Admin only).
- [ ] `DELETE /api/users/{userId}/roles/{roleName}` removes a role (Admin only).
- [ ] All new endpoints return 403 when caller is not Admin.
- [ ] `[Authorize(Roles = "Admin")]` on `FeatureFlagsController` admin endpoints works end-to-end after JWT fix.
- [ ] All tests listed in §10 pass.
- [ ] README updated with: how to seed roles, how to configure initial admin, migration instructions (none needed).

---

## 12. File Change Summary

| File | Action |
|---|---|
| `src/Application/Dtos/UserRolesDto.cs` | **Create** |
| `src/Application/Dtos/AssignRoleRequest.cs` | **Create** |
| `src/Application/Interfaces/IRoleService.cs` | **Create** |
| `src/Infrastructure/Options/SeedOptions.cs` | **Create** |
| `src/Infrastructure/RoleSeedWorker.cs` | **Create** |
| `src/Infrastructure/Services/RoleService.cs` | **Create** |
| `src/Infrastructure/DependencyInjection.cs` | **Edit** — register `SeedOptions`, `IRoleService`, `RoleSeedWorker` |
| `src/Api/Controllers/UsersController.cs` | **Edit** — add role endpoints, inject `IRoleService` |
| `src/Api/Controllers/AuthorizationController.cs` | **Edit** — add role claims to JWT |
| `tests/Infrastructure.Tests/Services/RoleServiceTests.cs` | **Create** |
| `tests/Infrastructure.Tests/RoleSeedWorkerTests.cs` | **Create** |
| `tests/Api.Tests/Controllers/UsersControllerRoleTests.cs` | **Create** |
| `tests/Api.Tests/Controllers/AuthorizationControllerRoleClaimsTests.cs` | **Create** |
| `README.md` | **Edit** — role management section |

---

*Design reviewed with the **mobile-ui** agent. API response shapes and role colour palette confirmed aligned with existing OwnerBuilder React Native component library conventions.*
