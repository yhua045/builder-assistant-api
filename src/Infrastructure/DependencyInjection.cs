using System.Net.Http.Headers;
using BuilderAssistantApi.Domain.Repositories;
using BuilderAssistantApi.Infrastructure.Options;
using BuilderAssistantApi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
            
            // Register OpenIddict entity sets
            options.UseOpenIddict();
        });

        // Register Identity
        services.AddIdentity<BuilderAssistantApi.Domain.Entities.User, Microsoft.AspNetCore.Identity.IdentityRole<long>>()
            .AddEntityFrameworkStores<BuilderAssistantDbContext>()
            .AddDefaultTokenProviders();

        // Configure OpenIddict
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                       .UseDbContext<BuilderAssistantDbContext>();
            })
            .AddServer(options =>
            {
                // Enable the token endpoint.
                options.SetTokenEndpointUris("connect/token");

                // Enable the password flow.
                options.AllowPasswordFlow()
                       .AllowRefreshTokenFlow();

                // Accept anonymous clients (i.e. clients that don't send a client_id)
                options.AcceptAnonymousClients();

                // Register the signing and encryption credentials.
                // Replace with persistent keys in production (e.g., UseX509Certificate)
                options.AddDevelopmentEncryptionCertificate()
                       .AddDevelopmentSigningCertificate();

                // Register the ASP.NET Core host and configure the ASP.NET Core options.
                options.UseAspNetCore()
                       .EnableTokenEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                // Import the configuration from the local OpenIddict server instance.
                options.UseLocalServer();

                // Register the ASP.NET Core host.
                options.UseAspNetCore();
            });

        // Register repositories
        services.AddScoped<IImageRepository, EfImageRepository>();

        // Groq options — validated at startup (app fails fast if ApiKey is missing)
        services.AddOptions<GroqOptions>()
            .BindConfiguration(GroqOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Groq named HttpClient — base address, auth header, timeout, resilience pipeline.
        // The CorrelationIdPropagationHandler is added in Program.cs (it lives in the Api layer).
        services.AddHttpClient("groq", (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<GroqOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", opts.ApiKey);
        })
        .AddStandardResilienceHandler();

        // Register Third-party services
        services.AddScoped<BuilderAssistantApi.Application.Interfaces.IGroqService, BuilderAssistantApi.Infrastructure.Services.GroqService>();
        services.AddScoped<BuilderAssistantApi.Application.Interfaces.ITelemetryService, BuilderAssistantApi.Infrastructure.Services.TelemetryService>();

        return services;
    }
}