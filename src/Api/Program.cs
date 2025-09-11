using BuilderAssistantApi.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();

// Add Infrastructure services (for EF Core design-time support)
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

app.MapControllers();

app.Run();
