using BuilderAssistantApi.Domain.Repositories;
using BuilderAssistantApi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuilderAssistantApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");
        }

        // Register DbContext with SQL Server as default provider
        services.AddDbContext<BuilderAssistantDbContext>(options =>
        {
            // Support both SQL Server and SQLite connection strings
            if (connectionString.Contains("Data Source=") && connectionString.Contains(".db"))
            {
                // SQLite connection string detected
                options.UseSqlite(connectionString);
            }
            else
            {
                // Default to SQL Server
                options.UseSqlServer(connectionString);
            }
        });

        // Register repositories
        services.AddScoped<IImageRepository, EfImageRepository>();

        return services;
    }
}