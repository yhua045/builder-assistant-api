# BuilderAssistantApi

Scaffolded .NET 8 ASP.NET clean architecture project skeleton with Entity Framework Core.

## Structure
- src/Api (Web API project)
- src/Application (Application layer)
- src/Domain (Domain layer)
- src/Infrastructure (Infrastructure layer with EF Core)

## Observability & Logging

The API uses Serilog for structured logging with correlation ID tracking for request tracing.

### Features
- **Structured Logging**: JSON-structured logs with Serilog
- **Correlation ID**: Each request gets a unique correlation ID for tracing
- **Multiple Sinks**: Console and file logging in development
- **Configuration-driven**: Easy to add third-party sinks via configuration

### Log Outputs
- **Console**: Formatted logs for development
- **File**: Rolling daily log files in `logs/` directory
- **Third-party sinks**: Easy to configure (see configuration section)

### Correlation ID
Requests automatically get a correlation ID that:
- Is generated as a short GUID if not provided
- Can be passed in via `X-Correlation-ID` header
- Is returned in response `X-Correlation-ID` header
- Appears in all log entries for the request

### Configuration

#### Adding Third-Party Log Sinks
To enable external logging services, add the appropriate NuGet package and update `appsettings.json`:

**Seq (Free Cloud/Self-hosted)**:
```bash
dotnet add package Serilog.Sinks.Seq
```

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "https://your-seq-instance.datalust.co",
          "apiKey": "your-api-key"
        }
      }
    ]
  }
}
```

**Loggly**:
```bash
dotnet add package Serilog.Sinks.Loggly
```

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Loggly",
        "Args": {
          "customerToken": "your-customer-token",
          "applicationName": "BuilderAssistantApi"
        }
      }
    ]
  }
}
```

**Papertrail**:
```bash
dotnet add package Serilog.Sinks.Syslog
```

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "UdpSyslog",
        "Args": {
          "host": "logs.papertrailapp.com",
          "port": 12345,
          "appName": "BuilderAssistantApi"
        }
      }
    ]
  }
}
```

#### Environment Variables
Use environment variables or user secrets for sensitive configuration:

```bash
# For local development
dotnet user-secrets set "Serilog:WriteTo:0:Args:apiKey" "your-secret-key"

# Or environment variables
export Serilog__WriteTo__0__Args__apiKey="your-secret-key"
```

## Database Setup

The project uses Entity Framework Core with SQL Server as the default provider (SQLite also supported for local development).

### Create Migration
To create a new migration after modifying domain entities:

```bash
dotnet ef migrations add <MigrationName> --project src/Infrastructure --startup-project src/Api -o Migrations
```

### Apply Migrations
To apply migrations and update the database:

```bash
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

### Configuration
Configure the database connection string in `src/Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=BuilderAssistantDb;Trusted_Connection=true;MultipleActiveResultSets=true"
  }
}
```

For SQLite (local development):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=builderassistant.db"
  }
}
```

## Local development with Docker

You can use the included `docker-compose.dev.yml` to run a local SQL Server, apply EF Core migrations, and start the API.

1. Start the stack (the `migrations` service will apply migrations before the API starts):

```bash
docker compose -f docker-compose.dev.yml up --build
```

2. The compose file uses a strong default SA password for local development. Replace `Your_password123` in `docker-compose.dev.yml` with a secure value in your environment if you prefer.

3. To run migrations manually (without Docker):

```bash
# Create a migration after changing domain models
dotnet ef migrations add <MigrationName> --project src/Infrastructure --startup-project src/Api -o Migrations

# Apply migrations to the local DB
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

## Endpoints
- POST /uploads/init (placeholder controller)
- GET /uploads/health (health check with logging demonstration)

No logic implemented yet.

## Correlation ID propagation

The API includes middleware that ensures every request has a correlation id and returns it in the response header `X-Correlation-ID`.

To propagate correlation ids to downstream HTTP calls, use the provided named HttpClient `propagatingClient` which automatically copies the header from the incoming request:

```csharp
var client = httpClientFactory.CreateClient("propagatingClient");
await client.GetAsync("https://downstream-service/api/...");
```

If you run the application via Docker Compose (`docker-compose.dev.yml`), the `migrations` service applies migrations before the API starts.
