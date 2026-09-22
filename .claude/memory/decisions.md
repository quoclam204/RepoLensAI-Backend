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
