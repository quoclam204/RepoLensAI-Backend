---
name: repository-analysis
description: "Instructions and best practices for static code analysis, Roslyn AST traversal, symbol extraction, and architecture discovery in RepoLens."
---

# Repository Analysis Skill

This skill guides the implementation and extension of static analysis extractors within `RepoLens.Analysis`.

## Core Invariant: Strictly Static Analysis

> [!CAUTION]
> Analyzed code must **NEVER** be executed or compiled for execution.
> - Use Roslyn syntax trees (`CSharpSyntaxTree.ParseText`) and syntax walkers (`CSharpSyntaxWalker`).
> - For multi-language analysis, use text parsing, regex, or static AST parsers.
> - Never run untrusted scripts, build tools, or binaries found in target repositories.

## Roslyn Analysis Architecture

The `RepoLens.Analysis` project is structured into specialized extractors:
```
RepoLens.Analysis/
├── Abstractions/       # IRepositoryAnalyzer, ISymbolExtractor, IDependencyExtractor
├── Common/             # Shared AST traversal utilities, SourceLocation helpers
├── CSharp/             # Roslyn syntax visitors and semantic extractors
├── Symbols/            # Type, method, property, parameter extractors
├── Dependencies/       # Project reference & NuGet package detectors
├── Api/                # ASP.NET Core & Minimal API endpoint detectors
└── Database/           # EF Core entity & DbContext detectors
```

## Extraction Patterns

### 1. Symbol Extraction with Roslyn SyntaxWalker
When extracting classes, interfaces, or methods, extend `CSharpSyntaxWalker`:
- Override `VisitClassDeclaration(ClassDeclarationSyntax node)`
- Override `VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)`
- Override `VisitRecordDeclaration(RecordDeclarationSyntax node)`
- Override `VisitMethodDeclaration(MethodDeclarationSyntax node)`

Capture:
- Identifier name
- Modifiers (`public`, `internal`, `abstract`, `static`)
- Base types and interfaces
- Location span: `node.GetLocation().GetLineSpan()` (1-indexed start and end line)

### 2. API Endpoint Extraction
Look for:
- Controller classes inheriting from `ControllerBase` or decorated with `[ApiController]`
- Action methods with HTTP verb attributes (`[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpDelete]`, `[Route]`)
- ASP.NET Core Minimal API endpoint route registrations (`app.MapGet(...)`, `app.MapPost(...)`, etc.)

### 3. Database Entity Extraction
Look for:
- Classes inheriting from `DbContext`
- Properties of type `DbSet<TEntity>`
- Classes implementing `IEntityTypeConfiguration<TEntity>`
- Navigation properties and foreign key definitions

### 4. Evidence Construction
For every extracted entity, construct an `Evidence` record:

> [!NOTE]
> **ILLUSTRATIVE, chưa có trong code**: Đoạn mã dưới đây là minh họa thiết kế theo đặc tả (Task T023/T040). Class `Evidence` và enum `EvidenceType` chưa được hiện thực trong `src/RepoLens.Domain`. Khi triển khai, cần kiểm tra code thực tế hoặc tạo các class này theo đúng task spec.

```csharp
var evidence = new Evidence(
    filePath: relativeFilePath,
    startLine: lineSpan.StartLinePosition.Line + 1,
    endLine: lineSpan.EndLinePosition.Line + 1,
    snippet: node.ToString(),
    evidenceType: EvidenceType.Declaration,
    confidence: 1.0f
);
```
