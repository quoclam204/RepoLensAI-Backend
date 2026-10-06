# Supported Repository Types and Framework Patterns (T118)

## 1. Supported Programming Languages

| Language | Analysis Support Level | Parser Implementation | Notes |
|---|---|---|---|
| **C#** | **Full** | Roslyn CSharpSyntaxTree (in-memory AST) | Full namespace, class, interface, method, property, inheritance, EF Core & controller discovery |
| **TypeScript** | **Heuristic / Partial** | Regex / Tokenizer static scanner | Discovers classes, interfaces, types, functions, routes, imports |
| **JavaScript** | **Heuristic / Partial** | Regex / Tokenizer static scanner | Discovers functions, classes, ES imports, CommonJS requires |

---

## 2. Supported Repository Classification Types (3-Layer Detection Engine)

RepoLens AI tự động phân loại repository bằng 3 lớp bằng chứng (File markers, Code patterns, Component relationships):

1. **ApiBackend**:
   - Backend APIs: ASP.NET Core Web API, Express, NestJS, Spring Boot, FastAPI, Go Gin.
   - Sơ đồ tương ứng: `architecture` (Phân tầng Controller → Service → Repository), `endpoints` (REST routes), `erd` (Database Entity Relationship Diagram nếu có DbContext).
2. **Frontend**:
   - Single-page hoặc server-rendered web applications: React, Next.js, Vue, Angular, Svelte, Vite.
   - Sơ đồ tương ứng: `architecture`, `routes` (Bản đồ route), `components` (Cây phân cấp component).
3. **Monorepo**:
   - Kho chứa đa dự án / đa không gian làm việc: pnpm workspaces, Lerna, Nx, Turborepo, .NET sln đa project độc lập.
   - Sơ đồ tương ứng: `overview` (Toàn cảnh hệ thống), `workspaces` (Các workspace), `dependencies` (Quan hệ phụ thuộc liên package).
4. **Library**:
   - Thư viện / Package tái sử dụng (NuGet class library, npm package, SDK).
   - Sơ đồ tương ứng: `architecture`, `classes` (Namespace & Classes), `dependencies`.
5. **Cli**:
   - Ứng dụng dòng lệnh console (System.CommandLine, CommandLineApplication, Commander, Cobra).
   - Sơ đồ tương ứng: `architecture`, `commands` (Cây lệnh & flags), `callflow` (Luồng thực thi).
6. **Unsupported**:
   - Repository không chứa các file marker hoặc pattern mã nguồn nhận diện được.
   - Sơ đồ tương ứng: `overview` (Thông báo giải thích thiếu bằng chứng).

---

## 3. Supported Architectural Patterns

- **Clean Architecture / Layered Architecture**: Automatically categorizes Domain, Application, Infrastructure, and API layers based on project naming and references.
- **REST APIs**: Detects `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpDelete]`, `[Route]`, `MapGet`, `MapPost`, `MapPut`, `MapDelete`.
- **Database Mapping**: Detects `DbContext`, `DbSet`, table mappings, primary keys, and foreign keys.

---

## 4. Known Architectural Limitations

See [docs/analysis/limitations.md](file:///d:/Github/RepoLensAI-Backend/docs/analysis/limitations.md) for full rationale:
- **MSBuildWorkspace** is intentionally excluded to prevent target repository code execution during MSBuild evaluation.
- **Node.js TypeScript Compiler Host** is intentionally excluded to prevent npm supply-chain execution attacks in untrusted repositories.
- **Multi-user / Teams**: Out of MVP scope (single-tenant local execution).
