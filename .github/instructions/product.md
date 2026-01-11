# Product Overview

EveConv is a conversational AI system built with a modular architecture focusing on knowledge management, memory, data storage and model executor capabilities.

## Core Components

- **Knowledge Management**: Knowledge base management.
  - **File extraction** Document content extraction. Supports multiple file types. E.g. docx, xslx, pdf, markdown and html.
  - **Knowledge processing**: Knowledge store(chunking and embedding) and query(search and reranking).
    - **Chunking** Based on extracted document, supports RAPTOR(Recursive Abstractive Processing for Tree Organized Retrieval), ST-RAPTOR(Semi-Structured Table Question Answering), Sentence splitting, Recursive character text splitting, Semantic chunking, Hybrid chunking, Intelligent chunking, ...
    - **Embedding** Based on chunked texts, supports local onnx model and remote API. Commonly, a dense vector is generated.
    - **Searching** Stored embedding vector to vector database(E.g. qdrant, sqlite-vector, pgvector) and searching.
    - **Reranking** Select top K items from search result. Supports local model or remote rerank(E.g. BGE-reranker, Qwen3-reranker), RRF(Reciprocal Rank Fusion), ...+
  - **Coding**: A specified knowledge base that need process by syntax.
    - **AST parser**: Abstract Syntax Tree. Detect programming language and parse code files to AST and keep source mapping.
    - **Semantic process**: Analysis AST to got reference relationships of projects, models, files, classes, methods, vairables and comments.
    - **Store**: Indexing and store semantic graph and AST tree by identifiers and embeddings.
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

## Features

1. Multimodal input. E.g. voice, image, text, video etc.
2. Local model inference. E.g. onnx, ollama, etc.
3. Remote LLM integration(OpenAI compatible API)
4. Memory system. Include recent memory, session memory and long-term memory.
5. Knowledge management to extra and search files. E.g. html, markdown, docx, xlsx, txt, etc. Embedding with RAPTOR, ST-RAPTOR or other algorithms.
6. Customizable. Extend and select model, model executor, database,... everything is easily.
