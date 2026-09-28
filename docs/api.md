# RepoLens AI — Backend API Reference (T117)

All endpoints conform to `contracts/api.md` and are available in OpenAPI/Swagger UI at `/swagger`.

---

## 1. Analysis Lifecycle APIs

### Create Analysis (Git Repository)
- **Method / Route**: `POST /api/analyses`
- **Content-Type**: `application/json`
- **Request Body**:
  ```json
  {
    "sourceUrl": "https://github.com/org/repo"
  }
  ```
- **Response**: `202 Accepted`
  ```json
  {
    "analysisId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "repositoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "status": "Created",
    "startedAt": "2026-09-28T08:00:00Z"
  }
  ```

### Create Analysis (ZIP Upload)
- **Method / Route**: `POST /api/analyses/upload`
- **Content-Type**: `multipart/form-data`
- **Request**: Form field `file` containing archive `.zip`.
- **Response**: `202 Accepted`

### Get Analysis Status
- **Method / Route**: `GET /api/analyses/{id}`
- **Response**: `200 OK`
  ```json
  {
    "analysisId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "repositoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "status": "Completed",
    "currentStage": "Completed",
    "progressPercentage": 100,
    "startedAt": "2026-09-28T08:00:00Z",
    "completedAt": "2026-09-28T08:00:15Z",
    "errorMessage": null
  }
  ```

---

## 2. Query & Explorer APIs

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/analyses/{id}/overview` | High-level statistics, detected languages, project count |
| `GET` | `/api/analyses/{id}/architecture` | Graph nodes (projects) and dependency edges (supports `?format=archify`) |
| `GET` | `/api/analyses/{id}/architecture/archify` | Archify-compatible C4 specification document for frontend/Archify viewer |
| `GET` | `/api/analyses/{id}/dependencies` | Code dependencies (project/package/symbol references) |
| `GET` | `/api/analyses/{id}/endpoints` | Discovered HTTP endpoints (method, route, controller) |
| `GET` | `/api/analyses/{id}/database` | Detected EF Core database entities and relationships |
| `GET` | `/api/analyses/{id}/files` | Paginated file tree with language and hash |
| `GET` | `/api/analyses/{id}/symbols/{symbolId}` | Specific symbol detail with location and line span |
| `GET` | `/api/analyses/{id}/evidence` | Paginated evidence records |
| `GET` | `/api/analyses/{id}/evidence/{evidenceId}` | Single evidence record with masked snippet |

---

## 3. Evidence-Grounded AI Chat API (T091)

- **Method / Route**: `POST /api/analyses/{id}/chat`
- **Content-Type**: `application/json`
- **Request Body**:
  ```json
  {
    "question": "What API endpoints exist in this project?"
  }
  ```
- **Response**: `200 OK`
  ```json
  {
    "analysisId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "question": "What API endpoints exist in this project?",
    "answer": "The project exposes an HTTP GET endpoint at /api/orders in OrdersController.",
    "confidence": "High",
    "evidence": [
      {
        "evidenceId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "filePath": "src/Controllers/OrdersController.cs",
        "startLine": 15,
        "endLine": 25,
        "symbol": "OrdersController.GetOrders",
        "snippet": "[HttpGet] public IActionResult GetOrders() => Ok(...);"
      }
    ],
    "hasSufficientEvidence": true
  }
  ```
- **Fallback / Insufficient Evidence Response**:
  ```json
  {
    "analysisId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "question": "Where is the Kubernetes cluster deployment configured?",
    "answer": "Insufficient evidence in the analyzed repository.",
    "confidence": "Unknown",
    "evidence": [],
    "hasSufficientEvidence": false
  }
  ```
