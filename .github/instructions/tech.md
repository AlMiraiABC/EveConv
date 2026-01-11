# Technology Stack

## Framework & Runtime

- **.NET 10.0**: Target framework for all projects
- **ASP.NET Core**: Web API framework with Swagger/OpenAPI support
- **C#**: Primary programming language with nullable reference types enabled
- **Rust**: For native libraries (e.g., HuggingFace tokenizer C-API)

## Key Libraries & Packages

Dependent on Nuget CPM(Central Package Manager)

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

# Build specific project
dotnet build --project <project-name>

# Restore packages
dotnet restore
```

### Development

```bash
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
  public SomeClass(ILoggerFactory? loggerFactory)
  {
    _logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<SomeClass>();
  }
  ```
