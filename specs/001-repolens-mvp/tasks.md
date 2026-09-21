# RepoLens AI — Implementation Tasks

**Version:** 1.0.0
**Status:** Draft
**Date:** 2026-09-17

---

## 1. Purpose

This document decomposes the RepoLens AI implementation plan into small, independently executable tasks.

Each task MUST:

* Have a clearly defined scope.
* Identify dependencies.
* Identify affected projects/files.
* Have explicit acceptance criteria.
* Include validation/tests.
* Avoid modifying unrelated modules.
* Preserve the architecture and requirements defined in:

  * `constitution.md`
  * `spec.md`
  * `plan.md`
  * `contracts/api.md`

The implementation follows:

> **Static analysis establishes what exists. AI explains the evidence.**

AI MUST NOT become the source of truth for repository structure.

---

# 2. Task Execution Rules

## 2.1 Scope Rule

When implementing a task:

1. Read the relevant specification first.
2. Inspect the current repository state.
3. Modify only files required for the task.
4. Do not refactor unrelated code.
5. Do not introduce features from future tasks.
6. Do not silently change API contracts.
7. Do not change architecture layers without explicit approval.

If a required change affects another task, stop and report it instead of expanding scope.

---

## 2.2 Required Claude Code Final Report

Every completed task MUST end with:

```text
Task:
Status:

Created files:
Modified files:
Deleted files:

Implementation summary:

Tests executed:
Test results:

Build:
Warnings:
Errors:

Out of scope / not implemented:
Blockers:
Follow-up tasks:
```

---

## 2.3 Validation Rule

Unless the task explicitly concerns documentation only:

* Build affected projects.
* Run relevant tests.
* Report warnings separately from errors.
* Never claim success without actually running validation.

---

# 3. Phase 0 — Project Foundation

## T001 — Initialize .NET Solution

**Requirements:** FR-001, NFR-ARCH-001
**Dependencies:** None

### Scope

Create:

```text
RepoLens.sln
```

using .NET 10.

### Projects

```text
src/RepoLens.Api
src/RepoLens.Application
src/RepoLens.Domain
src/RepoLens.Infrastructure
src/RepoLens.Analysis

tests/RepoLens.UnitTests
tests/RepoLens.IntegrationTests
tests/RepoLens.AnalysisTests
```

### Acceptance Criteria

* Solution builds.
* All projects target .NET 10.
* Projects are correctly registered in the solution.
* No unrelated application code is introduced.

### Tests

* `dotnet build`

---

## T002 — Create Domain Project

**Requirements:** NFR-ARCH-001
**Dependencies:** T001

### Scope

Create Domain project and base domain structure.

### Acceptance Criteria

* Domain has no dependency on Infrastructure, API, database, or AI SDK.
* Project builds successfully.

### Tests

* `dotnet build`

---

## T003 — Create Application Project

**Requirements:** NFR-ARCH-001
**Dependencies:** T001, T002

### Scope

Create Application project.

### Acceptance Criteria

Application references Domain only.

---

## T004 — Create Infrastructure Project

**Requirements:** NFR-ARCH-001
**Dependencies:** T001, T002, T003

### Scope

Create Infrastructure project.

### Acceptance Criteria

Infrastructure references Application and Domain.

---

## T005 — Create API Project

**Requirements:** FR-001, NFR-ARCH-001
**Dependencies:** T001, T003, T004

### Scope

Create ASP.NET Core Web API.

### Acceptance Criteria

* API starts.
* OpenAPI is available.
* API references Application and Infrastructure.
* No business logic is placed directly in controllers.

---

## T006 — Create Analysis Project

**Requirements:** FR-003, FR-004
**Dependencies:** T001, T002

### Scope

Create static-analysis project.

### Acceptance Criteria

* Analysis references Domain.
* Analysis does not depend on API.
* Analysis does not depend on UI.

---

## T007 — Configure Project Dependency Rules

**Requirements:** NFR-ARCH-001
**Dependencies:** T002–T006

### Required dependency direction

```text
Domain
  ↑
Application
  ↑
Infrastructure

Analysis → Domain

API → Application
API → Infrastructure
```

### Acceptance Criteria

Automated or documented verification confirms forbidden dependencies are absent.

---

## T008 — Configure Shared Development Settings

**Dependencies:** T001

### Scope

Configure:

* `.editorconfig`
* nullable reference types
* implicit usings
* warning policy
* common build settings

### Acceptance Criteria

All projects use consistent settings.

---

# 4. Phase 1 — Infrastructure Foundation

## T009 — Configure PostgreSQL

**Requirements:** NFR-DATA-001
**Dependencies:** T004

### Scope

Configure PostgreSQL connection infrastructure.

### Acceptance Criteria

* Connection configuration is environment-based.
* Credentials are not hardcoded.

---

## T010 — Configure EF Core DbContext

**Requirements:** NFR-DATA-001
**Dependencies:** T009

### Scope

Create:

```text
RepoLensDbContext
```

### Acceptance Criteria

* EF Core is configured.
* Domain persistence mapping is isolated from Domain entities.

---

## T011 — Configure Database Migrations

**Dependencies:** T010

### Acceptance Criteria

* Initial migration can be generated.
* Database can be created from migrations.

---

## T012 — Configure Docker Development Environment

**Dependencies:** T009

### Scope

Provide local PostgreSQL environment.

### Acceptance Criteria

A developer can start the database using documented commands.

---

## T013 — Configure Application Dependency Injection

**Dependencies:** T003, T004, T005

### Scope

Register:

* repositories
* analysis services
* storage services
* AI abstractions
* embedding abstractions

### Acceptance Criteria

Application starts without DI errors.

---

## T014 — Configure Logging

**Requirements:** NFR-OBS-001
**Dependencies:** T005

### Scope

Configure structured application logging.

### Acceptance Criteria

Logs contain enough context to identify:

* analysis ID
* processing stage
* errors
* duration

No secrets are logged.

---

# 5. Phase 2 — Domain Model

## T015 — Create Repository Entity

**Requirements:** FR-001, FR-002
**Dependencies:** T002

### Fields

At minimum:

```text
Id
Name
SourceType
SourceLocation
Status
CreatedAt
UpdatedAt
```

### Acceptance Criteria

Repository lifecycle can be represented.

---

## T016 — Create Analysis Entity

**Requirements:** FR-002, FR-003
**Dependencies:** T015

### Fields

```text
Id
RepositoryId
Status
CurrentStage
StartedAt
CompletedAt
Error
```

---

## T017 — Create Project Entity

**Requirements:** FR-004
**Dependencies:** T002

Represent detected projects inside a repository.

---

## T018 — Create SourceFile Entity

**Requirements:** FR-004
**Dependencies:** T002

Represent:

* path
* language
* size
* hash
* project association

---

## T019 — Create CodeSymbol Entity

**Requirements:** FR-004, FR-005
**Dependencies:** T002

Represent:

* namespace
* class
* interface
* method
* property
* enum
* symbol relationships

---

## T020 — Create Dependency Entity

**Requirements:** FR-005
**Dependencies:** T019

Represent dependencies between projects/files/symbols.

---

## T021 — Create API Endpoint Entity

**Requirements:** FR-006
**Dependencies:** T019

Represent detected HTTP endpoints.

---

## T022 — Create Database Entity Model

**Requirements:** FR-007
**Dependencies:** T019

Represent:

* entity/table
* columns/properties
* relationships
* foreign keys

---

## T023 — Create Evidence Entity

**Requirements:** FR-008, NFR-AI-001
**Dependencies:** T018–T022

Evidence MUST point back to repository content.

Minimum evidence information:

```text
FilePath
StartLine
EndLine
Symbol
EvidenceType
```

---

## T024 — Create AnalysisIssue Entity

**Requirements:** NFR-ROBUST-001
**Dependencies:** T016

Represent non-fatal analysis problems.

Examples:

```text
UnsupportedLanguage
ParserFailure
InvalidProject
UnreadableFile
```

---

## T025 — Create DocumentChunk Entity

**Requirements:** FR-009
**Dependencies:** T018, T023

Represent AI/RAG chunks with source references.

---

## T026 — Add Domain Validation

**Dependencies:** T015–T025

### Acceptance Criteria

Invalid states cannot be created silently.

Unit tests cover:

* invalid status transitions
* invalid repository source
* missing required identifiers
* invalid evidence ranges

---

# 6. Phase 3 — Repository Acquisition

## T027 — Define IRepositorySource

**Requirements:** FR-001
**Dependencies:** T003

Create abstraction for repository acquisition.

```text
IRepositorySource
```

---

## T028 — Implement GitHub Repository Source

**Requirements:** FR-001
**Dependencies:** T027

Support public Git repository URLs.

### Acceptance Criteria

* URL validated.
* Repository downloaded/cloned into isolated workspace.
* Invalid URL produces controlled error.

---

## T029 — Implement ZIP Repository Source

**Requirements:** FR-001
**Dependencies:** T027

Support ZIP upload.

### Security requirements

* Prevent path traversal.
* Reject dangerous archive entries.
* Apply file count/size limits.

---

## T030 — Implement Repository Validation

**Requirements:** FR-001, NFR-SEC-001
**Dependencies:** T028, T029

Validate:

* source type
* repository size
* file count
* supported project types

---

## T031 — Implement Temporary Workspace Management

**Requirements:** NFR-SEC-002
**Dependencies:** T028, T029

### Acceptance Criteria

* Each analysis has isolated workspace.
* Workspace is cleaned after processing.
* Cleanup occurs after failure as well.

---

# 7. Phase 4 — Repository Scanner

## T032 — Implement File Scanner

**Requirements:** FR-003
**Dependencies:** T031

Scan repository recursively.

Capture:

```text
relative path
extension
size
hash
```

---

## T033 — Implement Ignore Rules

**Requirements:** NFR-SEC-001
**Dependencies:** T032

Ignore:

```text
.git
node_modules
bin
obj
dist
build
coverage
.env
secrets
generated files
```

Allow configurable rules where appropriate.

---

## T034 — Implement Secret Detection Boundary

**Requirements:** NFR-SEC-001
**Dependencies:** T032

Detect likely:

* API keys
* connection strings
* private keys
* credentials
* tokens

### Critical rule

Detected secrets MUST NOT be sent to AI providers.

---

## T035 — Implement Language Detection

**Requirements:** FR-003
**Dependencies:** T032

Initial supported languages:

```text
C#
TypeScript
JavaScript
```

---

## T036 — Implement Project Detection

**Requirements:** FR-003
**Dependencies:** T032, T035

Detect:

```text
.sln
.csproj
package.json
tsconfig.json
```

and related project structures.

---

# 8. Phase 5 — C# Static Analysis

## T037 — Configure Roslyn Workspace

**Requirements:** FR-004
**Dependencies:** T036

Create C# analysis infrastructure using Roslyn.

---

## T038 — Extract C# Namespaces

**Requirements:** FR-004
**Dependencies:** T037

---

## T039 — Extract C# Classes and Interfaces

**Requirements:** FR-004
**Dependencies:** T037

---

## T040 — Extract C# Methods and Properties

**Requirements:** FR-004
**Dependencies:** T039

---

## T041 — Extract C# Inheritance

**Requirements:** FR-005
**Dependencies:** T039

Detect:

```text
INHERITS
IMPLEMENTS
```

relationships.

---

## T042 — Extract C# Project Dependencies

**Requirements:** FR-005
**Dependencies:** T037

Analyze project references and relevant assembly dependencies.

---

## T043 — Extract Symbol Dependencies

**Requirements:** FR-005
**Dependencies:** T040

Detect meaningful symbol references.

Avoid attempting to create a perfect call graph in MVP.

---

## T044 — Detect ASP.NET Core Endpoints

**Requirements:** FR-006
**Dependencies:** T040

Detect common patterns:

```text
[ApiController]
[Route]
[HttpGet]
[HttpPost]
[HttpPut]
[HttpDelete]
MapGet
MapPost
MapPut
MapDelete
```

---

## T045 — Detect EF Core Entities

**Requirements:** FR-007
**Dependencies:** T039, T040

Detect common:

```text
DbContext
DbSet<T>
EntityTypeConfiguration
[Key]
[ForeignKey]
```

patterns.

---

## T046 — Detect EF Core Relationships

**Requirements:** FR-007
**Dependencies:** T045

Detect:

```text
one-to-one
one-to-many
many-to-many
foreign keys
```

where evidence exists.

---

# 9. Phase 6 — TypeScript / JavaScript Analysis

## T047 — Configure TypeScript AST Analyzer

**Requirements:** FR-004
**Dependencies:** T035

Use an appropriate AST parser.

---

## T048 — Extract TypeScript Symbols

**Requirements:** FR-004
**Dependencies:** T047

Detect:

```text
interface
class
function
type
enum
variable
```

where appropriate.

---

## T049 — Extract JavaScript Symbols

**Requirements:** FR-004
**Dependencies:** T047

---

## T050 — Extract npm Dependencies

**Requirements:** FR-005
**Dependencies:** T047

Read:

```text
package.json
```

without executing project code.

---

## T051 — Detect Frontend Routes / API Calls

**Requirements:** FR-006
**Dependencies:** T048

Detect common patterns where reliably identifiable.

---

# 10. Phase 7 — Analysis Pipeline

## T052 — Define Analysis Pipeline Abstraction

**Requirements:** FR-003
**Dependencies:** T032–T036

Pipeline:

```text
Acquire
 ↓
Scan
 ↓
Detect
 ↓
Analyze
 ↓
Normalize
 ↓
Persist
 ↓
Index
```

---

## T053 — Implement Analysis Stage Tracking

**Requirements:** FR-002
**Dependencies:** T016, T052

Track:

```text
Created
Cloning
Scanning
Analyzing
Indexing
Completed
Failed
```

---

## T054 — Implement Analysis Orchestration

**Requirements:** FR-002, FR-003
**Dependencies:** T052, T053

---

## T055 — Implement Non-Fatal Error Handling

**Requirements:** NFR-ROBUST-001
**Dependencies:** T054

A single unsupported file MUST NOT necessarily fail the entire repository analysis.

Store errors as `AnalysisIssue`.

---

# 11. Phase 8 — Knowledge Model

## T056 — Define Knowledge Graph Model

**Requirements:** FR-005, FR-008
**Dependencies:** T015–T025

Nodes:

```text
Repository
Project
File
Namespace
Class
Interface
Method
Endpoint
DatabaseEntity
```

Edges:

```text
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

## T057 — Normalize Analyzer Results

**Dependencies:** T038–T051, T056

All analyzers MUST produce a common representation.

---

## T058 — Attach Evidence to Knowledge Nodes

**Requirements:** FR-008
**Dependencies:** T023, T057

Every important inferred fact should have source evidence.

---

## T059 — Persist Knowledge Model

**Requirements:** FR-008
**Dependencies:** T057, T058, T010

---

# 12. Phase 9 — Backend Query APIs

## T060 — Implement Create Analysis API

**Requirements:** FR-001, FR-002
**Dependencies:** T054

Endpoint:

```http
POST /api/analyses
```

Expected behavior:

```http
202 Accepted
```

---

## T061 — Implement Analysis Status API

**Requirements:** FR-002
**Dependencies:** T053

```http
GET /api/analyses/{id}
```

---

## T062 — Implement Overview API

**Requirements:** FR-003
**Dependencies:** T059

```http
GET /api/analyses/{id}/overview
```

---

## T063 — Implement Architecture API

**Requirements:** FR-005
**Dependencies:** T059

```http
GET /api/analyses/{id}/architecture
```

Return graph-oriented data.

---

## T064 — Implement Dependency API

**Requirements:** FR-005
**Dependencies:** T059

```http
GET /api/analyses/{id}/dependencies
```

---

## T065 — Implement Endpoint API

**Requirements:** FR-006
**Dependencies:** T059

```http
GET /api/analyses/{id}/endpoints
```

---

## T066 — Implement Database API

**Requirements:** FR-007
**Dependencies:** T059

```http
GET /api/analyses/{id}/database
```

---

## T067 — Implement File Explorer API

**Requirements:** FR-004
**Dependencies:** T059

```http
GET /api/analyses/{id}/files
```

---

## T068 — Implement Symbol API

**Requirements:** FR-004
**Dependencies:** T059

```http
GET /api/analyses/{id}/symbols/{symbolId}
```

---

## T069 — Implement Evidence API

**Requirements:** FR-008
**Dependencies:** T058

```http
GET /api/analyses/{id}/evidence
```

---

# 13. Phase 10 — Frontend Foundation

## T070 — Initialize Frontend Repository

**Requirements:** FR-010
**Dependencies:** T060

Create separate frontend repository:

```text
repolens-frontend
```

Use:

```text
Next.js
TypeScript
Tailwind CSS
```

---

## T071 — Configure Frontend API Client

**Requirements:** FR-010
**Dependencies:** T070, T060

Create typed API client.

---

## T072 — Create Application Layout

**Requirements:** FR-010
**Dependencies:** T070

Main navigation:

```text
Analyze
Projects
Overview
Architecture
Dependencies
APIs
Database
Files
AI Chat
```

---

## T073 — Create Analyze Page

**Requirements:** FR-001
**Dependencies:** T071

Allow:

```text
Git URL
ZIP upload
```

---

## T074 — Create Analysis Progress UI

**Requirements:** FR-002
**Dependencies:** T061, T073

Display:

```text
Queued
Cloning
Scanning
Analyzing
Indexing
Completed
Failed
```

---

# 14. Phase 11 — Visualization

## T075 — Implement Project Overview

**Requirements:** FR-003
**Dependencies:** T062

Display:

* detected languages
* project count
* file count
* symbol count
* endpoints
* database entities
* analysis status

---

## T076 — Implement Architecture Visualization

**Requirements:** FR-005
**Dependencies:** T063

Use React Flow or equivalent.

---

## T077 — Implement Dependency Visualization

**Requirements:** FR-005
**Dependencies:** T064

Display:

```text
Project → Project
File → File
Symbol → Symbol
```

relationships where available.

---

## T078 — Implement API Explorer

**Requirements:** FR-006
**Dependencies:** T065

Display:

* method
* route
* controller/handler
* source evidence

---

## T079 — Implement Database Explorer

**Requirements:** FR-007
**Dependencies:** T066

Display:

* entities
* properties
* relationships

---

## T080 — Implement File / Symbol Explorer

**Requirements:** FR-004
**Dependencies:** T067, T068

Allow navigation:

```text
File
 ↓
Symbol
 ↓
Evidence
```

---

# 15. Phase 12 — AI / RAG

## T081 — Define AI Provider Abstraction

**Requirements:** FR-009, NFR-AI-001
**Dependencies:** T003

Create:

```text
IAiProvider
```

AI SDKs MUST NOT leak into Domain.

---

## T082 — Define Embedding Provider Abstraction

**Requirements:** FR-009
**Dependencies:** T003

Create:

```text
IEmbeddingProvider
```

---

## T083 — Implement Document Chunking

**Requirements:** FR-009
**Dependencies:** T025, T058

Chunk repository documentation/source information for retrieval.

Every chunk MUST preserve evidence metadata.

---

## T084 — Configure pgvector

**Requirements:** FR-009
**Dependencies:** T010

Configure vector storage.

---

## T085 — Generate Embeddings

**Requirements:** FR-009
**Dependencies:** T082, T083

Generate embeddings only for approved/safe content.

---

## T086 — Implement Vector Retrieval

**Requirements:** FR-009
**Dependencies:** T084, T085

Retrieve relevant chunks based on user query.

---

## T087 — Implement Evidence-Grounded RAG

**Requirements:** FR-009, NFR-AI-001
**Dependencies:** T081, T086

Pipeline:

```text
Question
 ↓
Retrieve evidence
 ↓
Build context
 ↓
AI generation
 ↓
Evidence validation
 ↓
Response
```

---

## T088 — Implement AI Evidence Validation

**Requirements:** NFR-AI-001
**Dependencies:** T087

Validate that generated claims are supported by retrieved evidence.

Unsupported claims MUST be removed, marked uncertain, or result in an insufficient-evidence response.

---

## T089 — Implement AI Confidence

**Requirements:** NFR-AI-001
**Dependencies:** T088

Allowed values:

```text
High
Medium
Low
Unknown
```

Confidence MUST be evidence-based.

---

## T090 — Implement "Insufficient Evidence" Response

**Requirements:** NFR-AI-001
**Dependencies:** T088

When repository evidence is insufficient:

```text
Insufficient evidence in the analyzed repository.
```

The system MUST NOT fabricate an answer.

---

## T091 — Implement Chat API

**Requirements:** FR-009
**Dependencies:** T087–T090

Endpoint:

```http
POST /api/analyses/{id}/chat
```

---

## T092 — Implement AI Chat UI

**Requirements:** FR-009, FR-010
**Dependencies:** T091

Display:

* answer
* confidence
* evidence
* referenced files
* line ranges where available

---

# 16. Phase 13 — Security

## T093 — Harden ZIP Extraction

**Requirements:** NFR-SEC-001
**Dependencies:** T029

Test:

* `../`
* absolute paths
* symlink-like entries
* oversized archives
* excessive file counts

---

## T094 — Enforce Repository Resource Limits

**Requirements:** NFR-SEC-002
**Dependencies:** T030

Limits:

* maximum repository size
* maximum ZIP size
* maximum file count
* maximum file size
* maximum analysis duration

---

## T095 — Prevent Secret Leakage to AI

**Requirements:** NFR-SEC-001
**Dependencies:** T034, T085

Ensure detected secret-like content cannot enter:

```text
chunks
embeddings
AI prompts
logs
chat context
```

unless explicitly designed as redacted evidence.

---

## T096 — Prevent Arbitrary Repository Code Execution

**Requirements:** NFR-SEC-001
**Dependencies:** T032

The analyzer MUST inspect repository files without executing untrusted project code.

---

# 17. Phase 14 — Testing

## T097 — Domain Unit Tests

**Dependencies:** T026

Cover:

* entity creation
* validation
* lifecycle
* invalid states

---

## T098 — Application Unit Tests

**Dependencies:** T054

Cover:

* orchestration
* repository creation
* status transitions
* error handling

---

## T099 — Analysis Unit Tests

**Dependencies:** T038–T051

Test representative repositories/snippets for:

* symbols
* dependencies
* endpoints
* EF entities
* relationships

---

## T100 — Analysis Golden Dataset

**Requirements:** NFR-TEST-001
**Dependencies:** T057

Create small known repositories with expected analysis results.

Example:

```text
sample-csharp-api
sample-nextjs-app
sample-typescript-app
```

---

## T101 — Integration Tests

**Requirements:** NFR-TEST-001
**Dependencies:** T059, T060–T069

Test:

```text
repository input
→ analysis
→ database
→ API response
```

---

## T102 — Frontend Tests

**Dependencies:** T070–T080

Test:

* analyze form
* progress state
* overview
* graph rendering
* API explorer
* chat

---

## T103 — Security Tests

**Requirements:** NFR-SEC-001
**Dependencies:** T093–T096

Verify:

* ZIP traversal prevention
* secret redaction
* resource limits
* no arbitrary execution

---

# 18. Phase 15 — AI Evaluation

## T104 — Create AI Evaluation Dataset

**Requirements:** NFR-AI-002
**Dependencies:** T100, T092

Create questions such as:

```text
What is the architecture of this project?

Where is authentication implemented?

What API endpoints exist?

Which classes depend on X?

Which database entities are present?

Where is the database connection configured?
```

---

## T105 — Evaluate Retrieval Quality

**Dependencies:** T104

Measure whether relevant evidence is retrieved.

---

## T106 — Evaluate Answer Grounding

**Requirements:** NFR-AI-001
**Dependencies:** T104

Measure:

* supported claims
* unsupported claims
* evidence correctness
* unknown handling

---

## T107 — Evaluate Unknown / Insufficient Evidence Behavior

**Requirements:** NFR-AI-001
**Dependencies:** T106

Test questions that cannot be answered from repository evidence.

Expected behavior:

```text
Unknown / insufficient evidence
```

rather than fabricated information.

---

# 19. Phase 16 — Performance

## T108 — Measure Repository Scan Performance

**Requirements:** NFR-PERF-001
**Dependencies:** T032

Measure representative repository sizes.

---

## T109 — Measure Static Analysis Performance

**Dependencies:** T057

Measure:

```text
scan time
parse time
analysis time
persistence time
```

---

## T110 — Measure RAG Latency

**Requirements:** NFR-PERF-001
**Dependencies:** T087

Measure:

```text
retrieval latency
AI generation latency
total response time
```

---

## T111 — Add Safe Caching

**Requirements:** NFR-PERF-002
**Dependencies:** T108, T110

Cache deterministic analysis artifacts where appropriate.

Cache keys MUST distinguish repositories/versions.

---

# 20. Phase 17 — Observability and Reliability

## T112 — Add Analysis Metrics

**Dependencies:** T054

Track:

```text
analysis duration
files scanned
files skipped
symbols extracted
issues
embedding count
AI requests
```

---

## T113 — Add Failure Diagnostics

**Dependencies:** T055

Failures MUST expose useful diagnostics without exposing secrets.

---

## T114 — Implement Retry Boundaries

**Dependencies:** T054

Retry only safe transient operations.

Do not blindly retry:

* invalid repositories
* invalid ZIPs
* parser failures
* security violations

---

# 21. Phase 18 — Documentation

## T115 — Document Local Development

**Dependencies:** T012, T070

Document:

```text
prerequisites
database startup
backend startup
frontend startup
environment variables
migration commands
test commands
```

---

## T116 — Document Architecture

**Requirements:** NFR-ARCH-001
**Dependencies:** T059

Document:

```text
system architecture
module responsibilities
dependency direction
analysis pipeline
AI/RAG pipeline
security boundaries
```

---

## T117 — Document API

**Requirements:** FR-010
**Dependencies:** T060–T069, T091

Ensure OpenAPI matches:

```text
contracts/api.md
```

---

## T118 — Document Supported Repository Types

**Dependencies:** T036, T050

Document:

```text
supported languages
supported project types
supported framework patterns
known limitations
```

---

# 22. Phase 19 — Final MVP Validation

## T119 — Full Backend Build

**Dependencies:** All backend implementation tasks

Run:

```text
dotnet build
```

Acceptance:

* zero build errors
* warnings documented

---

## T120 — Full Backend Test Suite

Run:

```text
dotnet test
```

Acceptance:

* all required tests pass
* failures investigated

---

## T121 — Full Frontend Validation

Run:

```text
npm run build
npm run lint
npm test
```

or equivalent configured commands.

---

## T122 — End-to-End Repository Analysis

Analyze at least:

1. A C# ASP.NET Core project.
2. A TypeScript/Next.js project.
3. A mixed repository.

Validate:

```text
Input
→ Scan
→ Static analysis
→ Knowledge model
→ Persistence
→ Visualization
→ RAG
→ AI answer
→ Evidence
```

---

## T123 — Security Acceptance Test

Verify:

* malicious ZIP rejection
* secret exclusion
* repository isolation
* no arbitrary execution
* resource limits

---

## T124 — AI Grounding Acceptance Test

Verify:

* supported answers cite evidence
* unsupported answers do not invent facts
* confidence reflects evidence
* insufficient evidence is explicitly reported

---

## T125 — MVP Release Review

Review:

### Functional

* [ ] Git URL analysis
* [ ] ZIP upload
* [ ] repository scanning
* [ ] C# analysis
* [ ] TypeScript/JavaScript analysis
* [ ] architecture graph
* [ ] dependency graph
* [ ] API explorer
* [ ] database explorer
* [ ] file/symbol explorer
* [ ] AI chat
* [ ] evidence references

### Architecture

* [ ] Clean Architecture boundaries
* [ ] static analysis isolated
* [ ] AI provider abstraction
* [ ] embedding abstraction
* [ ] repository source abstraction

### Security

* [ ] no arbitrary code execution
* [ ] secret protection
* [ ] ZIP traversal protection
* [ ] resource limits
* [ ] repository isolation

### Quality

* [ ] unit tests
* [ ] integration tests
* [ ] analysis tests
* [ ] frontend tests
* [ ] AI evaluation dataset
* [ ] documentation

---

# 23. Critical Path

The recommended implementation order is:

```text
T001
 ↓
T002 → T003 → T004 → T005
 ↓
T006 → T007 → T008
 ↓
T009 → T010 → T011
 ↓
T015–T026
 ↓
T027–T036
 ↓
T037–T046
 ↓
T047–T051
 ↓
T052–T059
 ↓
T060–T069
 ↓
T070–T080
 ↓
T081–T092
 ↓
T093–T096
 ↓
T097–T107
 ↓
T108–T114
 ↓
T115–T118
 ↓
T119–T125
```

---

# 24. Parallel Work Opportunities

After the foundation is stable, the following work can proceed independently:

### Analysis

```text
T037–T046
```

### TypeScript

```text
T047–T051
```

### Backend APIs

```text
T060–T069
```

### Frontend

```text
T070–T080
```

### AI

```text
T081–T092
```

However, API and frontend implementation MUST follow the API contract.

---

# 25. MVP Scope Boundary

The following are explicitly OUT OF MVP:

```text
Private GitHub repositories
GitHub OAuth
Multi-user collaboration
Team workspaces
IDE plugins
Pull request review
Automatic code modification
Automatic refactoring
Full vulnerability scanner
CI/CD integration
Deployment analysis
Kubernetes infrastructure analysis
Cloud architecture discovery
Support for every programming language
Autonomous code execution
Message brokers
Distributed microservices architecture
```

These features MUST NOT be introduced merely because they appear useful.

---

# 26. Definition of Done

A task is complete only when:

1. Required implementation exists.
2. Scope has not expanded unexpectedly.
3. Relevant tests pass.
4. Build succeeds where applicable.
5. API contract remains compatible.
6. Security constraints remain satisfied.
7. No unrelated files were modified.
8. Claude Code final report is provided.

The MVP is complete only when T125 is satisfied.

---

# 27. Core Engineering Rule

RepoLens AI must never behave as:

```text
Repository
   ↓
LLM
   ↓
Guess
```

The required architecture is:

```text
Repository
   ↓
Deterministic Scanner
   ↓
Static Analysis
   ↓
Knowledge Model
   ↓
Evidence
   ↓
Retrieval
   ↓
AI Explanation
   ↓
Validated Answer
```

The repository is the source of truth.

AI is the explanation layer.
