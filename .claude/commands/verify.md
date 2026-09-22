---
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
description: Verify the entire RepoLens solution by compiling code, running tests, checking package vulnerabilities, and enforcing Clean Architecture rules.
---

# Solution Verification

Execute the complete verification pipeline for RepoLensAI-Backend:

## Step 1: Solution Build
Run compilation across all projects in `RepoLens.sln`:
```bash
dotnet build --configuration Debug
```
*Criteria*: Must exit with 0 errors and 0 warnings.

## Step 2: Automated Test Execution
Run the complete test suite across Unit, Analysis, and Integration test projects:
```bash
dotnet test --no-build --logger "console;verbosity=normal"
```
*Criteria*: 100% of tests must pass.

## Step 3: Dependency Vulnerability Audit
Scan all referenced NuGet packages across all projects:
```bash
dotnet list package --vulnerable
```
*Criteria*: No critical or high vulnerabilities.

## Step 4: Architectural Sanity Checks
- Verify `RepoLens.Domain` project file has no references to `Infrastructure`, `Analysis`, or `Api`.
- Verify `RepoLens.Application` references only `RepoLens.Domain`.
- Confirm no calls to `Process.Start`, `Assembly.Load`, or compilation-execution APIs exist in `RepoLens.Analysis`.
