# RepoLens AI — Implementation Plan

**Version:** 1.0.0
**Status:** Draft
**Created:** 2026-09-17
**Project:** RepoLens AI
**Constitution:** v1.0.0
**Specification:** v1.0.0

---

# 1. Purpose

This document defines the technical implementation plan for RepoLens AI.

It translates the requirements in `spec.md` into:

* System architecture
* Technology choices
* Project structure
* Data architecture
* Analysis architecture
* AI/RAG architecture
* API architecture
* Frontend architecture
* Security controls
* Testing strategy
* Development phases

Implementation MUST follow this plan unless a justified architectural change is approved.

---

# 2. Technical Strategy

RepoLens AI will be implemented as a **modular monolith** for the MVP.

The backend will use:

* .NET 10
* ASP.NET Core
* C#
* Clean Architecture
* Entity Framework Core
* PostgreSQL
* pgvector

The frontend will use:

* Next.js
* TypeScript
* Tailwind CSS
* React Flow

Static code analysis will use:

* Roslyn for C#
* AST-based parsing for TypeScript/JavaScript

AI functionality will use:

* Provider abstraction
* Embeddings
* PostgreSQL/pgvector
* Retrieval-Augmented Generation (RAG)

---

# 3. Architecture Overview

The system is divided into five primary backend projects:

```text id="qg0x6f"
RepoLens.sln
│
├── src/
│   ├── RepoLens.Api
│   ├── RepoLens.Application
│   ├── RepoLens.Domain
│   ├── RepoLens.Infrastructure
│   └── RepoLens.Analysis
│
└── tests/
    ├── RepoLens.UnitTests
    ├── RepoLens.IntegrationTests
    └── RepoLens.AnalysisTests
```

Frontend is maintained separately:

```text id="r0wq7h"
repolens-frontend/
```

The frontend and backend SHOULD be separate Git repositories.

---

# 4. Dependency Rules

The dependency graph MUST remain:

```text id="5f4qmf"
                    ┌─────────────────┐
                    │     Domain      │
                    └────────┬────────┘
                             ▲
                             │
                    ┌────────┴────────┐
                    │   Application   │
                    └────────┬────────┘
                             ▲
               ┌─────────────┴─────────────┐
               │                           │
      ┌────────┴────────┐        ┌────────┴────────┐
      │  Infrastructure │        │    Analysis     │
      └────────┬────────┘        └────────┬────────┘
               ▲                           ▲
               │                           │
               └─────────────┬─────────────┘
                             │
                    ┌────────┴────────┐
                    │       API       │
                    └─────────────────┘
```

More precisely:

```text id="6q6d8b"
Domain
  ← Application
  ← Analysis
  ← Infrastructure

Application
  ← Infrastructure
  ← API

Infrastructure
  ← API
```

The Domain project MUST NOT reference any other project.

---

# 5. Backend Project Responsibilities

## 5.1 RepoLens.Domain

Contains pure business concepts.

Expected folders:

```text id="3r5my1"
RepoLens.Domain/
├── Entities/
├── Enums/
├── ValueObjects/
├── Exceptions/
└── Common/
```

Initial entities:

```text
Repository
Analysis
Project
SourceFile
CodeSymbol
Dependency
ApiEndpoint
DatabaseEntity
DatabaseRelationship
Evidence
AnalysisIssue
DocumentChunk
```

Domain MUST remain independent of:

* EF Core
* ASP.NET Core
* PostgreSQL
* AI SDKs
* HTTP
* File-system implementations

---

# 6. RepoLens.Application

Contains use cases and application orchestration.

Expected structure:

```text id="u7dz2q"
RepoLens.Application/
├── Abstractions/
│   ├── Persistence/
│   ├── AI/
│   ├── RepositorySources/
│   └── Analysis/
│
├── Features/
│   ├── Analyses/
│   ├── Architecture/
│   ├── Dependencies/
│   ├── APIs/
│   ├── Database/
│   ├── Files/
│   └── Chat/
│
├── DTOs/
├── Validators/
└── Common/
```

Application owns:

* Analysis orchestration
* Query handling
* DTO mapping
* AI use cases
* Retrieval orchestration
* Validation
* Application-level rules

---

# 7. RepoLens.Infrastructure

Infrastructure contains implementations of external dependencies.

Expected structure:

```text id="bq2f8y"
RepoLens.Infrastructure/
├── Persistence/
│   ├── Configurations/
│   ├── Migrations/
│   └── RepoLensDbContext.cs
│
├── RepositorySources/
│   ├── Git/
│   └── Zip/
│
├── AI/
│   ├── Providers/
│   ├── Embeddings/
│   └── RAG/
│
├── Storage/
└── DependencyInjection.cs
```

Infrastructure owns:

* EF Core
* PostgreSQL
* pgvector
* Git repository acquisition
* ZIP extraction
* AI provider implementations
* Embedding provider
* Persistent storage

---

# 8. RepoLens.Analysis

The Analysis project contains deterministic repository-analysis functionality.

Expected structure:

```text id="ohj5r4"
RepoLens.Analysis/
├── Abstractions/
├── Repository/
├── Languages/
│   ├── CSharp/
│   └── TypeScript/
├── Models/
├── Pipeline/
├── Detection/
└── Common/
```

---

# 9. Analysis Abstractions

The analyzer architecture SHOULD use interfaces.

Example:

```csharp id="y6l8hf"
public interface ILanguageAnalyzer
{
    bool Supports(string language);

    Task<AnalysisResult> AnalyzeAsync(
        AnalysisContext context,
        CancellationToken cancellationToken);
}
```

C# implementation:

```text id="x1qvkj"
CSharpAnalyzer : ILanguageAnalyzer
```

TypeScript implementation:

```text id="z9v6n4"
TypeScriptAnalyzer : ILanguageAnalyzer
```

This allows future language analyzers to be added without changing the core pipeline.

---

# 10. Repository Acquisition

Repository acquisition MUST be abstracted.

Example:

```csharp id="j4g5r0"
public interface IRepositorySource
{
    Task<RepositoryWorkspace> AcquireAsync(
        RepositorySourceRequest request,
        CancellationToken cancellationToken);
}
```

Implementations:

```text id="2t4s6k"
GitRepositorySource
ZipRepositorySource
```

The Application layer depends on `IRepositorySource`.

Infrastructure provides the implementations.

---

# 11. Repository Analysis Pipeline

The analysis engine SHOULD follow this sequence:

```text id="1qf3yr"
1. Validate Input
        ↓
2. Acquire Repository
        ↓
3. Create Workspace
        ↓
4. Scan Files
        ↓
5. Apply Ignore Rules
        ↓
6. Detect Languages
        ↓
7. Detect Projects
        ↓
8. Run Language Analyzers
        ↓
9. Extract Symbols
        ↓
10. Extract Dependencies
        ↓
11. Detect APIs
        ↓
12. Detect Database Structures
        ↓
13. Generate Evidence
        ↓
14. Build Knowledge Model
        ↓
15. Persist Results
        ↓
16. Create Chunks
        ↓
17. Generate Embeddings
        ↓
18. Store Vectors
        ↓
19. Mark Completed
```

Each stage SHOULD be independently testable.

---

# 12. C# Analysis Architecture

Roslyn MUST be used for C# analysis.

The analyzer SHOULD create a Roslyn:

```text id="1vlz7r"
AdhocWorkspace
      ↓
Project
      ↓
Compilation
      ↓
SyntaxTree
      ↓
SemanticModel
```

The analyzer SHOULD use both:

* Syntax analysis
* Semantic analysis

Syntax analysis identifies source structures.

Semantic analysis identifies actual symbols and relationships.

---

# 13. C# Symbol Extraction

The C# analyzer SHOULD extract:

```text id="1z17hz"
Namespace
Class
Interface
Record
Struct
Enum
Method
Constructor
Property
Field
Parameter
Attribute
```

For each symbol:

```text id="3qj7d9"
Name
FullName
SymbolType
FilePath
StartLine
EndLine
ContainingSymbol
```

---

# 14. C# Dependency Extraction

Dependencies SHOULD be extracted from:

* Project references
* Namespace references
* Type references
* Constructor parameters
* Method parameters
* Return types
* Inheritance
* Interface implementation
* Method invocations where feasible

Dependency types SHOULD include:

```text id="yr4zj4"
ProjectReference
TypeReference
Inheritance
Implementation
MethodCall
Composition
```

---

# 15. C# Responsibility Detection

The system SHOULD detect common architectural roles using evidence.

Examples:

```text id="t7k5z4"
Controller
Service
Repository
Entity
DTO
DbContext
Configuration
Middleware
Validator
```

Detection signals MAY include:

* Base types
* Interfaces
* Attributes
* Naming conventions
* Namespace
* Method signatures
* Framework-specific patterns

The resulting classification MUST be marked as derived analysis information.

---

# 16. API Analysis

The API analyzer SHOULD inspect:

### MVC Controllers

```text id="s8q6zm"
[ApiController]
[Route]
[HttpGet]
[HttpPost]
[HttpPut]
[HttpPatch]
[HttpDelete]
```

### Minimal APIs

```text id="6w6f7j"
MapGet
MapPost
MapPut
MapPatch
MapDelete
```

Endpoint model:

```text id="q8yq4s"
Id
ProjectId
SymbolId
HttpMethod
Route
Controller
Action
EvidenceId
```

---

# 17. EF Core Analysis

The analyzer SHOULD detect:

```text id="q4c9r7"
DbContext
DbSet<T>
EntityTypeConfiguration<T>
HasKey
HasOne
HasMany
WithOne
WithMany
HasForeignKey
```

The analyzer SHOULD also inspect:

```text id="v9f5cy"
OnModelCreating
IEntityTypeConfiguration<T>
Migration files
```

---

# 18. TypeScript Analysis

TypeScript/JavaScript analysis SHOULD use an AST parser.

The analyzer SHOULD extract:

```text id="xk6n3j"
Module
Import
Export
Class
Function
Interface
Type
Variable
React Component
Route
API Client
```

The first version does not need to provide the same analysis depth as C#.

---

# 19. Knowledge Model

The knowledge model is the central representation of repository structure.

Conceptually:

```text id="yt7x9j"
Repository
    │
    ├── Projects
    │      │
    │      ├── Files
    │      │     │
    │      │     └── Symbols
    │      │
    │      └── Dependencies
    │
    ├── APIs
    │
    ├── Database Entities
    │
    └── Evidence
```

Relationships:

```text id="x7f4am"
CONTAINS
DEFINES
DEPENDS_ON
IMPLEMENTS
INHERITS
CALLS
EXPOSES
MAPS_TO
READS
WRITES
```

---

# 20. Evidence Model

Evidence is a first-class concept.

Every major extracted relationship SHOULD have evidence.

Example:

```text id="95cb8f"
Evidence
├── AnalysisId
├── FilePath
├── Symbol
├── StartLine
├── EndLine
├── EvidenceType
└── Description
```

Evidence MUST point to the analyzed repository version.

---

# 21. Persistence Architecture

PostgreSQL is the primary database.

Entity Framework Core is the persistence technology.

Expected DbContext:

```text id="c2a8r4"
RepoLensDbContext
```

Expected tables:

```text id="3u3c6k"
repositories
analyses
projects
source_files
code_symbols
dependencies
api_endpoints
database_entities
database_relationships
evidence
analysis_issues
document_chunks
```

Foreign keys MUST preserve repository/analysis ownership.

---

# 22. Vector Storage

pgvector SHOULD store embeddings for repository chunks.

Conceptually:

```text id="j8ez6j"
document_chunks
├── id
├── analysis_id
├── file_path
├── symbol
├── content
├── start_line
├── end_line
└── embedding
```

Embedding retrieval MUST be scoped by `analysis_id`.

---

# 23. RAG Architecture

The RAG subsystem SHOULD combine structured and semantic retrieval.

```text id="u9vqjg"
Question
    │
    ├───────────────┐
    ↓               ↓
Graph Retrieval   Vector Search
    │               │
    └───────┬───────┘
            ↓
      Evidence Ranking
            ↓
      Context Builder
            ↓
           LLM
            ↓
    Evidence Validator
            ↓
          Answer
```

---

# 24. Retrieval Strategy

The system SHOULD support multiple retrieval strategies.

## Structured Retrieval

Used for:

* Files
* Symbols
* Dependencies
* APIs
* Database entities
* Relationships

## Vector Retrieval

Used for:

* Code explanations
* Semantic search
* Business logic questions
* Natural-language questions

## Hybrid Retrieval

For complex questions, structured and vector retrieval SHOULD be combined.

---

# 25. AI Provider Architecture

The application MUST use an abstraction:

```csharp id="s5t9vc"
public interface IAiProvider
{
    Task<AiResponse> GenerateAsync(
        AiRequest request,
        CancellationToken cancellationToken);
}
```

Embedding SHOULD also be abstracted:

```csharp id="0z84a7"
public interface IEmbeddingProvider
{
    Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken);
}
```

This prevents direct coupling between business logic and a specific AI provider.

---

# 26. AI Prompt Architecture

The AI prompt SHOULD contain:

```text id="z1s9xk"
System Instructions
+
Repository Metadata
+
Relevant Structured Evidence
+
Relevant Retrieved Code
+
User Question
```

The system instruction SHOULD explicitly require:

* Evidence-based answers
* No fabricated repository facts
* Explicit uncertainty
* Repository scope compliance
* Concise explanation
* Source references

---

# 27. Evidence Validation

Before returning an AI response, the system SHOULD validate:

1. Evidence belongs to the current analysis.
2. Referenced file exists.
3. Referenced symbol exists where applicable.
4. Line range is valid where available.
5. The answer does not introduce unsupported repository-specific facts.

Invalid evidence SHOULD be removed or cause the response to be regenerated.

---

# 28. Frontend Architecture

Frontend:

```text id="z5d5k8"
Next.js
   +
TypeScript
   +
Tailwind CSS
   +
React Flow
```

Suggested structure:

```text id="2q2suw"
src/
├── app/
│   ├── analyze/
│   └── projects/
│
├── components/
│   ├── analysis/
│   ├── architecture/
│   ├── dependencies/
│   ├── api/
│   ├── database/
│   ├── files/
│   └── chat/
│
├── lib/
│   ├── api/
│   └── utils/
│
├── types/
└── hooks/
```

---

# 29. Frontend Pages

## Landing Page

Purpose:

* Explain RepoLens AI
* Accept repository input
* Start analysis

## Analysis Page

Displays:

* Current stage
* Progress
* Errors
* Completion state

## Overview

Displays:

* Repository information
* Statistics
* Languages
* Main modules

## Architecture

Displays:

* Architecture graph
* Components
* Relationships
* Evidence

## Dependencies

Displays:

* Dependency graph
* Incoming/outgoing dependencies

## APIs

Displays:

* Endpoint list
* HTTP methods
* Routes
* Controllers
* Evidence

## Database

Displays:

* Entities
* Properties
* Relationships
* Evidence

## Files

Displays:

* Directory tree
* Source files
* Symbols
* Source code

## Chat

Displays:

* AI conversation
* Evidence
* Source navigation

---

# 30. API Layer Architecture

Controllers SHOULD remain thin.

Example:

```text id="7njj1e"
HTTP Request
     ↓
Controller
     ↓
Application Use Case
     ↓
Domain / Abstractions
     ↓
Infrastructure
     ↓
Database / External Service
```

Controllers MUST NOT contain:

* Repository scanning logic
* Roslyn logic
* RAG implementation
* Database business rules
* AI prompt construction

---

# 31. Background Processing

Repository analysis SHOULD be designed so that long-running work can later be moved to a background worker.

For MVP, implementation MAY initially use an application-managed background process.

The design SHOULD avoid coupling the API request lifecycle directly to the entire analysis process.

Future architecture MAY introduce:

```text id="4m8jck"
API
 ↓
Job
 ↓
Worker
 ↓
Analysis Pipeline
```

A message broker is not required for MVP.

---

# 32. Security Architecture

The system MUST use isolated workspaces.

Conceptually:

```text id="c9sqbx"
Analysis A
└── workspace/A/

Analysis B
└── workspace/B/
```

No analysis may access another analysis workspace.

ZIP extraction MUST validate every entry path.

Repository processing MUST enforce resource limits.

---

# 33. Secret Detection

Before AI processing, the pipeline SHOULD identify likely secrets.

Detection SHOULD include:

* Environment variables
* Token patterns
* Private keys
* Connection strings
* Credential files

Sensitive content SHOULD be:

```text
Detected
   ↓
Redacted / Excluded
   ↓
Safe Analysis
```

---

# 34. Testing Architecture

## Unit Tests

```text
RepoLens.UnitTests
├── Domain
├── Application
└── AI
```

Test:

* Domain behavior
* Use cases
* Validators
* Retrieval orchestration
* Evidence validation

---

## Analysis Tests

```text
RepoLens.AnalysisTests
├── CSharp
├── TypeScript
├── Dependencies
├── APIs
└── Database
```

Analysis tests SHOULD use small fixture repositories.

---

## Integration Tests

```text
RepoLens.IntegrationTests
├── Persistence
├── API
├── Repository Acquisition
└── RAG
```

---

# 35. Test Fixtures

Representative fixture repositories SHOULD be created.

Example:

```text id="b2h4vq"
tests/Fixtures/
├── CSharpCleanArchitecture/
├── CSharpWebApi/
├── CSharpEfCore/
└── TypeScriptNextApp/
```

Fixtures MUST remain small and deterministic.

---

# 36. AI Evaluation

An evaluation dataset SHOULD contain:

```text id="t9x0b1"
Question
Expected Evidence
Expected Concepts
Expected Answer Characteristics
```

Example:

```json id="4y1d4u"
{
  "question": "Where is authentication configured?",
  "expectedEvidence": [
    "Program.cs"
  ],
  "expectedConcepts": [
    "Authentication",
    "Authorization"
  ]
}
```

The evaluation SHOULD detect:

* Unsupported claims
* Missing evidence
* Incorrect evidence
* Retrieval failures
* Hallucination

---

# 37. API Contract Development

API contracts SHOULD be documented separately in:

```text id="7cb4cr"
specs/001-repolens-mvp/contracts/api.md
```

The contract SHOULD define:

* Request models
* Response models
* Error responses
* Status codes
* Pagination where required
* Analysis state representation

Frontend implementation SHOULD consume the defined API contract rather than assuming response shapes.

---

# 38. Configuration

Configuration SHOULD be environment-based.

Examples:

```text id="y6f5m9"
ConnectionStrings__Default
AI__Provider
AI__ApiKey
AI__Model
Embedding__Model
Repository__MaxSize
Repository__MaxFiles
```

Secrets MUST NOT be committed to Git.

A safe template MAY be provided:

```text id="1a9j7k"
.env.example
appsettings.example.json
```

---

# 39. Local Development

The project SHOULD provide Docker support for infrastructure dependencies.

Expected MVP infrastructure:

```text id="5p3s4v"
PostgreSQL
pgvector
```

Application services SHOULD run locally during early development.

A future full-stack Docker Compose configuration MAY include:

```text id="r7j4xq"
Frontend
Backend
PostgreSQL
```

---

# 40. Logging

Structured logging SHOULD include:

```text id="s3q1x8"
AnalysisId
RepositoryId
Stage
Duration
Status
ErrorType
```

Sensitive repository content MUST NOT be written to logs.

---

# 41. Performance Strategy

The MVP SHOULD use:

* Incremental file processing
* Streaming ZIP extraction where possible
* Parallel independent file analysis where safe
* Batch embeddings
* Database batching
* Indexed repository/analysis fields

Performance optimization SHOULD occur after measurement.

---

# 42. Caching

Caching MAY be introduced for:

* Repository metadata
* Analysis results
* Embeddings
* Frequently requested architecture data

Caching MUST preserve repository isolation.

Cache keys SHOULD contain repository/analysis identity.

---

# 43. Observability

Analysis stages SHOULD be measurable.

Example:

```text id="6w8j2v"
Validation             120ms
Repository Scan        430ms
C# Analysis           1.8s
Dependency Analysis   0.7s
API Analysis          0.2s
Database Analysis     0.5s
Persistence           0.8s
Embedding             2.4s
Indexing              0.6s
```

Exact targets will be established after benchmark testing.

---

# 44. Development Phases

The implementation will proceed in phases.

## Phase 1 — Foundation

Goals:

* Repository setup
* Solution
* Clean Architecture
* Basic API
* Database
* Docker
* Testing infrastructure

---

## Phase 2 — Repository Ingestion

Goals:

* Git URL ingestion
* ZIP upload
* Validation
* Scanner
* Ignore rules
* Language detection

---

## Phase 3 — Static Analysis

Goals:

* Analyzer abstraction
* Roslyn
* C# symbols
* C# dependencies
* API detection
* EF Core detection
* TypeScript analysis

---

## Phase 4 — Knowledge Model

Goals:

* Domain models
* Evidence
* Relationships
* Persistence
* Analysis results

---

## Phase 5 — Visualization

Goals:

* Overview
* Architecture graph
* Dependency graph
* API explorer
* Database explorer
* File explorer

---

## Phase 6 — AI/RAG

Goals:

* AI abstraction
* Embedding abstraction
* Chunking
* pgvector
* Retrieval
* Evidence-grounded prompts
* Evidence validation

---

## Phase 7 — AI Chat

Goals:

* Chat API
* Chat UI
* Repository questions
* Evidence display
* Source navigation

---

## Phase 8 — Evaluation and Hardening

Goals:

* Unit tests
* Integration tests
* Analysis tests
* AI evaluation
* Security testing
* Performance testing
* UX polish
* Documentation

---

# 45. Initial Repository Structure

Final target:

```text id="1w3xw4"
RepoLens/
│
├── RepoLens.sln
│
├── src/
│   ├── RepoLens.Api/
│   ├── RepoLens.Application/
│   ├── RepoLens.Domain/
│   ├── RepoLens.Infrastructure/
│   └── RepoLens.Analysis/
│
├── tests/
│   ├── RepoLens.UnitTests/
│   ├── RepoLens.IntegrationTests/
│   └── RepoLens.AnalysisTests/
│
├── specs/
│   └── 001-repolens-mvp/
│       ├── spec.md
│       ├── plan.md
│       └── contracts/
│           └── api.md
│
├── docs/
│   ├── architecture/
│   └── diagrams/
│
├── .github/
│   └── workflows/
│
├── docker/
│
├── .gitignore
├── README.md
└── docker-compose.yml
```

Frontend SHOULD be separate:

```text id="5o1v4j"
repolens-frontend/
```

---

# 46. Dependency Selection

Initial backend dependencies SHOULD include:

```text id="3a7d2k"
ASP.NET Core
Entity Framework Core
Npgsql
pgvector
Roslyn
Swagger / OpenAPI
FluentValidation
```

Additional dependencies MUST have a clear justification.

The project SHOULD avoid unnecessary packages.

---

# 47. Architecture Decision Records

Significant architectural decisions SHOULD be documented under:

```text id="w1f8n2"
docs/architecture/adr/
```

Potential ADRs:

```text id="g0q6a5"
ADR-001 Modular Monolith
ADR-002 Clean Architecture
ADR-003 PostgreSQL + pgvector
ADR-004 Roslyn for C# Analysis
ADR-005 Evidence-Grounded RAG
ADR-006 Separate Frontend and Backend
```

---

# 48. Documentation Strategy

The project SHOULD maintain:

```text id="2z2q2k"
README.md
docs/
├── architecture/
├── diagrams/
├── development/
└── evaluation/
```

Documentation MUST describe actual implemented behavior.

Documentation MUST NOT claim features that do not exist.

---

# 49. Implementation Rules

Every implementation task MUST:

1. Reference the relevant requirement.
2. Identify the target project/layer.
3. Keep changes within scope.
4. Add tests when behavior changes.
5. Run relevant tests.
6. Run a build after significant changes.
7. Report warnings/errors.
8. Update documentation/contracts if required.

AI coding agents MUST NOT perform unrelated refactoring.

---

# 50. Commit Strategy

Commits SHOULD be organized by logical change.

Examples:

```text id="e9w5n7"
chore(solution): initialize backend solution

feat(domain): add analysis domain model

feat(repository): add git repository source

feat(analysis): add roslyn analyzer

feat(analysis): extract csharp symbols

feat(analysis): detect aspnet endpoints

feat(analysis): detect ef core entities

feat(api): expose architecture analysis

feat(frontend): add repository analysis page

feat(rag): add repository retrieval

feat(chat): add evidence grounded chat
```

---

# 51. Implementation Order

The recommended order is:

```text id="u7n1kg"
T001 Foundation
 ↓
T002 Domain
 ↓
T003 Application
 ↓
T004 Infrastructure
 ↓
T005 API
 ↓
T006 Database
 ↓
T007 Repository Ingestion
 ↓
T008 Scanner
 ↓
T009 Language Detection
 ↓
T010 C# Analyzer
 ↓
T011 Symbol Extraction
 ↓
T012 Dependency Extraction
 ↓
T013 API Detection
 ↓
T014 Database Detection
 ↓
T015 Knowledge Model
 ↓
T016 Persistence
 ↓
T017 Architecture API
 ↓
T018 Frontend Foundation
 ↓
T019 Architecture UI
 ↓
T020 API / DB / File UI
 ↓
T021 AI Provider
 ↓
T022 Chunking
 ↓
T023 Embedding
 ↓
T024 RAG
 ↓
T025 Evidence Validation
 ↓
T026 Chat API
 ↓
T027 Chat UI
 ↓
T028 Testing
 ↓
T029 Evaluation
 ↓
T030 Security / Performance
```

The detailed task list will be defined in `tasks.md`.

---

# 52. Risk Management

## Risk — LLM Hallucination

Mitigation:

* Static analysis
* Evidence retrieval
* Evidence validation
* Explicit uncertainty

## Risk — Large Repository

Mitigation:

* Resource limits
* Incremental analysis
* Filtering
* Batching

## Risk — Malicious Repository

Mitigation:

* Never execute arbitrary code
* Safe ZIP extraction
* Isolated workspace
* Resource limits

## Risk — Incorrect Architecture Inference

Mitigation:

* Evidence-backed relationships
* Confidence classification
* Distinguish confirmed vs inferred relationships

## Risk — AI Provider Dependency

Mitigation:

* `IAiProvider`
* `IEmbeddingProvider`

## Risk — Scope Expansion

Mitigation:

* MVP scope
* Task-based implementation
* Constitution enforcement

---

# 53. Key Architectural Principle

The most important implementation boundary is:

```text id="0l4h6p"
             DETERMINISTIC
                  │
                  ▼
        ┌───────────────────┐
        │ Repository        │
        │ Static Analysis   │
        │ Knowledge Model   │
        │ Evidence          │
        └─────────┬─────────┘
                  │
                  ▼
               AI / RAG
                  │
                  ▼
        ┌───────────────────┐
        │ Explanation       │
        │ Summarization     │
        │ Q&A               │
        └───────────────────┘
```

The deterministic layer establishes the facts.

The AI layer makes those facts understandable.

This separation MUST be preserved throughout implementation.

---

# 54. Final Technical Direction

RepoLens AI MVP will use:

```text id="5a0t8f"
Frontend
Next.js + TypeScript + Tailwind + React Flow
                    │
                    │ REST API
                    ▼
Backend
ASP.NET Core + .NET 10
                    │
        ┌───────────┼────────────┐
        ▼           ▼            ▼
     Domain    Application    Analysis
                    │
                    ▼
              Infrastructure
                    │
          ┌─────────┼─────────┐
          ▼         ▼         ▼
      PostgreSQL  pgvector    AI
```

The MVP will remain a modular monolith.

The architecture is intentionally designed so that:

* Static analysis can evolve independently.
* New languages can be added.
* New AI providers can be added.
* The frontend can evolve independently.
* Repository data remains isolated.
* AI answers remain evidence-grounded.

---

# 55. Plan Status

**Version:** 1.0.0
**Status:** Draft
**Constitution:** v1.0.0
**Specification:** v1.0.0
**Next Artifact:** `contracts/api.md` and `tasks.md`
