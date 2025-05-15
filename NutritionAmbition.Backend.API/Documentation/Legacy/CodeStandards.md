# NutritionAmbition Backend Code Standards

## Interface Organization

In the NutritionAmbition.Backend project, we follow the approach of keeping interfaces in the same file as their primary implementation:

- **Interfaces should be defined in the same file as their implementation class.**
- Example: `IProfileService` should be in the same file as `ProfileService.cs`
- This standard applies to all service, repository, and utility interfaces

### Why This Approach?

1. **Reduced File Proliferation**: Having one file per interface (plus one per implementation) leads to too many files
2. **Improved Discoverability**: Both the contract and implementation are visible in the same file
3. **Simplifies Refactoring**: When modifying a service, you only need to open one file to see/edit both interface and implementation
4. **Better Version Control**: Changes to both interface and implementation are recorded in the same commit

### Example:

```csharp
// ProfileService.cs
namespace NutritionAmbition.Backend.API.Services
{
    public interface IProfileService
    {
        Task<ProfileResponse> GetProfileAsync(string userId);
        Task<ProfileResponse> CreateProfileAsync(ProfileRequest request);
    }

    public class ProfileService : IProfileService
    {
        // Implementation
    }
}
```

## General Architectural Principles

We follow Clean Architecture with clear separation of concerns:

1. **Controllers**: Handle HTTP requests, validate inputs, authenticate users, return responses
2. **Services**: Contain all business logic, orchestrate work, handle errors
3. **Repositories**: Handle data persistence and retrieval
4. **Models**: Define domain entities
5. **DataContracts**: Define the Request/Response objects for communication between layers

## Code Structure

Each layer has specific responsibilities:

### Controllers
- Only handle HTTP request validation and calling appropriate services
- Return appropriate HTTP status codes and responses
- No business logic allowed

### Services
- Contain all business logic
- Use dependency injection to access repositories and other services
- Handle errors with try/catch blocks
- Return Response objects with success/error information

### Repositories
- Handle database operations (CRUD)
- Return domain models
- No business logic allowed

## Naming Conventions

- **Interfaces**: Start with "I" (e.g., `IProfileService`)
- **Classes**: Named after their purpose (e.g., `ProfileService`)
- **Methods**: Use verb-noun format (e.g., `GetProfileAsync`)
- **Parameters**: Use camelCase (e.g., `userId`)
- **Private fields**: Use underscore prefix (e.g., `_repository`)

## Error Handling

- All exceptions should be caught in the Service layer
- Use Response objects to communicate errors to the Controller
- Log all exceptions using Serilog
- Return user-friendly error messages to clients

## Asynchronous Programming

- Use async/await for all I/O operations (database, external APIs)
- Method names should have an "Async" suffix
- Always return Task or Task<T> from async methods
- Use ConfigureAwait(false) when appropriate 