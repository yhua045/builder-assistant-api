using BuilderAssistantApi.Domain.Constants;
using BuilderAssistantApi.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BuilderAssistantApi.Api.HostedServices;

public class FeatureFlagValidationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FeatureFlagValidationService> _logger;

    public FeatureFlagValidationService(IServiceProvider serviceProvider, ILogger<FeatureFlagValidationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BuilderAssistantDbContext>();

        try
        {
            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                _logger.LogWarning("Database is not accessible. Skipping feature flag validation.");
                return;
            }

            var dbFeatures = await dbContext.Features.Select(f => f.Key).ToListAsync(cancellationToken);
            var missingFeatures = FeatureKeys.All.Except(dbFeatures).ToList();
            
            if (missingFeatures.Any())
            {
                var error = $"The following required feature keys are missing from the database: {string.Join(", ", missingFeatures)}";
                _logger.LogError(error);
                throw new InvalidOperationException(error);
            }

            _logger.LogInformation("All required feature flags ({Count}) are present in the database.", FeatureKeys.All.Count);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogWarning(ex, "Failed to validate feature flags (e.g. database not migrated yet). Skipping feature flag validation.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}