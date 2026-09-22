# .NET & C# Guidelines

## Framework & Language Level
- **Target Framework**: `.NET 10.0` (`net10.0`)
- **Language Version**: C# 13
- **Compiler Settings**: `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<Deterministic>true</Deterministic>`

## Coding Conventions
1. **Naming Conventions**:
   - PascalCase: Classes, records, structs, interfaces, methods, public properties, enum values.
   - `I` prefix: Interfaces (`IRepositoryAnalyzer`, `IEvidenceStore`).
   - `_camelCase`: Private instance fields (`private readonly IDbContext _context;`).
   - `camelCase`: Method parameters and local variables.
   - File-scoped namespaces: Use `namespace RepoLens.Domain.Entities;`.

2. **C# 13 Idioms**:
   - Prefer `record` / `readonly record struct` for immutable DTOs, value objects, domain events, and query results.
   - Use pattern matching expressions (`switch` expressions, `is { }`) over type-checking casts.
   - Use collection expressions `[item1, item2]` where appropriate.
   - Leverage nullable reference types strictly; avoid `!` unless mathematically or architecturally guaranteed.

3. **Asynchronous Programming**:
   - Methods returning `Task` or `ValueTask` MUST end with the `Async` suffix.
   - Always accept and propagate `CancellationToken cancellationToken = default`.
   - Never use `async void`.
   - Never use blocking sync-over-async calls (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`).
   - Use `await using` for types implementing `IAsyncDisposable`.

4. **Performance & Memory**:
   - Guard against multiple enumeration on `IEnumerable<T>`. Materialize with `.ToList()`, `.ToArray()`, or work with `IReadOnlyList<T>`.
   - In EF Core, apply `.AsNoTracking()` to all read-only queries.
   - For string parsing in static analysis loops, leverage `ReadOnlySpan<char>` or string pools to avoid excessive allocations.

5. **Testing**:
   - Test framework is **xUnit**.
   - Assertions using FluentAssertions or xUnit asserts.
   - Mocking via NSubstitute or Moq.
   - Every test follows Arrange-Act-Assert (AAA) structure.
