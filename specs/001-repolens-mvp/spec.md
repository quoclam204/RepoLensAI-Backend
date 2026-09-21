# RepoLens AI — Software Requirements Specification

**Version:** 1.0.0
**Status:** Draft
**Created:** 2026-09-17
**Project:** RepoLens AI
**Constitution:** v1.0.0

---

# 1. Introduction

## 1.1 Purpose

RepoLens AI is an AI-assisted software repository understanding platform.

The system allows a user to provide a software repository through:

* Public Git repository URL
* Uploaded ZIP archive

RepoLens AI analyzes the repository and generates a structured representation of the project.

The generated information is presented through:

* Project overview
* Architecture visualization
* Dependency graph
* API exploration
* Database/entity exploration
* File and symbol exploration
* Evidence references
* AI-powered repository questions and explanations

The primary goal is to reduce the time required for a developer to understand an unfamiliar codebase.

---

# 2. Problem Statement

When developers encounter an unfamiliar project, they commonly need to manually inspect:

* README files
* Folder structures
* Project files
* Source code
* Controllers
* Services
* Repositories
* Database entities
* API routes
* Dependencies
* Configuration
* Relationships between components

This process can be slow and difficult, especially for large projects.

Traditional documentation may also be:

* Missing
* Outdated
* Incomplete
* Difficult to navigate
* Detached from the actual source code

RepoLens AI addresses this problem by analyzing the repository itself and creating an evidence-based project knowledge model.

---

# 3. Product Goal

The product goal is:

> **Given an unfamiliar software repository, help a developer understand its structure, relationships, APIs, database model, and major code responsibilities through automatically generated analysis and evidence-grounded AI explanations.**

The system MUST prioritize correctness and traceability over generating plausible but unsupported explanations.

---

# 4. Target Users

## 4.1 Developer Joining an Existing Project

A developer receives an unfamiliar codebase and needs to understand:

* Project structure
* Architecture
* Main modules
* APIs
* Database
* Important classes
* Dependencies
* Business flows

## 4.2 Student / Learner

A student wants to understand an existing project without manually reading every file.

## 4.3 Technical Reviewer

A developer or reviewer wants a high-level view of:

* Architecture
* Dependencies
* API surface
* Database structure
* Code organization

---

# 5. User Journey

The primary user journey is:

```text
Open RepoLens AI
       ↓
Provide Repository
       ↓
Start Analysis
       ↓
Analysis Processing
       ↓
Project Overview
       ↓
Architecture
       ↓
Dependencies / APIs / Database
       ↓
Files / Symbols
       ↓
Ask AI Questions
       ↓
View Evidence
```

---

# 6. Scope

## 6.1 MVP In Scope

The MVP MUST support:

1. Public Git repository URL input
2. ZIP repository upload
3. Repository validation
4. Repository scanning
5. Language detection
6. C# static analysis
7. TypeScript/JavaScript basic analysis
8. Project/file/symbol extraction
9. Dependency extraction
10. API endpoint extraction
11. Database/entity extraction where detectable
12. Evidence generation
13. Analysis persistence
14. Architecture visualization
15. Dependency visualization
16. API explorer
17. Database explorer
18. File/symbol explorer
19. AI repository Q&A
20. Evidence-grounded AI responses
21. Analysis status tracking
22. Error reporting

---

## 6.2 MVP Out of Scope

The MVP MUST NOT require:

* Private GitHub repository integration
* GitHub OAuth
* IDE plugins
* Pull request review
* Automatic code modification
* Automatic refactoring
* Full vulnerability scanning
* CI/CD analysis
* Production deployment analysis
* Kubernetes analysis
* Real-time collaboration
* Multi-user editing
* Autonomous code agents
* Complete support for every programming language

These capabilities MAY be introduced in later versions.

---

# 7. Supported Repository Sources

## 7.1 Public Git Repository

The user SHOULD be able to provide a public Git repository URL.

Example:

```text
https://github.com/example/project
```

The system MUST validate that the URL represents a supported repository source.

The system MUST NOT require repository credentials for the MVP.

---

## 7.2 ZIP Upload

The user SHOULD be able to upload a ZIP archive containing a project.

The system MUST:

* Validate the archive
* Prevent path traversal
* Apply file/resource limits
* Extract into an isolated workspace
* Ignore unnecessary generated files

---

# 8. Functional Requirements

# FR-001 — Repository Input

The system MUST allow the user to provide a repository using:

* Public Git URL
* ZIP upload

The user MUST be able to identify the repository before starting analysis.

### Acceptance Criteria

* URL input is displayed in the UI.
* ZIP upload is available.
* Invalid input is rejected.
* Valid input can start an analysis.

---

# FR-002 — Repository Validation

Before analysis begins, the system MUST validate the repository.

Validation SHOULD include:

* Repository accessibility
* Archive validity
* Repository size
* Supported file types
* Resource limits
* Unsafe archive paths

If validation fails, the system MUST provide an understandable error.

---

# FR-003 — Repository Scanning

The system MUST scan the repository and identify relevant source files.

The scanner SHOULD identify:

* Source files
* Project files
* Solution files
* Configuration files
* Dependency manifests
* Documentation
* Database-related files

Generated or irrelevant directories SHOULD be ignored.

Examples:

```text
bin/
obj/
node_modules/
dist/
build/
coverage/
```

---

# FR-004 — Language Detection

The system MUST determine which supported programming languages are present.

Initial supported languages:

```text
C#
TypeScript
JavaScript
```

The analysis result MUST identify unsupported languages rather than pretending they are fully supported.

Example:

```text
Detected:
C#        72%
TypeScript 21%
JSON       7%

Analysis:
C#          Supported
TypeScript   Supported
JSON         Partial/Metadata
```

---

# FR-005 — Project Detection

The system MUST identify projects where the repository structure provides sufficient evidence.

For .NET repositories, the analyzer SHOULD recognize:

```text
.sln
.csproj
.fsproj
```

For JavaScript/TypeScript repositories, the analyzer SHOULD inspect:

```text
package.json
tsconfig.json
```

Project information SHOULD include:

* Name
* Path
* Language
* Project type
* Dependencies

---

# FR-006 — Source File Analysis

The system MUST create a representation of analyzed source files.

Each source file SHOULD include:

* Path
* Language
* Project
* File size
* Start/end metadata where applicable
* Analysis status

The system SHOULD preserve enough information to navigate from generated results back to the original file.

---

# FR-007 — C# Symbol Analysis

C# analysis MUST use Roslyn or an equivalent compiler-aware analysis mechanism.

The analyzer SHOULD identify:

* Namespace
* Class
* Interface
* Record
* Enum
* Struct
* Method
* Constructor
* Property
* Field
* Attribute
* Generic type
* Inheritance
* Interface implementation

The analyzer SHOULD identify symbol relationships.

---

# FR-008 — C# Responsibility Classification

The system SHOULD classify common C# components based on explicit code evidence.

Possible classifications include:

```text
Controller
Service
Repository
Entity
DTO
DbContext
Configuration
Middleware
Validator
Interface
Unknown
```

Classification MUST be treated as derived analysis information.

The system MUST NOT present heuristic classification as a guaranteed architectural fact.

---

# FR-009 — TypeScript / JavaScript Analysis

The system SHOULD analyze TypeScript and JavaScript using AST-based parsing.

The analyzer SHOULD identify:

* Files
* Modules
* Imports
* Exports
* Functions
* Classes
* Interfaces
* Types
* Components
* Routes where detectable
* API client calls where detectable

---

# FR-010 — Dependency Analysis

The system MUST identify detectable dependencies between projects, modules, or symbols.

Possible relationships include:

```text
Project A → Project B
File A → File B
Class A → Class B
Method A → Method B
Module A → Module B
```

Each relationship SHOULD have an evidence source.

---

# FR-011 — API Endpoint Detection

The system SHOULD detect HTTP API endpoints from source code.

For ASP.NET Core, the analyzer SHOULD recognize common patterns including:

```text
[ApiController]
[Route]
[HttpGet]
[HttpPost]
[HttpPut]
[HttpPatch]
[HttpDelete]
MapGet
MapPost
MapPut
MapPatch
MapDelete
```

An API endpoint SHOULD contain:

* HTTP method
* Route
* Controller/handler
* Project
* Source file
* Symbol
* Evidence

Example:

```text
GET /api/contracts/{id}

Controller:
ContractController

Method:
GetByIdAsync

Source:
ContractController.cs
```

---

# FR-012 — Database Entity Detection

The system SHOULD detect database-related structures when sufficient source evidence exists.

For Entity Framework Core, the analyzer SHOULD recognize:

* `DbContext`
* `DbSet<T>`
* Entity configurations
* Fluent mappings
* Entity classes
* Relationships
* Primary keys
* Foreign keys where detectable

The system MAY also recognize database information from:

* SQL files
* Migration files
* Schema files

---

# FR-013 — Database Relationship Detection

The system SHOULD identify relationships such as:

```text
One-to-One
One-to-Many
Many-to-Many
```

Relationships MUST only be displayed as confirmed when sufficient evidence exists.

If the relationship is inferred rather than explicitly established, the UI SHOULD identify it as inferred.

---

# FR-014 — Evidence Generation

The analysis pipeline MUST generate evidence for important extracted information.

Evidence SHOULD contain:

```text
Repository
Analysis
File
Symbol
StartLine
EndLine
EvidenceType
Description
```

Examples:

```text
Controller
Service
Repository
Dependency
Endpoint
Entity
Relationship
Configuration
```

---

# FR-015 — Analysis Status

The system MUST expose analysis status.

Minimum states:

```text
Created
Cloning
Scanning
Analyzing
Indexing
Completed
Failed
```

The UI MUST show the current status.

Where possible, the UI SHOULD show the current processing stage.

---

# FR-016 — Project Overview

After successful analysis, the system MUST display a project overview.

The overview SHOULD contain:

* Project name
* Repository information
* Detected languages
* Project count
* Source file count
* Symbol count
* API count
* Database entity count
* Dependency count
* Analysis status

The overview SHOULD provide navigation to deeper analysis views.

---

# FR-017 — Architecture View

The system MUST provide an architecture visualization.

The visualization SHOULD show:

* Projects/modules
* Important components
* Dependencies
* Architectural relationships

Users SHOULD be able to:

* Zoom
* Pan
* Select nodes
* Filter relationships
* Navigate to evidence

The visualization MUST NOT create relationships that are absent from the analysis model.

---

# FR-018 — Dependency Graph

The system MUST provide a dependency graph.

The graph SHOULD support:

* Project dependencies
* Module dependencies
* Symbol dependencies where available

Users SHOULD be able to inspect a selected node's incoming and outgoing dependencies.

---

# FR-019 — API Explorer

The system MUST provide an API explorer.

The explorer SHOULD allow filtering by:

* HTTP method
* Route
* Controller
* Project

Selecting an endpoint SHOULD display its source evidence.

---

# FR-020 — Database Explorer

The system MUST provide a database/entity explorer.

The explorer SHOULD display:

* Entities
* Properties
* Relationships
* DbContext mappings
* Source evidence

---

# FR-021 — File Explorer

The system MUST provide a repository file explorer.

Users SHOULD be able to:

* Browse directories
* Open files
* Search files
* View symbols
* Navigate from symbols to evidence

The MVP MAY display source code in a read-only viewer.

---

# FR-022 — AI Repository Q&A

The system MUST provide an AI chat interface for repository questions.

Example questions:

```text
What is this project?

What architecture does this project use?

Where is authentication implemented?

How does a request reach the database?

Which class handles contract approval?

What APIs are available?

Which project depends on the most other projects?

Where is the database configuration?

How does this feature work?
```

The AI MUST answer using repository-specific evidence.

---

# FR-023 — AI Evidence

AI responses SHOULD provide evidence whenever repository-specific claims are made.

Example:

```json id="sg50lm"
{
  "answer": "Authentication is handled by the API authentication middleware.",
  "confidence": "high",
  "evidence": [
    {
      "file": "Program.cs",
      "startLine": 45,
      "endLine": 57,
      "reason": "Registers authentication and authorization middleware."
    }
  ]
}
```

---

# FR-024 — AI Uncertainty

When the system cannot determine an answer with sufficient evidence, it MUST communicate uncertainty.

Examples:

```text
Insufficient evidence to determine this from the analyzed repository.
```

```text
The relationship appears possible, but it could not be confirmed by static analysis.
```

The AI MUST NOT fabricate an answer simply to satisfy the question.

---

# FR-025 — Repository-Scoped Retrieval

All AI retrieval MUST be scoped to the current repository/analysis.

A question about Repository A MUST NOT retrieve:

* Source code from Repository B
* Embeddings from Repository B
* Evidence from Repository B
* Analysis metadata from Repository B

---

# FR-026 — Analysis Errors

The system MUST report analysis failures.

Errors SHOULD identify:

* Stage
* Error type
* Affected file/project when available
* Human-readable explanation

The system MUST avoid exposing secrets or sensitive implementation information in errors.

---

# FR-027 — Analysis History

The system SHOULD retain analysis metadata.

The MVP MAY support viewing previous analyses for the same repository.

Each analysis SHOULD record:

* Repository
* Repository version/commit where available
* Start time
* Completion time
* Status
* Analyzer version

---

# 9. Non-Functional Requirements

# NFR-001 — Correctness

Deterministically extracted facts MUST accurately represent the analyzed source to the extent supported by the analyzer.

The system MUST prefer "unknown" over unsupported certainty.

---

# NFR-002 — Explainability

Repository-specific AI answers SHOULD be traceable to source evidence.

Users SHOULD be able to navigate from:

```text
AI Answer
   ↓
Evidence
   ↓
File
   ↓
Symbol
   ↓
Source Code
```

---

# NFR-003 — Security

The system MUST:

* Isolate repository data
* Prevent path traversal
* Avoid executing untrusted source code
* Protect credentials
* Detect/redact sensitive information before external AI processing where applicable
* Prevent cross-repository retrieval

---

# NFR-004 — Performance

The system SHOULD process repositories incrementally.

The system SHOULD avoid loading an entire repository into memory unnecessarily.

Performance measurements SHOULD include:

* Repository scanning duration
* Static analysis duration
* Persistence duration
* Embedding duration
* Retrieval latency
* AI response latency

Concrete performance targets SHOULD be established during implementation benchmarking.

---

# NFR-005 — Scalability

The MVP SHOULD support small and medium-sized repositories.

The architecture SHOULD allow future improvements for larger repositories through:

* Background processing
* Incremental analysis
* Caching
* Parallel file analysis
* Incremental indexing

---

# NFR-006 — Availability

A failure in one analysis MUST NOT corrupt unrelated analyses.

A failed analysis SHOULD remain isolated to its repository/analysis context.

---

# NFR-007 — Maintainability

The backend MUST maintain separation between:

* Domain
* Application
* Infrastructure
* API
* Analysis

The system SHOULD use dependency inversion for external services.

---

# NFR-008 — Testability

Important deterministic functionality MUST have automated tests.

The project SHOULD contain:

```text
Unit Tests
Integration Tests
Analysis Tests
AI Evaluation Tests
```

---

# NFR-009 — Extensibility

The system SHOULD allow additional:

* Programming languages
* AI providers
* Analysis strategies
* Repository sources
* Visualization types

without requiring major changes to the core domain model.

---

# 10. Domain Model

The initial domain model SHOULD contain the following concepts.

## Repository

Represents the repository being analyzed.

```text
Id
Name
SourceType
SourceUrl
CreatedAt
```

---

## Analysis

Represents one analysis execution.

```text
Id
RepositoryId
Status
CommitHash
StartedAt
CompletedAt
Error
```

---

## Project

Represents a logical project/module.

```text
Id
AnalysisId
Name
Path
Language
ProjectType
```

---

## SourceFile

Represents an analyzed file.

```text
Id
ProjectId
Path
Language
Size
AnalysisStatus
```

---

## CodeSymbol

Represents a code-level symbol.

```text
Id
SourceFileId
Name
FullName
SymbolType
StartLine
EndLine
```

---

## Dependency

Represents a relationship between analyzed elements.

```text
Id
AnalysisId
SourceId
TargetId
DependencyType
EvidenceId
```

---

## ApiEndpoint

Represents an API endpoint.

```text
Id
ProjectId
Method
Route
SymbolId
EvidenceId
```

---

## DatabaseEntity

Represents a database-related entity.

```text
Id
AnalysisId
Name
EntityType
SourceSymbolId
```

---

## DatabaseRelationship

Represents a database relationship.

```text
Id
AnalysisId
SourceEntityId
TargetEntityId
RelationshipType
EvidenceId
```

---

## Evidence

Represents source evidence supporting an analysis result.

```text
Id
AnalysisId
FilePath
Symbol
StartLine
EndLine
EvidenceType
Description
```

---

## AnalysisIssue

Represents a problem discovered during analysis.

```text
Id
AnalysisId
FilePath
IssueType
Severity
Message
```

---

# 11. Analysis Pipeline

The complete analysis pipeline SHOULD follow:

```text
Repository Input
      ↓
Validation
      ↓
Repository Acquisition
      ↓
File Scanning
      ↓
Ignore Rules
      ↓
Language Detection
      ↓
Language Analyzers
      ↓
Symbol Extraction
      ↓
Dependency Extraction
      ↓
API Extraction
      ↓
Database Extraction
      ↓
Knowledge Model
      ↓
Evidence Generation
      ↓
Persistence
      ↓
Document Chunking
      ↓
Embedding
      ↓
Vector Index
      ↓
Analysis Completed
```

AI Q&A occurs after the analysis/indexing stage.

---

# 12. AI/RAG Pipeline

For repository questions:

```text
User Question
      ↓
Question Processing
      ↓
Repository Scope
      ↓
Knowledge Graph Retrieval
      ↓
Vector Retrieval
      ↓
Evidence Ranking
      ↓
Context Construction
      ↓
LLM
      ↓
Evidence Validation
      ↓
Answer
```

The AI context SHOULD contain only relevant repository information.

---

# 13. AI Response Contract

The conceptual response format is:

```json id="6vv1m4"
{
  "answer": "string",
  "confidence": "high|medium|low|unknown",
  "evidence": [
    {
      "file": "string",
      "symbol": "string",
      "startLine": 0,
      "endLine": 0,
      "reason": "string"
    }
  ]
}
```

The implementation MAY extend this structure but SHOULD preserve these concepts.

---

# 14. Frontend Requirements

The frontend SHOULD use a dashboard-oriented experience.

Primary routes:

```text
/
 /analyze
 /projects/{id}/overview
 /projects/{id}/architecture
 /projects/{id}/dependencies
 /projects/{id}/apis
 /projects/{id}/database
 /projects/{id}/files
 /projects/{id}/chat
```

---

# 15. Architecture View Requirements

The architecture page SHOULD provide:

```text
┌─────────────────────────────────────┐
│             Architecture            │
├─────────────────────────────────────┤
│                                     │
│  Project A ───────→ Project B       │
│      │                  │           │
│      ↓                  ↓           │
│  Controller          Service        │
│                           │         │
│                           ↓         │
│                       Database      │
│                                     │
└─────────────────────────────────────┘
```

Selecting an element SHOULD reveal:

* Name
* Type
* Project
* Source file
* Relationships
* Evidence

---

# 16. API Contract

Representative endpoints:

```text
POST /api/analyses

GET /api/analyses/{id}

GET /api/analyses/{id}/overview

GET /api/analyses/{id}/architecture

GET /api/analyses/{id}/dependencies

GET /api/analyses/{id}/endpoints

GET /api/analyses/{id}/database

GET /api/analyses/{id}/files

GET /api/analyses/{id}/files/{fileId}

POST /api/analyses/{id}/chat
```

Exact request/response contracts SHOULD be defined in `contracts/api.md`.

---

# 17. Security Requirements

## SEC-001 — Archive Safety

ZIP extraction MUST prevent path traversal attacks.

The system MUST reject entries that attempt to escape the analysis workspace.

---

## SEC-002 — Resource Limits

The system SHOULD enforce:

* Maximum upload size
* Maximum extracted size
* Maximum file count
* Maximum individual file size
* Maximum analysis duration where practical

---

## SEC-003 — Secret Protection

Sensitive files SHOULD be excluded from external AI processing.

Detected secrets MUST NOT be returned in normal AI responses.

---

## SEC-004 — Repository Isolation

All database and vector queries MUST contain repository/analysis scope.

---

## SEC-005 — Untrusted Code

The MVP MUST NOT execute arbitrary application code from analyzed repositories.

---

# 18. Acceptance Scenarios

## Scenario 1 — Analyze a Public .NET Repository

**Given** a valid public Git repository URL

**When** the user starts analysis

**Then** RepoLens AI:

1. Downloads the repository
2. Scans the files
3. Detects C#
4. Analyzes C# projects
5. Extracts symbols
6. Extracts dependencies
7. Detects APIs
8. Detects database structures where possible
9. Generates evidence
10. Persists the analysis
11. Displays the project overview

---

## Scenario 2 — Upload ZIP

**Given** a valid project ZIP

**When** the user uploads it

**Then** the system extracts and analyzes it using the same analysis pipeline.

---

## Scenario 3 — Invalid ZIP

**Given** an invalid or unsafe ZIP

**When** the user uploads it

**Then** the system rejects it and displays a meaningful error.

---

## Scenario 4 — Architecture Exploration

**Given** a completed analysis

**When** the user opens Architecture

**Then** the system displays detected projects/components and supported relationships.

Selecting a component MUST reveal its evidence.

---

## Scenario 5 — API Exploration

**Given** a repository containing ASP.NET Core controllers

**When** the analysis completes

**Then** detected endpoints appear in the API explorer.

---

## Scenario 6 — Database Exploration

**Given** a repository containing EF Core entities and DbContext

**When** analysis completes

**Then** detectable entities and relationships appear in the database explorer.

---

## Scenario 7 — Grounded AI Question

**Given** a completed analysis

**When** the user asks:

```text
Where is authentication implemented?
```

**Then** the AI retrieves relevant evidence and provides an answer with source references.

---

## Scenario 8 — Unknown Information

**Given** the repository does not contain sufficient evidence

**When** the user asks a question that cannot be determined

**Then** the AI MUST communicate that the repository does not provide sufficient evidence.

It MUST NOT invent an answer.

---

## Scenario 9 — Repository Isolation

**Given** Repository A and Repository B have both been analyzed

**When** the user asks a question about Repository A

**Then** retrieval MUST only use Repository A evidence.

---

# 19. Evaluation Requirements

RepoLens AI MUST be evaluated against representative repositories.

Evaluation SHOULD include:

## Static Analysis Accuracy

Measure:

* Project detection
* Symbol detection
* Dependency detection
* API detection
* Database detection

## Architecture Accuracy

Compare generated architecture relationships against manually verified ground truth.

## RAG Quality

Measure:

* Retrieval relevance
* Evidence correctness
* Groundedness
* Unsupported claims

## AI Answer Quality

Evaluate questions such as:

```text
What is the project architecture?

Where is authentication implemented?

Which service handles X?

What APIs exist?

How does X reach the database?

Which modules depend on Y?
```

---

# 20. Observability Requirements

Every analysis SHOULD have traceable processing stages.

Example:

```text
Analysis ID: abc123

[✓] Validation
[✓] Repository Scan
[✓] Language Detection
[✓] C# Analysis
[✓] Dependency Analysis
[✓] API Analysis
[✓] Database Analysis
[✓] Persistence
[✓] Embedding
[✓] Indexing
[✓] Completed
```

Errors SHOULD identify the stage where they occurred.

---

# 21. Future Extensions

Potential future versions MAY introduce:

### V1.1

* More language analyzers
* Improved TypeScript analysis
* Better architecture inference
* Improved AI retrieval

### V1.2

* Private repository integration
* GitHub authentication
* Analysis history
* Repository comparison

### V2.0

* IDE integration
* Pull request analysis
* Code-change impact analysis
* Automated documentation generation
* Advanced architecture governance

These features are not required for MVP.

---

# 22. Success Criteria

The MVP is successful when a new developer can provide an unfamiliar repository and, without manually reading the entire codebase, obtain:

1. A clear project overview
2. A visual representation of project architecture
3. A dependency graph
4. A list of APIs
5. A database/entity view
6. A navigable file/symbol structure
7. AI explanations grounded in repository evidence
8. Source references for important claims
9. Explicit uncertainty when information cannot be determined

The product SHOULD significantly reduce the initial exploration effort required to understand an unfamiliar project.

---

# 23. Definition of Done

The MVP is considered complete when:

* Repository URL input works
* ZIP upload works
* Repository validation works
* Repository scanning works
* C# analysis works
* Basic TypeScript/JavaScript analysis works
* Dependency extraction works
* API extraction works
* Database/entity extraction works where supported
* Evidence is generated
* Analysis is persisted
* Overview is displayed
* Architecture visualization works
* Dependency graph works
* API explorer works
* Database explorer works
* File explorer works
* AI Q&A works
* AI answers are repository-scoped
* AI answers include evidence
* Unsupported claims are appropriately rejected/qualified
* Security checks are implemented
* Automated tests pass
* Integration tests pass where applicable
* AI evaluation dataset exists
* Documentation is consistent with implementation

---

# 24. Requirements Traceability

The following high-level mapping MUST be preserved during planning and implementation:

| Requirement Area               | Primary Component                  |
| ------------------------------ | ---------------------------------- |
| Repository Input               | API / Application / Infrastructure |
| Repository Acquisition         | Infrastructure                     |
| Scanning                       | Analysis                           |
| Language Detection             | Analysis                           |
| C# Analysis                    | Analysis                           |
| TypeScript/JavaScript Analysis | Analysis                           |
| Dependency Analysis            | Analysis                           |
| API Analysis                   | Analysis                           |
| Database Analysis              | Analysis                           |
| Knowledge Model                | Domain / Application               |
| Persistence                    | Infrastructure                     |
| Architecture                   | API / Frontend                     |
| Visualization                  | Frontend                           |
| RAG                            | Application / Infrastructure       |
| AI Provider                    | Infrastructure                     |
| AI Chat                        | API / Application / Frontend       |
| Security                       | All applicable layers              |
| Testing                        | Tests                              |
| AI Evaluation                  | Evaluation                         |

---

# 25. Governing Principles

This specification is governed by:

**RepoLens AI Constitution v1.0.0**

In case of conflict:

1. Security requirements take precedence.
2. Evidence and correctness take precedence over AI convenience.
3. Repository isolation takes precedence over retrieval convenience.
4. Architectural integrity takes precedence over implementation shortcuts.
5. MVP scope takes precedence over unnecessary feature expansion.

Any requirement change that conflicts with the Constitution MUST be resolved through an explicit Constitution amendment or specification revision.

---

# 26. Specification Status

**Version:** 1.0.0
**Status:** Draft
**Constitution:** RepoLens AI v1.0.0
**Next Artifact:** `plan.md`
