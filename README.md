# BuilderAssistantApi

Scaffolded .NET 8 ASP.NET clean architecture project skeleton with Entity Framework Core.

## Structure
- src/Api (Web API project)
- src/Application (Application layer)
- src/Domain (Domain layer)
- src/Infrastructure (Infrastructure layer with EF Core)

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

No logic implemented yet.
