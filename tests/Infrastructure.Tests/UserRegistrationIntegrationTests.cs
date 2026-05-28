using BuilderAssistantApi.Application.Ports;
using BuilderAssistantApi.Application.Services;
using BuilderAssistantApi.Domain.Entities;
using BuilderAssistantApi.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BuilderAssistantApi.Infrastructure.Tests;

/// <summary>
/// Integration tests that run against a real SQLite in-memory database with the full
/// ASP.NET Core Identity stack. These tests verify end-to-end service behaviour —
/// not just mock interactions.
/// </summary>
public sealed class UserRegistrationIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IServiceProvider _provider;

    public UserRegistrationIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();

        services.AddDbContext<BuilderAssistantDbContext>(options =>
            options.UseSqlite(_connection));

        services.AddIdentity<User, IdentityRole<long>>()
                .AddEntityFrameworkStores<BuilderAssistantDbContext>()
                .AddDefaultTokenProviders();

        services.AddLogging();

        // Use a spy email sender so we can capture the confirmation token
        services.AddScoped<IEmailSender, SpyEmailSender>();
        services.AddScoped<IUserRegistrationService, UserRegistrationService>();

        _provider = services.BuildServiceProvider();

        // Create the schema once
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuilderAssistantDbContext>();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private IServiceScope CreateScope() => _provider.CreateScope();

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_FullFlow_UserAndRolePersisted()
    {
        using var scope = CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserRegistrationService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var result = await service.RegisterAsync(new RegisterRequest("owner@example.com", "Password1!"));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);

        // User persisted in the database
        var savedUser = await userManager.FindByEmailAsync("owner@example.com");
        Assert.NotNull(savedUser);
        Assert.Equal("owner@example.com", savedUser.Email);

        // Owner role assigned
        var roles = await userManager.GetRolesAsync(savedUser);
        Assert.Contains("Owner", roles);
    }

    [Fact]
    public async Task ConfirmEmail_AfterRegistration_EmailConfirmedTrue()
    {
        using var scope = CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserRegistrationService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var spySender = (SpyEmailSender)scope.ServiceProvider.GetRequiredService<IEmailSender>();

        // Register — email not confirmed yet
        var registerResult = await service.RegisterAsync(new RegisterRequest("confirm@example.com", "Password1!"));
        Assert.True(registerResult.Succeeded);

        var user = await userManager.FindByEmailAsync("confirm@example.com");
        Assert.NotNull(user);
        Assert.False(user.EmailConfirmed);

        // Confirm using the captured token (extracted from body: "Email confirmation token: {token}")
        var lastBody = spySender.LastBody!;
        Assert.NotNull(lastBody);
        const string prefix = "Email confirmation token: ";
        var token = lastBody[prefix.Length..];

        var confirmResult = await service.ConfirmEmailAsync(user.Id, token);
        Assert.True(confirmResult.Succeeded);

        // Re-fetch and verify
        var confirmedUser = await userManager.FindByEmailAsync("confirm@example.com");
        Assert.NotNull(confirmedUser);
        Assert.True(confirmedUser.EmailConfirmed);
    }

    /// <summary>
    /// Verifies the pre-condition for the AuthorizationController email guard:
    /// a newly registered (unconfirmed) account has EmailConfirmed = false.
    /// The AuthorizationController.Exchange() checks IsEmailConfirmedAsync and
    /// returns Forbid for unconfirmed accounts, blocking token issuance.
    /// Full HTTP pipeline testing requires WebApplicationFactory (out of scope here).
    /// </summary>
    [Fact]
    public async Task PasswordGrantBlocked_UnconfirmedEmail_UserHasEmailConfirmedFalse()
    {
        using var scope = CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserRegistrationService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var result = await service.RegisterAsync(new RegisterRequest("unconfirmed@example.com", "Password1!"));
        Assert.True(result.Succeeded);

        var user = await userManager.FindByEmailAsync("unconfirmed@example.com");
        Assert.NotNull(user);

        // The AuthorizationController checks this before issuing a token
        var isEmailConfirmed = await userManager.IsEmailConfirmedAsync(user);
        Assert.False(isEmailConfirmed, "Unconfirmed user must not pass the email confirmation guard in AuthorizationController.");
    }

    // ── Spy email sender ──────────────────────────────────────────────────────

    private sealed class SpyEmailSender : IEmailSender
    {
        public string? LastBody { get; private set; }

        public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
        {
            LastBody = body;
            return Task.CompletedTask;
        }
    }
}
