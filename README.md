# EVE Conversation

This project designed to chat with LLM.

## Features

1. Multimodal input. E.g. voice, image, text, video etc.
2. Local model inference. E.g. onnx, ollama, etc.
3. Remote LLM integration(OpenAI compatible API)
4. Memory system. Include recent memory, session memory and long-term memory.
5. Knowledge management to extra and search files. E.g. html, markdown, docx, xlsx, txt, etc. Embedding with RAPTOR, ST-RAPTOR or other algorithms.
6. Customizable. Extend and select model, model executor, database,... everything is easily.

## Tech

1. .NET 10
2. Cross platform, supports cloud and PC, especially on x86_64 windows and linux(optional MacOS and ARM)
3. Using rust for native libs.
4. Prioritize accuracy validation. Compare the results with python.
5. IDE: Visual Studio 2026 and VSCode

## Development

1. Execute `git config core.hooksPath githooks`.
2. Install .NET 10 SDK.
3. Install astral-uv if you want to run `Tests/Python`.
4. Install cargo(MSRV: 1.82) if you want to build native libs, and install cross if you want to build cross-platform native libs.
5. Run `dotnet build` to restore and build the solution.
