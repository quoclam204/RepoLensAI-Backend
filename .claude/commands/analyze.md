---
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
argument-hint: [project-or-path]
description: Analyze source code, static analysis pipelines, or verify repository analyzer components in RepoLens.Analysis.
---

# Analyze Static Repository & Analysis Components

Target: $ARGUMENTS

## Safety Invariant
> [!CAUTION]
> Analyzed repository code must **NEVER** be executed. All analysis must be strictly static (Roslyn AST, file inspection, manifest parsing).

## Analysis Workflow

1. **Target Identification**:
   - If `$ARGUMENTS` points to an internal analyzer in `src/RepoLens.Analysis/`, inspect its parser rules, AST visitor patterns, and symbol extraction logic.
   - If `$ARGUMENTS` refers to test sample repositories or fixtures in `tests/`, verify how they are parsed and that evidence is correctly extracted.

2. **Analyzer Verification**:
   - Verify Roslyn syntax parsing handles modern C# features (records, primary constructors, top-level statements).
   - Check symbol extraction accuracy: interfaces, classes, methods, parameters, and return types.
   - Check endpoint discovery patterns (controller routes, Minimal APIs).
   - Check EF Core / DB entity discovery patterns.

3. **Evidence Validation**:
   - Ensure extracted components contain relative file paths, valid line spans (1-indexed), and code snippets.
   - Verify confidence scoring calculation for detected patterns.

4. **Archify Model Alignment**:
   - Confirm analyzed components conform to Archify component/relationship schemas.
