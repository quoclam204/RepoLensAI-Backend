# Local Development Guide (T115)

## Prerequisites

1. **.NET 10 SDK** (`net10.0`)
   - Verify: `dotnet --version` (should output `10.0.xxx`)
2. **Docker & Docker Compose** (for PostgreSQL + pgvector)
   - Verify: `docker --version`, `docker compose version`
3. **Git CLI** (for repository acquisition)

---

## 1. Database Setup (PostgreSQL with pgvector)

Start the local PostgreSQL container with the `pgvector` extension pre-installed:

```bash
docker compose up -d
```

The database configuration:
- **Host**: `localhost`
- **Port**: `5432`
- **Database**: `repolens`
- **Username**: `repolens`
- **Password**: `repolens_dev_secret`

---

## 2. Database Migrations

Apply Entity Framework Core migrations to create the schema and enable pgvector:

```bash
dotnet ef database update --project src/RepoLens.Infrastructure --startup-project src/RepoLens.Api
```

---

## 3. Running the Backend API

Start the ASP.NET Core Web API:

```bash
dotnet run --project src/RepoLens.Api
```

- **HTTP Endpoint**: `http://localhost:5000`
- **Swagger / OpenAPI Documentation**: `http://localhost:5000/swagger`

---

## 4. Running Tests

Run the complete test suite (Unit, Analysis, Integration):

```bash
# Run all tests
dotnet test

# Run individual test projects
dotnet test tests/RepoLens.UnitTests
dotnet test tests/RepoLens.AnalysisTests
dotnet test tests/RepoLens.IntegrationTests
```

---

## 5. Configuration & Environment Variables

| Variable | Description | Default |
|---|---|---|
| `ConnectionStrings__Postgres` | PostgreSQL connection string with pgvector | `Host=localhost;Database=repolens;...` |
| `Workspace__BaseDirectory` | Temporary folder for repository workspaces | `%TEMP%/repolens-workspaces` |
| `Acquisition__MaxZipSizeBytes` | Maximum allowed ZIP upload size (bytes) | `104857600` (100 MB) |
| `Acquisition__MaxFiles` | Maximum number of files in an acquired repository | `10000` |
| `AI__Provider` | AI Provider (`OpenAI`, `Gemini`, `Ollama`) | `OpenAI` |
| `AI__ApiKey` | API Key for external AI service | (Secret) |
| `AI__EmbeddingDimension` | Dimension of embedding vectors | `1536` |
