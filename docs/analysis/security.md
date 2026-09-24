# Security Model & Untrusted Input Safeguards

## 1. Untrusted Input Principle
Repository được nạp vào hệ thống (thông qua Git clone hoặc ZIP upload) được coi là **HOÀN TOÀN KHÔNG TIN CẬY (UNTRUSTED INPUT)**.

RepoLens áp dụng các biện pháp bảo mật cốt lõi:
1. **Zero Execution Guarantee**:
   - Tuyệt đối không gọi `System.Diagnostics.Process.Start` trên mã nguồn phân tích.
   - Không chạy lệnh shell (`bash`, `sh`, `cmd`, `powershell`).
   - Không chạy `npm install`, `npm run`, `node`, `yarn`.
   - Không chạy `dotnet build`, `dotnet test`, `dotnet run` trên repository mục tiêu.
   - Toàn bộ việc trích xuất được thực hiện qua AST parser (Roslyn cho C#, System.Xml.Linq cho XML, và regex heuristic cho TS/JS).
2. **Read-Only Guarantee**:
   - Hệ thống mở file ở chế độ chỉ đọc (`FileAccess.Read`, `FileShare.Read`).
   - Không bao giờ ghi đè, sửa đổi, hay commit vào repository mục tiêu.
3. **Path Traversal & Zip Slip Protection**:
   - `RepositoryScanner` kiểm tra mọi đường dẫn tuyệt đối với `StartsWith(rootFullPath)` để ngăn chặn tấn công directory traversal (`../`).
   - Mọi ký tự `\` được chuẩn hóa thành `/`.
4. **Secret & Credential Masking**:
   - `SecretMasker` tự động phát hiện mật khẩu, chuỗi kết nối (`Password=...`, `Uid=...`), token (`Bearer ...`), API keys (`sk-proj-...`) và client secrets.
   - Các giá trị này được thay thế bằng `***MASKED***` trước khi lưu vào `Evidence.Description` hoặc knowledge graph.
5. **Configurable Limits & DoS Protection (`AnalysisLimits`)**:
   - `MaxFiles` (mặc định 10,000 files): dừng duyệt nếu vượt quá.
   - `MaxFileSizeBytes` (mặc định 5 MB): bỏ qua file quá lớn kèm cảnh báo.
   - `MaxTotalRepositorySizeBytes` (mặc định 500 MB): giới hạn tổng dung lượng.
   - `MaxRelationships` (mặc định 50,000 edges).
   - Hỗ trợ `CancellationToken` xuyên suốt quá trình quét và phân tích.
6. **Cross-Repository Isolation**:
   - Mọi entity trong persistence (`Evidence`, `Dependency`, `CodeSymbol`, `ApiEndpoint`, `DatabaseEntity`, `DocumentChunk`) đều gắn liền với `AnalysisId` và `RepositoryId`.
   - Các truy vấn qua EF Core luôn lọc theo `AnalysisId`, đảm bảo không có sự rò rỉ dữ liệu giữa các repository khác nhau.
