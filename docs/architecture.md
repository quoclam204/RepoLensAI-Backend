# RepoLens AI — Backend Architecture & System Design (T116)

## 1. Architectural Style: Clean Architecture

RepoLens AI is built using Clean Architecture principles with strict unidirectional dependencies:

```text
       ┌────────────────────────┐
       │     RepoLens.Domain    │ (Zero dependencies, pure business models)
       └───────────▲────────────┘
                   │
       ┌───────────┴────────────┐
       │  RepoLens.Application  │ (Use cases, Ports, DTOs, RAG service)
       └───────────▲────────────┘
                   │
       ┌───────────┴────────────┐
       │ RepoLens.Infrastructure│ (PostgreSQL/pgvector, Git/ZIP acquisition, adapters)
       └───────────▲────────────┘
                   │
       ┌───────────┴────────────┐
       │     RepoLens.Api       │ (Minimal APIs & Controllers, composition root)
       └────────────────────────┘

       ┌────────────────────────┐
       │   RepoLens.Analysis    │ (Roslyn AST, TS parser, knowledge graph)
       └───────────▲────────────┘
                   │ references Domain only
```

---

## 2. Core Execution Pipelines

### 2.1 Repository Analysis Pipeline (T052 - T054)

```text
POST /api/analyses (Git URL or ZIP)
  │
  ▼
[Validation Stage] ────────► RepositoryValidator checks size, URL, file counts
  │
  ▼
[Acquisition Stage] ───────► GitRepositorySource (shallow clone) / ZipRepositorySource (safe extraction)
  │
  ▼
[Scanning Stage] ──────────► FileScanner identifies languages, projects, files, hashes
  │
  ▼
[Static Analysis Stage] ───► RoslynRepositoryAnalyzerAdapter runs in-memory AST walkers
  │
  ▼
[Embedding Stage] ─────────► ChunkEmbeddingService populates vector embeddings (vector(1536))
  │
  ▼
[Persistence Stage] ───────► AnalysisPersistenceService writes all entities & chunks atomically
  │
  ▼
[Completion Stage] ────────► Status becomes Completed, temporary workspace safely cleaned up
```

### 2.2 Evidence-Grounded RAG Pipeline (T087 - T091)

```text
POST /api/analyses/{id}/chat (Question)
  │
  ▼
[Vector Retrieval] ────────► VectorChunkRetriever finds top-K similar chunks via pgvector cosine distance
  │
  ▼
[Prompt Construction] ─────► RagPromptBuilder formats user question and extracted evidence snippets
  │
  ▼
[AI Provider] ─────────────► IAiProvider generates completion
  │
  ▼
[Evidence Validation] ─────► AiEvidenceValidator validates claims against retrieved evidence nodes
  │
  ▼
[Confidence Calculator] ───► AiConfidenceCalculator evaluates High / Medium / Low / Unknown confidence
  │
  ▼
[Response or Fallback] ────► If evidence is missing, returns "Insufficient evidence in the analyzed repository"
```

---

## 3. Security Boundaries & Invariants

1. **Zero Execution of Analyzed Code (NFR-SEC-001)**:
   - Analyzed repository code is treated as strictly untrusted input.
   - No `Process.Start`, `Assembly.Load`, `dotnet build`, or `npm install` inside the target workspace.
2. **Safe Ingestion (Zip Slip Prevention)**:
   - All ZIP entries are checked using `Path.GetFullPath` and verified to reside inside the workspace root.
3. **Secret Redaction**:
   - `SecretMasker` redacts connection strings, API tokens, passwords, and private keys before content enters chunks, embeddings, prompts, or logs.
4. **Data Isolation**:
   - Every entity, chunk, and query is scoped strictly by `AnalysisId`. Cross-repository data access is prevented at the database and query level.
