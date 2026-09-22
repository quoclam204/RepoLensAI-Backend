# Security & Sandboxing Rules

## 1. Zero Code Execution (Non-Negotiable)

RepoLens AI ingests arbitrary, third-party source code and repositories that may contain malicious payloads or exploit attempts.

> [!CAUTION]
> **NEVER EXECUTE ANALYZED REPOSITORY CODE.**
> - All analysis must be 100% static: text parsing, Roslyn AST traversal, regex, and manifest extraction.
> - **Forbidden APIs**:
>   - `System.Diagnostics.Process.Start`
>   - `System.Reflection.Assembly.Load` / `Assembly.LoadFile` / `Assembly.LoadFrom`
>   - `Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Emit` followed by in-memory execution
>   - Invoking target shell scripts, build tools (`dotnet build`, `npm install`, `make`, `python`), or binaries found within the ingested repository.

## 2. Secure Archive & File Ingestion

1. **Zip Slip / Path Traversal Prevention**:
   - Always validate every entry path before extracting:
     ```csharp
     var destinationPath = Path.GetFullPath(Path.Combine(destinationDir, entry.FullName));
     if (!destinationPath.StartsWith(destinationDir, StringComparison.OrdinalIgnoreCase))
     {
         throw new SecurityException("Archive entry attempts path traversal outside target directory.");
     }
     ```
2. **Zip Bomb Prevention**:
   - Enforce maximum uncompressed size limit (e.g. 500 MB).
   - Enforce maximum entry count limit (e.g. 50,000 files).
   - Check compression ratio threshold during streaming extraction.

3. **Symlink / Hardlink Security**:
   - Do not follow symlinks pointing outside the extraction root.

## 3. Data Protection & Secrets Handling

1. **Evidence Sanitization**:
   - Redact detected secrets, tokens, private keys, and passwords before storing evidence or passing snippets to LLM context.
2. **Configuration & Credentials**:
   - Never commit sensitive configuration files (`appsettings.Development.json` with live credentials, `.env` with production keys).
   - Use environment variables or secure secret vaults for PostgreSQL credentials and LLM API keys.
3. **Database Security**:
   - Rely strictly on EF Core parameterized queries or LINQ expressions.
   - Never concatenate raw user input into SQL queries.
