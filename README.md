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

## Endpoints
- POST /uploads/init (placeholder controller)

No logic implemented yet.
