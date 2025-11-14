# Technology Stack

## Framework & Runtime
- **.NET 10.0**: Target framework for all projects
- **ASP.NET Core**: Web API framework with Swagger/OpenAPI support
- **C#**: Primary programming language with nullable reference types enabled

## Key Libraries & Packages
- **Swashbuckle.AspNetCore**: API documentation and testing interface
- **Microsoft.Extensions.Configuration**: Configuration management
- **Microsoft.Extensions.Logging**: Structured logging with console output
- **Microsoft.Extensions.DependencyInjection**: Built-in IoC container

## Build System
- **MSBuild**: Standard .NET build system via `dotnet` CLI
- **Visual Studio Solution**: Multi-project solution structure

## Common Commands

### Build & Run
```bash
# Build entire solution
dotnet build

# Run API project
dotnet run --project EveConv.Api

# Build specific project
dotnet build --project <project-name>

# Restore packages
dotnet restore
```

### Development
```bash
# Watch for changes and auto-rebuild or hot-reload
dotnet watch --project EveConv.Api

# Run tests (when available)
dotnet test

# Clean build artifacts
dotnet clean
```

## Code Standards
- **Nullable Reference Types**: Enabled across all projects
- **Implicit Usings**: Enabled for cleaner code
- **Async/Await**: Preferred for I/O operations

## Code Rules
- **Logging**: Add optional ILoggerFactory? to constructor if it should injected and set as below
  ```cs
  using EveConv.Abstraction.Diagnostic;
  private readonly ILogger _logger;
  public SomeClass(ILogger? logger)
  {
    _logger = logger ?? DefaultLogger<SomeClass>.Instance;
  }
 
  ```