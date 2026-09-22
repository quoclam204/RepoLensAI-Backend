---
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
argument-hint: [diff | pr | file-path]
description: Perform a comprehensive code review focusing on C#/.NET 10 standards, Clean Architecture boundaries, performance, and security.
---

# Code Review

Target: $ARGUMENTS

## Review Procedure

1. **Diff Scope**:
   - If `$ARGUMENTS` is empty or `diff`, inspect uncommitted changes: `git diff HEAD`
   - If a specific commit or branch is given, review the diff against `main`: `git diff main...HEAD`
   - If a file path is provided, inspect the file in full.

2. **Pre-Review Checks**:
   - Check compilation: `dotnet build`
   - Check package vulnerabilities: `dotnet list package --vulnerable`

3. **Evaluation Dimensions**:
   - **Architecture**: Enforces layer direction (`Domain` <- `Application` <- `Infrastructure` / `Analysis` <- `Api`).
   - **Security**: No execution of analyzed target repository code; safe ZIP extraction; sanitized queries.
   - **Async Best Practices**: No `async void`; `CancellationToken` passed throughout; no blocking `.Result`/`.Wait()`.
   - **Resource Disposal**: Proper use of `using` / `await using` on disposable objects.
   - **Performance**: No EF Core N+1 queries; `.AsNoTracking()` on reads; no multiple enumerations of `IEnumerable`.
   - **Testing**: Corresponding xUnit tests created/updated with comprehensive assertions.

4. **Feedback Output**:
   Format each finding with severity `[CRITICAL]`, `[HIGH]`, `[MEDIUM]`, or `[LOW]`, including line numbers, risk analysis, and exact code remediations.
