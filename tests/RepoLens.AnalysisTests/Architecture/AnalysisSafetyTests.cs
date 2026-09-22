using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RepoLens.AnalysisTests.Architecture;

public class AnalysisSafetyTests
{
    private static string GetSolutionRoot()
    {
        // Traverse upwards from test execution directory to find the solution root
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "RepoLens.sln")))
            {
                return dir;
            }

            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }

        // Fallback to relative path
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    }

    [Fact]
    public void AnalysisEngine_ContainsZeroProcessExecutionApis()
    {
        // Arrange: Find all C# source files in src/RepoLens.Analysis
        var solutionRoot = GetSolutionRoot();
        var analysisSrcDir = Path.Combine(solutionRoot, "src", "RepoLens.Analysis");
        Assert.True(Directory.Exists(analysisSrcDir), $"Analysis source directory not found at: {analysisSrcDir}");

        var csFiles = Directory.GetFiles(analysisSrcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("obj") && !f.Contains("bin"))
            .ToList();

        Assert.NotEmpty(csFiles);

        var forbiddenIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Process", "ProcessStartInfo"
        };

        // Act & Assert: Parse each file with Roslyn AST and inspect IdentifierNameSyntax
        foreach (var file in csFiles)
        {
            var code = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(code, path: file);
            var root = tree.GetCompilationUnitRoot();

            // 1. Check using directives
            foreach (var usingDirective in root.Usings)
            {
                var usingName = usingDirective.Name?.ToString() ?? "";
                Assert.False(
                    usingName.Equals("System.Diagnostics", StringComparison.OrdinalIgnoreCase) ||
                    usingName.StartsWith("System.Diagnostics.Process", StringComparison.OrdinalIgnoreCase),
                    $"File '{Path.GetFileName(file)}' contains forbidden using '{usingName}'");
            }

            // 2. Check identifier usages
            var identifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>();
            foreach (var id in identifiers)
            {
                var idText = id.Identifier.Text;
                Assert.False(
                    forbiddenIdentifiers.Contains(idText),
                    $"File '{Path.GetFileName(file)}' at line {tree.GetLineSpan(id.Span).StartLinePosition.Line + 1} contains forbidden process identifier '{idText}'");
            }
        }
    }

    [Fact]
    public void AnalysisProject_ReferencesOnlyDomainAndRoslyn()
    {
        // Arrange
        var solutionRoot = GetSolutionRoot();
        var csprojPath = Path.Combine(solutionRoot, "src", "RepoLens.Analysis", "RepoLens.Analysis.csproj");
        Assert.True(File.Exists(csprojPath), $"Csproj not found at: {csprojPath}");

        var doc = XDocument.Load(csprojPath);

        // Act: Project references
        var projectRefs = doc.Descendants("ProjectReference")
            .Select(x => (string?)x.Attribute("Include"))
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();

        // Assert: Only RepoLens.Domain allowed
        Assert.Single(projectRefs);
        Assert.Contains("RepoLens.Domain", projectRefs[0]!);

        // Act: Package references
        var packageRefs = doc.Descendants("PackageReference")
            .Select(x => (string?)x.Attribute("Include"))
            .Where(x => !string.IsNullOrEmpty(x))
            .ToList();

        // Assert: Only Microsoft.CodeAnalysis.CSharp allowed
        Assert.Single(packageRefs);
        Assert.Equal("Microsoft.CodeAnalysis.CSharp", packageRefs[0]);
    }
}
