---
name: evidence-validation
description: "Guidelines and verification rules for evidence grounding, confidence scoring, and validating that architectural facts and RAG outputs trace back to repository source code."
---

# Evidence Validation Skill

In RepoLens AI, **Evidence is a first-class concept**.
The core principle is:
> **"Static analysis establishes what actually exists. AI explains the evidence."**

## Evidence Model Specification

Every piece of extracted knowledge or architectural claim links to an `Evidence` entity (`RepoLens.Domain.Entities.Evidence`):
- **`Id`**: Unique Guid identifier.
- **`AnalysisJobId`**: Guid of the parent analysis job.
- **`Location`** (`SourceLocation` Value Object):
  - `FilePath`: Relative path from repository root (forward-slash normalized: `src/RepoLens.Api/Program.cs`).
  - `StartLine`: 1-indexed start line number ($\ge 1$).
  - `EndLine`: 1-indexed end line number ($\ge StartLine$).
- **`Snippet`**: Exact source snippet extracted from the repository snapshot.
- **`EvidenceType`** (`EvidenceType` Enum):
  - `Declaration`: Class, interface, record, method, or property definition.
  - `Invocation`: Method call, service instantiation, event publishing.
  - `Configuration`: AppSettings, `.env`, dependency injection registration, launchSettings.
  - `Dependency`: Project reference, NuGet package reference, package dependency.
  - `Route`: HTTP endpoint or routing declaration.
  - `Database`: EF Core DbContext, DbSet, or entity schema declaration.
- **`Confidence`** (`ConfidenceScore` Value Object): Float value from 0.0 to 1.0.
- **`Symbol`**: Optional name of the extracted symbol (e.g. class or method name).

## Validation Rules

1. **Existence & Boundary Check**:
   - The file at `FilePath` MUST exist in the analyzed repository version.
   - `StartLine` must be $\ge 1$.
   - `EndLine` must be $\ge StartLine$ and $\le$ total line count of the file.
   - `Snippet` must accurately reflect the file content at the specified line range.

2. **Confidence Assignment**:
   - `1.0`: Direct AST match verified by compiler/Roslyn semantic model.
   - `0.8 - 0.9`: Syntax tree visitor match without full semantic binding.
   - `0.6 - 0.7`: Heuristic or regex-based match on configuration/manifest files.
   - `< 0.5`: Weak heuristic; MUST NOT be used as conclusive proof of an architectural layer.

3. **Grounded RAG / LLM Invariants**:
   - All AI-generated explanations of architecture or code paths MUST cite evidence nodes.
   - If a user asks about a component or feature not supported by extracted evidence, the system MUST return an **"Insufficient Evidence"** response.
   - Hallucinated components, fictional relationships, or speculative architecture without evidence are considered critical errors.
