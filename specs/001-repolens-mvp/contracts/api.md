# RepoLens AI — API Contract

**Version:** 1.0.0
**Status:** Draft
**Created:** 2026-09-17
**Project:** RepoLens AI
**Specification:** v1.0.0
**Plan:** v1.0.0

---

# 1. Purpose

This document defines the HTTP API contract between the RepoLens AI backend and frontend.

The API provides functionality for:

* Repository analysis
* Analysis status
* Project overview
* Architecture
* Dependencies
* APIs
* Database entities
* Files and symbols
* Evidence
* AI-powered repository Q&A

The frontend MUST consume the API according to this contract.

Backend implementations MUST NOT change response semantics without updating this contract.

---

# 2. API Conventions

Base URL:

```text
/api
```

Content type:

```text
application/json
```

Unless otherwise specified.

---

# 3. Common Response Conventions

Successful responses SHOULD use standard HTTP status codes.

| Status | Meaning                                      |
| ------ | -------------------------------------------- |
| 200    | Successful request                           |
| 201    | Resource created                             |
| 202    | Request accepted for asynchronous processing |
| 400    | Invalid request                              |
| 404    | Resource not found                           |
| 409    | Resource state conflict                      |
| 413    | Payload too large                            |
| 422    | Validation failure                           |
| 500    | Internal server error                        |
| 503    | External dependency unavailable              |

---

# 4. Error Contract

All API errors SHOULD use a consistent structure.

```json
{
  "error": {
    "code": "ANALYSIS_NOT_FOUND",
    "message": "The requested analysis does not exist.",
    "details": null,
    "traceId": "00-abc123"
  }
}
```

Fields:

| Field   | Type        | Required | Description                 |
| ------- | ----------- | -------: | --------------------------- |
| code    | string      |      Yes | Machine-readable error code |
| message | string      |      Yes | Human-readable message      |
| details | object/null |       No | Additional information      |
| traceId | string/null |       No | Diagnostic identifier       |

The API MUST NOT expose secrets or sensitive repository content through error responses.

---

# 5. Repository Source

The system supports two repository source types.

```text
GitUrl
ZipUpload
```

Conceptual enum:

```json
{
  "sourceType": "GitUrl"
}
```

or:

```json
{
  "sourceType": "ZipUpload"
}
```

---

# 6. Create Analysis

## POST `/api/analyses`

Creates a new repository analysis.

---

## 6.1 Git URL Request

```json
{
  "sourceType": "GitUrl",
  "sourceUrl": "https://github.com/example/project"
}
```

---

## 6.2 ZIP Upload

For ZIP uploads, the endpoint SHOULD use:

```text
multipart/form-data
```

Example fields:

```text
sourceType = ZipUpload
file = project.zip
```

---

## 6.3 Response

Status:

```text
202 Accepted
```

Response:

```json
{
  "analysisId": "7e4f9c4d-8b4d-4a8f-91d4-2f8d7e8c3a12",
  "repositoryId": "8b0d9f8d-4f93-4b31-8e5d-1e3d9f4a6b21",
  "status": "Created",
  "createdAt": "2026-09-17T10:00:00Z"
}
```

---

# 7. Get Analysis

## GET `/api/analyses/{analysisId}`

Returns the current state of an analysis.

---

## Response

```json
{
  "id": "7e4f9c4d-8b4d-4a8f-91d4-2f8d7e8c3a12",
  "repositoryId": "8b0d9f8d-4f93-4b31-8e5d-1e3d9f4a6b21",
  "status": "Analyzing",
  "stage": "CSharpAnalysis",
  "progress": 62,
  "startedAt": "2026-09-17T10:00:05Z",
  "completedAt": null,
  "error": null
}
```

---

# 8. Analysis Status

Valid statuses:

```text
Created
Cloning
Scanning
Analyzing
Indexing
Completed
Failed
```

Possible processing stages:

```text
Validation
RepositoryAcquisition
FileScanning
LanguageDetection
ProjectDetection
StaticAnalysis
DependencyAnalysis
ApiAnalysis
DatabaseAnalysis
EvidenceGeneration
Persistence
Chunking
Embedding
Indexing
Completed
```

---

# 9. Overview

## GET `/api/analyses/{analysisId}/overview`

Returns high-level information about the analyzed repository.

---

## Response

```json
{
  "analysisId": "7e4f9c4d-8b4d-4a8f-91d4-2f8d7e8c3a12",
  "repository": {
    "name": "example-project",
    "sourceType": "GitUrl",
    "sourceUrl": "https://github.com/example/project",
    "commitHash": "a1b2c3d4"
  },
  "statistics": {
    "projects": 5,
    "sourceFiles": 184,
    "symbols": 1320,
    "dependencies": 247,
    "apiEndpoints": 36,
    "databaseEntities": 18
  },
  "languages": [
    {
      "name": "C#",
      "fileCount": 142,
      "percentage": 77.17,
      "support": "Full"
    },
    {
      "name": "TypeScript",
      "fileCount": 31,
      "percentage": 16.85,
      "support": "Partial"
    }
  ]
}
```

---

# 10. Architecture

## GET `/api/analyses/{analysisId}/architecture`

Returns architecture nodes and relationships.

---

## Response

```json
{
  "analysisId": "7e4f9c4d-8b4d-4a8f-91d4-2f8d7e8c3a12",
  "nodes": [
    {
      "id": "project-api",
      "type": "Project",
      "name": "Example.Api",
      "path": "src/Example.Api"
    },
    {
      "id": "project-application",
      "type": "Project",
      "name": "Example.Application",
      "path": "src/Example.Application"
    }
  ],
  "edges": [
    {
      "id": "edge-001",
      "source": "project-api",
      "target": "project-application",
      "type": "DEPENDS_ON",
      "confidence": "confirmed",
      "evidence": {
        "file": "src/Example.Api/Example.Api.csproj",
        "startLine": 12,
        "endLine": 15
      }
    }
  ]
}
```

---

# 11. Architecture Node

Architecture nodes SHOULD support:

```text
Project
Module
Namespace
Class
Interface
Service
Repository
Controller
Database
```

Node structure:

```json
{
  "id": "string",
  "type": "Project",
  "name": "string",
  "path": "string",
  "metadata": {}
}
```

---

# 12. Architecture Edge

Architecture relationships SHOULD support:

```text
DEPENDS_ON
CONTAINS
IMPLEMENTS
INHERITS
CALLS
EXPOSES
MAPS_TO
READS
WRITES
```

Example:

```json
{
  "id": "edge-001",
  "source": "service-001",
  "target": "repository-001",
  "type": "DEPENDS_ON",
  "confidence": "confirmed",
  "evidenceId": "evidence-001"
}
```

Confidence values:

```text
confirmed
inferred
unknown
```

---

# 13. Dependencies

## GET `/api/analyses/{analysisId}/dependencies`

Returns the dependency graph.

---

## Query Parameters

Optional:

```text
projectId
type
direction
page
pageSize
```

Example:

```text
/api/analyses/{id}/dependencies?projectId=abc&type=ProjectReference
```

---

## Response

```json
{
  "items": [
    {
      "id": "dep-001",
      "source": {
        "id": "project-api",
        "name": "Example.Api",
        "type": "Project"
      },
      "target": {
        "id": "project-application",
        "name": "Example.Application",
        "type": "Project"
      },
      "type": "ProjectReference",
      "evidenceId": "evidence-001"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 50
}
```

---

# 14. Dependency Detail

## GET `/api/analyses/{analysisId}/dependencies/{dependencyId}`

Returns detailed information about a dependency.

---

## Response

```json
{
  "id": "dep-001",
  "type": "ProjectReference",
  "source": {
    "id": "project-api",
    "name": "Example.Api"
  },
  "target": {
    "id": "project-application",
    "name": "Example.Application"
  },
  "evidence": [
    {
      "id": "evidence-001",
      "file": "Example.Api.csproj",
      "startLine": 12,
      "endLine": 15,
      "description": "Project reference to Example.Application."
    }
  ]
}
```

---

# 15. API Endpoints

## GET `/api/analyses/{analysisId}/endpoints`

Returns detected HTTP endpoints.

---

## Query Parameters

Optional:

```text
method
route
projectId
controller
page
pageSize
```

---

## Response

```json
{
  "items": [
    {
      "id": "endpoint-001",
      "method": "GET",
      "route": "/api/contracts/{id}",
      "project": {
        "id": "project-api",
        "name": "Example.Api"
      },
      "controller": "ContractController",
      "action": "GetByIdAsync",
      "symbolId": "symbol-001",
      "evidenceId": "evidence-001"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 50
}
```

---

# 16. API Endpoint Detail

## GET `/api/analyses/{analysisId}/endpoints/{endpointId}`

Returns endpoint details.

---

## Response

```json
{
  "id": "endpoint-001",
  "method": "GET",
  "route": "/api/contracts/{id}",
  "controller": "ContractController",
  "action": "GetByIdAsync",
  "project": "Example.Api",
  "source": {
    "file": "Controllers/ContractController.cs",
    "symbol": "ContractController.GetByIdAsync"
  },
  "evidence": [
    {
      "file": "Controllers/ContractController.cs",
      "startLine": 24,
      "endLine": 38,
      "reason": "Defines the GET endpoint."
    }
  ]
}
```

---

# 17. Database

## GET `/api/analyses/{analysisId}/database`

Returns detected database entities and relationships.

---

## Response

```json
{
  "entities": [
    {
      "id": "entity-contract",
      "name": "Contract",
      "type": "Entity",
      "sourceSymbolId": "symbol-contract",
      "properties": [
        {
          "name": "Id",
          "type": "Guid",
          "nullable": false
        },
        {
          "name": "Title",
          "type": "string",
          "nullable": false
        }
      ]
    }
  ],
  "relationships": [
    {
      "id": "relationship-001",
      "sourceEntityId": "entity-contract",
      "targetEntityId": "entity-partner",
      "type": "ManyToOne",
      "confidence": "confirmed",
      "evidenceId": "evidence-entity-001"
    }
  ]
}
```

---

# 18. Database Entity Detail

## GET `/api/analyses/{analysisId}/database/entities/{entityId}`

Returns detailed database entity information.

---

## Response

```json
{
  "id": "entity-contract",
  "name": "Contract",
  "type": "Entity",
  "source": {
    "file": "Domain/Entities/Contract.cs",
    "symbol": "Contract"
  },
  "properties": [
    {
      "name": "Id",
      "type": "Guid",
      "nullable": false
    }
  ],
  "relationships": [],
  "evidence": [
    {
      "file": "Domain/Entities/Contract.cs",
      "startLine": 10,
      "endLine": 45,
      "reason": "Defines the Contract entity."
    }
  ]
}
```

---

# 19. Files

## GET `/api/analyses/{analysisId}/files`

Returns repository files.

---

## Query Parameters

```text
path
language
projectId
search
page
pageSize
```

---

## Response

```json
{
  "items": [
    {
      "id": "file-001",
      "path": "src/Example.Api/Controllers/ContractController.cs",
      "language": "CSharp",
      "projectId": "project-api",
      "size": 4821,
      "analysisStatus": "Completed"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 50
}
```

---

# 20. File Detail

## GET `/api/analyses/{analysisId}/files/{fileId}`

Returns file metadata and symbols.

---

## Response

```json
{
  "id": "file-001",
  "path": "src/Example.Api/Controllers/ContractController.cs",
  "language": "CSharp",
  "projectId": "project-api",
  "size": 4821,
  "symbols": [
    {
      "id": "symbol-001",
      "name": "ContractController",
      "fullName": "Example.Api.Controllers.ContractController",
      "type": "Class",
      "startLine": 8,
      "endLine": 75
    },
    {
      "id": "symbol-002",
      "name": "GetByIdAsync",
      "fullName": "ContractController.GetByIdAsync",
      "type": "Method",
      "startLine": 24,
      "endLine": 38
    }
  ]
}
```

---

# 21. Source File Content

## GET `/api/analyses/{analysisId}/files/{fileId}/content`

Returns source content for a file.

---

## Response

```json
{
  "fileId": "file-001",
  "path": "src/Example.Api/Controllers/ContractController.cs",
  "language": "CSharp",
  "content": "using Microsoft.AspNetCore.Mvc;...",
  "lineCount": 75
}
```

Source content MUST be returned read-only.

The API MUST NOT modify repository source code.

---

# 22. Symbols

## GET `/api/analyses/{analysisId}/symbols/{symbolId}`

Returns detailed symbol information.

---

## Response

```json
{
  "id": "symbol-002",
  "name": "GetByIdAsync",
  "fullName": "ContractController.GetByIdAsync",
  "type": "Method",
  "file": {
    "id": "file-001",
    "path": "Controllers/ContractController.cs"
  },
  "startLine": 24,
  "endLine": 38,
  "relationships": [
    {
      "type": "CALLS",
      "targetSymbolId": "symbol-020"
    }
  ]
}
```

---

# 23. Evidence

## GET `/api/analyses/{analysisId}/evidence/{evidenceId}`

Returns source evidence.

---

## Response

```json
{
  "id": "evidence-001",
  "analysisId": "analysis-001",
  "filePath": "Controllers/ContractController.cs",
  "symbol": "ContractController.GetByIdAsync",
  "startLine": 24,
  "endLine": 38,
  "evidenceType": "ApiEndpoint",
  "description": "Defines the GET /api/contracts/{id} endpoint."
}
```

---

# 24. Evidence Search

## GET `/api/analyses/{analysisId}/evidence`

Returns evidence records.

---

## Query Parameters

```text
filePath
symbol
type
page
pageSize
```

---

# 25. AI Chat

## POST `/api/analyses/{analysisId}/chat`

Asks an AI question about the analyzed repository.

---

## Request

```json
{
  "question": "Where is authentication implemented?"
}
```

---

# 26. AI Chat Response

```json
{
  "answer": "Authentication is configured in Program.cs and the authentication middleware is registered in the API pipeline.",
  "confidence": "high",
  "evidence": [
    {
      "file": "Program.cs",
      "symbol": "Program",
      "startLine": 45,
      "endLine": 57,
      "reason": "Registers authentication and authorization services and middleware."
    }
  ]
}
```

---

# 27. AI Confidence

Allowed values:

```text
high
medium
low
unknown
```

Confidence MUST be based on available evidence.

It MUST NOT be interpreted as a statistical probability.

---

# 28. AI Evidence Contract

Every repository-specific claim SHOULD have supporting evidence.

Evidence:

```json
{
  "file": "string",
  "symbol": "string|null",
  "startLine": 0,
  "endLine": 0,
  "reason": "string"
}
```

The backend MUST validate that evidence belongs to the requested analysis.

---

# 29. AI Unknown Response

If evidence is insufficient:

```json
{
  "answer": "Insufficient evidence to determine this from the analyzed repository.",
  "confidence": "unknown",
  "evidence": []
}
```

The system MUST prefer this response over unsupported claims.

---

# 30. Chat History

Chat history MAY be introduced in the MVP if implementation capacity allows.

If implemented:

## GET `/api/analyses/{analysisId}/chat`

Returns previous messages.

Response:

```json
{
  "items": [
    {
      "id": "message-001",
      "role": "user",
      "content": "Where is authentication implemented?",
      "createdAt": "2026-09-17T10:30:00Z"
    },
    {
      "id": "message-002",
      "role": "assistant",
      "content": "Authentication is configured in Program.cs...",
      "createdAt": "2026-09-17T10:30:03Z",
      "evidence": []
    }
  ]
}
```

---

# 31. Pagination

List endpoints SHOULD use:

```text
page
pageSize
```

Example:

```text
?page=1&pageSize=50
```

Maximum page size SHOULD be limited.

Example:

```text
pageSize <= 100
```

---

# 32. Paginated Response

Standard format:

```json
{
  "items": [],
  "totalCount": 0,
  "page": 1,
  "pageSize": 50,
  "totalPages": 0
}
```

---

# 33. Analysis Not Ready

If a user requests analysis data before analysis is completed, the API SHOULD return:

```text
409 Conflict
```

Example:

```json
{
  "error": {
    "code": "ANALYSIS_NOT_READY",
    "message": "The analysis has not completed yet.",
    "details": {
      "status": "Analyzing"
    }
  }
}
```

---

# 34. Analysis Failed

If an analysis has failed:

```text
409 Conflict
```

Response:

```json
{
  "error": {
    "code": "ANALYSIS_FAILED",
    "message": "The repository analysis failed.",
    "details": {
      "stage": "CSharpAnalysis"
    }
  }
}
```

Sensitive diagnostics MUST NOT be returned directly to the user.

---

# 35. Repository Validation Errors

Possible error codes:

```text
INVALID_REPOSITORY_URL
REPOSITORY_NOT_ACCESSIBLE
UNSUPPORTED_REPOSITORY
INVALID_ZIP
UNSAFE_ARCHIVE
FILE_LIMIT_EXCEEDED
SIZE_LIMIT_EXCEEDED
UNSUPPORTED_CONTENT
```

---

# 36. AI Errors

Possible error codes:

```text
AI_PROVIDER_UNAVAILABLE
AI_REQUEST_FAILED
AI_RESPONSE_INVALID
AI_EVIDENCE_INVALID
AI_RETRIEVAL_FAILED
```

The API SHOULD distinguish between:

* Temporary provider failure
* Invalid generated response
* Retrieval failure
* Evidence validation failure

---

# 37. Security Rules

The API MUST enforce analysis scope.

Every endpoint receiving:

```text
analysisId
```

MUST validate that the requested resource belongs to that analysis.

Examples:

```text
/api/analyses/{analysisId}/files/{fileId}

/api/analyses/{analysisId}/evidence/{evidenceId}

/api/analyses/{analysisId}/chat
```

The API MUST reject cross-analysis resource access.

---

# 38. Source Content Security

Source code returned by the API MUST be treated as untrusted content.

The frontend SHOULD display source code as plain text/code.

The frontend MUST NOT interpret repository source as executable HTML or JavaScript.

---

# 39. API Versioning

The initial API is:

```text
/api
```

If breaking changes become necessary, versioning SHOULD be introduced.

Example:

```text
/api/v2
```

Breaking changes MUST NOT silently alter the existing contract.

---

# 40. OpenAPI

The ASP.NET Core API SHOULD expose an OpenAPI specification.

The generated OpenAPI document SHOULD remain consistent with this contract.

The project SHOULD use OpenAPI documentation during frontend integration.

---

# 41. Frontend API Client

The frontend SHOULD use a typed API client.

Conceptually:

```text
src/lib/api/
├── analyses.ts
├── architecture.ts
├── dependencies.ts
├── endpoints.ts
├── database.ts
├── files.ts
└── chat.ts
```

API types SHOULD be centralized.

Frontend components SHOULD NOT manually duplicate backend response models.

---

# 42. API Client Error Handling

The frontend API client SHOULD normalize errors.

Example:

```typescript
type ApiError = {
  code: string;
  message: string;
  details?: unknown;
  traceId?: string;
};
```

UI components SHOULD display user-friendly messages.

Raw server diagnostics SHOULD NOT be exposed unnecessarily.

---

# 43. Contract Evolution

When a backend change modifies:

* Request fields
* Response fields
* Status codes
* Error codes
* Endpoint paths
* Semantics

the developer MUST:

1. Update this API contract.
2. Update backend implementation.
3. Update frontend types/client.
4. Update affected tests.
5. Verify frontend/backend compatibility.

---

# 44. API Acceptance Checklist

An API feature is complete when:

* Endpoint is implemented
* Request validation exists
* Response contract matches this document
* Error behavior is defined
* Repository/analysis scope is enforced
* Relevant tests pass
* OpenAPI reflects the implementation
* Frontend client can consume the endpoint
* No sensitive data is unintentionally exposed

---

# 45. Initial Endpoint Summary

| Method | Endpoint                                          | Purpose                |
| ------ | ------------------------------------------------- | ---------------------- |
| POST   | `/api/analyses`                                   | Create analysis        |
| GET    | `/api/analyses/{id}`                              | Get analysis status    |
| GET    | `/api/analyses/{id}/overview`                     | Project overview       |
| GET    | `/api/analyses/{id}/architecture`                 | Architecture graph     |
| GET    | `/api/analyses/{id}/dependencies`                 | Dependency graph       |
| GET    | `/api/analyses/{id}/dependencies/{dependencyId}`  | Dependency detail      |
| GET    | `/api/analyses/{id}/endpoints`                    | API endpoints          |
| GET    | `/api/analyses/{id}/endpoints/{endpointId}`       | API detail             |
| GET    | `/api/analyses/{id}/database`                     | Database model         |
| GET    | `/api/analyses/{id}/database/entities/{entityId}` | Entity detail          |
| GET    | `/api/analyses/{id}/files`                        | File list              |
| GET    | `/api/analyses/{id}/files/{fileId}`               | File detail            |
| GET    | `/api/analyses/{id}/files/{fileId}/content`       | Source content         |
| GET    | `/api/analyses/{id}/symbols/{symbolId}`           | Symbol detail          |
| GET    | `/api/analyses/{id}/evidence`                     | Evidence list          |
| GET    | `/api/analyses/{id}/evidence/{evidenceId}`        | Evidence detail        |
| POST   | `/api/analyses/{id}/chat`                         | AI repository Q&A      |
| GET    | `/api/analyses/{id}/chat`                         | Chat history, optional |

---

# 46. Contract Status

**Version:** 1.0.0
**Status:** Draft

This contract is governed by:

* RepoLens AI Constitution v1.0.0
* RepoLens AI Specification v1.0.0
* RepoLens AI Implementation Plan v1.0.0

The next artifact is:

```text
specs/001-repolens-mvp/tasks.md
```
