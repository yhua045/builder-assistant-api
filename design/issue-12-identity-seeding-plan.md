# Design Plan — Issue #12: Seed ASP.NET Identity (Roles & Initial Users)

> **Revision 2 — 27 May 2026**
> The original plan (C# `IIdentitySeeder` / `--seed` CLI flag) has been superseded.
> The new approach uses a standalone idempotent SQL script executed manually,
> keeping all seeding concern out of the application binary entirely.
>
> See §6 for the full list of artefacts to revert/remove.

---

## 1. Context

The project uses ASP.NET Core Identity with a custom `User : IdentityUser<long>` entity and `IdentityRole<long>`, backed by `BuilderAssistantDbContext : IdentityDbContext<User, IdentityRole<long>, long>`. OpenIddict is wired for the password grant flow.

**Revision 2 replaces the C# `IIdentitySeeder` / `--seed` CLI approach with a standalone SQL script.** The rationale: seeding is a one-time, ad-hoc development task. Coupling it to the application binary (via `UserManager`, DI registration, and a CLI flag) adds test surface, startup complexity, and DI noise for something that only needs to run once per environment setup. A plain SQL file is simpler, easier to review, and has zero impact on the application at runtime.

---

## 2. Requirements Summary

| # | Requirement |
|---|-------------|
| R1 | Create roles: `Admin`, `SiteManager`, `ProjectManager`, `Owner` |
| R2 | Create one `User` per role with a dev-only starting password (`Dev1234!`) |
| R3 | Assign the matching role to each user |
| R4 | Seeding must be **idempotent** (safe to run multiple times) |
| R5 | Not embedded in EF Core auto-generated migrations |
| R6 | ~~Use `UserManager<User>` and `RoleManager<IdentityRole<long>>`~~ → **use raw idempotent SQL instead** |
| R7 | Documented: how to run locally |

---

## 3. Architecture

### 3.1 Approach: Standalone SQL Script

All seeding lives in a single file:

```
scripts/
  seed-development-identity.sql   ← idempotent T-SQL; execute once after migrations
```

No new C# files are required. The script targets the same `BuilderAssistantDbContext` schema
produced by the existing EF Core migrations (specifically `AddIdentityAndOpenIddict`).

### 3.2 Role Constants — `ApplicationRoles.cs` (KEEP)

```csharp
// Domain/Constants/ApplicationRoles.cs — unchanged
public static class ApplicationRoles
{
    public const string Admin          = "Admin";
    public const string SiteManager    = "SiteManager";
    public const string ProjectManager = "ProjectManager";
    public const string Owner          = "Owner";

    public static IReadOnlyList<string> All =>
        [Admin, SiteManager, ProjectManager, Owner];
}
```

`ApplicationRoles.cs` stays in `Domain`. The role name string values in the SQL file
**must exactly match** these constants, including casing. This is documented in the SQL
file header. The constants are still used for `[Authorize(Roles = ...)]` decorators in
controllers.

### 3.3 SQL Script Design

**Target tables** (created by the EF migration `AddIdentityAndOpenIddict`):

| Table | Relevant Columns |
|-------|-----------------|
| `AspNetRoles` | `Id` (bigint IDENTITY), `Name`, `NormalizedName`, `ConcurrencyStamp` |
| `AspNetUsers` | `Id` (bigint IDENTITY), `UserName`, `NormalizedUserName`, `Email`, `NormalizedEmail`, `EmailConfirmed`, `PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, `PhoneNumberConfirmed`, `TwoFactorEnabled`, `LockoutEnabled`, `AccessFailedCount`, `CreatedAt` |
| `AspNetUserRoles` | `UserId` (bigint FK), `RoleId` (bigint FK) |

**Idempotency strategy**: Each INSERT is guarded by `IF NOT EXISTS (SELECT 1 FROM ... WHERE NormalizedName = ...)`. `AspNetUserRoles` uses a `NOT EXISTS` subquery on the join. The script is safe to run multiple times.

**Password hash**: The `PasswordHash` column stores an ASP.NET Core Identity v3
PBKDF2/HMACSHA1 hash. The hash embedded in the script was computed once for `Dev1234!`
using `new PasswordHasher<object>().HashPassword(new object(), "Dev1234!")` against
`Microsoft.AspNetCore.Identity 8.x`. All four seed users share the same hash (acceptable
for development; password hashing is intentionally salted so verification via Identity
still works correctly).

To regenerate the hash for a different password:

```bash
cd /tmp/HashGen && dotnet run   # or any project referencing Microsoft.AspNetCore.Identity
# Program.cs: Console.WriteLine(new PasswordHasher<object>().HashPassword(new object(), "NewPassword!"));
```

### 3.4 Seed Users

| Role | UserName | Email |
|------|----------|-------|
| `Admin` | `admin` | admin@builderassistant.dev |
| `SiteManager` | `sitemanager` | sitemanager@builderassistant.dev |
| `ProjectManager` | `projectmanager` | projectmanager@builderassistant.dev |
| `Owner` | `owner` | owner@builderassistant.dev |

Password for all: `Dev1234!`

### 3.5 Invocation

```bash
# 1. Ensure migrations are applied
dotnet ef database update --project src/Infrastructure --startup-project src/Api

# 2. Run the seed script (SQL Server / sqlcmd)
sqlcmd -S localhost -d BuilderAssistantDb -U sa -P "Your_password123" \
       -i scripts/seed-development-identity.sql

# Or in Azure Data Studio / SSMS: open the file and execute against the target DB
```

For Docker-based dev:
```bash
docker exec -i <sql-container> /opt/mssql-tools/bin/sqlcmd \
    -S localhost -U sa -P "Your_password123" \
    -d BuilderAssistantDb \
    -i /scripts/seed-development-identity.sql
```

---

## 4. C# Artefacts to Remove (developer task)

The following files introduced by the original plan **must be deleted**:

| Action | File |
|--------|------|
| **Delete** | `src/Application/Seeding/IIdentitySeeder.cs` |
| **Delete** | `src/Application/Seeding/SeedingOptions.cs` |
| **Delete** | `src/Infrastructure/Seeding/IdentitySeeder.cs` |
| **Delete** | `src/Infrastructure/Seeding/IdentitySeederExtensions.cs` |
| **Delete** | `tests/Infrastructure.Tests/Seeding/IdentitySeedingTests.cs` |
| **Revert** | `src/Infrastructure/DependencyInjection.cs` — remove `services.AddIdentitySeeder(configuration)` and the `using` import for `BuilderAssistantApi.Infrastructure.Seeding` |
| **Revert** | `src/Api/Program.cs` — remove the `if (args.Contains("--seed")) { ... }` block |

---

## 5. Files to Add / Modify (developer task)

| Action | File | Notes |
|--------|------|-------|
| **New** | `scripts/seed-development-identity.sql` | Idempotent T-SQL; content defined in §3 |
| **Modify** | `README.md` | Replace `--seed` CLI section with SQL invocation instructions (see §7) |
| **Keep** | `src/Domain/Constants/ApplicationRoles.cs` | No changes required |

---

## 6. TDD Notes

There are **no new automated tests** for the SQL script itself. SQL seed scripts are
validated by:
1. Running the script against a local DB and verifying the resulting rows manually or
   via a one-off query.
2. Running existing integration tests (`dotnet test`) — they must continue to pass after
   the C# seeder files are removed (they will, because `IdentitySeedingTests.cs` is also
   removed).

If a future requirement calls for programmatic seeding (e.g., CI pipeline automation),
a new design should be created at that point.

---

## 7. README Changes

The existing **"Identity Seeding"** section in `README.md` must be replaced as follows:

```markdown
## Development Identity Seeding

> **Warning** The seed script and its accounts (`Dev1234!`) are for local development
> only. Never execute it against a staging or production database.

### Prerequisites

Apply EF Core migrations before seeding:

```bash
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

### Running the seed script

Execute `scripts/seed-development-identity.sql` against your local SQL Server instance.

**With sqlcmd:**
```bash
sqlcmd -S localhost -d BuilderAssistantDb -U sa -P "Your_password123" \
       -i scripts/seed-development-identity.sql
```

**With Docker Compose dev stack:**
```bash
docker exec -i <sql-container-name> /opt/mssql-tools/bin/sqlcmd \
    -S localhost -U sa -P "Your_password123" \
    -d BuilderAssistantDb \
    -i /scripts/seed-development-identity.sql
```

The script is idempotent — safe to run multiple times.

### Roles and accounts created

| Role | UserName | Email | Password |
|------|----------|-------|----------|
| `Admin` | `admin` | admin@builderassistant.dev | `Dev1234!` |
| `SiteManager` | `sitemanager` | sitemanager@builderassistant.dev | `Dev1234!` |
| `ProjectManager` | `projectmanager` | projectmanager@builderassistant.dev | `Dev1234!` |
| `Owner` | `owner` | owner@builderassistant.dev | `Dev1234!` |
```

---

## 8. Mobile UI Review

This issue is a backend-only, development-tooling change. There are no UI components,
screens, routes, or style changes. The mobile-ui agent confirmed no UI implications —
the seed accounts are consumed by the mobile app via the existing
`POST /connect/token` (password grant) endpoint, which is unchanged.

---

## 9. Risks & Constraints

| Risk | Mitigation |
|------|------------|
| Dev passwords in version control | Hash is embedded (not plaintext); `Dev1234!` is documented as dev-only in the script header and README |
| Script run against production | Requires explicit manual execution with DB credentials; not wired into any automated startup path |
| Hash incompatibility after Identity version upgrade | Script header documents how to regenerate; only affects new dev environment setups |
| SQL Server dialect only | The project targets SQL Server in all non-test environments; this is acceptable. SQLite is used for integration tests only, and those tests do not depend on the seed script |

---

## 10. Handoff

Design document: [design/issue-12-identity-seeding-plan.md](issue-12-identity-seeding-plan.md)  
SQL artefact: [scripts/seed-development-identity.sql](../scripts/seed-development-identity.sql)

> **Start TDD**: Plan approved. Implement the code removals and SQL file per §4 and §5.

