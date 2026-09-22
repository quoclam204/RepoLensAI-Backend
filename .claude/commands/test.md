---
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
argument-hint: [project | filter | file-path]
description: Run .NET test suites with xUnit or generate comprehensive unit/integration tests for a target class or component.
---

# Test Runner & Test Generator

Target: $ARGUMENTS

## Test Environment (.NET 10 / xUnit)

- **Test Projects**:
  - `tests/RepoLens.UnitTests`: Domain and Application unit tests
  - `tests/RepoLens.AnalysisTests`: Static analysis engine & Roslyn parser tests
  - `tests/RepoLens.IntegrationTests`: End-to-end and database integration tests
- **Framework**: xUnit (`[Fact]`, `[Theory]`, `[InlineData]`)
- **Mocking**: NSubstitute / Moq

## Mode 1: Run Tests

If `$ARGUMENTS` is empty or specifies a project/filter:
1. Run all tests:
   ```bash
   dotnet test --logger "console;verbosity=normal"
   ```
2. Run specific test project:
   ```bash
   dotnet test tests/RepoLens.UnitTests
   dotnet test tests/RepoLens.AnalysisTests
   dotnet test tests/RepoLens.IntegrationTests
   ```
3. Run with filter:
   ```bash
   dotnet test --filter "$ARGUMENTS"
   ```

## Mode 2: Generate Tests

If `$ARGUMENTS` specifies a source file or component to test:
1. **Analyze Target**: Read the target class/interface, identify public methods, dependencies, edge cases, and exceptions.
2. **Determine Test Scope**:
   - Domain logic: Pure unit tests, testing business invariants and value object equality.
   - Application handlers: Mock injected interfaces (repositories, analyzers, external clients).
   - Analysis engine: Provide mock C# source code snippets as strings, pass to Roslyn syntax tree, and assert extracted symbols/evidence.
3. **Structure Test Class**:
   - Follow standard xUnit conventions: `ClassNameTests.cs`.
   - Use AAA pattern (Arrange, Act, Assert).
   - Test happy path, boundary conditions, null/empty inputs, and expected domain exceptions.
   - Ensure proper isolation with no shared mutable state between tests.
