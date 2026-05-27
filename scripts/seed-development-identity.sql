-- =============================================================================
-- seed-development-identity.sql
-- =============================================================================
-- PURPOSE  : Bootstrap ASP.NET Core Identity tables with the four application
--            roles and one development user per role.
-- AUDIENCE : Local development environments ONLY.
--
-- WARNING  : NEVER execute this script against a staging or production database.
--            The accounts and password below are not suitable for any
--            environment accessible to end users.
--
-- PREREQUISITES
--   EF Core migrations must have been applied first:
--     dotnet ef database update --project src/Infrastructure --startup-project src/Api
--
-- USAGE (sqlcmd)
--   sqlcmd -S localhost -d BuilderAssistantDb -U sa -P "Your_password123" \
--          -i scripts/seed-development-identity.sql
--
-- USAGE (Docker Compose dev stack)
--   docker exec -i <sql-container-name> /opt/mssql-tools/bin/sqlcmd \
--       -S localhost -U sa -P "Your_password123" \
--       -d BuilderAssistantDb \
--       -i /scripts/seed-development-identity.sql
--
-- IDEMPOTENCY
--   Every INSERT is guarded by IF NOT EXISTS. The script is safe to run
--   multiple times against the same database.
--
-- ROLE NAME CONSTANTS
--   The role name values below must exactly match those in
--   src/Domain/Constants/ApplicationRoles.cs (including casing):
--     Admin | SiteManager | ProjectManager | Owner
--
-- PASSWORD HASH
--   The PasswordHash value was generated once for the password "Dev1234!" using
--   ASP.NET Core Identity's PasswordHasher<object>
--   (Microsoft.AspNetCore.Identity 8.x, PBKDF2/HMACSHA1, 10 000 iterations).
--
--   To regenerate for a different password, run a small .NET program:
--
--     using Microsoft.AspNetCore.Identity;
--     var hasher = new PasswordHasher<object>();
--     Console.WriteLine(hasher.HashPassword(new object(), "NewPassword!"));
--
--   All four seed users intentionally share the same hash. Because the hash
--   algorithm is deterministic given a fixed salt (embedded in the hash), and
--   because Identity's VerifyHashedPassword reads the salt from the stored
--   hash before comparing, verification still works correctly for each user.
-- =============================================================================

SET NOCOUNT ON;

DECLARE @pwdHash   NVARCHAR(256) = N'AQAAAAEAACcQAAAAEBrVg20QTUx21g8szzReHSNfrZM8UHwx3SSTq8w4BFGuP4npV8yD0gNSspKhJESKCw==';
DECLARE @emailDomain NVARCHAR(64) = N'builderassistant.dev';

BEGIN TRANSACTION;

-- =============================================================================
-- 1. Roles
--    NormalizedName is the canonical lookup key used by Identity.
--    ConcurrencyStamp is a random token — NEWID() is fine on first insert.
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'ADMIN')
BEGIN
    INSERT INTO [AspNetRoles] ([Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (N'Admin', N'ADMIN', CONVERT(NVARCHAR(36), NEWID()));
    PRINT 'Created role: Admin';
END
ELSE
    PRINT 'Role already exists: Admin';

IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'SITEMANAGER')
BEGIN
    INSERT INTO [AspNetRoles] ([Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (N'SiteManager', N'SITEMANAGER', CONVERT(NVARCHAR(36), NEWID()));
    PRINT 'Created role: SiteManager';
END
ELSE
    PRINT 'Role already exists: SiteManager';

IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'PROJECTMANAGER')
BEGIN
    INSERT INTO [AspNetRoles] ([Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (N'ProjectManager', N'PROJECTMANAGER', CONVERT(NVARCHAR(36), NEWID()));
    PRINT 'Created role: ProjectManager';
END
ELSE
    PRINT 'Role already exists: ProjectManager';

IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'OWNER')
BEGIN
    INSERT INTO [AspNetRoles] ([Name], [NormalizedName], [ConcurrencyStamp])
    VALUES (N'Owner', N'OWNER', CONVERT(NVARCHAR(36), NEWID()));
    PRINT 'Created role: Owner';
END
ELSE
    PRINT 'Role already exists: Owner';

-- =============================================================================
-- 2. Users
--    SecurityStamp and ConcurrencyStamp must be non-null (Identity expectation).
--    EmailConfirmed = 1 skips the email verification flow for dev accounts.
--    LockoutEnabled = 1 matches Identity defaults.
--    CreatedAt is the custom field on the domain User entity.
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM [AspNetUsers] WHERE [NormalizedUserName] = N'ADMIN')
BEGIN
    INSERT INTO [AspNetUsers] (
        [UserName], [NormalizedUserName],
        [Email],    [NormalizedEmail],
        [EmailConfirmed],
        [PasswordHash],
        [SecurityStamp],   [ConcurrencyStamp],
        [PhoneNumber],     [PhoneNumberConfirmed],
        [TwoFactorEnabled],
        [LockoutEnd],      [LockoutEnabled],
        [AccessFailedCount],
        [CreatedAt]
    )
    VALUES (
        N'admin', N'ADMIN',
        N'admin@' + @emailDomain, N'ADMIN@' + UPPER(@emailDomain),
        1,
        @pwdHash,
        CONVERT(NVARCHAR(36), NEWID()), CONVERT(NVARCHAR(36), NEWID()),
        NULL, 0,
        0,
        NULL, 1,
        0,
        SYSDATETIMEOFFSET()
    );
    PRINT 'Created user: admin';
END
ELSE
    PRINT 'User already exists: admin';

IF NOT EXISTS (SELECT 1 FROM [AspNetUsers] WHERE [NormalizedUserName] = N'SITEMANAGER')
BEGIN
    INSERT INTO [AspNetUsers] (
        [UserName], [NormalizedUserName],
        [Email],    [NormalizedEmail],
        [EmailConfirmed],
        [PasswordHash],
        [SecurityStamp],   [ConcurrencyStamp],
        [PhoneNumber],     [PhoneNumberConfirmed],
        [TwoFactorEnabled],
        [LockoutEnd],      [LockoutEnabled],
        [AccessFailedCount],
        [CreatedAt]
    )
    VALUES (
        N'sitemanager', N'SITEMANAGER',
        N'sitemanager@' + @emailDomain, N'SITEMANAGER@' + UPPER(@emailDomain),
        1,
        @pwdHash,
        CONVERT(NVARCHAR(36), NEWID()), CONVERT(NVARCHAR(36), NEWID()),
        NULL, 0,
        0,
        NULL, 1,
        0,
        SYSDATETIMEOFFSET()
    );
    PRINT 'Created user: sitemanager';
END
ELSE
    PRINT 'User already exists: sitemanager';

IF NOT EXISTS (SELECT 1 FROM [AspNetUsers] WHERE [NormalizedUserName] = N'PROJECTMANAGER')
BEGIN
    INSERT INTO [AspNetUsers] (
        [UserName], [NormalizedUserName],
        [Email],    [NormalizedEmail],
        [EmailConfirmed],
        [PasswordHash],
        [SecurityStamp],   [ConcurrencyStamp],
        [PhoneNumber],     [PhoneNumberConfirmed],
        [TwoFactorEnabled],
        [LockoutEnd],      [LockoutEnabled],
        [AccessFailedCount],
        [CreatedAt]
    )
    VALUES (
        N'projectmanager', N'PROJECTMANAGER',
        N'projectmanager@' + @emailDomain, N'PROJECTMANAGER@' + UPPER(@emailDomain),
        1,
        @pwdHash,
        CONVERT(NVARCHAR(36), NEWID()), CONVERT(NVARCHAR(36), NEWID()),
        NULL, 0,
        0,
        NULL, 1,
        0,
        SYSDATETIMEOFFSET()
    );
    PRINT 'Created user: projectmanager';
END
ELSE
    PRINT 'User already exists: projectmanager';

IF NOT EXISTS (SELECT 1 FROM [AspNetUsers] WHERE [NormalizedUserName] = N'OWNER')
BEGIN
    INSERT INTO [AspNetUsers] (
        [UserName], [NormalizedUserName],
        [Email],    [NormalizedEmail],
        [EmailConfirmed],
        [PasswordHash],
        [SecurityStamp],   [ConcurrencyStamp],
        [PhoneNumber],     [PhoneNumberConfirmed],
        [TwoFactorEnabled],
        [LockoutEnd],      [LockoutEnabled],
        [AccessFailedCount],
        [CreatedAt]
    )
    VALUES (
        N'owner', N'OWNER',
        N'owner@' + @emailDomain, N'OWNER@' + UPPER(@emailDomain),
        1,
        @pwdHash,
        CONVERT(NVARCHAR(36), NEWID()), CONVERT(NVARCHAR(36), NEWID()),
        NULL, 0,
        0,
        NULL, 1,
        0,
        SYSDATETIMEOFFSET()
    );
    PRINT 'Created user: owner';
END
ELSE
    PRINT 'User already exists: owner';

-- =============================================================================
-- 3. User-Role Assignments
--    Look up UserId and RoleId by their normalized names so the script does not
--    depend on IDENTITY-generated Id values.
-- =============================================================================

-- admin → Admin
INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
SELECT u.[Id], r.[Id]
FROM   [AspNetUsers]  u
JOIN   [AspNetRoles]  r ON r.[NormalizedName] = N'ADMIN'
WHERE  u.[NormalizedUserName] = N'ADMIN'
  AND  NOT EXISTS (
           SELECT 1 FROM [AspNetUserRoles] ur
           WHERE ur.[UserId] = u.[Id] AND ur.[RoleId] = r.[Id]
       );
IF @@ROWCOUNT > 0 PRINT 'Assigned role Admin to user admin';
ELSE               PRINT 'Assignment already exists: admin → Admin';

-- sitemanager → SiteManager
INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
SELECT u.[Id], r.[Id]
FROM   [AspNetUsers]  u
JOIN   [AspNetRoles]  r ON r.[NormalizedName] = N'SITEMANAGER'
WHERE  u.[NormalizedUserName] = N'SITEMANAGER'
  AND  NOT EXISTS (
           SELECT 1 FROM [AspNetUserRoles] ur
           WHERE ur.[UserId] = u.[Id] AND ur.[RoleId] = r.[Id]
       );
IF @@ROWCOUNT > 0 PRINT 'Assigned role SiteManager to user sitemanager';
ELSE               PRINT 'Assignment already exists: sitemanager → SiteManager';

-- projectmanager → ProjectManager
INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
SELECT u.[Id], r.[Id]
FROM   [AspNetUsers]  u
JOIN   [AspNetRoles]  r ON r.[NormalizedName] = N'PROJECTMANAGER'
WHERE  u.[NormalizedUserName] = N'PROJECTMANAGER'
  AND  NOT EXISTS (
           SELECT 1 FROM [AspNetUserRoles] ur
           WHERE ur.[UserId] = u.[Id] AND ur.[RoleId] = r.[Id]
       );
IF @@ROWCOUNT > 0 PRINT 'Assigned role ProjectManager to user projectmanager';
ELSE               PRINT 'Assignment already exists: projectmanager → ProjectManager';

-- owner → Owner
INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
SELECT u.[Id], r.[Id]
FROM   [AspNetUsers]  u
JOIN   [AspNetRoles]  r ON r.[NormalizedName] = N'OWNER'
WHERE  u.[NormalizedUserName] = N'OWNER'
  AND  NOT EXISTS (
           SELECT 1 FROM [AspNetUserRoles] ur
           WHERE ur.[UserId] = u.[Id] AND ur.[RoleId] = r.[Id]
       );
IF @@ROWCOUNT > 0 PRINT 'Assigned role Owner to user owner';
ELSE               PRINT 'Assignment already exists: owner → Owner';

COMMIT TRANSACTION;

PRINT '';
PRINT '=== Seed complete ===';
PRINT 'Roles and users have been seeded successfully.';
PRINT 'All seed accounts use password: Dev1234!';
