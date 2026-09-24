# Archify Adapter & C4 Schema Mapping

## 1. Archify Integration Strategy
RepoLens tích hợp với định dạng **Archify** (mô hình C4) thông qua adapter contract và mapping layer, tuân thủ nghiêm ngặt nguyên tắc:
- Static Analysis không chứa UI renderer hoặc web viewer của Archify.
- Static Analysis xuất dữ liệu thông qua `ArchitectureModel` và `IArchifyAdapter` (tại `RepoLens.Application`).

## 2. C4 Concept Mapping Matrix
| RepoLens Concept | Archify C4 Level | Chi tiết ánh xạ |
| :--- | :--- | :--- |
| **Repository Root** | **System Context** | `ArchifySystem` với `Name`, `Description`. |
| **Project (.csproj, package.json)** | **Container** | `ArchifyContainer` với `Type` (WebApi, ApplicationCore, InfrastructureService, DomainModel, ClassLibrary), `Technology` (.NET 10 / C#). |
| **Class / Interface / Controller / Service** | **Component** | `ArchifyComponent` nằm bên trong container tương ứng, gắn kèm `evidenceIds`. |
| **Edge (ProjectReference, Calls, Inherits, etc.)** | **Relationship** | `ArchifyRelationship` định hướng với `sourceId`, `targetId`, `type`, `evidenceIds`, `confidence`. |

## 3. Schema Example
```json
{
  "system": {
    "name": "RepoLensAI",
    "description": "Evidence-grounded architectural representation generated from static analysis",
    "containers": [
      {
        "id": "repolens-api",
        "name": "RepoLens.Api",
        "type": "WebApi",
        "technology": ".NET 10 / C#",
        "components": [
          {
            "id": "analysescontroller",
            "name": "AnalysesController",
            "evidenceIds": ["ev:src/RepoLens.Api/Controllers/AnalysesController.cs:10-50"]
          }
        ]
      }
    ],
    "relationships": [
      {
        "sourceId": "repolens-api",
        "targetId": "repolens-domain",
        "type": "ProjectReference",
        "evidenceIds": ["ev:src/RepoLens.Api/RepoLens.Api.csproj:10-12"],
        "confidence": "confirmed"
      }
    ]
  }
}
```

## 4. Preservation Guarantees
- **Evidence Retention**: Mọi relationship và component đều bảo toàn danh sách `evidenceIds`.
- **Determinism**: Sử dụng slugify chuẩn hóa không phụ thuộc vào GUID ngẫu nhiên.
- **Independence**: `ArchifyAdapter` không phụ thuộc thư viện bên ngoài; hoàn toàn dựa trên pure record DTOs.
