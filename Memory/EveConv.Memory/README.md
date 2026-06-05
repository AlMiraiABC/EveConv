# EveConv.Memory — Three-Tier LLM Chat Memory System

Implements the EveConv memory system with three tiers:

- **Recent Memory**: Most recent N chat messages per session, cached + DB persisted
- **Session Memory**: Full message history with automatic LLM-based compaction
- **Long Memory**: Cross-session LLM-extracted user facts/habits/preferences

## Architecture

```
EveConv.Abstraction/Memory/          ← Public interfaces & contract models
EveConv.Memory/
  ├── Models/                        ← SqlSugar entities + MemoryMetadataContent
  ├── Services/                      ← TiktokenCounter, EveConvChatReducer, MemoryService
  ├── Managers/                      ← RecentMemoryManager, LongMemoryManager
  ├── Middleware/                    ← MemoryPersistingChatClient, LongMemoryChatClient
  ├── Stores/                        ← RdbSessionMemoryStore, InMemorySessionMemoryStore, EntityMapper
  └── Extensions/                    ← DI registration, ChatClientBuilder extensions
```

## Key Dependencies

| Package                   | Purpose                                                       |
|---------------------------|---------------------------------------------------------------|
| `Microsoft.Extensions.AI` | `IChatReducer`, `DelegatingChatClient`, `ChatClientBuilder`   |
| `SqlSugarCore`            | ORM for RDB persistence                                       |
| `EveConv.Abstraction`     | Cache interfaces, `ICache`, `IBasicCache<T>`, `IListCache<T>` |

> `EveConv.Memory` depends **only** on `EveConv.Abstraction` + NuGet packages. No references to `Reference/` projects. Infrastructure (cache, DB) is wired via DI.

## Configuration

```json
{
  "Memory": {
    "RecentMemoryCount": 20,
    "RecentMemoryCacheTtl": "01:00:00",
    "DefaultContextWindowTokens": 4096,
    "CompactReserveRecentCount": 5,
    "CompactionModelId": "gpt-4o-mini",
    "MaxCompactionLevel": 3,
    "LongMemoryExtractionModelId": "gpt-4o",
    "ExtractionSessionBatchSize": 10,
    "LongMemoryMaxTokens": 500,
    "ImportanceThreshold": 0.5,
    "LongMemory": {
      "OwnerKey": "default"
    }
  }
}
```

## DI Registration

```csharp
// In Program.cs:
builder.Services.AddEveConvMemory(builder.Configuration);
builder.Services.AddKeyedSingleton<IChatClient>("summarization", ...);
builder.Services.AddKeyedSingleton<IChatClient>("extraction", ...);

// In chat pipeline:
chatBuilder.UseMemory(serviceProvider);
```

## Cache Key Schema

| Pattern                       | Content                                            |
|-------------------------------|----------------------------------------------------|
| `recent:{sessionId}`          | Most recent N ChatMessage (JSON) — actively cached |
| `session:{sessionId}:meta`    | ChatSession metadata — DB-only                     |
| `compact:{sessionId}:{level}` | SessionCompaction — DB-only                        |
| `long:{ownerKey}:entries`     | LongMemoryEntry set — DB-only                      |
