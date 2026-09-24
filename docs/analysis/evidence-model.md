# Evidence Model & RAG Grounding Foundation

## 1. Evidence-First Principle
Mọi sự thật kiến trúc, quan hệ phụ thuộc (Call, Inherits, Implements, DependsOn, Exposes), API endpoint, và Database model đều phải được truy nguyên (grounded) về dòng mã nguồn cụ thể trong repository:
- **Tập tin nguồn (`FilePath`)**: Chuẩn hóa dấu phân cách `/`, không bắt đầu bằng `/`.
- **Dải dòng (`StartLine` - `EndLine`)**: Đánh số 1-indexed (`1 <= StartLine <= EndLine`).
- **Trích dẫn (`Snippet`/`Description`)**: Đoạn mã nguồn thực tế đã được tự động làm sạch và che giấu thông tin nhạy cảm qua `SecretMasker`.
- **Mức độ tin cậy (`ConfidenceScore`)**: Giá trị số thực từ `0.0` đến `1.0`.

## 2. Confidence Calibration
| Mức độ | Giá trị số | Kịch bản trích xuất |
| :--- | :--- | :--- |
| **Exact** | `1.0` | Đọc trực tiếp từ XML project file (`.csproj` ProjectReference, PackageReference). |
| **High** | `0.85` | Khai báo Symbol rõ ràng (Class, Interface, Record, Enum), Route attribute cụ thể (`[HttpGet("/api/...")]`), DbSet<T> trong DbContext. |
| **Medium** | `0.65` | Call graph dựa trên cú pháp AST (SyntaxWalker invocation), Dependency injection qua constructor type matching, Minimal API mapping. |
| **Low** | `0.35` | TypeScript/JavaScript heuristic regex matching (do không có semantic type checker đầy đủ). |

## 3. RAG Grounding & Hallucination Prevention
Đối với RAG (Retrieval-Augmented Generation):
1. User đặt câu hỏi kiến trúc (ví dụ: *"ContractController gọi service nào?"*).
2. `IEvidenceRetriever` tìm kiếm các `Evidence` đã được phân tích và lưu trữ trong database.
3. Nếu tìm thấy evidence có `Confidence >= minimumConfidence`:
   - Trả về `GroundedRetrievalResult` với `HasSufficientEvidence = true`, kèm theo đường dẫn file, dải dòng và đoạn mã chứng thực.
4. Nếu không tìm thấy hoặc confidence không đạt ngưỡng:
   - Trả về `HasSufficientEvidence = false` với `GroundingStatus = "Insufficient evidence"`.
   - LLM tuyệt đối **không được tự bịa ra vị trí dòng hoặc tên file** không có trong evidence.

## 4. Document Chunk Grounded Retrieval
`IEvidenceRetriever.RetrieveDocumentChunksAsync` cung cấp khả năng truy xuất trực tiếp các `DocumentChunk`:
- **Truy xuất theo Symbol hoặc Đường dẫn**: Lọc chính xác các chunk thuộc về symbol/file cụ thể.
- **Cách ly Analysis (NFR-004)**: Bắt buộc lọc theo `AnalysisId`, tuyệt đối không pha trộn giữa các repository/lần phân tích khác nhau.
- **Truy nguyên toàn diện (Provenance)**: Mỗi `RetrievedChunkItem` trả lời rõ ràng:
  1. *Thông tin đến từ đâu?* -> `FilePath` (thông qua `SourceFile` hoặc `Evidence`).
  2. *Dòng nào?* -> `StartLine` đến `EndLine`.
  3. *Chứng cứ nào?* -> `EvidenceId` liên kết với bảng `evidences`.
  4. *Độ tin cậy bao nhiêu?* -> `ConfidenceScore` kế thừa từ Evidence model.
