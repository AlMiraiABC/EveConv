---
applyTo: "**/*.cs,**/*.csproj"
---
# Product Overview

EveConv is a conversational AI system built with a modular architecture focusing on knowledge management, memory, data storage and model executor capabilities.

This is backend system only, includes core, plugins, services and APIs.

## Core Components

Core components include interfaces, extenable points, common utilities, ...etc. Used for plugins and services.

- **Knowledge Management**: Knowledge base management.
  - **File extraction** Document content extraction. Supports multiple file types. E.g. docx, xslx, pdf, markdown and html.
  - **Knowledge processing**: Knowledge store(chunking and embedding) and query(search and reranking).
    - **Chunking** Based on extracted document, supports RAPTOR(Recursive Abstractive Processing for Tree Organized Retrieval), ST-RAPTOR(Semi-Structured Table Question Answering), Sentence splitting, Recursive character text splitting, Semantic chunking, Hybrid chunking, Intelligent chunking, ...
    - **Embedding** Based on chunked texts, supports local onnx model and remote API. Commonly, a dense vector is generated.
    - **Searching** Stored embedding vector to vector database(E.g. qdrant, sqlite-vector, pgvector) and searching.
    - **Reranking** Select top K items from search result. Supports local model or remote rerank(E.g. BGE-reranker, Qwen3-reranker), RRF(Reciprocal Rank Fusion), ...+
- **Memory System**: Conversational memory and context management.
  - **Recent memory**: Recent `N` chat messages in session scope. The whole messages in this should be provided in each context.
  - **Session memory**: All chat messages in session scope(include recent memory). Select top K related messages in context.
  - **Long-term memory**: User infomation(E.g. habits, preferences, relationships) that session irrelevant. Keep in `X` words. It should be provided in each context for all sessions.
- **Storage Layer**: Multi-modal storage including cache, vector, graph, and relational databases.
  - **Cache**: In-Memory KV cache for frequent queries, recent memory, llm kv-cache, ...
  - **Vector**: Vector storage for embeddings and related metadatas.
  - **Graph**: Knowledge graph for things and relationships.
  - **RDB**: Persistent storage and archives.
- **Model Executor**: Supports ONNX, Ollama, OpenAI compatible API for embedding, reranking, chat, ...
  - **ONNX**: Local model inference with ONNX runtime. Used for embedding, reranking, plan, ...
  - **Ollama**: Local model inference with Ollama. Used for chat, tool calling, ....
  - **Remote API**: Remote model inference with OpenAI compatible API. Used for complex tasks.
- **Connectors**: `Microsoft.Extensions.AI` connectors for seamless integration of various AI models and services.
- **Agents**: Agent system with tool calling capabilities for complex task execution.
  - **Pipeline**: Multi-step task execution with multiple tools.
  - **ReAct**: Reasoning and acting with interleaved thought and action steps.
  - **Prompt**: Dynamic prompt generation and management for effective model interactions.
- **Plugin**: Plugin system management
  - **Management**: Plugin lifecycle management including loading, configuration, installation, removal, and upgrading.
  - **Communicate Bus**: Facilitate communication and data exchange between plugins and core components.

## Plugability & Extensibility

Extendable plugin system for custom functionalities and integrations. Based on features above, the plugin system can be used to implement various scenarios. It could includes mutlple agents, function orchestration, specified algorithms, third-party services, ...etc.
- **Core Plugins**: Built-in plugins that couldn't remove. These cannot be used directly, but provide basic capabilities for other plugins and services.
- **Scenario Plugins**: Based on core components, implement specific scenario with optimized performance.
  - **General Scenario**: The basic function orchestration. It can be used for general scenario, but with limited optimization.
  - **Code Scenario**: Optimized for code related tasks. E.g. code generation, code explanation, code review, ...etc.

## Services

To build a complete product, combine core components and plugins to provide services.

## APIs

Provide Web API, gRPC API, ...etc for frontend and third-party integration. It is defined as a damon service to startup with system.

## Dependency Graph

```
┌─────────┐        ┌─────────┐
│  Core   ├───────►│  Plugin │
└────┬────┘        └─────┬───┘
     │                   │
     └─────────┬─────────┘
               │
         ┌─────▼─────┐
         │   Service │
         └─────┬─────┘
               │
         ┌─────▼─────┐
         │    API    │
         └───────────┘
```

## Features

1. Multimodal input. E.g. voice, image, text, video etc.
2. Local model inference. E.g. onnx, ollama, etc.
3. Remote LLM integration(OpenAI compatible API)
4. Memory system. Include recent memory, session memory and long-term memory.
5. Knowledge management to extra and search files. E.g. html, markdown, docx, xlsx, txt, etc. Embedding with RAPTOR, ST-RAPTOR or other algorithms.
6. Customizable. Extend and select model, model executor, database,... everything is easily.
