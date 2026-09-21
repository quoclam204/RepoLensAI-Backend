# Ý tưởng tích hợp Archify (Archify Integration)

> [!NOTE]
> **Trạng thái**: Ngoài scope MVP (Out of Scope for MVP).
> Ý tưởng mở rộng cho các giai đoạn sau; hiện tại **chưa có trong SRS / Specs** và **chưa có trong mã nguồn**.
> Được chuyển từ `.claude/skills/archify` vào `docs/ideas/` theo quyết định kiểm toán kiến trúc 2026-09-21.

---

## 1. Tổng quan ý tưởng
RepoLens AI trong tương lai có thể tích hợp với **Archify** để cung cấp biểu diễn kiến trúc có cấu trúc theo mô hình C4 (C4 model), được bảo đảm bằng bằng chứng trích xuất (evidence-backed).

## 2. Ánh xạ phân cấp kiến trúc dự kiến

| Khái niệm trong RepoLens | Khái niệm trong Archify | Mô tả |
| :--- | :--- | :--- |
| **Repository / Solution** | **System Context** | Ranh giới hệ thống tổng thể |
| **Project (.csproj)** | **Container** | Đơn vị deployable, service, hoặc thư viện |
| **Feature / Namespace / Layer** | **Component** | Tập hợp các class/interface có tính gắn kết |
| **Class / Record / Interface** | **Code Element / Symbol** | Đơn vị mã nguồn cụ thể |
| **References & Invocations** | **Relationship** | `DependsOn`, `Calls`, `Implements`, `Exposes` |

## 3. Schema dự kiến (Archify Schema Draft)

```json
{
  "system": {
    "name": "RepoLensAI",
    "description": "Evidence-grounded repository intelligence platform",
    "containers": [
      {
        "id": "repolens-api",
        "name": "RepoLens.Api",
        "type": "WebApi",
        "technology": ".NET 10 / ASP.NET Core",
        "components": [
          {
            "id": "analysis-controller",
            "name": "AnalysisController",
            "evidenceIds": ["ev_101", "ev_102"]
          }
        ]
      }
    ],
    "relationships": [
      {
        "sourceId": "repolens-api",
        "targetId": "repolens-application",
        "type": "References",
        "evidenceIds": ["ev_201"]
      }
    ]
  }
}
```

## 4. Nguyên tắc ràng buộc khi triển khai sau MVP
1. **Truy nguyên bằng chứng (Evidence Grounding)**:
   Mọi container, component hay relationship trong Archify bắt buộc phải trỏ đến ít nhất một `evidenceId` có thật trong repository snapshot.
2. **Tách biệt tầng kiến trúc (Layer Separation)**:
   Module xuất dữ liệu Archify sẽ nằm ở `RepoLens.Infrastructure` hoặc thông qua abstraction của `RepoLens.Application`, không làm ô nhiễm `RepoLens.Domain`.
