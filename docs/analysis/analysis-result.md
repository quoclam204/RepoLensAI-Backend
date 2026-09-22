# Analysis Result Contract & Deterministic Identifiers

## 1. Analysis Result Overview
Hợp đồng đầu ra của pipeline phân tích được chuẩn hóa qua 2 tầng:
1. **Engine Level (`RepositoryAnalysisResult`)**: Nằm trong `RepoLens.Analysis.Orchestration`, chứa kết quả quét metadata (`ScannedRepository`) và đồ thị tri thức (`AnalysisResult` gồm `Nodes`, `Relationships`, `ProjectReferences`, `PackageReferences`, `Errors`).
2. **Application Level (`AnalysisResultModel`)**: Nằm trong `RepoLens.Application.DTOs.Persistence`, là hợp đồng DTO trung gian chuẩn cho persistence, architecture visualization và RAG chunking.

## 2. Deterministic Identifier Conventions
Để đảm bảo tính bất biến và tái lập (cùng repository + commit tạo ra cùng identity, không sinh duplicate node hoặc duplicate edge triple), các ID được quy chuẩn:
- **Repository Root**: `repo:root`
- **Project Node**: `project:{ProjectName}` (ví dụ `project:RepoLens.Api`)
- **Namespace Node**: `namespace:{Namespace}` (ví dụ `namespace:RepoLens.Domain.Entities`)
- **Class / Struct / Record**: `class:{FullyQualifiedName}` (ví dụ `class:BillingService.Controllers.InvoicesController`)
- **Interface**: `interface:{FullyQualifiedName}` (ví dụ `interface:BillingService.Services.IInvoiceService`)
- **Method Node**: `method:{FullyQualifiedName}.{MethodName}` (ví dụ `method:BillingService.Controllers.InvoicesController.GetInvoice`)
- **Endpoint Node**: `endpoint:{HttpMethod}:{Route}` (ví dụ `endpoint:GET:/api/invoices`)
- **Database Entity**: `db:{EntityName}` (ví dụ `db:Invoice`)
- **Evidence Key**: `ev:{FilePath}:{StartLine}-{EndLine}` (ví dụ `ev:src/Controllers/InvoicesController.cs:15-20`)
- **Relationship Triple**: `(SourceId, TargetId, Type)` được deduplicate tự động bởi `KnowledgeGraphBuilder` và `AnalysisResultMapper`.

## 3. Schema Fields of `AnalysisResultModel`
- `AnalysisId`: Guid của lượt phân tích hiện tại.
- `Projects`: Danh sách `ProjectPersistenceModel` (Name, Path, Language, ProjectType).
- `SourceFiles`: Danh sách `SourceFilePersistenceModel` (Path, Language, Size, Hash, AnalysisStatus).
- `CodeSymbols`: Danh sách `CodeSymbolPersistenceModel` (SymbolKey, FilePath, Name, FullName, SymbolType, StartLine, EndLine).
- `Evidences`: Danh sách `EvidencePersistenceModel` (EvidenceKey, FilePath, Symbol, StartLine, EndLine, EvidenceType, Description).
- `Dependencies`: Danh sách `DependencyPersistenceModel` (SourceId, TargetId, DependencyType, EvidenceKey, EvidenceId).
- `ApiEndpoints`: Danh sách `ApiEndpointPersistenceModel` (Method, Route, Controller, Action, SymbolKey, EvidenceKey).
- `DatabaseEntities`: Danh sách `DatabaseEntityPersistenceModel` (Name, EntityType, SourceSymbolKey).
- `DatabaseRelationships`: Danh sách `DatabaseRelationshipPersistenceModel` (SourceEntityName, TargetEntityName, RelationshipType, EvidenceKey).
- `Issues`: Danh sách `AnalysisIssuePersistenceModel` (IssueType, Severity, Message).
- `DocumentChunks`: Danh sách `DocumentChunkPersistenceModel` (FilePath, Content, TokenCount, ChunkIndex, EvidenceKey).
