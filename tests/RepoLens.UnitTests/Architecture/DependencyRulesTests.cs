using System.Xml.Linq;

namespace RepoLens.UnitTests.Architecture;

public class DependencyRulesTests
{
    private static readonly string SolutionRoot = FindSolutionRoot();

    private static string FindSolutionRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "RepoLens.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new InvalidOperationException("Could not find solution root directory containing RepoLens.sln.");
    }

    private static List<string> GetProjectReferences(string projectName)
    {
        var csprojPath = Path.Combine(SolutionRoot, "src", projectName, $"{projectName}.csproj");
        Assert.True(File.Exists(csprojPath), $"Project file not found at: {csprojPath}");

        var doc = XDocument.Load(csprojPath);
        return doc.Descendants("ProjectReference")
            .Select(pr => pr.Attribute("Include")?.Value)
            .Where(val => !string.IsNullOrEmpty(val))
            .Select(val => Path.GetFileNameWithoutExtension(val!))
            .ToList();
    }

    [Fact]
    public void Domain_MustNotReferenceAnyProject()
    {
        var references = GetProjectReferences("RepoLens.Domain");

        Assert.Empty(references);
    }

    [Fact]
    public void Application_MustReferenceOnlyDomain()
    {
        var references = GetProjectReferences("RepoLens.Application");

        Assert.Single(references);
        Assert.Contains("RepoLens.Domain", references);
        Assert.DoesNotContain("RepoLens.Infrastructure", references);
        Assert.DoesNotContain("RepoLens.Api", references);
        Assert.DoesNotContain("RepoLens.Analysis", references);
    }

    [Fact]
    public void Infrastructure_MustReferenceOnlyApplicationAndDomain()
    {
        var references = GetProjectReferences("RepoLens.Infrastructure");

        Assert.Equal(2, references.Count);
        Assert.Contains("RepoLens.Application", references);
        Assert.Contains("RepoLens.Domain", references);
        Assert.DoesNotContain("RepoLens.Api", references);
        Assert.DoesNotContain("RepoLens.Analysis", references);
    }

    [Fact]
    public void Analysis_MustReferenceOnlyDomain()
    {
        var references = GetProjectReferences("RepoLens.Analysis");

        Assert.Single(references);
        Assert.Contains("RepoLens.Domain", references);
        Assert.DoesNotContain("RepoLens.Application", references);
        Assert.DoesNotContain("RepoLens.Infrastructure", references);
        Assert.DoesNotContain("RepoLens.Api", references);
    }

    [Fact]
    public void Api_MustReferenceOnlyApplicationAndInfrastructure()
    {
        var references = GetProjectReferences("RepoLens.Api");

        Assert.Equal(2, references.Count);
        Assert.Contains("RepoLens.Application", references);
        Assert.Contains("RepoLens.Infrastructure", references);
        Assert.DoesNotContain("RepoLens.Domain", references);
        Assert.DoesNotContain("RepoLens.Analysis", references);
    }

    [Fact]
    public void DependencyGraph_MustBeAcyclic_AndFollowStrictLayering()
    {
        // Layer levels: higher layers may only depend on lower layers
        var layers = new Dictionary<string, int>
        {
            ["RepoLens.Domain"] = 0,
            ["RepoLens.Application"] = 1,
            ["RepoLens.Analysis"] = 1,
            ["RepoLens.Infrastructure"] = 2,
            ["RepoLens.Api"] = 3
        };

        foreach (var (project, layer) in layers)
        {
            var references = GetProjectReferences(project);
            foreach (var reference in references)
            {
                Assert.True(
                    layers.TryGetValue(reference, out var refLayer),
                    $"Project '{project}' references unknown project '{reference}'."
                );

                Assert.True(
                    refLayer < layer,
                    $"Layer violation: Project '{project}' (layer {layer}) cannot reference '{reference}' (layer {refLayer})."
                );
            }
        }
    }
}
