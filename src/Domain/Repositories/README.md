# Repository Pattern

This directory contains repository interfaces that define the contracts for data access operations in the domain layer.

## Overview

The Repository pattern provides an abstraction layer between the domain logic and data persistence. This approach:

- **Keeps the Domain layer independent** of infrastructure concerns like Entity Framework or database specifics
- **Enables testability** by allowing repositories to be easily mocked in unit tests
- **Provides consistency** across different entity data access patterns
- **Supports dependency inversion** principle by depending on abstractions rather than concrete implementations

## Repository Responsibilities

Each repository interface should:

1. **Define CRUD operations** specific to the entity's business needs
2. **Use domain entities** as parameters and return types
3. **Include appropriate async methods** with CancellationToken support
4. **Avoid infrastructure dependencies** - no EF Core types or database-specific concerns
5. **Focus on domain operations** rather than generic data access

## Implementation Guidelines

### Interface Design
- Place interfaces in `src/Domain/Repositories/`
- Use descriptive method names that reflect domain operations
- Include XML documentation for all public methods
- Support async operations with `CancellationToken` parameters

### Concrete Implementations
- Implement in `src/Infrastructure/Repositories/`
- Register in `src/Infrastructure/DependencyInjection.cs`
- Use EF Core DbContext for data access
- Keep implementations thin - delegate business logic to domain services

## Testing Repositories

When testing repository implementations:

```csharp
// Example of mocking IImageRepository in unit tests
var mockRepository = new Mock<IImageRepository>();
mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
          .ReturnsAsync(new Image { Id = 1, FileName = "test.png" });

// Use the mock in your service tests
var service = new ImageService(mockRepository.Object);
```

For integration testing of concrete repository implementations, use in-memory databases:

```csharp
var options = new DbContextOptionsBuilder<BuilderAssistantDbContext>()
    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
    .Options;
```

## Current Repositories

- `IImageRepository` - Manages Image entity persistence operations