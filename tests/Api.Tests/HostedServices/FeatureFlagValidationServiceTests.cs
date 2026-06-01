using System;
using System.Threading;
using System.Threading.Tasks;
using BuilderAssistantApi.Api.HostedServices;
using BuilderAssistantApi.Domain.Constants;
using BuilderAssistantApi.Domain.Entities;
using BuilderAssistantApi.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BuilderAssistantApi.Api.Tests.HostedServices;

public class FeatureFlagValidationServiceTests
{
    private readonly Mock<ILogger<FeatureFlagValidationService>> _mockLogger;

    public FeatureFlagValidationServiceTests()
    {
        _mockLogger = new Mock<ILogger<FeatureFlagValidationService>>();
    }

    private BuilderAssistantDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<BuilderAssistantDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new BuilderAssistantDbContext(options);
    }

    [Fact]
    public async Task StartAsync_WhenAllKeysPresent_CompletesWithoutError()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateInMemoryDbContext(dbName);
        
        // Seed required keys
        foreach (var key in FeatureKeys.All)
        {
            dbContext.Features.Add(new Feature { Key = key, Description = key });
        }
        await dbContext.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(dbContext);
        var serviceProvider = services.BuildServiceProvider();

        var service = new FeatureFlagValidationService(serviceProvider, _mockLogger.Object);

        // Act & Assert
        await service.StartAsync(CancellationToken.None);
        // It shouldn't throw an exception because all keys are there.
    }

    [Fact]
    public async Task StartAsync_WhenKeysMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateInMemoryDbContext(dbName);
        
        // Seed partial keys to deliberately cause a failure
        // Assuming we always have at least 1 feature required otherwise this test might not fail
        // If there's multiple required features, just don't add any.
        await dbContext.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(dbContext);
        var serviceProvider = services.BuildServiceProvider();

        var service = new FeatureFlagValidationService(serviceProvider, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(CancellationToken.None));
    }
}
