namespace RepoLens.Application.Models.Scanning;

/// <summary>
/// Represents a single file discovered during repository scanning.
/// This is the primary data contract between the Scanner (Person 1) and 
/// Static Analysis (Person 2).
/// </summary>
/// <param name="RelativePath">
/// Path relative to the repository root, normalized to use '/' as separator
/// regardless of OS. Example: "src/RepoLens.Domain/Entities/Repository.cs"
/// </param>
/// <param name="Extension">
/// File extension including the dot. Example: ".cs", ".ts", ".json"
/// </param>
/// <param name="Size">File size in bytes.</param>
/// <param name="Hash">
/// SHA-256 hash of the file content, hex-encoded lowercase.
/// Used for change detection and deduplication.
/// </param>
/// <param name="Language">
/// Detected programming language. Example: "C#", "TypeScript", "JavaScript", "Unknown".
/// </param>
public record ScannedFile(
    string RelativePath,
    string Extension,
    long Size,
    string Hash,
    string Language);

/// <summary>
/// Represents a detected project within the repository.
/// </summary>
/// <param name="Name">Project name (e.g. "RepoLens.Domain").</param>
/// <param name="RelativePath">
/// Path to the project file relative to the repository root, normalized with '/'.
/// Example: "src/RepoLens.Domain/RepoLens.Domain.csproj"
/// </param>
/// <param name="ProjectType">Type of project detected.</param>
public record ScannedProject(
    string Name,
    string RelativePath,
    ScannedProjectType ProjectType);

/// <summary>
/// Supported project types for detection.
/// </summary>
public enum ScannedProjectType
{
    /// <summary>.csproj file (C# / .NET project).</summary>
    CSharpProject,

    /// <summary>.sln file (.NET solution).</summary>
    DotNetSolution,

    /// <summary>package.json (Node.js / JavaScript / TypeScript project).</summary>
    NodeProject,

    /// <summary>tsconfig.json (TypeScript-specific project configuration).</summary>
    TypeScriptProject,

    /// <summary>Unknown or unsupported project type.</summary>
    Unknown
}

/// <summary>
/// Aggregated result of a full repository scan.
/// This is the output of the Scanner pipeline stage, consumed by downstream
/// analysis stages (Static Analysis, Dependency Analysis, etc.).
/// </summary>
public record ScanResult
{
    /// <summary>The analysis this scan belongs to.</summary>
    public required Guid AnalysisId { get; init; }

    /// <summary>Absolute path to the workspace root directory.</summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>All discovered source files after applying ignore rules.</summary>
    public required IReadOnlyList<ScannedFile> Files { get; init; }

    /// <summary>Programming languages detected in the repository.</summary>
    public required IReadOnlyList<string> DetectedLanguages { get; init; }

    /// <summary>Projects detected in the repository.</summary>
    public required IReadOnlyList<ScannedProject> DetectedProjects { get; init; }

    /// <summary>Total number of valid source files.</summary>
    public int TotalFiles => Files.Count;

    /// <summary>Total size of all scanned files in bytes.</summary>
    public long TotalSizeBytes => Files.Sum(f => f.Size);

    /// <summary>How long the scan took.</summary>
    public required TimeSpan Duration { get; init; }
}
