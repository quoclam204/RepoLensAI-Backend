---
name: evidence-validation
description: "Guidelines and verification rules for evidence grounding, confidence scoring, and validating that architectural facts and RAG outputs trace back to repository source code."
---

# Evidence Validation Skill

In RepoLens AI, **Evidence is a first-class concept**.
The core principle is:
> **"Static analysis establishes what actually exists. AI explains the evidence."**

## Evidence Model Specification

> [!NOTE]
> **ILLUSTRATIVE, chưa có trong code**: Cấu trúc dữ liệu và enum dưới đây là minh họa thiết kế theo đặc tả kỹ thuật (Task T023-T025). Các entity và enum này chưa có trong mã nguồn hiện tại của `RepoLens.Domain`. Khi code, cần kiểm tra code thực tế hoặc tạo các class này theo đúng task spec.

Every piece of extracted knowledge or architectural claim must link to one or more `Evidence` records:
- **`FilePath`**: Relative path from repository root (forward-slash normalized: `src/RepoLens.Api/Program.cs`).
- **`StartLine`**: 1-indexed start line number.
- **`EndLine`**: 1-indexed end line number (`StartLine <= EndLine`).
- **`Snippet`**: Exact source snippet extracted from the repository snapshot.
- **`EvidenceType`**:
  - `Declaration`: Class, interface, record, method, or property definition.
  - `Invocation`: Method call, service instantiation, event publishing.
  - `Configuration`: AppSettings, `.env`, dependency injection registration, launchSettings.
  - `Dependency`: Project reference, NuGet package reference, npm dependency.
  - `Route`: HTTP endpoint or routing declaration.
- **`Confidence`**: Float value from 0.0 to 1.0.

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
