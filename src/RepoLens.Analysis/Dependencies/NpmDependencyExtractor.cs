using System.Text.Json;
using RepoLens.Analysis.CSharp;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.Analysis.Dependencies;

public sealed record NpmDependencyResult(
    IReadOnlyList<PackageReference> Dependencies,
    IReadOnlyList<string> Errors);

/// <summary>
/// Safely extracts npm package dependencies directly from package.json JSON manifests
/// without executing any project code or npm scripts (T050 / FR-005).
/// </summary>
public class NpmDependencyExtractor
{
    public NpmDependencyResult Analyze(string packageJsonPath, string packageJsonContent)
    {
        var normalizedPath = string.IsNullOrWhiteSpace(packageJsonPath) ? "package.json" : packageJsonPath.Replace('\\', '/');
        var dependencies = new List<PackageReference>();
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(packageJsonContent))
        {
            return new NpmDependencyResult([], []);
        }

        try
        {
            using var doc = JsonDocument.Parse(packageJsonContent);
            var root = doc.RootElement;

            var lines = packageJsonContent.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

            ExtractSection(root, "dependencies", normalizedPath, lines, dependencies);
            ExtractSection(root, "devDependencies", normalizedPath, lines, dependencies);
            ExtractSection(root, "peerDependencies", normalizedPath, lines, dependencies);

            return new NpmDependencyResult(dependencies.AsReadOnly(), errors.AsReadOnly());
        }
        catch (JsonException ex)
        {
            errors.Add($"Malformed package.json at '{normalizedPath}': {ex.Message}");
            return new NpmDependencyResult([], errors.AsReadOnly());
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to parse package.json at '{normalizedPath}': {ex.Message}");
            return new NpmDependencyResult([], errors.AsReadOnly());
        }
    }

    private static void ExtractSection(
        JsonElement root,
        string sectionName,
        string filePath,
        string[] lines,
        List<PackageReference> results)
    {
        if (!root.TryGetProperty(sectionName, out var section) || section.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var prop in section.EnumerateObject())
        {
            var pkgName = prop.Name;
            var version = prop.Value.GetString();
            var lineNumber = FindLineNumber(lines, pkgName);
            var location = new SourceLocation(filePath, lineNumber, lineNumber);
            var snippet = $"\"{pkgName}\": \"{version}\"";

            results.Add(new PackageReference(
                PackageName: pkgName,
                Version: version,
                Location: location,
                Snippet: snippet,
                Confidence: ConfidenceScore.High));
        }
    }

    private static int FindLineNumber(string[] lines, string searchKey)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains($"\"{searchKey}\"", StringComparison.OrdinalIgnoreCase))
            {
                return i + 1;
            }
        }
        return 1;
    }
}
