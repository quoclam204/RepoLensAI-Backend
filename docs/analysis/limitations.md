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
4. **Documentation & Markdown Chunking**:
   - `DocumentChunkGenerator` phân đoạn tài liệu Markdown theo cấu trúc heading (`#`, `##`, `###`) và phân tách các khối vượt ngưỡng ký tự an toàn mà không phá vỡ dòng.
   - Chưa phân tích chuyên sâu các cú pháp nhúng phức tạp (ví dụ mermaid diagrams trong markdown được giữ nguyên dạng block).
5. **npm Dependencies**:
   - `NpmDependencyExtractor` phân tích tệp `package.json` thuần túy dưới dạng JSON tĩnh (System.Text.Json) mà không thực thi `npm install` hay các npm lifecycle scripts (`preinstall`, `postinstall`).
6. **T037 (MSBuildWorkspace) Rationale & Status (PARTIAL)**:
   - *Tại sao không dùng MSBuildWorkspace*: Roslyn `MSBuildWorkspace` đòi hỏi phải load và đánh giá các file `.csproj`/`.sln` bằng MSBuild runtime (`Microsoft.Build`). Quá trình này sẽ thực thi các MSBuild SDK target, Custom Build Tasks, NuGet restore, và target logic được định nghĩa trong repository mục tiêu. Điều này vi phạm trực tiếp nguyên tắc bảo mật tối cao **"Zero Execution of Analyzed Code" (NFR-SEC-001, ADR 001)**.
   - *Giải pháp hiện tại*: Sử dụng `CSharpSyntaxTree.ParseText` thuần in-memory và `ProjectDependencyExtractor` để parse XML `.csproj` an toàn tuyệt đối, không thực thi code repo mục tiêu.
   - *Trạng thái task*: Được duy trì ở mức **PARTIAL** (hoặc Limitation đã được chấp thuận bởi Architecture).

7. **T047 (TypeScript Compiler AST) Rationale & Status (PARTIAL)**:
   - *Tại sao không dùng TypeScript Compiler API (`tsc` / Node.js)*: Việc chạy TypeScript Compiler AST đòi hỏi máy chủ phải cài đặt môi trường Node.js runtime, thực thi `npm install` hoặc chạy tiến trình compiler ngoài trên mã nguồn untrusted. Điều này vi phạm nguyên tắc bảo mật, kéo theo rủi ro Supply Chain Attack (`postinstall` script injection) và phá vỡ kiến trúc độc lập của backend .NET 10.
   - *Giải pháp hiện tại*: Sử dụng `TsSymbolExtractor` (pure C# regex & tokenizer heuristics) và `NpmDependencyExtractor` (parse `package.json` bằng `System.Text.Json`), an toàn 100%, không cần runtime ngoài.
   - *Trạng thái task*: Được duy trì ở mức **PARTIAL** (hoặc Limitation đã được chấp thuận bởi Architecture).

8. **Out of Scope for Person 2**:
   - **Frontend UI / Viewer**: Việc render đồ thị trên trình duyệt hoặc tích hợp Web UI là scope của Frontend team (Person 4).
   - **Archify Web Viewer**: RepoLens chỉ cung cấp adapter contract và C4 model export; không clone hay nhúng viewer của Archify.
   - **Full RAG / LLM Agent Pipeline**: Static Analysis chỉ cung cấp Knowledge Model, Document Chunks, và Evidence Retrieval grounding; pipeline prompt LLM, vector embedding, chat thuộc scope của AI/RAG engineer (Person 5).
