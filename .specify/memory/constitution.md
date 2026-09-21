# RepoLens AI Constitution

**Version:** 1.0.0
**Ratified:** 2026-09-17
**Status:** Ratified
**Project:** RepoLens AI

---

## 1. Purpose

RepoLens AI is an AI-assisted software project analysis platform designed to help developers understand an unfamiliar codebase.

The system accepts a software repository, analyzes its structure and source code, constructs a machine-readable project knowledge model, and presents the result through architecture diagrams, dependency views, API/database exploration, file and symbol navigation, and evidence-grounded AI explanations.

The primary engineering principle is:

> **Static analysis establishes what exists. AI explains what it means.**

AI-generated information MUST NOT be treated as authoritative when deterministic evidence can be obtained directly from the repository.

This Constitution defines the non-negotiable engineering principles, architectural constraints, quality requirements, security rules, and development practices that govern RepoLens AI.

All specifications, plans, tasks, implementations, tests, and reviews MUST comply with this Constitution unless an explicit amendment is approved.

---

# 2. Core Principles

## Principle I — Evidence Before Explanation

All important project-understanding claims MUST be grounded in evidence extracted from the analyzed repository.

The system SHOULD prefer deterministic evidence from:

* Source files
* Project files
* Solution files
* Dependency manifests
* Configuration structure
* Database mappings
* API attributes/routes
* AST/syntax trees
* Symbol relationships
* Static analysis results

AI MAY:

* Summarize code
* Explain relationships
* Describe likely responsibilities
* Generate human-readable architecture explanations
* Answer questions using retrieved repository evidence
* Identify patterns from extracted evidence

AI MUST NOT:

* Invent files, classes, methods, APIs, tables, dependencies, or relationships
* Present unsupported assumptions as repository facts
* Hide uncertainty when evidence is incomplete
* Replace deterministic analysis where deterministic analysis is available

When sufficient evidence is unavailable, the system MUST explicitly communicate uncertainty.

Acceptable responses include:

> "Insufficient evidence to determine this from the repository."

or:

> "This relationship could not be confirmed from the analyzed source."

---

## Principle II — Deterministic Static Analysis

Repository structure and code facts SHOULD be extracted through deterministic analysis whenever technically possible.

For supported languages, the system SHOULD use language-aware analyzers instead of relying exclusively on LLM interpretation.

For C#, the preferred analysis technology is **Roslyn**.

For TypeScript/JavaScript, the system SHOULD use an AST-based parser such as Tree-sitter or an equivalent deterministic parser.

Static analysis SHOULD identify, where applicable:

* Projects
* Files
* Namespaces/modules
* Classes
* Interfaces
* Records
* Enums
* Methods/functions
* Properties
* Constructors
* Attributes/decorators
* Imports
* Exports
* Inheritance
* Interface implementations
* References
* Dependencies
* API endpoints
* Database entities
* Database relationships
* Configuration structures

The AI layer MUST consume static-analysis results rather than independently inventing a project model.

---

## Principle III — Traceability and Evidence

Every significant generated project insight SHOULD be traceable to its source evidence.

Evidence SHOULD contain sufficient information to locate the original source, including where applicable:

* Repository identifier
* File path
* Symbol name
* Start line
* End line
* Language
* Evidence type
* Explanation/reason

For example:

```json
{
  "file": "src/Services/ContractService.cs",
  "symbol": "ContractService.ApproveAsync",
  "startLine": 24,
  "endLine": 68,
  "reason": "Contains the approval transition logic."
}
```

AI-generated answers SHOULD provide evidence references whenever the answer depends on repository-specific facts.

The system MUST distinguish:

1. Repository fact
2. Derived relationship
3. AI interpretation
4. Uncertain inference

These categories MUST NOT be silently mixed.

---

## Principle IV — AI as an Interpreter, Not the Source of Truth

AI is a supporting intelligence layer.

The system architecture MUST NOT depend on the assumption that an LLM can accurately reconstruct an entire repository from raw source code alone.

The preferred pipeline is:

```text
Repository
    ↓
Repository Scanner
    ↓
Language Detection
    ↓
Static Analysis
    ↓
Knowledge Model
    ↓
Persistent Storage
    ↓
Retrieval / RAG
    ↓
AI Interpretation
    ↓
Evidence Validation
    ↓
User Response
```

AI MAY explain information already present in the knowledge model.

AI SHOULD NOT directly determine authoritative structural facts when deterministic analysis can provide those facts.

---

## Principle V — Spec-Driven Development

RepoLens AI MUST follow a specification-first development process.

Implementation SHOULD proceed through:

```text
Constitution
    ↓
Specification
    ↓
Architecture / Plan
    ↓
Task Breakdown
    ↓
Implementation
    ↓
Testing
    ↓
Evaluation
```

Developers and AI coding agents MUST inspect the relevant specification and architecture before implementing a task.

Every implementation task SHOULD:

* Have a clear objective
* Identify its scope
* Identify affected components
* Define acceptance criteria
* Define validation requirements
* Avoid unrelated modifications

AI coding agents MUST NOT expand the task scope without explicit justification.

If implementation requires a specification change, the specification SHOULD be updated before or together with the implementation.

---

## Principle VI — Clean Architecture and Separation of Concerns

The backend MUST follow a clear layered architecture.

The expected structure is:

```text
RepoLens.Api
        ↓
RepoLens.Application
        ↓
RepoLens.Domain

RepoLens.Infrastructure
        ↓
RepoLens.Application
RepoLens.Domain

RepoLens.Analysis
        ↓
RepoLens.Domain
```

### Domain

The Domain layer contains:

* Entities
* Value objects
* Domain rules
* Domain enums
* Domain-level abstractions

The Domain MUST NOT depend on:

* ASP.NET Core
* Entity Framework Core
* PostgreSQL
* LLM providers
* HTTP clients
* UI frameworks
* Infrastructure implementations

### Application

The Application layer contains:

* Use cases
* Application services
* DTOs
* Interfaces
* Validation
* Application-level orchestration

Application logic MUST depend on abstractions rather than infrastructure implementations.

### Infrastructure

Infrastructure contains implementations for:

* Database access
* Entity Framework Core
* PostgreSQL
* pgvector
* Repository persistence
* External services
* AI provider implementations
* Repository download/storage mechanisms

### API

The API layer contains:

* HTTP endpoints
* Request/response models
* Authentication/authorization boundaries
* Dependency injection configuration
* HTTP-specific concerns

Business logic MUST NOT be placed directly inside controllers.

### Analysis

The Analysis layer contains:

* Repository scanning
* Language detection
* Language-specific analyzers
* AST processing
* Symbol extraction
* Dependency extraction
* API extraction
* Database analysis
* Knowledge-model generation

Analysis components MUST remain independent from UI concerns.

---

## Principle VII — Security and Repository Privacy

Source repositories may contain sensitive information.

RepoLens AI MUST treat analyzed repositories as potentially confidential.

The system MUST:

* Isolate repository data between users/projects
* Prevent cross-repository retrieval
* Validate uploaded archives
* Restrict file types where appropriate
* Prevent path traversal during archive extraction
* Avoid executing arbitrary repository code during analysis
* Apply reasonable resource limits
* Avoid exposing secrets through AI prompts
* Avoid exposing secrets through generated responses

The analyzer MUST NOT execute arbitrary application code merely to understand the repository.

Potential secrets SHOULD be detected and excluded or redacted before content is sent to an external AI provider.

Examples include:

* API keys
* Access tokens
* Passwords
* Private keys
* Connection strings
* Authentication credentials
* `.env` secrets
* Cloud credentials

Files such as the following SHOULD receive special handling:

```text
.env
.env.*
*.pem
*.key
*.pfx
secrets.*
credentials.*
appsettings.*.json
```

The exact secret-detection policy MAY evolve independently, but the security principle is mandatory.

---

## Principle VIII — Repository Isolation

Each analyzed repository MUST have a clear ownership boundary.

All repository-derived data MUST be associated with the corresponding analysis/repository identity.

This applies to:

* Source files
* Symbols
* Dependencies
* API endpoints
* Database entities
* Evidence
* Embeddings
* Documents/chunks
* AI conversations
* Generated explanations

Retrieval queries MUST include repository/analysis scope.

The system MUST prevent an AI query for Repository A from retrieving evidence belonging to Repository B.

Cross-repository analysis MAY be introduced in a future version only with explicit specification and security rules.

---

## Principle IX — Knowledge Model Over Raw Text

RepoLens AI SHOULD represent the analyzed repository as a structured knowledge model.

The model SHOULD support entities such as:

```text
Repository
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

Relationships SHOULD represent meaningful connections such as:

```text
Repository
 └── CONTAINS → Project

Project
 └── CONTAINS → SourceFile

SourceFile
 └── DEFINES → CodeSymbol

CodeSymbol
 ├── DEPENDS_ON → CodeSymbol
 ├── IMPLEMENTS → CodeSymbol
 ├── INHERITS → CodeSymbol
 └── CALLS → CodeSymbol

Project
 └── EXPOSES → ApiEndpoint

CodeSymbol
 └── MAPS_TO → DatabaseEntity
```

The knowledge model SHOULD be the bridge between deterministic analysis and AI explanation.

---

## Principle X — Explainability of AI Responses

AI responses MUST be designed for verification.

When answering questions about a repository, the system SHOULD provide:

* Answer
* Confidence/uncertainty indicator
* Evidence
* Source file references
* Relevant symbols
* Line ranges when available

Example:

```json
{
  "answer": "ContractService handles the approval transition.",
  "confidence": "high",
  "evidence": [
    {
      "file": "ContractService.cs",
      "startLine": 24,
      "endLine": 68,
      "reason": "The method updates the contract approval state."
    }
  ]
}
```

Confidence MUST be derived from evidence quality/retrieval support rather than being an arbitrary number generated by the LLM.

If evidence conflicts, the system SHOULD surface the conflict rather than silently selecting an answer.

---

## Principle XI — Simplicity Before Complexity

The MVP MUST prioritize a small number of reliable capabilities over a large number of incomplete capabilities.

The project SHOULD avoid introducing infrastructure that does not directly support the MVP.

Examples of complexity that SHOULD NOT be introduced prematurely:

* Distributed microservices
* Event-driven architecture without a concrete requirement
* Multiple databases without necessity
* Kubernetes
* Complex workflow engines
* Custom vector databases when PostgreSQL/pgvector is sufficient
* Autonomous AI agents without a defined use case

The default architectural preference is:

> **Modular monolith first, distributed architecture only when justified.**

---

## Principle XII — Testability

All important deterministic behavior MUST be testable.

Testing SHOULD exist at multiple levels:

### Unit Tests

Used for:

* Domain rules
* Application services
* Parsers
* Static analyzers
* Knowledge-model transformations
* Validation logic

### Integration Tests

Used for:

* Database persistence
* PostgreSQL/pgvector integration
* Repository ingestion
* API behavior
* Cross-layer interactions

### Analysis Tests

Used for:

* Symbol extraction
* Dependency extraction
* API detection
* Database mapping detection
* Language-specific parsing

### AI Evaluation

AI behavior MUST be evaluated separately from deterministic software tests.

Evaluation SHOULD measure:

* Groundedness
* Evidence correctness
* Retrieval relevance
* Answer accuracy
* Unsupported-claim rate
* Failure behavior when evidence is missing

AI evaluation results MUST NOT be treated as equivalent to ordinary unit-test results.

---

## Principle XIII — No Hidden Reasoning or Unsupported Claims

The product is intended to explain software, not expose private chain-of-thought reasoning.

The system SHOULD expose:

* Evidence
* Relevant code
* Relationships
* Explicit reasoning summaries
* Analysis metadata

The system MUST NOT claim that an internal hidden reasoning process is repository evidence.

Statements such as:

> "The AI knows this because..."

SHOULD be avoided unless supported by explicit evidence.

Instead, explanations SHOULD state:

> "This is supported by `X.cs`, lines 20–40, where..."

---

## Principle XIV — Controlled Language Support

RepoLens AI SHOULD initially prioritize:

1. C#
2. TypeScript
3. JavaScript

C# SHOULD receive the highest analysis depth because the initial implementation targets the .NET ecosystem.

Additional languages MUST NOT be introduced into the MVP merely for breadth.

A language SHOULD be considered supported only when the system can provide meaningful:

* File detection
* Symbol extraction
* Dependency extraction
* Architecture information
* Evidence references

Unsupported languages MUST be clearly identified instead of being silently analyzed as supported.

---

# 3. Architecture Principles

## 3.1 Preferred System Architecture

The system SHOULD follow this architecture:

```text
┌─────────────────────────────────────┐
│              Frontend               │
│ Next.js + TypeScript + React Flow  │
└──────────────────┬──────────────────┘
                   │ HTTP
                   ▼
┌─────────────────────────────────────┐
│             RepoLens API             │
│        ASP.NET Core / .NET 10       │
└──────────────────┬──────────────────┘
                   │
          Application Layer
                   │
        ┌──────────┴──────────┐
        ▼                     ▼
┌───────────────┐      ┌───────────────┐
│    Analysis   │      │    Domain     │
└───────┬───────┘      └───────────────┘
        │
        ▼
┌─────────────────────────────────────┐
│           Infrastructure            │
│ PostgreSQL / pgvector / AI / Git   │
└─────────────────────────────────────┘
```

The architecture MAY evolve when justified by implementation requirements, but changes MUST preserve the principles of separation, testability, and repository isolation.

---

# 4. Repository Analysis Principles

## 4.1 Never Execute Untrusted Application Code

Repository analysis MUST be performed through static inspection whenever possible.

The system MUST NOT automatically:

* Run arbitrary executables from the repository
* Execute application startup code
* Execute build scripts from an untrusted repository
* Run package installation scripts without explicit security controls

Build or execution-based analysis, if introduced later, MUST be isolated and explicitly specified.

---

## 4.2 Respect Ignore Rules

The analyzer SHOULD respect common ignore mechanisms such as:

```text
.gitignore
.git/info/exclude
node_modules
bin
obj
dist
build
coverage
.vscode
.idea
```

Generated files SHOULD NOT be treated as primary architectural evidence unless explicitly required.

---

## 4.3 Large Repository Handling

The system SHOULD use incremental processing for large repositories.

It SHOULD avoid loading an entire repository into memory at once.

Processing SHOULD be capable of:

* File filtering
* Streaming/archive extraction
* Incremental parsing
* Incremental persistence
* Chunk-level indexing

Resource limits SHOULD be enforced to prevent denial-of-service conditions from malicious or extremely large repositories.

---

# 5. RAG Principles

## 5.1 Symbol-Aware Chunking

Repository content SHOULD be chunked based on meaningful code boundaries.

Preferred boundaries include:

* Class
* Interface
* Method
* Function
* Component
* Module
* Configuration section

Chunks SHOULD contain metadata such as:

```text
repositoryId
analysisId
filePath
symbol
language
startLine
endLine
contentType
```

Arbitrary fixed-size text splitting SHOULD NOT be the only chunking strategy for source code.

---

## 5.2 Retrieval Scope

Every retrieval operation MUST be scoped to the current repository/analysis.

Conceptually:

```text
query
  +
repositoryId
  +
analysisId
  ↓
retrieval
  ↓
evidence
  ↓
LLM
```

The AI provider MUST NOT receive unrelated repository content.

---

## 5.3 Retrieval Before Generation

For repository-specific questions, the system SHOULD retrieve relevant evidence before generating an answer.

The system SHOULD prefer:

```text
Question
 ↓
Intent detection
 ↓
Knowledge graph lookup
 ↓
Vector retrieval
 ↓
Evidence ranking
 ↓
Context construction
 ↓
LLM
```

The system SHOULD combine structured graph information and vector retrieval where appropriate.

---

# 6. AI Provider Principles

The application MUST use an abstraction for AI providers.

Conceptually:

```csharp
public interface IAiProvider
{
    Task<AiResponse> GenerateAsync(
        AiRequest request,
        CancellationToken cancellationToken);
}
```

Application code MUST NOT be tightly coupled to a specific commercial LLM provider.

The provider abstraction SHOULD allow future support for:

* OpenAI-compatible providers
* Local models
* Other cloud AI providers
* Test/fake providers

The application SHOULD be testable without requiring a live external AI service.

---

# 7. API Principles

The API SHOULD follow resource-oriented REST conventions.

Representative endpoints include:

```text
POST   /api/analyses
GET    /api/analyses/{id}
GET    /api/analyses/{id}/overview
GET    /api/analyses/{id}/architecture
GET    /api/analyses/{id}/dependencies
GET    /api/analyses/{id}/endpoints
GET    /api/analyses/{id}/database
GET    /api/analyses/{id}/files
POST   /api/analyses/{id}/chat
```

API contracts MUST be defined before implementation of significant frontend/backend integration.

Responses SHOULD:

* Use consistent status codes
* Validate input
* Return predictable error structures
* Avoid leaking sensitive repository information
* Avoid exposing infrastructure-specific implementation details

---

# 8. Frontend Principles

The frontend SHOULD prioritize understanding over decoration.

Primary user workflows SHOULD include:

```text
Input Repository
      ↓
Analyze
      ↓
Overview
      ↓
Architecture
      ↓
Dependencies / APIs / Database
      ↓
Files / Symbols
      ↓
Ask AI
```

Architecture visualization SHOULD prioritize:

* Readability
* Navigation
* Filtering
* Zooming
* Relationship clarity
* Source traceability

Diagrams MUST NOT imply relationships that are not supported by analysis evidence.

---

# 9. Database Principles

PostgreSQL SHOULD be the primary persistence layer for the MVP.

pgvector SHOULD be used for vector retrieval where required.

The database SHOULD preserve relationships between:

```text
Repository
 → Analysis
   → Project
     → SourceFile
       → CodeSymbol
```

and related analytical artifacts.

Database migrations MUST be version-controlled.

Schema changes MUST be reviewed against the domain model and API contracts.

---

# 10. Error Handling and Failure Behavior

The system MUST fail explicitly when analysis cannot be completed.

Possible states include:

```text
Created
   ↓
Cloning
   ↓
Scanning
   ↓
Analyzing
   ↓
Indexing
   ↓
Completed
```

Failure SHOULD produce:

```text
Failed
```

with useful diagnostic information.

The system SHOULD distinguish between:

* Invalid repository
* Download failure
* Unsupported language
* Parsing failure
* Database failure
* AI provider failure
* Resource limit exceeded
* Security rejection

A partial analysis MAY be preserved if the system can clearly identify which portions succeeded and which failed.

---

# 11. Observability

The system SHOULD provide sufficient logging to diagnose analysis failures.

Logs SHOULD include:

* Analysis ID
* Repository ID
* Processing stage
* Duration
* Error category
* Relevant diagnostic information

Logs MUST NOT expose:

* API keys
* Passwords
* Private tokens
* Full sensitive source content
* Secrets detected during scanning

Structured logging SHOULD be preferred over arbitrary console output.

---

# 12. Performance Principles

Performance targets MUST be defined using measurable workloads.

The system SHOULD measure at least:

* Repository ingestion time
* Static analysis time
* Knowledge-model persistence time
* Embedding/indexing time
* AI response latency
* Memory consumption
* Database query latency

Optimization MUST be evidence-driven.

Premature optimization SHOULD NOT override simplicity or correctness.

---

# 13. Quality Gates

A task MUST NOT be considered complete solely because code compiles.

Before completion, the implementation SHOULD pass applicable gates:

### Gate 1 — Scope

* Only required files/components changed
* No unrelated feature modifications

### Gate 2 — Build

* Solution builds successfully

### Gate 3 — Tests

* Relevant automated tests pass

### Gate 4 — Static Analysis

* Analyzer output is deterministic and valid

### Gate 5 — Security

* Sensitive data is not unintentionally exposed

### Gate 6 — Evidence

* Repository-specific AI claims are grounded

### Gate 7 — Documentation

* Contracts/specifications are updated when necessary

---

# 14. AI Coding Agent Rules

AI coding agents are first-class development tools in RepoLens AI, but their work MUST remain controlled.

Before editing, an AI agent SHOULD:

1. Inspect the repository
2. Read the relevant specification
3. Read the architecture/plan
4. Identify the assigned task
5. Identify affected files
6. Check existing implementation patterns
7. Form an implementation plan

During implementation, the agent MUST:

* Stay within task scope
* Preserve existing architecture
* Avoid unnecessary refactoring
* Avoid modifying unrelated modules
* Avoid inventing requirements
* Follow existing naming conventions
* Add/update tests where applicable

After implementation, the agent MUST report:

```text
Implementation Summary
Created Files
Modified Files
Deleted Files
Tests Run
Test Results
Build Results
Warnings
Errors
Blockers
Uncompleted Items
Recommended Next Task
```

If the agent cannot complete a requirement, it MUST state the limitation rather than pretending the task is complete.

---

# 15. Git and Change Management

Changes SHOULD be small and logically grouped.

Commit messages SHOULD follow:

```text
<type>(<scope>): <description>
```

Allowed types include:

```text
feat
fix
refactor
test
docs
chore
```

Examples:

```text
feat(analysis): add Roslyn symbol analyzer
feat(repository): add GitHub repository ingestion
test(analysis): add CSharpAnalyzer tests
fix(rag): enforce repository retrieval scope
docs(spec): define AI evidence contract
```

A commit SHOULD represent one coherent change.

Large unrelated commits SHOULD be avoided.

---

# 16. Definition of Done

A feature is considered complete only when all applicable conditions are satisfied.

### Functional

* Required behavior is implemented
* Acceptance criteria are satisfied
* Error cases are handled

### Architectural

* Correct layer owns the behavior
* Dependencies follow the architecture
* No unnecessary coupling is introduced

### Testing

* Relevant unit tests exist
* Integration tests exist when applicable
* Tests pass

### Security

* Repository isolation is preserved
* Sensitive data is handled safely
* Untrusted repository code is not executed

### AI

* AI behavior is grounded in evidence
* Unsupported claims are rejected or qualified
* Retrieval is scoped correctly

### Documentation

* Specification is consistent with implementation
* API contracts are updated where necessary
* Significant architectural changes are documented

### Change Management

* Changes are logically grouped
* Commit message follows project convention
* No unrelated modifications remain

---

# 17. MVP Scope Discipline

The MVP SHOULD focus on the core workflow:

```text
Repository Input
       ↓
Repository Analysis
       ↓
Project Knowledge Model
       ↓
Architecture / Dependencies / APIs / Database
       ↓
Evidence-Grounded AI Explanation
```

The following SHOULD remain outside the MVP unless explicitly promoted by the specification:

* Private GitHub repository integration
* IDE plugins
* Pull-request review
* Automatic source-code modification
* Full vulnerability scanning
* Enterprise collaboration
* Real-time multi-user editing
* Autonomous code agents
* Deployment automation
* Support for every programming language

Scope expansion MUST be justified by user value and implementation capacity.

---

# 18. Architecture Visualization Rules

Architecture diagrams are derived artifacts.

They MUST represent relationships supported by the knowledge model.

The system SHOULD distinguish:

```text
Confirmed relationship
Derived relationship
Uncertain relationship
```

The UI MUST NOT visually present an uncertain relationship as an authoritative fact.

Every significant node SHOULD be navigable back to its source evidence where possible.

For example:

```text
API Endpoint
     ↓
Controller
     ↓
Application Service
     ↓
Repository
     ↓
Database Entity
```

This chain SHOULD only be displayed when the corresponding relationships are supported by analysis evidence.

---

# 19. Analysis Reproducibility

Given the same repository version and analyzer configuration, deterministic analysis SHOULD produce equivalent results.

Analysis metadata SHOULD record:

* Repository commit/version
* Analyzer version
* Supported language versions where applicable
* Analysis configuration
* Timestamp
* Processing status

This allows users to understand which version of the repository was analyzed.

---

# 20. Versioning and Constitution Amendments

This Constitution follows semantic versioning:

```text
MAJOR.MINOR.PATCH
```

### MAJOR

Used when a fundamental principle is removed or substantially changed.

### MINOR

Used when a new principle or significant requirement is introduced without invalidating existing principles.

### PATCH

Used for clarifications, wording corrections, or non-substantive changes.

Any amendment MUST:

1. Identify the affected principle
2. Explain the reason
3. Update the version
4. Record the amendment date
5. Ensure specifications and plans remain consistent

---

# 21. Principle Priority

When principles conflict, the following priority order SHOULD be used:

```text
1. Security
2. Evidence / Correctness
3. Repository Isolation
4. Architectural Integrity
5. Testability
6. Specification Compliance
7. Performance
8. Convenience
9. Feature Breadth
```

A lower-priority goal MUST NOT justify violating a higher-priority principle.

For example:

* Faster AI responses MUST NOT justify removing evidence.
* More features MUST NOT justify weakening repository isolation.
* Easier implementation MUST NOT justify placing business logic in controllers.
* Better-looking diagrams MUST NOT justify inventing relationships.

---

# 22. Final Engineering Principle

RepoLens AI exists to reduce the time required to understand unfamiliar software.

The product succeeds when a developer can provide an unfamiliar repository and quickly answer questions such as:

```text
What is this project?

How is it structured?

What are its major modules?

How do the modules depend on each other?

Where are the APIs?

Where is the database logic?

What classes implement a particular responsibility?

How does a specific business flow work?

Where in the source code is the evidence?

What does the project do, and what cannot be determined from the repository?
```

The system MUST prioritize trustworthy understanding over impressive but unsupported AI output.

Therefore, the governing principle of RepoLens AI is:

> **Understand the codebase from evidence, model its structure deterministically, and use AI to make that understanding accessible.**

---

# 23. Ratification

This Constitution, version **1.0.0**, is the governing engineering document for RepoLens AI.

All future specifications, architecture decisions, implementation tasks, tests, and AI-assisted development activities MUST remain consistent with these principles unless this Constitution is formally amended.

**Status:** Ratified
**Version:** 1.0.0
**Date:** 2026-09-17
