# Project Structure

## Solution Organization

The EveConv solution follows a clean architecture pattern with clear separation of concerns:

```
EveConv/
├── EveConv.Abstraction/          # Core interfaces and contracts
├── EveConv.Api/                  # Web API entry point
├── Knowledge/                    # Knowledge management domain
│   ├── EveConv.FileExtractor/    # Document processing
│   └── EveConv.Knowledge/        # Knowledge operations
├── Memory/                       # Memory management domain
│   └── EveConv.Memory/           # Conversation memory
├── LLM/                          # LLM(Large Language Model) integration domain
│   ├── EveConv.TextGeneration/   # LLM text generation operations
│   └── EveConv.Embedding/        # LLM embedding operations
└── Reference/                    # Infrastructure implementations
    ├── EveConv.Cache/            # Caching layer
    ├── EveConv.GraphStorage/     # Graph database
    ├── EveConv.RdbStorage/       # Relational database
    └── EveConv.VectorStorage/    # Vector database
```

## Architecture Patterns

### Dependency Flow
- **EveConv.Abstraction**: Core interfaces (no dependencies)
- **Domain Projects**: Knowledge/, Memory/ (depend on Abstraction)
- **Infrastructure**: Reference/ projects (implement Abstraction interfaces)
- **API Layer**: EveConv.Api (orchestrates all layers)

### Naming Conventions
- **Interfaces**: Prefixed with `I` (e.g., `ICache`, `IMemory`, `IKnowledge`)
- **Projects**: Follow `EveConv.{Domain}` pattern
- **Namespaces**: Match project structure exactly

### Code Organization
- **Interfaces**: Define contracts in Abstraction project
- **Implementations**: Separate projects in Reference/ folder
- **Domain Logic**: Organized by business capability (Knowledge/, Memory/)
- **API Controllers**: RESTful endpoints in Controllers/ folder

## File Conventions
Based on .editorconfig