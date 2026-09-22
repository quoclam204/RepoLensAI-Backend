using System.Xml;
using System.Xml.Linq;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.Analysis.CSharp;

public sealed record ProjectReference(
    string TargetProjectName,
    string RelativePath,
    SourceLocation Location,
    string Snippet,
    ConfidenceScore Confidence);

public sealed record PackageReference(
    string PackageName,
    string? Version,
    SourceLocation Location,
    string Snippet,
    ConfidenceScore Confidence);

public sealed record ProjectDependencyResult(
    IReadOnlyList<ProjectReference> ProjectReferences,
    IReadOnlyList<PackageReference> PackageReferences);

/// <summary>
/// Extracts project references and NuGet package dependencies directly from .csproj XML manifests.
/// </summary>
public class ProjectDependencyExtractor
{
    public ProjectDependencyResult Analyze(string csprojPath, string csprojContent)
    {
        var normalizedPath = string.IsNullOrWhiteSpace(csprojPath) ? "unknown.csproj" : csprojPath.Replace('\\', '/');

        if (string.IsNullOrWhiteSpace(csprojContent))
        {
            return new ProjectDependencyResult([], []);
        }

        try
        {
            var doc = XDocument.Parse(csprojContent, LoadOptions.SetLineInfo);
            var projectReferences = new List<ProjectReference>();
            var packageReferences = new List<PackageReference>();

            // Extract <ProjectReference Include="..." />
            var projRefElements = doc.Descendants()
                .Where(e => e.Name.LocalName.Equals("ProjectReference", StringComparison.OrdinalIgnoreCase));

            foreach (var elem in projRefElements)
            {
                var includeAttr = elem.Attribute("Include")?.Value;
                if (string.IsNullOrWhiteSpace(includeAttr))
                {
                    continue;
                }

                var normalizedRelative = includeAttr.Replace('\\', '/').Trim();
                var targetProjectName = Path.GetFileNameWithoutExtension(normalizedRelative);

                var lineInfo = (IXmlLineInfo)elem;
                var lineNumber = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;
                var location = new SourceLocation(normalizedPath, lineNumber, lineNumber);
                var snippet = elem.ToString().Trim();

                projectReferences.Add(new ProjectReference(
                    TargetProjectName: targetProjectName,
                    RelativePath: normalizedRelative,
                    Location: location,
                    Snippet: snippet,
                    Confidence: ConfidenceScore.High));
            }

            // Extract <PackageReference Include="..." Version="..." />
            var pkgRefElements = doc.Descendants()
                .Where(e => e.Name.LocalName.Equals("PackageReference", StringComparison.OrdinalIgnoreCase));

            foreach (var elem in pkgRefElements)
            {
                var includeAttr = elem.Attribute("Include")?.Value;
                if (string.IsNullOrWhiteSpace(includeAttr))
                {
                    continue;
                }

                var versionAttr = elem.Attribute("Version")?.Value ?? elem.Elements()
                    .FirstOrDefault(x => x.Name.LocalName.Equals("Version", StringComparison.OrdinalIgnoreCase))?.Value;

                var lineInfo = (IXmlLineInfo)elem;
                var lineNumber = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;
                var location = new SourceLocation(normalizedPath, lineNumber, lineNumber);
                var snippet = elem.ToString().Trim();

                packageReferences.Add(new PackageReference(
                    PackageName: includeAttr.Trim(),
                    Version: versionAttr?.Trim(),
                    Location: location,
                    Snippet: snippet,
                    Confidence: ConfidenceScore.High));
            }

            return new ProjectDependencyResult(projectReferences.AsReadOnly(), packageReferences.AsReadOnly());
        }
        catch (XmlException)
        {
            // If XML is malformed, return empty result gracefully
            return new ProjectDependencyResult([], []);
        }
    }
}
