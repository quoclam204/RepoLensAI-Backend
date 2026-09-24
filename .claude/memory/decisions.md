# Architectural Decision Records (ADR) — RepoLensAI-Backend

## ADR 001: Cho phép `RepoLens.Infrastructure` reference `RepoLens.Analysis` (Adapter cho Ports trong `Application`)

* **Trạng thái**: Accepted
* **Ngày quyết định**: 2026-09-21
* **Phạm vi**: Cấu trúc tham chiếu các project trong `RepoLens.sln`

### 1. Bối cảnh (Context)
* `RepoLens.Analysis` được thiết kế như một static analysis engine độc lập, chuyên sâu về Microsoft Roslyn (C# AST, cú pháp, semantic extraction). Hiện tại nó chỉ phụ thuộc vào `RepoLens.Domain` (Layer 1).
* `RepoLens.Application` là tầng chứa business use cases và định nghĩa các Port (interfaces) như `IRepositoryAnalyzer`, `IAnalysisPipeline`, `ISymbolExtractor`.
* `RepoLens.Api` là tầng presentation/composition root. Theo kiến trúc ban đầu, `Api` chỉ tham chiếu `Application` và `Infrastructure`.
* Cần xác định rõ cơ chế kết nối giữa các Port trong `Application` và các component phân tích thực tế trong `Analysis`, đảm bảo tính đóng gói và không làm phá vỡ nguyên tắc Clean Architecture.

### 2. Quyết định (Decision)
1. **Cho phép `RepoLens.Infrastructure` reference `RepoLens.Analysis`**:
   * `Infrastructure` (Layer 2) sẽ đóng vai trò **Adapter** cho các Port phân tích mà `Application` định nghĩa.
   * Các adapter class (ví dụ `RoslynRepositoryAnalyzerAdapter`, `AnalysisPipelineService`) nằm trong `Infrastructure`, thực thi interface của `Application`, đồng thời gọi vào các parser/extractor của `Analysis`.
2. **Giữ nguyên ranh giới của `Analysis`**:
   * `RepoLens.Analysis` (Layer 1) tiếp tục **CHỈ reference `RepoLens.Domain`**. Không reference `Application`, `Infrastructure`, hay `Api`.
3. **Giữ nguyên ranh giới của `Api`**:
   * `RepoLens.Api` (Layer 3) tiếp tục **CHỈ reference `RepoLens.Application` và `RepoLens.Infrastructure`**. Không reference trực tiếp `Domain` hay `Analysis`.
4. **Đồng bộ test kiểm tra kiến trúc**:
   * Cập nhật [`DependencyRulesTests.cs`](file:///d:/Github/RepoLensAI-Backend/tests/RepoLens.UnitTests/Architecture/DependencyRulesTests.cs) để cho phép `Infrastructure` reference `Application`, `Domain`, và tùy chọn `Analysis`.

### 3. Đánh đổi & Hệ quả (Consequences & Trade-offs)

#### Ưu điểm:
* **Tính độc lập cao cho Analysis Engine**: `RepoLens.Analysis` hoàn toàn tách biệt khỏi database (Postgres), web server (Kestrel/ASP.NET Core) và chỉ tập trung vào phân tích cú pháp AST.
* **Tuân thủ Ports & Adapters (Hexagonal Architecture)**: `Application` chỉ phụ thuộc vào abstraction của chính nó; việc hiện thực hóa analysis engine được `Infrastructure` coi như một external/subsystem adapter.
* **Giữ Api gọn gàng**: `Api` không cần tham chiếu trực tiếp đến Roslyn SDK hay Analysis project.

#### Nhược điểm / Đánh đổi:
* `Infrastructure` đảm nhận thêm vai trò bridge/adapter cho Analysis (bên cạnh persistence/external APIs).
* Cần tổ chức folder rõ ràng trong `Infrastructure` (ví dụ `src/RepoLens.Infrastructure/Adapters/Analysis/`) để phân biệt rạch ròi với `Persistence/` và `Storage/`.

---

## ADR 002: Hợp đồng đầu ra của Static Analysis Engine (`AnalysisResult`) phục vụ Adapter trong `RepoLens.Infrastructure`

* **Trạng thái**: Accepted
* **Ngày quyết định**: 2026-09-22
* **Phạm vi**: Output Contract của `RepoLens.Analysis` và tích hợp Adapter trong `RepoLens.Infrastructure`

### 1. Bối cảnh (Context)
* Sau ADR 001, `RepoLens.Infrastructure` chịu trách nhiệm làm Adapter kết nối giữa các Port của `RepoLens.Application` (ví dụ `IRepositoryAnalyzer`) và công cụ phân tích tĩnh `RepoLens.Analysis`.
* Để Adapter có thể chuyển đổi dữ liệu phân tích thành entities lưu trữ vào cơ sở dữ liệu (PostgreSQL/EF Core) mà không làm rò rỉ chi tiết cú pháp của Roslyn AST hoặc XML parsing, cần một hợp đồng dữ liệu chuẩn hóa (`Output Contract`).

### 2. Quyết định (Decision)
1. **Chuẩn hóa Hợp đồng Kết quả Phân tích (`AnalysisResult`)**:
   * Định nghĩa record `AnalysisResult` tại `RepoLens.Analysis.Orchestration` bao gồm:
     * `Nodes`: `IReadOnlyList<KnowledgeNode>` — tập hợp các đỉnh trong đồ thị tri thức (Namespace, Class, Interface, Method, Property, Endpoint, DatabaseEntity).
     * `Relationships`: `IReadOnlyList<KnowledgeRelationship>` — tập hợp các cạnh định hướng (Contains, Defines, Inherits, Implements, Calls, DependsOn, Exposes, MapsTo) gắn kèm `Domain.Entities.Evidence`.
     * `ProjectReferences`: `IReadOnlyList<ProjectReference>` — các liên kết phụ thuộc project trích xuất từ file `.csproj`.
     * `PackageReferences`: `IReadOnlyList<PackageReference>` — các thư viện NuGet phụ thuộc kèm version từ `.csproj`.
     * `Errors`: `IReadOnlyList<string>` — danh sách thông báo lỗi không nghiêm trọng (non-fatal errors) thu thập trong quá trình duyệt file.
2. **Quy trình Phân tích Hai Pha (Two-Pass Orchestration)**:
   * **Pha 1 (Pre-scan Symbol Discovery)**: Quét sơ bộ tất cả file `.cs` để tập hợp từ điển symbol toàn cục (global symbol map).
   * **Pha 2 (Deep Semantic & Relationship Analysis)**: Chạy `CSharpFileAnalyzer` cho từng file dựa trên từ điển symbol toàn cục để liên kết chính xác quan hệ xuyên file (cross-file inheritance, calls, field dependencies) mà không sinh quan hệ khống khi trỏ ra ngoài project.
3. **Trách nhiệm của Adapter trong `Infrastructure`**:
   * Gọi `ProjectAnalyzer.Analyze(files, csprojFiles, jobId)`.
   * Chuyển đổi `AnalysisResult.Nodes` và `AnalysisResult.Relationships` thành persistence entities hoặc đồ thị tri thức tương ứng.
   * Lưu các `Evidence` đi kèm vào bảng lưu trữ bằng chứng để phục vụ AI/RAG grounding.

### 3. Đánh đổi & Hệ quả (Consequences & Trade-offs)
* **Ưu điểm**:
  * Đóng gói hoàn toàn logic AST của Roslyn và parsing XML trong `Analysis`.
  * `Infrastructure` chỉ làm việc với các POCO/records thuần túy (`KnowledgeNode`, `KnowledgeRelationship`, `Evidence`).
  * Đảm bảo nguyên tắc "Evidence-First": mọi relationship cấu trúc đều có bằng chứng nguồn gốc kiểm chứng được.
* **Nhược điểm / Đánh đổi**:
  * Mô hình `AnalysisResult` trung gian yêu cầu một bước ánh xạ (mapping) sang persistence entity trong `Infrastructure`.
