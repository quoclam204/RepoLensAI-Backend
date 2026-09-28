using System.Text.Json;
using RepoLens.Application.Models.Scanning;

namespace RepoLens.Infrastructure.Scanning;

/// <summary>
/// Detects project files and solutions within the repository (T036, FR-003).
/// Identifies C# .csproj, .sln, Node package.json, and TypeScript tsconfig.json.
/// </summary>
public sealed class ProjectDetector
{
    /// <summary>
    /// Checks if a discovered file is a project definition or solution manifest.
    /// </summary>
    /// <param name="relativePath">Normalized relative path using '/'.</param>
    /// <param name="fullPath">Absolute path on disk to inspect contents if necessary.</param>
    /// <returns>ScannedProject record if recognized, null otherwise.</returns>
    public ScannedProject? DetectProject(string relativePath, string fullPath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var fileName = Path.GetFileName(relativePath);

        // C# Project (.csproj)
        if (fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            var projectName = Path.GetFileNameWithoutExtension(fileName);
            return new ScannedProject(projectName, relativePath, ScannedProjectType.CSharpProject);
        }

        // .NET Solution (.sln)
        if (fileName.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            var solutionName = Path.GetFileNameWithoutExtension(fileName);
            return new ScannedProject(solutionName, relativePath, ScannedProjectType.DotNetSolution);
        }

        // Node.js Project (package.json)
        if (string.Equals(fileName, "package.json", StringComparison.OrdinalIgnoreCase))
        {
            var projectName = TryExtractPackageJsonName(fullPath) ?? GetParentDirectoryName(relativePath);
            return new ScannedProject(projectName, relativePath, ScannedProjectType.NodeProject);
        }

        // TypeScript Project (tsconfig.json)
        if (string.Equals(fileName, "tsconfig.json", StringComparison.OrdinalIgnoreCase))
        {
            var projectName = $"{GetParentDirectoryName(relativePath)}";
            return new ScannedProject(projectName, relativePath, ScannedProjectType.TypeScriptProject);
        }

        return null;
    }

    private static string? TryExtractPackageJsonName(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            return null;
        }

        try
        {
            using var fileStream = File.OpenRead(fullPath);
            using var document = JsonDocument.Parse(fileStream);

            if (document.RootElement.TryGetProperty("name", out var nameProp) &&
                nameProp.ValueKind == JsonValueKind.String)
            {
                var name = nameProp.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
        }
        catch
        {
            // If malformed json, fallback to directory name
        }

        return null;
    }

    private static string GetParentDirectoryName(string relativePath)
    {
        var dir = Path.GetDirectoryName(relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrEmpty(dir))
        {
            return "root";
        }

        return Path.GetFileName(dir) ?? "project";
    }
}
