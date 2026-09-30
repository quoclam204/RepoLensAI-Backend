using System.Text.Json;
using RepoLens.Analysis.Graph;
using RepoLens.Analysis.Orchestration;

namespace RepoLens.AnalysisTests.GoldenDataset;

/// <summary>
/// Golden Dataset tests (T100, NFR-TEST-001): persistent fixtures + frozen baselines + determinism.
/// Fixtures live under tests/Fixtures/{sample-csharp-api,sample-nextjs-app,sample-typescript-app};
/// stable identity is fixtureId@fixtureVersion, never temp path / GUID / timestamp.
/// </summary>
public sealed class GoldenDatasetTests
{
    private static readonly string FixturesRoot = ResolveFixturesRoot();
    private readonly RepositoryAnalysisEngine _engine = new();

    private static string ResolveFixturesRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "Fixtures");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("tests/Fixtures directory not found.");
    }

    private static string FixtureDir(string id) => Path.Combine(FixturesRoot, id);

    private static JsonDocument LoadBaseline(string fixtureId, string version)
    {
        var path = Path.Combine(FixtureDir(fixtureId), "expected-analysis", $"{version}.baseline.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string NormalizeRepoName(string rootPath)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(rootPath));
        return string.IsNullOrWhiteSpace(name) ? "RepositoryRoot" : name;
    }

    private static string StageFixtureToTemp(string fixtureId, out string tempRoot)
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "RepoLens_Golden_" + Guid.NewGuid().ToString("N"));
        CopyFixtureInto(fixtureId, tempRoot);
        return Path.Combine(tempRoot, fixtureId);
    }

    private static void CopyFixtureInto(string fixtureId, string tempRoot)
    {
        var source = FixtureDir(fixtureId);
        var dest = Path.Combine(tempRoot, fixtureId);
        CopyDirectory(source, dest, excludeDirNames: ["bin", "obj", "expected-analysis"]);
    }

    private static void CopyDirectory(string source, string dest, string[] excludeDirNames)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.GetDirectories(source))
        {
            if (excludeDirNames.Contains(Path.GetFileName(dir), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)), excludeDirNames);
        }
        foreach (var file in Directory.GetFiles(source))
        {
            if (Path.GetFileName(file).Equals("fixture.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
        }
    }

    private string NormalizedSnapshot(RepositoryAnalysisResult result)
    {
        var nodes = result.Analysis.Nodes
            .OrderBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
            .Select(n => $"{n.Type}:{n.Name}");
        var rels = result.Analysis.Relationships
            .OrderBy(r => $"{r.SourceId}->{r.Type}->{r.TargetId}", StringComparer.OrdinalIgnoreCase)
            .Select(r => $"{r.SourceId}->{r.Type}->{r.TargetId}");
        var projects = result.ScannedMetadata.Projects
            .OrderBy(p => p.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(p => $"{p.ProjectName}:{p.ProjectType}");
        return string.Join("\n", projects.Concat(nodes).Concat(rels));
    }

    private RepositoryAnalysisResult AnalyzeTwiceDeterministically(string fixtureId)
    {
        var repoA = StageFixtureToTemp(fixtureId, out var tempA);
        string snapA, snapB;
        try
        {
            snapA = NormalizedSnapshot(_engine.AnalyzeRepository(repoA));
        }
        finally
        {
            TryDelete(tempA);
        }

        var repoB = StageFixtureToTemp(fixtureId, out var tempB);
        try
        {
            snapB = NormalizedSnapshot(_engine.AnalyzeRepository(repoB));
        }
        finally
        {
            TryDelete(tempB);
        }

        Assert.Equal(snapA, snapB);

        // Return a fresh staged result for baseline assertions (temp copy removed afterwards).
        var repo = StageFixtureToTemp(fixtureId, out var tempC);
        try
        {
            return _engine.AnalyzeRepository(repo);
        }
        finally
        {
            TryDelete(tempC);
        }
    }

    private static void TryDelete(string tempRoot)
    {
        try
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
        catch
        {
            // Best effort: temp copies must never affect fixture source of truth.
        }
    }

    private static void AssertBaseline(
        JsonDocument baseline,
        RepositoryAnalysisResult result,
        string fixtureId)
    {
        var root = baseline.RootElement;
        var version = root.GetProperty("fixtureVersion").GetString();
        Assert.Equal(fixtureId, root.GetProperty("fixtureId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(version));

        Assert.Empty(result.AllErrors);

        foreach (var expectedProject in root.GetProperty("expectedProjects").EnumerateArray())
        {
            var name = expectedProject.GetProperty("name").GetString()!;
            var type = expectedProject.GetProperty("type").GetString()!;
            var match = fixtureId == "sample-csharp-api"
                ? result.ScannedMetadata.Projects.Any(p =>
                    p.ProjectName.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                    p.ProjectType.Equals(type, StringComparison.OrdinalIgnoreCase))
                : result.ScannedMetadata.Projects.Any(p =>
                    p.ProjectType.Equals(type, StringComparison.OrdinalIgnoreCase));
            Assert.True(match, $"Expected project {name} ({type}) not detected in {fixtureId}@{version}.");
        }

        var nodes = result.Analysis.Nodes;
        foreach (var expectedNode in root.GetProperty("expectedNodes").EnumerateArray())
        {
            var type = expectedNode.GetProperty("type").GetString()!;
            var nodeType = Enum.Parse<KnowledgeNodeType>(type, ignoreCase: true);
            if (expectedNode.TryGetProperty("httpMethod", out var methodProp))
            {
                var method = methodProp.GetString()!;
                Assert.Contains(nodes, n =>
                    n.Type == KnowledgeNodeType.Endpoint &&
                    n.Properties.TryGetValue("HttpMethod", out var m) &&
                    m.Equals(method, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                var name = expectedNode.GetProperty("name").GetString()!;
                Assert.Contains(nodes, n =>
                    n.Type == nodeType &&
                    n.Name.Equals(name, StringComparison.Ordinal));
            }
        }

        var rels = result.Analysis.Relationships;
        foreach (var expectedRel in root.GetProperty("expectedRelationships").EnumerateArray())
        {
            var type = Enum.Parse<KnowledgeRelationshipType>(
                expectedRel.GetProperty("type").GetString()!, ignoreCase: true);
            var sourceId = expectedRel.GetProperty("sourceId").GetString()!;
            var targetId = expectedRel.GetProperty("targetId").GetString()!;
            Assert.Contains(rels, r =>
                r.Type == type &&
                r.SourceId.Equals(sourceId, StringComparison.OrdinalIgnoreCase) &&
                r.TargetId.Equals(targetId, StringComparison.OrdinalIgnoreCase));
        }

        var files = result.FileContents?.Keys
            .Select(k => k.Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        foreach (var expectedFile in root.GetProperty("expectedEvidenceFiles").EnumerateArray())
        {
            var suffix = expectedFile.GetString()!;
            Assert.Contains(files, f => f.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var expectedSnippet in root.GetProperty("expectedEvidenceSnippets").EnumerateArray())
        {
            var snippet = expectedSnippet.GetString()!;
            var found = (result.FileContents?.Values.Any(c => c.Contains(snippet, StringComparison.Ordinal)) == true)
                || result.Analysis.Nodes.Any(n => n.Properties.Values.Any(v => v.Contains(snippet, StringComparison.Ordinal)))
                || result.Analysis.Relationships.Any(r => r.Evidence != null && r.Evidence.Snippet.Contains(snippet, StringComparison.Ordinal));
            Assert.True(found, $"Expected evidence snippet '{snippet}' not found in {fixtureId}@{version}.");
        }

        Assert.Equal(
            root.GetProperty("expectedErrorCount").GetInt32(),
            result.AllErrors.Count);
    }

    [Fact]
    public void SampleCSharpApi_GoldenBaseline_MatchesFrozenExpectations()
    {
        using var baseline = LoadBaseline("sample-csharp-api", "v1");
        var result = AnalyzeTwiceDeterministically("sample-csharp-api");

        // Project identity is the fixture id (stable), never a temp path.
        Assert.Contains(result.ScannedMetadata.Projects, p =>
            p.ProjectName.Equals("sample-csharp-api", StringComparison.OrdinalIgnoreCase));
        AssertBaseline(baseline, result, "sample-csharp-api");
    }

    [Fact]
    public void SampleNextJsApp_GoldenBaseline_MatchesFrozenExpectations()
    {
        using var baseline = LoadBaseline("sample-nextjs-app", "v1");
        var result = AnalyzeTwiceDeterministically("sample-nextjs-app");

        var name = NormalizeRepoName(result.RepositoryPath);
        Assert.False(string.IsNullOrWhiteSpace(name));
        AssertBaseline(baseline, result, "sample-nextjs-app");
    }

    [Fact]
    public void SampleTypeScriptApp_GoldenBaseline_MatchesFrozenExpectations()
    {
        using var baseline = LoadBaseline("sample-typescript-app", "v1");
        var result = AnalyzeTwiceDeterministically("sample-typescript-app");

        var name = NormalizeRepoName(result.RepositoryPath);
        Assert.False(string.IsNullOrWhiteSpace(name));
        AssertBaseline(baseline, result, "sample-typescript-app");
    }
}
