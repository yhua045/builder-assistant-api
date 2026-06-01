using BuilderAssistantApi.Domain.Constants;
using BuilderAssistantApi.Domain.Entities;
using BuilderAssistantApi.Infrastructure.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuilderAssistantApi.Infrastructure;

public sealed class RoleSeedWorker : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RoleSeedWorker> _logger;

    public RoleSeedWorker(IServiceProvider serviceProvider, ILogger<RoleSeedWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<long>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var seedOptions = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;

        // Idempotently seed all application roles
        foreach (var roleName in ApplicationRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<long> { Name = roleName });
                if (result.Succeeded)
                    _logger.LogInformation("Created role '{Role}'.", roleName);
                else
                    _logger.LogWarning("Failed to create role '{Role}': {Errors}.", roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        // Optionally bootstrap an admin user
        if (!string.IsNullOrWhiteSpace(seedOptions.AdminEmail))
        {
            var existing = await userManager.FindByEmailAsync(seedOptions.AdminEmail);
            if (existing is null)
            {
                if (string.IsNullOrWhiteSpace(seedOptions.AdminPassword))
                {
                    _logger.LogWarning("Seed:AdminEmail is set but Seed:AdminPassword is missing — skipping admin user creation.");
                    return;
                }

                var adminUser = new User
                {
                    UserName = seedOptions.AdminEmail,
                    Email = seedOptions.AdminEmail,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(adminUser, seedOptions.AdminPassword);
                if (!createResult.Succeeded)
                {
                    _logger.LogWarning("Failed to create admin user '{Email}': {Errors}.", seedOptions.AdminEmail,
                        string.Join(", ", createResult.Errors.Select(e => e.Description)));
                    return;
                }

                await userManager.AddToRoleAsync(adminUser, ApplicationRoles.Admin);
                _logger.LogInformation("Seeded admin user '{Email}'.", seedOptions.AdminEmail);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
