# RepoLens AI — Backend

> **Static analysis establishes what exists. AI explains the evidence.**

RepoLens AI is an evidence-grounded architectural intelligence backend for source code repositories. It ingests public Git repositories or ZIP archives, performs deep static analysis (C# Roslyn Syntax Trees and TypeScript AST extraction), extracts dependencies, endpoints, database models, and generates an evidence-grounded Knowledge Graph and C4-compatible architectural model. Through semantic document chunking and PostgreSQL `pgvector` vector retrieval, it delivers verifiable, hallucination-resistant answers with strict evidence traceability.

---

## Architecture Overview

Built using Clean Architecture principles on **.NET 10**:

```text
                  ┌────────────────────────┐
                  │      RepoLens.Api      │ (REST Controllers, CORS, OpenAPI)
                  └───────────┬────────────┘
                              │
                  ┌───────────▼────────────┐
                  │ RepoLens.Infrastructure│ (Persistence, AI/Embeddings, Acquisition, Pipeline)
                  └───┬──────────────┬─────┘
                      │              │
        ┌─────────────▼───┐      ┌───▼─────────────┐
        │RepoLens.Analysis│      │RepoLens.Applic'n│ (Use Cases, RAG Service, DTOs, Archify)
        └─────────────┬───┘      └───┬─────────────┘
                      │              │
                      └───────┬──────┘
                              ▼
                      ┌───────────────┐
                      │RepoLens.Domain│ (Entities, Value Objects, Enums — Zero Dependencies)
                      └───────────────┘
```

- **Target Repository Execution**: Strictly prohibited. The target repository is untrusted input — zero builds, zero restores, zero package installations, and zero script executions.
- **Tenant & Analysis Isolation**: All database queries, vector similarity lookups, and memory caches are strictly partitioned by `AnalysisId`.
- **Secret Redaction**: Credentials, tokens, connection strings, and private keys are redacted to `***MASKED***` by `SecretMasker` before persistence, embedding, or prompting.

---

## Quick Start

### 1. Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/)
- [Docker Desktop](https://www.docker.com/) (for PostgreSQL + pgvector)

### 2. Start PostgreSQL with pgvector
```bash
docker compose up -d
```
This spins up PostgreSQL 16 on `localhost:5432` with the `pgvector` extension pre-installed.

### 3. Run Database Migrations
```bash
dotnet ef database update --project src/RepoLens.Infrastructure --startup-project src/RepoLens.Api
```

### 4. Configure Application
RepoLens AI operates in two AI/Embedding modes configured via `appsettings.json` or environment variables:

- **Deterministic Mode** (Default for local development and CI testing — no API keys required):
  ```json
  "Ai": { "Provider": "Deterministic" },
  "Embeddings": { "Provider": "Deterministic" }
  ```
- **OpenAI Mode** (Production):
  ```json
  "Ai": {
    "Provider": "OpenAi",
    "ApiKey": "YOUR_OPENAI_API_KEY",
    "Model": "gpt-4o-mini"
  },
  "Embeddings": {
    "Provider": "OpenAi",
    "ApiKey": "YOUR_OPENAI_API_KEY",
    "Model": "text-embedding-3-small"
  }
  ```

### 5. Run the Backend API
```bash
dotnet run --project src/RepoLens.Api
```
The API starts at `http://localhost:5237` (with OpenAPI documentation available at `/openapi/v1.json`).

---

## End-to-End API Workflow

### 1. Submit Repository for Analysis
**Git URL:**
```http
POST /api/analyses
Content-Type: application/json

{
  "sourceUrl": "https://github.com/dotnet/eshop"
}
```
*Response: `202 Accepted` with `{ "analysisId": "...", "status": "Created" }`*

**ZIP Upload:**
```http
POST /api/analyses/upload
Content-Type: multipart/form-data

file=@archive.zip
```

### 2. Poll Analysis Status
```http
GET /api/analyses/{id}
```
*Status transitions: `Created` ➔ `Cloning` ➔ `Scanning` ➔ `Analyzing` ➔ `Indexing` ➔ `Completed` (or `Failed`).*

### 3. Retrieve Architecture & Archify C4 Model
**Standard Architectural Graph:**
```http
GET /api/analyses/{id}/architecture
```

**Archify C4 Schema Format:**
```http
GET /api/analyses/{id}/architecture/archify
```
*Or:* `GET /api/analyses/{id}/architecture?format=archify`

### 4. Evidence-Grounded RAG Chat
```http
POST /api/analyses/{id}/chat
Content-Type: application/json

{
  "question": "What database entities are mapped by the OrdersDbContext?"
}
```
*Response:*
```json
{
  "answer": "The OrdersDbContext maps Order and OrderItem entities with a one-to-many relationship...",
  "confidence": "high",
  "evidence": [
    {
      "file": "src/Ordering.Infrastructure/OrdersDbContext.cs",
      "symbol": "OrdersDbContext",
      "startLine": 25,
      "endLine": 45,
      "reason": "Retrieved chunk 0 (similarity: 92%, confidence: 1.00)"
    }
  ]
}
```
*If evidence is missing or cannot support the question, the system returns:*
```json
{
  "answer": "Insufficient evidence in the analyzed repository",
  "confidence": "unknown",
  "evidence": []
}
```

---

## Running Automated Tests

```bash
dotnet test
```
The test suite validates:
- **`RepoLens.AnalysisTests`**: Roslyn syntax walkers, TypeScript symbol extraction, ignore rules, and golden datasets.
- **`RepoLens.IntegrationTests`**: Full end-to-end pipeline execution from ZIP extraction to RAG chat answer and refusal.
- **`RepoLens.UnitTests`**: Background queue, DI container validation, security acceptance (Zip Slip, zip bomb streaming protection, secret masking), and Archify mapping.

---

## Documentation Index

- [Local Development Guide](docs/local-development.md)
- [System Architecture](docs/architecture.md)
- [REST API Specification](docs/api.md)
- [Supported Repositories & Frameworks](docs/supported-repositories.md)
- [MVP Release Review](docs/mvp-release-review.md)
