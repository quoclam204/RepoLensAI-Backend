# Clean Architecture & Evidence Rules

## Clean Architecture Layering & Reference Model

RepoLensAI-Backend strictly enforces Clean Architecture and Ports & Adapters rules:

```
┌───────────────────────────────────────────────────────────────┐
│                         RepoLens.Api                          │ (Presentation, Minimal APIs / Controllers, DI root)
└───────────────┬───────────────────────────────┬───────────────┘
                │                               │
                ▼                               │
┌───────────────────────────────┐               │
│    RepoLens.Infrastructure    │               │
│ (Postgres, EF Core, Git, I/O, │               │
│   Analysis Engine Adapters)   │               │
└───────┬───────────────┬───────┘               │
        │               │                       │
        │               ▼                       ▼
        │      ┌────────────────────────────────────────────────┐
        │      │              RepoLens.Application             │ (Use cases, Ports / Interfaces, DTOs)
        │      └────────────────────────┬───────────────────────┘
        ▼                               │
┌───────────────────────────────┐       │
│       RepoLens.Analysis       │       │
│  (Roslyn AST, Static Engine)  │       │
└───────────────┬───────────────┘       │
                │                       │
                ▼                       ▼
┌───────────────────────────────────────────────────────────────┐
│                        RepoLens.Domain                        │ (Entities, Value Objects, Enums, Exceptions)
└───────────────────────────────────────────────────────────────┘
```

### Reference Rules Matrix

| Project | Layer | Allowed References | Forbidden References | Role |
| :--- | :---: | :--- | :--- | :--- |
| **`RepoLens.Domain`** | 0 | *None* | All projects & external libraries | Pure domain concepts, value objects, domain exceptions |
| **`RepoLens.Application`** | 1 | `RepoLens.Domain` | `Infrastructure`, `Analysis`, `Api` | Use cases, ports/abstractions, DTOs, orchestrators |
| **`RepoLens.Analysis`** | 1 | `RepoLens.Domain` | `Application`, `Infrastructure`, `Api` | Roslyn AST engine, static syntax visitors, extractors |
| **`RepoLens.Infrastructure`**| 2 | `Application`, `Domain`, `Analysis` | `Api` | Database, external services, and adapters for Analysis ports |
| **`RepoLens.Api`** | 3 | `Application`, `Infrastructure` | `Domain`, `Analysis` | HTTP routing, middleware, composition root |

### Layer Constraints & Roles
1. **RepoLens.Domain**:
   - MUST NOT reference any project or third-party infrastructure package.
   - Contains pure business logic, domain entities, value objects, enums, and domain exceptions.
2. **RepoLens.Application**:
   - References ONLY `RepoLens.Domain`.
   - Defines ports (interfaces) for analysis, repositories, storage, and AI retrieval.
   - Orchestrates use cases; independent of frameworks, databases, and UI.
3. **RepoLens.Analysis**:
   - References ONLY `RepoLens.Domain`.
   - Independent static analysis engine: Roslyn AST parsing, syntax traversal, symbol discovery.
   - Does NOT depend on Application or Infrastructure directly.
4. **RepoLens.Infrastructure**:
   - References `RepoLens.Application`, `RepoLens.Domain`, and is permitted to reference `RepoLens.Analysis`.
   - Acts as the Adapter layer: implements Application ports (e.g. `IRepositoryAnalyzer`), wrapping and invoking `RepoLens.Analysis` components.
   - Handles persistence (PostgreSQL / EF Core), Git cloning, and safe file/archive extraction.
5. **RepoLens.Api**:
   - References ONLY `RepoLens.Application` and `RepoLens.Infrastructure`.
   - Acts as the composition root, configuring dependency injection. Does NOT directly reference `RepoLens.Domain` or `RepoLens.Analysis`.

---

## Evidence-First Principle

> **"Static analysis establishes what actually exists. AI explains the evidence."**

1. **Ground Truth**: The analyzed repository's source code is the sole ground truth.
2. **Verifiability**: No architectural node, layer, component, or relation may be asserted without concrete `Evidence` (file path, line range, snippet).
3. **Absence of Proof**: If evidence is missing or ambiguous, return an explicit "Insufficient Evidence" status. Never extrapolate or hallucinate structural relationships.
