# Design Plan: Policy-Based Authorization for Admin Endpoints

**Purpose**: Migrate all endpoints that currently use `[Authorize(Roles = ApplicationRoles.Admin)]` from role-based to policy-based authorization.  
**Scope**: `FeatureFlagsController` (2 endpoints) and `UsersController` (3 endpoints).  
**Approach**: TDD — define abstractions first; write failing tests; then implement.  
**Mobile UI**: Not applicable — pure API-layer change with no mobile UI surface.

---

## 1. Current State

### FeatureFlagsController (`src/Api/Controllers/FeatureFlagsController.cs`)

| Method | Route | Authorization |
|--------|-------|---------------|
| GET    | `/api/features` | `[AllowAnonymous]` |
| POST   | `/api/features/admin/entitlements` | `[Authorize(Roles = ApplicationRoles.Admin)]` ← **migrate** |
| DELETE | `/api/features/admin/entitlements/{roleName}/{featureKey}` | `[Authorize(Roles = ApplicationRoles.Admin)]` ← **migrate** |

### UsersController (`src/Api/Controllers/UsersController.cs`)

| Method | Route | Authorization |
|--------|-------|---------------|
| POST   | `/api/users/register` | `[AllowAnonymous]` |
| POST   | `/api/users/confirm-email` | `[AllowAnonymous]` |
| POST   | `/api/users/verify-2fa` | `[AllowAnonymous]` |
| GET    | `/api/roles` | `[Authorize]` |
| GET    | `/api/users/{userId}/roles` | `[Authorize(Roles = ApplicationRoles.Admin)]` ← **migrate** |
| POST   | `/api/users/{userId}/roles` | `[Authorize(Roles = ApplicationRoles.Admin)]` ← **migrate** |
| DELETE | `/api/users/{userId}/roles/{roleName}` | `[Authorize(Roles = ApplicationRoles.Admin)]` ← **migrate** |

### Program.cs (relevant excerpt)

```csharp
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

There are no named policies registered. No `IAuthorizationRequirement` or `IAuthorizationHandler` types exist anywhere in the solution.

---

## 2. Problem

Inline `[Authorize(Roles = ...)]` attributes couple controllers directly to role names. This:
- Cannot be unit-tested in isolation (role logic lives implicitly in the attribute).
- Cannot be extended (e.g., adding a second condition) without modifying the controller.
- Spreads role-to-capability mapping across many call sites rather than centralising it.

---

## 3. Target State

### Policy: `ManageFeatureFlags`

| Property | Value |
|----------|-------|
| **Name constant** | `ApplicationPolicies.ManageFeatureFlags` |
| **Behaviour** | Caller must be authenticated and hold the `Admin` role |
| **Applies to** | `FeatureFlagsController` admin endpoints |

### Policy: `ManageUserRoles`

| Property | Value |
|----------|-------|
| **Name constant** | `ApplicationPolicies.ManageUserRoles` |
| **Behaviour** | Caller must be authenticated and hold the `Admin` role |
| **Applies to** | `UsersController` role-management endpoints |

**Why two separate policies instead of one shared `AdminOnly` policy?**  
Separate policies allow:
- Independent evolution of the access rules for each capability (e.g., feature-flag management might later require a `FeatureManager` claim, while role management remains `Admin`-only).
- Tests that assert the exact policy name on each endpoint, preventing accidental coupling.
- Clear audit trail — each policy name is self-documenting about the capability it guards.

**Why a custom requirement + handler instead of `policy.RequireRole(Admin)` only?**  
A custom `IAuthorizationRequirement`/`IAuthorizationHandler` pair allows:
- Direct unit testing of the access-control logic without ASP.NET Core middleware.
- Future extension (additional claim checks, time-bound rules) without touching controller code.

### Updated FeatureFlagsController

| Method | Route | Authorization |
|--------|-------|---------------|
| GET    | `/api/features` | `[AllowAnonymous]` — **unchanged** |
| POST   | `/api/features/admin/entitlements` | `[Authorize(Policy = ApplicationPolicies.ManageFeatureFlags)]` |
| DELETE | `/api/features/admin/entitlements/{roleName}/{featureKey}` | `[Authorize(Policy = ApplicationPolicies.ManageFeatureFlags)]` |

### Updated UsersController

| Method | Route | Authorization |
|--------|-------|---------------|
| GET    | `/api/roles` | `[Authorize]` — **unchanged** |
| GET    | `/api/users/{userId}/roles` | `[Authorize(Policy = ApplicationPolicies.ManageUserRoles)]` |
| POST   | `/api/users/{userId}/roles` | `[Authorize(Policy = ApplicationPolicies.ManageUserRoles)]` |
| DELETE | `/api/users/{userId}/roles/{roleName}` | `[Authorize(Policy = ApplicationPolicies.ManageUserRoles)]` |

---

## 4. Abstractions

### 4.1 Policy name constants

**File**: `src/Domain/Constants/ApplicationPolicies.cs` *(new)*

```csharp
namespace BuilderAssistantApi.Domain.Constants;

public static class ApplicationPolicies
{
    /// <summary>
    /// Grants the ability to create, update, and delete role-feature entitlements
    /// via <c>FeatureFlagsController</c>.
    /// Satisfied when the caller is authenticated and holds the
    /// <see cref="ApplicationRoles.Admin"/> role.
    /// </summary>
    public const string ManageFeatureFlags = "ManageFeatureFlags";

    /// <summary>
    /// Grants the ability to view, assign, and remove roles from users
    /// via <c>UsersController</c>.
    /// Satisfied when the caller is authenticated and holds the
    /// <see cref="ApplicationRoles.Admin"/> role.
    /// </summary>
    public const string ManageUserRoles = "ManageUserRoles";
}
```

Placed in `Domain.Constants` alongside `ApplicationRoles` — same layer, no upward dependencies.

---

### 4.2 Requirements (markers)

Requirements are pure marker types; all logic lives in the handler.

**File**: `src/Api/Authorization/ManageFeatureFlagsRequirement.cs` *(new)*

```csharp
using Microsoft.AspNetCore.Authorization;

namespace BuilderAssistantApi.Api.Authorization;

/// <summary>
/// Marker requirement for the <c>ManageFeatureFlags</c> policy.
/// Satisfied when the caller holds the <c>Admin</c> role.
/// </summary>
public sealed class ManageFeatureFlagsRequirement : IAuthorizationRequirement { }
```

**File**: `src/Api/Authorization/ManageUserRolesRequirement.cs` *(new)*

```csharp
using Microsoft.AspNetCore.Authorization;

namespace BuilderAssistantApi.Api.Authorization;

/// <summary>
/// Marker requirement for the <c>ManageUserRoles</c> policy.
/// Satisfied when the caller holds the <c>Admin</c> role.
/// </summary>
public sealed class ManageUserRolesRequirement : IAuthorizationRequirement { }
```

---

### 4.3 Handlers

Handlers intentionally do **not** call `context.Fail()`. Absence of `Succeed` is sufficient for the framework to deny access, and this preserves the ability for other handlers to satisfy a requirement in the future.

**File**: `src/Api/Authorization/ManageFeatureFlagsHandler.cs` *(new)*

```csharp
using BuilderAssistantApi.Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace BuilderAssistantApi.Api.Authorization;

public sealed class ManageFeatureFlagsHandler
    : AuthorizationHandler<ManageFeatureFlagsRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ManageFeatureFlagsRequirement requirement)
    {
        if (context.User.IsInRole(ApplicationRoles.Admin))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
```

**File**: `src/Api/Authorization/ManageUserRolesHandler.cs` *(new)*

```csharp
using BuilderAssistantApi.Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace BuilderAssistantApi.Api.Authorization;

public sealed class ManageUserRolesHandler
    : AuthorizationHandler<ManageUserRolesRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ManageUserRolesRequirement requirement)
    {
        if (context.User.IsInRole(ApplicationRoles.Admin))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
```

---

## 5. Registration

Changes to `src/Api/Program.cs`:

```csharp
// Register handlers in DI (before AddAuthorization)
builder.Services.AddSingleton<IAuthorizationHandler, ManageFeatureFlagsHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, ManageUserRolesHandler>();

// In AddAuthorization — add alongside existing FallbackPolicy
options.AddPolicy(
    ApplicationPolicies.ManageFeatureFlags,
    policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new ManageFeatureFlagsRequirement()));

options.AddPolicy(
    ApplicationPolicies.ManageUserRoles,
    policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new ManageUserRolesRequirement()));
```

`RequireAuthenticatedUser()` is included within each policy so each is self-contained and does not rely on the global fallback.

---

## 6. Controller Changes

### FeatureFlagsController

In `src/Api/Controllers/FeatureFlagsController.cs`, replace both occurrences of:

```csharp
[Authorize(Roles = ApplicationRoles.Admin)]
```

with:

```csharp
[Authorize(Policy = ApplicationPolicies.ManageFeatureFlags)]
```

The `using BuilderAssistantApi.Domain.Constants;` import is already present.

### UsersController

In `src/Api/Controllers/UsersController.cs`, replace all three occurrences of:

```csharp
[Authorize(Roles = ApplicationRoles.Admin)]
```

with:

```csharp
[Authorize(Policy = ApplicationPolicies.ManageUserRoles)]
```

The `using BuilderAssistantApi.Domain.Constants;` import is already present.

---

## 7. Test Plan (TDD — write failing tests first)

### 7.1 Handler unit tests — ManageFeatureFlagsHandler

**File**: `tests/Api.Tests/Authorization/ManageFeatureFlagsHandlerTests.cs` *(new)*

| Test | Setup | Expected |
|------|-------|----------|
| `HandleRequirement_AdminRole_Succeeds` | `ClaimsPrincipal` with `Role = Admin` | `context.HasSucceeded == true` |
| `HandleRequirement_NonAdminRole_DoesNotSucceed` | `ClaimsPrincipal` with `Role = SiteManager` | `context.HasSucceeded == false` |
| `HandleRequirement_MultipleRolesNoAdmin_DoesNotSucceed` | `ClaimsPrincipal` with roles `SiteManager`, `Owner` | `context.HasSucceeded == false` |
| `HandleRequirement_Unauthenticated_DoesNotSucceed` | Empty `ClaimsPrincipal` | `context.HasSucceeded == false` |
| `HandleRequirement_AdminPlusOtherRoles_Succeeds` | `ClaimsPrincipal` with `Admin` + `SiteManager` | `context.HasSucceeded == true` |

### 7.2 Handler unit tests — ManageUserRolesHandler

**File**: `tests/Api.Tests/Authorization/ManageUserRolesHandlerTests.cs` *(new)*

| Test | Setup | Expected |
|------|-------|----------|
| `HandleRequirement_AdminRole_Succeeds` | `ClaimsPrincipal` with `Role = Admin` | `context.HasSucceeded == true` |
| `HandleRequirement_NonAdminRole_DoesNotSucceed` | `ClaimsPrincipal` with `Role = SiteManager` | `context.HasSucceeded == false` |
| `HandleRequirement_MultipleRolesNoAdmin_DoesNotSucceed` | `ClaimsPrincipal` with roles `SiteManager`, `Owner` | `context.HasSucceeded == false` |
| `HandleRequirement_Unauthenticated_DoesNotSucceed` | Empty `ClaimsPrincipal` | `context.HasSucceeded == false` |
| `HandleRequirement_AdminPlusOtherRoles_Succeeds` | `ClaimsPrincipal` with `Admin` + `SiteManager` | `context.HasSucceeded == true` |

### 7.3 Controller attribute tests — FeatureFlagsController (reflection-based)

**File**: `tests/Api.Tests/Controllers/FeatureFlagsControllerAuthorizationTests.cs` *(new)*

These assert the presence/absence of attributes without requiring a running host.

| Test | Assertion |
|------|-----------|
| `UpsertEntitlement_HasManageFeatureFlagsPolicy` | `AuthorizeAttribute.Policy == ApplicationPolicies.ManageFeatureFlags` on `UpsertEntitlement` |
| `DeleteEntitlement_HasManageFeatureFlagsPolicy` | `AuthorizeAttribute.Policy == ApplicationPolicies.ManageFeatureFlags` on `DeleteEntitlement` |
| `UpsertEntitlement_NoRoleBasedAuthorize` | No `AuthorizeAttribute` with non-null `.Roles` on `UpsertEntitlement` |
| `DeleteEntitlement_NoRoleBasedAuthorize` | No `AuthorizeAttribute` with non-null `.Roles` on `DeleteEntitlement` |
| `GetFeatures_AllowAnonymousUnchanged` | `AllowAnonymousAttribute` present on `GetFeatures` |
| `Controller_NoClassLevelRoleBasedAuthorize` | No `AuthorizeAttribute` with non-null `.Roles` on the class itself |

### 7.4 Controller attribute tests — UsersController (reflection-based)

**File**: `tests/Api.Tests/Controllers/UsersControllerAuthorizationTests.cs` *(new)*

| Test | Assertion |
|------|-----------|
| `GetUserRoles_HasManageUserRolesPolicy` | `AuthorizeAttribute.Policy == ApplicationPolicies.ManageUserRoles` on `GetUserRoles` |
| `AssignRole_HasManageUserRolesPolicy` | `AuthorizeAttribute.Policy == ApplicationPolicies.ManageUserRoles` on `AssignRole` |
| `RemoveRole_HasManageUserRolesPolicy` | `AuthorizeAttribute.Policy == ApplicationPolicies.ManageUserRoles` on `RemoveRole` |
| `GetUserRoles_NoRoleBasedAuthorize` | No `AuthorizeAttribute` with non-null `.Roles` on `GetUserRoles` |
| `AssignRole_NoRoleBasedAuthorize` | No `AuthorizeAttribute` with non-null `.Roles` on `AssignRole` |
| `RemoveRole_NoRoleBasedAuthorize` | No `AuthorizeAttribute` with non-null `.Roles` on `RemoveRole` |
| `Register_AllowAnonymousUnchanged` | `AllowAnonymousAttribute` present on `Register` |
| `ConfirmEmail_AllowAnonymousUnchanged` | `AllowAnonymousAttribute` present on `ConfirmEmail` |
| `VerifyTwoFactor_AllowAnonymousUnchanged` | `AllowAnonymousAttribute` present on `VerifyTwoFactor` |
| `GetRoles_PlainAuthorizeUnchanged` | `AuthorizeAttribute` with null `Policy` and null `Roles` present on `GetRoles` |
| `Controller_NoClassLevelRoleBasedAuthorize` | No `AuthorizeAttribute` with non-null `.Roles` on the class itself |

### 7.5 Existing tests

All passing tests in `FeatureFlagsControllerTests` and `UsersControllerTests` remain valid — they instantiate controllers directly and bypass authorization. No changes required to existing test files.

---

## 8. Files Affected

| File | Change |
|------|--------|
| `src/Domain/Constants/ApplicationPolicies.cs` | **New** — `ManageFeatureFlags` and `ManageUserRoles` policy name constants |
| `src/Api/Authorization/ManageFeatureFlagsRequirement.cs` | **New** — requirement marker |
| `src/Api/Authorization/ManageFeatureFlagsHandler.cs` | **New** — handler logic |
| `src/Api/Authorization/ManageUserRolesRequirement.cs` | **New** — requirement marker |
| `src/Api/Authorization/ManageUserRolesHandler.cs` | **New** — handler logic |
| `src/Api/Program.cs` | Register 2 handlers in DI + add 2 named policies |
| `src/Api/Controllers/FeatureFlagsController.cs` | Replace `[Authorize(Roles = ...)]` → `[Authorize(Policy = ...ManageFeatureFlags)]` on 2 endpoints |
| `src/Api/Controllers/UsersController.cs` | Replace `[Authorize(Roles = ...)]` → `[Authorize(Policy = ...ManageUserRoles)]` on 3 endpoints |
| `tests/Api.Tests/Authorization/ManageFeatureFlagsHandlerTests.cs` | **New** — 5 handler unit tests |
| `tests/Api.Tests/Authorization/ManageUserRolesHandlerTests.cs` | **New** — 5 handler unit tests |
| `tests/Api.Tests/Controllers/FeatureFlagsControllerAuthorizationTests.cs` | **New** — 6 reflection-based attribute tests |
| `tests/Api.Tests/Controllers/UsersControllerAuthorizationTests.cs` | **New** — 11 reflection-based attribute tests |

---

## 9. Acceptance Criteria

| Criterion | How it is satisfied |
|-----------|---------------------|
| `FeatureFlagsController` admin endpoints protected by `ManageFeatureFlags` policy | `[Authorize(Policy = ApplicationPolicies.ManageFeatureFlags)]` on both admin endpoints |
| `UsersController` role endpoints protected by `ManageUserRoles` policy | `[Authorize(Policy = ApplicationPolicies.ManageUserRoles)]` on all 3 role endpoints |
| No `[Authorize(Roles = ...)]` remains in either controller | Enforced by reflection-based tests in sections 7.3 and 7.4 |
| Policy logic is testable in isolation | Both handlers tested directly without ASP.NET Core middleware |
| Policies are self-contained | Each policy includes `RequireAuthenticatedUser()` independently of the global fallback |
| Existing behaviour preserved | Both handlers check `IsInRole(Admin)` — identical semantics to the previous attributes |

---

## 10. Out of Scope

- `DocumentProcessingController` `[RequireFeature("ocr_scan")]` — unrelated mechanism.
- Changes to token issuance or the OpenIddict pipeline.
- `GetRoles` endpoint in `UsersController` — already uses plain `[Authorize]` with no role constraint; no change needed.
