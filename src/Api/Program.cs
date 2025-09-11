using BuilderAssistantApi.Api.Middleware;
using BuilderAssistantApi.Infrastructure;
using Serilog;

// Configure Serilog early to capture startup logs
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting Builder Assistant API");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog from configuration
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

    // Add services
    builder.Services.AddControllers();
    // HttpContext accessor for correlation propagation
    builder.Services.AddHttpContextAccessor();
    // Register propagation handler
    builder.Services.AddTransient<BuilderAssistantApi.Api.Http.CorrelationIdPropagationHandler>();
    // Example named HttpClient that will propagate correlation id
    builder.Services.AddHttpClient("propagatingClient").AddHttpMessageHandler<BuilderAssistantApi.Api.Http.CorrelationIdPropagationHandler>();

    // Add Infrastructure services (for EF Core design-time support)
    builder.Services.AddInfrastructureServices(builder.Configuration);

    var app = builder.Build();

    // Add correlation ID middleware
    app.UseMiddleware<CorrelationIdMiddleware>();

    // Configure request logging
    app.UseSerilogRequestLogging();

    app.MapControllers();

    Log.Information("Builder Assistant API started successfully");
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
