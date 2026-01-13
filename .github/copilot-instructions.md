# Copilot Instructions for EveConv Codebase

## Overview
The EveConv project is a chat backend that supports multi-modal input, local model inference, multi-level memory management, customization and scalability.

Structured into multiple components that interact to provide a cohesive service. Understanding the architecture and workflows is crucial for effective contributions.

For more detail instructions, refer to [instructions](./instructions/) folder.

## Architecture
- **Core**:
  - **EveConv.Api**: The entry point for API interactions, built using ASP.NET Core. It handles incoming requests and routes them to appropriate services.
  - **EveConv.Abstraction**: Contains interfaces and base classes that define the core functionalities and abstractions used across the project.

- **Services**: Each component is designed to encapsulate specific functionalities, promoting separation of concerns. For example, the `EveConv.Abstraction` project defines interfaces that are implemented in other projects, ensuring a clear contract for interactions.

- **References** Business independent projects to utilize common functionalities and infrastructure.

- **Tests**: Each component has a corresponding test project and created in `Tests/` folder. (e.g., `EveConv.Abstraction.Tests`) that uses xUnit for unit testing.

For more details, refer to the [Product Overview](./instructions/product.instructions.md) documentation.

## Developer Workflows
- **Building the Project**: Use the command `dotnet build` from the root directory to compile all projects. Ensure that all dependencies are restored first with `dotnet restore`.

- **Running Tests**: Tests can be executed using `dotnet test` within the respective test project directories. The test projects are configured to use xUnit, which allows for easy integration with CI/CD pipelines.

- **Debugging**: Set breakpoints in Visual Studio or use `dotnet watch` to run the application in watch mode, which automatically rebuilds and restarts the application on file changes.

## Project-Specific Conventions
- **Nullable Reference Types**: The project uses nullable reference types to enhance code safety. Ensure to handle nullability appropriately in your implementations.

- **Implicit Usings**: The project leverages implicit usings to reduce boilerplate code. Familiarize yourself with the namespaces included by default.

- **Configuration Management**: Configuration settings are managed through `appsettings.json` files, with environment-specific overrides in `appsettings.Development.json`.

## Integration Points
- **External Dependencies**: Using Nuget CPM(Central Package Manager) to manage external libraries. Ensure to keep dependencies updated and compatible.

- **Cross-Component Communication**: Components communicate through interfaces defined in the `EveConv.Abstraction` project. For example, the `IDownloader` interface is implemented by various downloader services, allowing for flexible integration.

- **Soluction Folder** The soluction folder is mapping to actual system folders.

- **Cross-Platform Support**: The project is designed to run on multiple platforms, including Windows and Linux. Ensure that any platform-specific code is properly abstracted.

- **Documentation**: Each folder and project should contains a README file, and public class and methods should has documentation comments. Maintain up-to-date documentation for any new features or changes in the architecture to facilitate onboarding and collaboration.

## Key Files and Directories
- **EveConv.Api**: Main HTTP API project.
- **EveConv.Abstraction**: Contains core interfaces and abstractions.
- **Tests**: Unit tests for various components.

For more details, refer to the [Project Structure](./instructions/structure.instructions.md) documentation.
