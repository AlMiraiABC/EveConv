# Project Structure

## Solution Organization

The EveConv solution follows a clean architecture pattern with clear separation of concerns:

```sh
EveConv/
├── EveConv.Abstraction/          # Core interfaces and contracts
├── EveConv.Api/                  # Web API entry point
├── Knowledge/                    # Knowledge management domain
│   └── EveConv.DocDecder/        # Document processing service
├── ModelExecutor/                # Model execution implementations
│   └── EveConv.Onnx/             # ONNX model executor service
├── Tokenizer/                    # Tokenizer implementations
│   └── EveConv.HuggingFaceFastTokenizer/ # HuggingFace tokenizer service
└── Reference/                    # Infrastructure implementations
    ├── Cache/                    # Caching layer
    │   ├── EveConv.Cache.InMemory/ # In-Memory cache used for development.
    │   ├── EveConv.Cache.Redis/  # Redis cache service
    ├── EveConv.GraphStorage/     # Graph database
    ├── EveConv.RdbStorage/       # Relational database
    ├── VectorStorage/            # Vector database
    │   └── EveConv.VectorStorage.Qdrant/ # Qdrant vector database service
    ├── FileStorage/              # File storage services
    ├── EveConv.Downloader/       # File downloading services
    ├── Helper/                   # Helper utilities
    │   ├── EveConv.HuggingFaceFastTokenizer.Raw/ # HuggingFace tokenizer C-API wrapper
    │   ├── hf-tokenizer-c/       # HuggingFace tokenizer C-API(Rust)
    │   └── EveConv.S3Helper/     # S3 compatible storage helper
├── Tests/                        # Unit tests for various components
```

## Architecture Patterns

### Dependency Flow

- **EveConv.Abstraction**: Core interfaces (no dependencies)
- **Domain Projects**: Implements (depend on Abstraction and `Reference/` projects)
- **Infrastructure**: `Reference/` that business independent projects to utilize common functionalities and infrastructure.

### Naming Conventions

- **Interfaces**: Prefixed with `I` (e.g., `ICache`, `IMemory`, `IKnowledge`)
- **Projects**: Follow `EveConv.{Domain}` pattern
- **Namespaces**: Match project structure exactly

### Code Organization

- **Interfaces**: Define contracts in Abstraction project
- **Implementations**: Separate projects for each implementation. If it contains multiple related implementations, group them in a folder (e.g., VectorStorage/, Tokenizer/)
- **Domain Logic**: Organized by business capability (e.g., Tokenizer/, ModelExecutor/)

## File Conventions

Based on .editorconfig
