# Supported Repository Types and Framework Patterns (T118)

## 1. Supported Programming Languages

| Language | Analysis Support Level | Parser Implementation | Notes |
|---|---|---|---|
| **C#** | **Full** | Roslyn CSharpSyntaxTree (in-memory AST) | Full namespace, class, interface, method, property, inheritance, EF Core & controller discovery |
| **TypeScript** | **Heuristic / Partial** | Regex / Tokenizer static scanner | Discovers classes, interfaces, types, functions, routes, imports |
| **JavaScript** | **Heuristic / Partial** | Regex / Tokenizer static scanner | Discovers functions, classes, ES imports, CommonJS requires |

---

## 2. Supported Project Types

1. **.NET Solution / Projects**:
   - Solution files (`.sln`)
   - C# Projects (`.csproj`)
   - ASP.NET Core Web APIs (Minimal APIs and Controller-based `[ApiController]`)
   - Entity Framework Core (`DbContext`, `DbSet<T>`, Fluent API configurations)
2. **Node.js / Frontend Projects**:
   - `package.json` manifest parsing (dependencies and devDependencies without running `npm install`)
   - Next.js / React projects (`tsconfig.json`, `app/` routes, `pages/`)

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
