# Copilot Instructions

This file provides context and guidance for GitHub Copilot coding agent when working on tasks in this repository.

## Project Overview

This is a .NET-focused repository that emphasizes Domain-Driven Design (DDD), SOLID principles, and modern C# practices. When working on any task, Copilot should respect the architectural patterns and coding standards established in this repository.

## Development Guidelines

### Coding Standards

- Follow C# 14 features and modern language constructs
- Use PascalCase for public members and methods; camelCase for private fields and local variables
- Prefix interfaces with "I" (e.g., `IUserService`)
- Use file-scoped namespace declarations
- Apply code formatting defined in `.editorconfig` when present
- Prefer pattern matching and switch expressions
- Use `nameof` instead of hardcoded strings for member names

### Architecture Patterns

- Follow Domain-Driven Design (DDD) principles
- Maintain clear separation of concerns (Domain, Application, Infrastructure layers)
- Use dependency injection for loose coupling
- Implement repository pattern for data access
- Use async/await for I/O-bound operations

### Testing Standards

- Use the naming pattern: `MethodName_Condition_ExpectedResult()`
- Include unit tests for domain logic
- Include integration tests for cross-layer functionality
- Target minimum 85% code coverage for domain and application layers

## Repository Structure

- `.github/agents/` - Custom agent definitions with specialized capabilities
- `.github/instructions/` - Path-specific coding instructions (e.g., C# guidelines, DDD architecture)
- `.github/prompts/` - Reusable prompt templates for common scenarios

## Commands and Tools

### Build and Test

Before proposing changes, ensure:

```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build --no-restore

# Run tests
dotnet test --no-build
```

### Code Quality

```bash
# Format code
dotnet format

# Run analyzers
dotnet build --no-restore /warnaserror
```

## Boundaries

### Do Not Modify

- Configuration files containing secrets or sensitive data
- Production deployment configurations without explicit approval
- Third-party library code in vendor directories

### Special Considerations

- Always preserve existing test coverage when refactoring
- Ensure backward compatibility for public APIs
- Follow security best practices (input validation, proper authentication/authorization)
- Use `decimal` for monetary calculations

## Communication

When implementing changes:

1. Explain the approach before making modifications
2. Reference relevant DDD patterns and SOLID principles
3. Document any architectural decisions
4. Highlight potential breaking changes
5. Request clarification when requirements are ambiguous

## Additional Resources

For more detailed guidance, see:

- `.github/instructions/csharp.instructions.md` - C# specific guidelines
- `.github/instructions/dotnet-architecture-good-practices.instructions.md` - DDD and architecture guidelines
- `.github/agents/expert-dotnet-software-engineer.agent.md` - Expert .NET engineering guidance
