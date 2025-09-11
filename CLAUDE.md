# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET 9 ASP.NET Core Web API project following Clean Architecture principles. The project is a Builder Assistant API that appears to be scaffolded but has minimal implementation.

## Project Structure

The solution follows a layered architecture pattern:

- **src/Api** - Web API layer (ASP.NET Core controllers, Program.cs)
- **src/Application** - Application layer (services, business logic interfaces)
- **src/Domain** - Domain layer (entities, core business objects)

### Domain Entities
The domain contains these main entities with relationships:
- **User** - Core user entity with email as business key
- **Project** - User-owned projects that contain prompts and images
- **Prompt** - Text prompts associated with projects
- **Image** - Images with categories, linked to projects
- **ImageCategory** - Categories for organizing images

All entities use `long` for primary keys and include `DateTimeOffset` for timestamp fields.

## Development Commands

### Building and Running
```bash
# Build the entire solution
dotnet build

# Run the API project
dotnet run --project src/Api

# Build specific project
dotnet build src/Api
dotnet build src/Application
dotnet build src/Domain
```

### Development Environment
- **Framework**: .NET 9
- **Nullable Reference Types**: Enabled
- **Implicit Usings**: Enabled

## Architecture Notes

- The API layer references Application layer
- Application layer references Domain layer
- Domain layer has no dependencies (Clean Architecture)
- All projects use `BuilderAssistantApi.*` namespace structure
- Current implementation is minimal with placeholder interfaces and a single controller endpoint (`POST /uploads/init`)

## Current State

This is a scaffolded project with:
- Basic entity definitions in Domain layer
- Placeholder `IUploadService` interface in Application layer
- Single `UploadsController` in API layer
- Minimal Program.cs setup with just controller mapping

No database, authentication, or business logic implementation yet.