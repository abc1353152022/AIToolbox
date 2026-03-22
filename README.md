# README.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run

```bash
dotnet build AIToolbox/AIToolbox.csproj
dotnet run --project AIToolbox
```

## Architecture

**AIToolbox** is a .NET 10.0 console application providing a unified interface for AI model providers.

### Core Components

- **Program.cs** - Entry point with CLI loop, commands (`/new`, `/model`, `/clear`, `/provider`, `/stream`, `/retry`, `/info`, `/stats`, `/history`, `/copy`, `/export`, `/help`, `/quit`), and conversation history.

- **Services/IAIService.cs** - Interface: `SendMessageAsync`, `SendMessageStreamAsync`, `GetAvailableModelsAsync`, `GetServiceInfo`, `TestConnectionAsync`, `UpdateConfig`.

- **Services/BaseAIService.cs** - Abstract base with shared HTTP logic (`PostRequestAsync`, `GetRequestAsync`), SSE stream processing (`ProcessStreamResponseAsync`), API key/base URL management.

- **Services/AIServiceFactory.cs** - Factory for creating service instances. Supports: Ollama, Aitools, DeepSeek, OpenAI.

- **Services/*Service.cs** - Concrete implementations (OllamaService, AitoolsService, DeepSeekService, OpenAIService), each inheriting from BaseAIService.

- **Models/** - DTOs: `ChatRequest`, `ChatResponse`, `Message`, `StreamChunk`, `ModelInfo`, `ServiceInfo`, `AppSettings`.

- **Utils/ConsoleHelper.cs** - Console UI utilities for colored output.

### Adding a New Provider

1. Create `Services/[Provider]Service.cs` inheriting from `BaseAIService`
2. Implement abstract methods: `SendMessageAsync`, `SendMessageStreamAsync`, `GetAvailableModelsAsync`, `GetServiceInfo`, `TestConnectionAsync`
3. Add factory case in `AIServiceFactory.Create`
4. Add provider config in `appsettings.json`
