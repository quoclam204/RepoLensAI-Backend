# Static Analysis & Knowledge Model Architecture

## 1. Overview
Module `RepoLens.Analysis` đóng vai trò trung tâm trong việc phân tích mã nguồn của repository (C#, TypeScript, JavaScript, XML project manifests) mà **hoàn toàn không thực thi mã nguồn** của repository mục tiêu.

Hệ thống tuân thủ Clean Architecture và nguyên tắc Hexagonal (Ports & Adapters):
```
Repository / ZIP (Untrusted Input)
        ↓
RepositoryScanner (Safe traversal, ignore filters, limit controls)
        ↓
RepositoryAnalysisEngine (Two-pass Roslyn AST + Heuristic parsers)
        ↓
KnowledgeGraphBuilder (Deduplication, nodes, relationships with Evidence)
        ↓
RepositoryAnalysisResult (Analysis POCO contract)
        ↓
RoslynRepositoryAnalyzerAdapter (Infrastructure Adapter for IRepositoryAnalyzer)
        ↓
AnalysisResultModel (Application Persistence DTO)
   ├── Persistence (AnalysisPersistenceService → PostgreSQL / EF Core)
   ├── ArchitectureModel (Intermediate visualizer-neutral representation)
   │       ↓
   │   ArchifyAdapter → Archify C4 JSON export
   └── RAG Foundation (DocumentChunkGenerator → Vector embedding / Grounded Retrieval)
```

## 2. Layering & Clean Architecture Boundaries
- **Layer 0 (`RepoLens.Domain`)**: Chứa các thực thể cốt lõi (`Analysis`, `Repository`, `Project`, `SourceFile`, `CodeSymbol`, `Dependency`, `ApiEndpoint`, `DatabaseEntity`, `DatabaseRelationship`, `Evidence`, `AnalysisIssue`, `DocumentChunk`) và Value Objects (`SourceLocation`, `ConfidenceScore`). Tuyệt đối không tham chiếu bất kỳ project nào và không rò rỉ compiler types (Roslyn).
- **Layer 1 (`RepoLens.Analysis`)**: Chứa parser Roslyn, TS/JS heuristic extractor, scanner, graph builder. Chỉ tham chiếu `RepoLens.Domain` và `Microsoft.CodeAnalysis.CSharp`. Không tham chiếu `Application`, `Infrastructure`, hay `Api`.
- **Layer 1 (`RepoLens.Application`)**: Chứa business logic, use cases, query DTOs và các Port abstractions (`IRepositoryAnalyzer`, `IAnalysisPersistenceService`, `IArchifyAdapter`, `IEvidenceRetriever`). Chỉ tham chiếu `RepoLens.Domain`.
- **Layer 2 (`RepoLens.Infrastructure`)**: Đóng vai trò Adapter kết nối giữa các Port của `Application` và các component phân tích của `Analysis` (theo ADR 001 & ADR 002).
- **Layer 3 (`RepoLens.Api`)**: API host / Presentation layer.
