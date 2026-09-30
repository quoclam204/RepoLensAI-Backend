using System.Text.Json;
using RepoLens.Application.Abstractions;
using RepoLens.Application.Abstractions.AI;
using RepoLens.Application.Abstractions.AI.Models;
using RepoLens.Application.Models.RAG;
using RepoLens.Application.Services;

namespace RepoLens.UnitTests.Evaluation;

/// <summary>
/// AI Evaluation Benchmark Suite covering T104 - T107 (NFR-AI-001, NFR-AI-002).
/// T104 dataset lives in t104-evaluation-dataset.json (machine-readable, deterministic)
/// and is grounded ONLY in T100 golden fixtures + frozen baselines — never the real RepoLens repository.
/// T105-T107 tests below are unchanged (future scoring/grounding logic is out of T104 scope).
/// </summary>
public class AiEvaluationSuiteTests
{
    private readonly Guid _analysisId = Guid.NewGuid();

    private static readonly string DatasetPath = ResolveDatasetPath();
    private static readonly string RepoRoot = ResolveRepoRoot();

    private static string ResolveRepoRoot()
    {
        // Under dotnet test the JSON is content-copied next to the test assembly;
        // walk up to the tests/ folder, then step into the shared tests/Fixtures tree.
        var dir = new DirectoryInfo(Path.GetDirectoryName(DatasetPath)!);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Fixtures");
            if (Directory.Exists(candidate))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("tests/Fixtures directory not found.");
    }

    private static string ResolveDatasetPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "RepoLens.UnitTests", "Evaluation", "t104-evaluation-dataset.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException("t104-evaluation-dataset.json not found under any tests/ directory.");
    }

    private static string LoadDatasetText() => File.ReadAllText(DatasetPath);

    private static JsonDocument LoadDataset() => JsonDocument.Parse(LoadDatasetText());

    private static string FixtureDir(string fixtureId) => Path.Combine(RepoRoot, "Fixtures", fixtureId);

    private static JsonElement LoadBaseline(string fixtureId, string version)
    {
        var path = Path.Combine(FixtureDir(fixtureId), "expected-analysis", $"{version}.baseline.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }

    private static IReadOnlyList<string> FixtureSourceTexts(string fixtureId)
    {
        var root = FixtureDir(fixtureId);
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(f =>
            {
                var rel = Path.GetRelativePath(root, f).Replace('\\', '/');
                return !rel.StartsWith("bin/", StringComparison.OrdinalIgnoreCase)
                    && !rel.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
                    && !rel.StartsWith("expected-analysis/", StringComparison.OrdinalIgnoreCase)
                    && !rel.EndsWith("fixture.json", StringComparison.OrdinalIgnoreCase);
            })
            .Select(File.ReadAllText)
            .ToList();
    }

    [Fact]
    public void T104_EvaluationDataset_IsDeterministicAndWellFormed()
    {
        // Deterministic: two independent loads yield identical bytes.
        var first = LoadDatasetText();
        var second = LoadDatasetText();
        Assert.Equal(first, second);

        using var dataset = LoadDataset();
        var root = dataset.RootElement;
        Assert.Equal("t104-ai-evaluation", root.GetProperty("datasetId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("datasetVersion").GetString()));

        var cases = root.GetProperty("cases").EnumerateArray().ToList();
        Assert.Equal(10, cases.Count);

        var ids = cases.Select(c => c.GetProperty("caseId").GetString()!).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());

        var allowedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "architecture", "authentication", "api-endpoints",
            "class-dependencies", "module-dependencies",
            "database-entities", "database-connection-configuration", "unknown"
        };

        foreach (var c in cases)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("caseId").GetString()));
            Assert.True(allowedCategories.Contains(c.GetProperty("category").GetString()!));
            Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("fixtureId").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("fixtureVersion").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("question").GetString()));

            var answerable = c.GetProperty("answerable").GetBoolean();
            if (answerable)
            {
                Assert.NotEmpty(c.GetProperty("expectedEvidenceFiles").EnumerateArray().ToList());
                Assert.NotEmpty(c.GetProperty("expectedEvidenceSnippets").EnumerateArray().ToList());
                Assert.NotEmpty(c.GetProperty("expectedConcepts").EnumerateArray().ToList());
                Assert.NotEmpty(c.GetProperty("expectedFacts").EnumerateArray().ToList());
                Assert.NotEmpty(c.GetProperty("answerCharacteristics").EnumerateArray().ToList());
            }
            else
            {
                Assert.Empty(c.GetProperty("expectedEvidenceFiles").EnumerateArray().ToList());
                Assert.Empty(c.GetProperty("expectedEvidenceSnippets").EnumerateArray().ToList());
            }
        }
    }

    [Fact]
    public void T104_EvaluationDataset_CoversRequiredQuestionCategories()
    {
        using var dataset = LoadDataset();
        var categories = dataset.RootElement.GetProperty("cases").EnumerateArray()
            .Select(c => c.GetProperty("category").GetString()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("architecture", categories);
        Assert.Contains("authentication", categories);
        Assert.Contains("api-endpoints", categories);
        Assert.True(categories.Contains("class-dependencies") || categories.Contains("module-dependencies"));
        Assert.Contains("database-entities", categories);
        Assert.Contains("database-connection-configuration", categories);
        Assert.Contains("unknown", categories);
    }

    [Fact]
    public void T104_EvaluationDataset_GroundTruthResolvesToT100Fixtures()
    {
        using var dataset = LoadDataset();
        foreach (var c in dataset.RootElement.GetProperty("cases").EnumerateArray())
        {
            var caseId = c.GetProperty("caseId").GetString()!;
            var fixtureId = c.GetProperty("fixtureId").GetString()!;
            var version = c.GetProperty("fixtureVersion").GetString()!;

            // Fixture + frozen baseline must exist and carry the same stable identity.
            var dir = FixtureDir(fixtureId);
            Assert.True(Directory.Exists(dir), $"{caseId}: missing T100 fixture '{fixtureId}'.");
            using var fixtureMeta = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(dir, "fixture.json")));
            Assert.Equal(fixtureId, fixtureMeta.RootElement.GetProperty("id").GetString());
            Assert.Equal(version, fixtureMeta.RootElement.GetProperty("version").GetString());

            var baseline = LoadBaseline(fixtureId, version);
            var nodeIds = baseline.GetProperty("expectedNodes").EnumerateArray()
                .Select(n => n.GetProperty("id").GetString()!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var relEndpoints = baseline.GetProperty("expectedRelationships").EnumerateArray()
                .SelectMany(r => new[] { r.GetProperty("sourceId").GetString()!, r.GetProperty("targetId").GetString()! })
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var baselineSnippets = string.Join("\n", baseline.GetProperty("expectedEvidenceSnippets")
                .EnumerateArray().Select(s => s.GetString()!));
            var sourceTexts = FixtureSourceTexts(fixtureId);
            var fixtureFiles = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(dir, f).Replace('\\', '/'))
                .ToList();

            foreach (var f in c.GetProperty("expectedEvidenceFiles").EnumerateArray())
            {
                var expected = f.GetString()!;
                Assert.Contains(fixtureFiles, actual =>
                    actual.Equals(expected, StringComparison.OrdinalIgnoreCase)
                    || actual.EndsWith("/" + expected, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var s in c.GetProperty("expectedEvidenceSnippets").EnumerateArray())
            {
                var snippet = s.GetString()!;
                var found = baselineSnippets.Contains(snippet, StringComparison.Ordinal)
                    || sourceTexts.Any(t => t.Contains(snippet, StringComparison.Ordinal));
                Assert.True(found, $"{caseId}: snippet '{snippet}' not in T100 baseline or fixture sources.");
            }

            foreach (var k in c.GetProperty("expectedEvidenceKeys").EnumerateArray())
            {
                var key = k.GetString()!;
                Assert.True(ResolveEvidenceKey(key, nodeIds, relEndpoints, fixtureId),
                    $"{caseId}: evidence key '{key}' does not resolve to T100 ground truth.");
            }
        }
    }

    private static bool ResolveEvidenceKey(
        string key,
        HashSet<string> nodeIds,
        HashSet<string> relEndpoints,
        string fixtureId)
    {
        if (nodeIds.Contains(key) || relEndpoints.Contains(key))
        {
            return true;
        }

        if (key.StartsWith("package:", StringComparison.OrdinalIgnoreCase))
        {
            var name = key["package:".Length..];
            var pkgPath = Path.Combine(FixtureDir(fixtureId), "package.json");
            if (!File.Exists(pkgPath))
            {
                return false;
            }
            using var pkg = JsonDocument.Parse(File.ReadAllText(pkgPath));
            foreach (var section in new[] { "dependencies", "devDependencies", "peerDependencies" })
            {
                if (pkg.RootElement.TryGetProperty(section, out var deps)
                    && deps.EnumerateObject().Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            return false;
        }

        if (key.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return File.Exists(Path.Combine(FixtureDir(fixtureId), key["file:".Length..].Replace('/', Path.DirectorySeparatorChar)));
        }

        if (key.StartsWith("module:", StringComparison.OrdinalIgnoreCase))
        {
            var module = key["module:".Length..];
            return FixtureSourceTexts(fixtureId).Any(t => t.Contains(module, StringComparison.Ordinal));
        }

        return false;
    }

    [Fact]
    public void T104_EvaluationDataset_UsesNoRealRepoGroundTruthAndNoSecrets()
    {
        var text = LoadDatasetText();

        // No case may use the real RepoLens repository as ground truth (legacy preliminary dataset did).
        foreach (var marker in new[] { "RepoLensDbContext", "src/RepoLens.", "/api/analyses", "Clean Architecture with Domain, Application" })
        {
            Assert.DoesNotContain(marker, text, StringComparison.OrdinalIgnoreCase);
        }

        // No secrets, credentials, private keys, or connection-string credential assignments.
        foreach (var marker in new[] { "BEGIN PRIVATE KEY", "password=", "password:", "passwd", "api_key", "apikey", "secret_key", "mongodb://", "postgres://" })
        {
            Assert.DoesNotContain(marker, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task T105_RetrievalQuality_ReturnsRelevantEvidenceForGroundedQuestions()
    {
        // Arrange
        var fakeRetriever = new TestVectorRetriever();
        var relevantChunk = new VectorChunkSearchResult(
            ChunkId: Guid.NewGuid(),
            AnalysisId: _analysisId,
            SourceFileId: Guid.NewGuid(),
            FilePath: "src/RepoLens.Api/Program.cs",
            Symbol: "Program.Main",
            StartLine: 1,
            EndLine: 30,
            Content: "builder.Services.AddDbContext<RepoLensDbContext>(opt => opt.UseNpgsql(...));",
            TokenCount: 20,
            ChunkIndex: 0,
            EvidenceId: Guid.NewGuid(),
            ConfidenceScore: 0.95f,
            CosineDistance: 0.1,
            SimilarityScore: 0.90);

        fakeRetriever.RegisterResult([relevantChunk]);

        // Act
        var results = await fakeRetriever.RetrieveSimilarChunksAsync(_analysisId, new float[1536], 5);

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Content.Contains("AddDbContext<RepoLensDbContext>"));
        Assert.All(results, r => Assert.True(r.SimilarityScore >= 0.7));
    }

    [Fact]
    public async Task T106_AnswerGrounding_ValidatesSupportedClaimsAndCalculatesConfidence()
    {
        // Arrange
        var chunk = new VectorChunkSearchResult(
            ChunkId: Guid.NewGuid(),
            AnalysisId: _analysisId,
            SourceFileId: Guid.NewGuid(),
            FilePath: "src/Controllers/WeatherController.cs",
            Symbol: "WeatherController.Get",
            StartLine: 15,
            EndLine: 25,
            Content: "[HttpGet] public IActionResult Get() => Ok();",
            TokenCount: 15,
            ChunkIndex: 0,
            EvidenceId: Guid.NewGuid(),
            ConfidenceScore: 0.9f,
            CosineDistance: 0.05,
            SimilarityScore: 0.95);

        var validator = new AiEvidenceValidator();
        var calculator = new AiConfidenceCalculator();

        var candidateAnswer = "The system exposes an HTTP GET endpoint at /api/Weather in WeatherController.";

        // Act
        var validation = await validator.ValidateAnswerAsync(candidateAnswer, [chunk]);
        var confidenceResult = calculator.EvaluateConfidence(new ConfidenceEvaluationRequest(
            "What endpoints exist?", candidateAnswer, [chunk], validation));

        // Assert
        Assert.True(validation.IsValid);
        Assert.NotEqual(AiConfidenceLevel.Unknown, confidenceResult.Level);
    }

    [Fact]
    public async Task T107_UnknownAndInsufficientEvidence_RefusesToFabricateAndReturnsStandardText()
    {
        // Arrange: Vector retriever returns 0 chunks for out-of-scope question
        var fakeRetriever = new TestVectorRetriever();
        var fakeEmbeddingProvider = new TestEmbeddingProvider();
        var fakeAiProvider = new TestAiProvider("Fabricated answer that should never be shown");

        var ragService = new RagService(
            fakeAiProvider,
            fakeEmbeddingProvider,
            fakeRetriever);

        var outOfScopeQuestion = "Where is the Kubernetes cluster deployment configuration located?";

        // Act
        var result = await ragService.AnswerQuestionAsync(_analysisId, outOfScopeQuestion);

        // Assert
        Assert.Equal(InsufficientEvidenceResponse.DefaultMessage, result.Answer);
        Assert.Equal(AiConfidenceLevel.Unknown, result.Confidence);
        Assert.Empty(result.Evidence);
        Assert.False(result.HasSufficientEvidence);
    }

    private sealed class TestVectorRetriever : IVectorChunkRetriever
    {
        private IReadOnlyList<VectorChunkSearchResult> _results = [];

        public void RegisterResult(IReadOnlyList<VectorChunkSearchResult> results)
        {
            _results = results;
        }

        public Task<IReadOnlyList<VectorChunkSearchResult>> RetrieveSimilarChunksAsync(
            Guid analysisId,
            float[] queryEmbedding,
            int topK = 5,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<VectorChunkSearchResult>>(_results.Take(topK).ToList());
        }
    }

    private sealed class TestEmbeddingProvider : IEmbeddingProvider
    {
        public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
        {
            var vectors = inputs.Select(_ => new float[1536]).ToList();
            return Task.FromResult<IReadOnlyList<float[]>>(vectors);
        }
    }

    private sealed class TestAiProvider : IAiProvider
    {
        private readonly string _response;
        public TestAiProvider(string response) => _response = response;

        public Task<AiResponse> GenerateAsync(AiRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiResponse
            {
                Answer = _response,
                Confidence = AiConfidenceLevel.High,
                Evidence = []
            });
        }
    }
}
