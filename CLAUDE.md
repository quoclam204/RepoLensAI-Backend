# CLAUDE.md — RepoLensAI-Backend

This file provides repo-specific guidance for Claude Code when working in **RepoLensAI-Backend**.

---

## 1. Project Overview & Primary Sources of Truth

RepoLens AI is an evidence-grounded repository intelligence platform that analyzes software repositories and explains their architecture, structure, and dependencies.

- **Stack**: .NET 10 (`net10.0`), C# 13, ASP.NET Core, PostgreSQL, Entity Framework Core, Microsoft Roslyn.
- **Architectural Style**: Clean Architecture with strict reference rules and Ports & Adapters.
- **Core Principle**:
  > **"Static analysis establishes what actually exists. AI explains the evidence."**

### Primary Sources of Truth (Nguồn tài liệu chính)
When understanding requirements, tasks, domain models, or scope, always reference:
1. [`srs.txt`](file:///d:/Github/RepoLensAI-Backend/srs.txt): Toàn bộ đặc tả yêu cầu phần mềm (SRS), phạm vi MVP, functional & non-functional requirements.
2. [`specs/001-repolens-mvp/tasks.md`](file:///d:/Github/RepoLensAI-Backend/specs/001-repolens-mvp/tasks.md): Danh sách các task triển khai chi tiết cho MVP (T001 - T112).
3. [`specs/001-repolens-mvp/plan.md`](file:///d:/Github/RepoLensAI-Backend/specs/001-repolens-mvp/plan.md) & [`spec.md`](file:///d:/Github/RepoLensAI-Backend/specs/001-repolens-mvp/spec.md): Kế hoạch kiến trúc và đặc tả chi tiết.
4. [`decisions.md`](file:///d:/Github/RepoLensAI-Backend/.claude/memory/decisions.md): Các quyết định kiến trúc đã thống nhất (ADR).

---

## 2. Verified Solution Structure

```
RepoLens.sln
├── src/
│   ├── RepoLens.Domain/          # Pure business models, enums, value objects, exceptions (zero external deps)
│   ├── RepoLens.Application/     # Use cases, interfaces/ports, DTOs, application services (depends ONLY on Domain)
│   ├── RepoLens.Infrastructure/  # EF Core, PostgreSQL, Git cloning, archive extraction, Analysis adapters
│   ├── RepoLens.Analysis/        # Static analysis engine, Roslyn AST visitors, symbol/dependency extractors (depends ONLY on Domain)
│   └── RepoLens.Api/             # ASP.NET Core Minimal APIs / Controllers, middleware, DI composition root
└── tests/
    ├── RepoLens.UnitTests/       # Domain and Application unit tests (xUnit, Moq/NSubstitute, Architecture rules)
    ├── RepoLens.AnalysisTests/   # Roslyn AST extraction and parser tests (xUnit)
    └── RepoLens.IntegrationTests/# Database & API integration tests (xUnit)
```

---

## 3. Essential CLI Commands

```bash
# Build entire solution (must pass with 0 errors, 0 warnings)
dotnet build

# Run all test suites
dotnet test

# Run individual test projects
dotnet test tests/RepoLens.UnitTests
dotnet test tests/RepoLens.AnalysisTests
dotnet test tests/RepoLens.IntegrationTests

# Run tests with filter
dotnet test --filter "FullyQualifiedName~DependencyRulesTests"

# Run the Web API locally
dotnet run --project src/RepoLens.Api

# Check for vulnerable dependencies
dotnet list package --vulnerable
```

---

## 4. Critical Architecture & Development Rules

1. **Kiểm tra code thực tế trước khi giả định class/package tồn tại**:
   > [!IMPORTANT]
   > Nhiều entity/model/package (ví dụ: `Evidence`, `Repository`, Roslyn `Microsoft.CodeAnalysis.CSharp`, EF Core `Npgsql`) đã được định nghĩa trong `tasks.md` nhưng **chưa được code** trong `src/`.
   > Phải luôn dùng `grep_search`, `list_dir`, hoặc `view_file` kiểm tra mã nguồn thực tế trước khi sử dụng hoặc viết code dựa trên giả định.

2. **Zero Execution of Analyzed Code**:
   - Analyzed target repository code must **NEVER** be executed, compiled to binary for running, or loaded dynamically into the current process.
   - All extraction must be strictly static (Roslyn AST, file inspection, manifest parsing).
   - Prohibited APIs: `Process.Start`, `Assembly.Load*`, `CSharpCompilation.Emit` + run.

3. **Evidence-First Grounding**:
   - Every architectural finding, component, or relation must link to verified `Evidence` (file path, 1-indexed line span, code snippet, confidence).
   - If evidence is absent, report "Insufficient Evidence". Never extrapolate or hallucinate unproven architecture.

4. **Safe Ingestion**:
   - Protect against Zip slip (path traversal) and decompression bombs during ZIP extraction.

5. **Layer Boundary Enforcement (ADR 001)**:
   - `RepoLens.Domain`: Zero dependencies.
   - `RepoLens.Application`: Depends ONLY on `RepoLens.Domain`.
   - `RepoLens.Analysis`: Depends ONLY on `RepoLens.Domain`.
   - `RepoLens.Infrastructure`: Depends on `Application`, `Domain`, and is permitted to reference `Analysis` (as an adapter for ports defined in Application).
   - `RepoLens.Api`: Depends ONLY on `Application` and `Infrastructure`.

---

## 5. C# 13 & .NET 10 Standards

- **Nullable Reference Types**: Project-wide enabled. Do not suppress without architectural proof.
- **Asynchronous Code**: Always accept and pass `CancellationToken`. Never write `async void`. Never use `.Result` or `.Wait()`.
- **Resource Cleanup**: Use `await using` or `using` on types implementing `IAsyncDisposable` / `IDisposable`.
- **EF Core**: Use `.AsNoTracking()` for read queries. Guard against N+1 queries.
- **Testing**: Follow Arrange-Act-Assert (AAA). Write unit tests for all new domain/application/analyzer logic.

---

## 6. Claude Code Toolkit in this Repository

| Type | Name | Purpose |
| :--- | :--- | :--- |
| **Agent** | [`dotnet-developer`](file:///.claude/agents/dotnet-developer.md) | Implement/refactor C# 13 / .NET 10 Clean Architecture code |
| **Agent** | [`repository-analyzer`](file:///.claude/agents/repository-analyzer.md) | Roslyn AST parsers, static extraction, and symbol analysis |
| **Agent** | [`code-reviewer`](file:///.claude/agents/code-reviewer.md) | C# 13 code reviews, security checks, and .NET best practices |
| **Agent** | [`architecture-reviewer`](file:///.claude/agents/architecture-reviewer.md) | Layer boundary integrity, dependency matrix, evidence validation |
| **Command**| [`/analyze`](file:///.claude/commands/analyze.md) | Inspect and verify static analysis pipelines |
| **Command**| [`/test`](file:///.claude/commands/test.md) | Run xUnit suites or generate comprehensive tests |
| **Command**| [`/review`](file:///.claude/commands/review.md) | Perform comprehensive C#/.NET code reviews |
| **Command**| [`/verify`](file:///.claude/commands/verify.md) | Run complete build, test, and vulnerability verification |
| **Skill** | [`brainstorming`](file:///.claude/skills/brainstorming/SKILL.md) | Clarify requirements and design before implementation |
| **Skill** | [`repository-analysis`](file:///.claude/skills/repository-analysis/SKILL.md) | Roslyn AST traversal and symbol extraction patterns (có nhãn illustrative) |
| **Skill** | [`evidence-validation`](file:///.claude/skills/evidence-validation/SKILL.md) | Evidence model validation, confidence scoring, RAG grounding (có nhãn illustrative) |
| **Rules** | [`rules/dotnet.md`](file:///.claude/rules/dotnet.md) | .NET 10 & C# 13 development rules |
| **Rules** | [`rules/architecture.md`](file:///.claude/rules/architecture.md) | Clean Architecture & evidence-first rules (đã đồng bộ ADR 001) |
| **Rules** | [`rules/security.md`](file:///.claude/rules/security.md) | Zero code execution & archive sandboxing rules |
| **Memory** | [`memory/decisions.md`](file:///.claude/memory/decisions.md) | Architectural Decision Records (ADR 001) |
| **Ideas** | [`docs/ideas/archify.md`](file:///docs/ideas/archify.md) | Tài liệu ý tưởng tích hợp Archify (ngoài scope MVP) |