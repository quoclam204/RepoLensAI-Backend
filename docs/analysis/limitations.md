# Technical Limitations & Scope Boundaries

## 1. Static Analysis Precision Boundaries
Nhằm duy trì tính an toàn (không build, không thực thi code mục tiêu) và hiệu năng phân tích nhanh, module Static Analysis có các giới hạn kỹ thuật được xác định rõ ràng:

1. **C# Call Graph (Syntax-Based vs Full Semantic Compilation)**:
   - Hiện tại, `CallGraphExtractor` sử dụng Roslyn `CSharpSyntaxWalker` trên cây cú pháp AST (SyntaxTree) mà không thực hiện full compilation (`Compilation.GetSemanticModel`).
   - *Hệ quả*: Có thể không phân giải chính xác 100% trong trường hợp method overloading phức tạp, generic type inference nhiều tầng, hoặc invocation qua reflection/dynamic dispatch.
   - *Mức độ tin cậy*: Được gắn nhãn `ConfidenceScore.Medium` (0.65).
2. **TypeScript & JavaScript Analysis (Heuristic vs Compiler API)**:
   - `TsSymbolExtractor` sử dụng static regex/heuristic tokenizer nhằm tránh phụ thuộc vào việc chạy Node.js runtime hay TypeScript Compiler API.
   - *Hệ quả*: Nhận diện tốt các khai báo chuẩn (`export class`, `function`, `interface`, import module, React FC), nhưng có thể bỏ sót các pattern metaprogramming, dynamic imports, hoặc type re-exports nâng cao.
   - *Mức độ tin cậy*: Được gắn nhãn `ConfidenceScore.Low` (0.35).
3. **Database Relationships**:
   - `DatabaseEntityExtractor` trích xuất các entity từ EF Core DbContext (`DbSet<T>`) và các Fluent API call phổ biến (`HasOne`, `HasMany`, `WithOne`, `WithMany`).
   - Chưa phân tích các stored procedure, raw SQL migrations, hoặc database provider không phải EF Core.
4. **Out of Scope for Person 2**:
   - **Frontend UI / Viewer**: Việc render đồ thị trên trình duyệt hoặc tích hợp Web UI là scope của Frontend team.
   - **Archify Web Viewer**: RepoLens chỉ cung cấp adapter contract và C4 model export; không clone hay nhúng viewer của Archify.
   - **Full RAG / LLM Agent Pipeline**: Static Analysis chỉ cung cấp Knowledge Model, Document Chunks, và Evidence Retrieval grounding; pipeline prompt LLM thuộc scope của AI/RAG engineer (Người 5).
