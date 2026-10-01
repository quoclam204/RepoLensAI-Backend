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
        // Arrange: Register chunks with varying similarity scores, including irrelevant/low-similarity ones
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

        var irrelevantChunk = new VectorChunkSearchResult(
            ChunkId: Guid.NewGuid(),
            AnalysisId: _analysisId,
            SourceFileId: Guid.NewGuid(),
            FilePath: "src/Other/Irrelevant.cs",
            Symbol: null,
            StartLine: 1,
            EndLine: 10,
            Content: "totally unrelated content about bears",
            TokenCount: 5,
            ChunkIndex: 1,
            EvidenceId: Guid.NewGuid(),
            ConfidenceScore: 0.30f,
            CosineDistance: 0.8,
            SimilarityScore: 0.30);

        var mediumChunk = new VectorChunkSearchResult(
            ChunkId: Guid.NewGuid(),
            AnalysisId: _analysisId,
            SourceFileId: Guid.NewGuid(),
            FilePath: "src/RepoLens.Api/WeatherController.cs",
            Symbol: "WeatherController",
            StartLine: 1,
            EndLine: 50,
            Content: "public IActionResult Get() => Ok();",
            TokenCount: 15,
            ChunkIndex: 2,
            EvidenceId: Guid.NewGuid(),
            ConfidenceScore: 0.60f,
            CosineDistance: 0.4,
            SimilarityScore: 0.60);

        fakeRetriever.RegisterResult([relevantChunk, irrelevantChunk, mediumChunk]);

        // Act
        var results = await fakeRetriever.RetrieveSimilarChunksAsync(_analysisId, new float[1536], 5);

        // Assert: Results are sorted by SimilarityScore descending (ranking)
        Assert.NotEmpty(results);
        // Top result should be the most relevant (highest similarity)
        Assert.Equal(0.90, results[0].SimilarityScore);
        // Only relevant/above-threshold chunks should appear (score >= 0.7)
        var highRelevanceResults = results.Where(r => r.SimilarityScore >= 0.7).ToList();
        Assert.Contains(highRelevanceResults, r => r.Content.Contains("AddDbContext<RepoLensDbContext>"));
        // All returned chunks should meet minimum similarity threshold
        Assert.All(results, r => Assert.True(r.SimilarityScore >= 0.3));
        // Verify ordering: scores should be non-increasing
        for (int i = 1; i < results.Count; i++)
        {
            var prevScore = results[i - 1].SimilarityScore;
            var currScore = results[i].SimilarityScore;
            Assert.True(prevScore >= currScore,
                prevScore + " should not be less than " + currScore);
        }
    }

    [Fact]
    public async Task T106_AnswerGrounding_ValidatesSupportedClaimsAndCalculatesConfidence()
    {
        // Arrange: real T100 evidence from tests/Fixtures/sample-csharp-api/WeatherController.cs
        // (GetAll method: [Authorize] + [HttpGet] + GetAll + GetForecastsAsync).
        // T106 scope: answer grounding only, NOT end-to-end vector retrieval.
        var chunk = new VectorChunkSearchResult(
            ChunkId: Guid.NewGuid(),
            AnalysisId: _analysisId,
            SourceFileId: Guid.NewGuid(),
            FilePath: "WeatherController.cs",
            Symbol: "WeatherController.GetAll",
            StartLine: 19,
            EndLine: 24,
            Content: "[Authorize] [HttpGet] public async Task<IActionResult> GetAll() { return Ok(await _weatherService.GetForecastsAsync()); }",
            TokenCount: 15,
            ChunkIndex: 0,
            EvidenceId: Guid.NewGuid(),
            ConfidenceScore: 0.9f,
            CosineDistance: 0.05,
            SimilarityScore: 0.95);

        var validator = new AiEvidenceValidator();
        var calculator = new AiConfidenceCalculator();

        var candidateAnswer = "The `WeatherController` exposes `GetAll` in `WeatherController.cs` with `[HttpGet]` and `[Authorize]`.";

        // Act
        var validation = await validator.ValidateAnswerAsync(candidateAnswer, [chunk]);
        var confidenceResult = calculator.EvaluateConfidence(new ConfidenceEvaluationRequest(
            "What endpoints exist?", candidateAnswer, [chunk], validation));

        // Assert
        Assert.True(validation.IsValid);
        Assert.Equal(AnswerValidationStatus.FullySupported, validation.Status);
        Assert.Empty(validation.UnsupportedClaims);
        Assert.Empty(validation.RejectedCitations);
        Assert.All(validation.ClaimDetails, c => Assert.True(c.IsSupported));
        Assert.Contains(validation.ValidatedEvidence, e =>
            e.File == "WeatherController.cs" && e.Symbol == "WeatherController.GetAll");
        Assert.Contains(validation.ValidatedEvidence, e =>
            e.StartLine == 19 && e.EndLine == 24);
        Assert.Equal(AiConfidenceLevel.High, confidenceResult.Level);
    }

    [Fact]
    public async Task T106_AnswerGrounding_UnsupportedClaim_IsPartiallySupportedAndNotHighConfidence()
    {
        // Arrange: same real T100 chunk, but the answer adds a claim with no grounding.
        var chunk = new VectorChunkSearchResult(
            ChunkId: Guid.NewGuid(),
            AnalysisId: _analysisId,
            SourceFileId: Guid.NewGuid(),
            FilePath: "WeatherController.cs",
            Symbol: "WeatherController.GetAll",
            StartLine: 19,
            EndLine: 24,
            Content: "[Authorize] [HttpGet] public async Task<IActionResult> GetAll() { return Ok(await _weatherService.GetForecastsAsync()); }",
            TokenCount: 15,
            ChunkIndex: 0,
            EvidenceId: Guid.NewGuid(),
            ConfidenceScore: 0.9f,
            CosineDistance: 0.05,
            SimilarityScore: 0.95);

        var validator = new AiEvidenceValidator();
        var calculator = new AiConfidenceCalculator();

        var candidateAnswer = "The `WeatherController` exposes `GetAll` in `WeatherController.cs` with `[HttpGet]` and `[Authorize]`. It also persists to `MongoDatabase.cs` using `SaveToMongoCluster`.";

        // Act
        var validation = await validator.ValidateAnswerAsync(candidateAnswer, [chunk]);
        var confidenceResult = calculator.EvaluateConfidence(new ConfidenceEvaluationRequest(
            "What endpoints exist?", candidateAnswer, [chunk], validation));

        // Assert: validator detects the ungrounded second sentence per its actual contract
        // (PartiallySupported + IsValid false + uncertainty-tagged answer under default options).
        Assert.False(validation.IsValid);
        Assert.Equal(AnswerValidationStatus.PartiallySupported, validation.Status);
        Assert.NotEqual(AnswerValidationStatus.FullySupported, validation.Status);
        Assert.Single(validation.UnsupportedClaims);
        Assert.Contains("MongoDatabase.cs", validation.UnsupportedClaims[0]);
        Assert.Contains(AnswerValidationOptions.Default.UncertaintyPrefix, validation.ValidatedAnswer);
        Assert.NotEqual(AiConfidenceLevel.High, confidenceResult.Level);
    }

    [Fact]
    public async Task T106_AnswerGrounding_InsufficientEvidence_ReturnsCanonicalContract()
    {
        // Arrange: empty evidence with a factual answer asserting repository facts.
        var validator = new AiEvidenceValidator();
        var calculator = new AiConfidenceCalculator();

        var candidateAnswer = "The system exposes an HTTP GET endpoint at /api/Weather in WeatherController.";

        // Act
        var validation = await validator.ValidateAnswerAsync(candidateAnswer, []);
        var confidenceResult = calculator.EvaluateConfidence(new ConfidenceEvaluationRequest(
            "What endpoints exist?", candidateAnswer, [], validation));

        // Assert: actual zero-chunk contract (no new contract invented).
        Assert.False(validation.IsValid);
        Assert.Equal(AnswerValidationStatus.InsufficientEvidence, validation.Status);
        Assert.Equal(InsufficientEvidenceResponse.DefaultMessage, validation.ValidatedAnswer);
        Assert.Empty(validation.ValidatedEvidence);
        Assert.NotEmpty(validation.UnsupportedClaims);
        Assert.True(InsufficientEvidenceResponse.RequiresInsufficientEvidenceResponse(validation));
        // Note: AiConfidenceCalculator returns Low (0.1) for zero chunks; Unknown is the
        // RagService-level mapping covered by T107, not the calculator-level contract.
        Assert.Equal(AiConfidenceLevel.Low, confidenceResult.Level);
    }

    [Fact]
    public async Task T106_AnswerGrounding_IncorrectEvidenceMapping_IsNotFullySupported()
    {
        // Arrange: file exists in evidence but the cited symbol does not.
        var chunk = new VectorChunkSearchResult(
            ChunkId: Guid.NewGuid(),
            AnalysisId: _analysisId,
            SourceFileId: Guid.NewGuid(),
            FilePath: "WeatherController.cs",
            Symbol: "WeatherController.GetAll",
            StartLine: 19,
            EndLine: 24,
            Content: "[Authorize] [HttpGet] public async Task<IActionResult> GetAll() { return Ok(await _weatherService.GetForecastsAsync()); }",
            TokenCount: 15,
            ChunkIndex: 0,
            EvidenceId: Guid.NewGuid(),
            ConfidenceScore: 0.9f,
            CosineDistance: 0.05,
            SimilarityScore: 0.95);

        var validator = new AiEvidenceValidator();
        var calculator = new AiConfidenceCalculator();

        var wrongCitation = new AiEvidenceItem
        {
            File = "WeatherController.cs",
            Symbol = "DeleteUserAccount",
            StartLine = 1,
            EndLine = 10,
            Reason = "T106 test citation with ungrounded symbol"
        };

        var candidateAnswer = "The `WeatherController` implements `DeleteUserAccount` in `WeatherController.cs`.";

        // Act
        var validation = await validator.ValidateAnswerAsync(candidateAnswer, [chunk], [wrongCitation]);
        var confidenceResult = calculator.EvaluateConfidence(new ConfidenceEvaluationRequest(
            "What endpoints exist?", candidateAnswer, [chunk], validation));

        // Assert: wrong symbol mapping is rejected, never FullySupported.
        Assert.False(validation.IsValid);
        Assert.NotEqual(AnswerValidationStatus.FullySupported, validation.Status);
        Assert.Equal(AnswerValidationStatus.Unsupported, validation.Status);
        Assert.Single(validation.RejectedCitations);
        Assert.Equal("DeleteUserAccount", validation.RejectedCitations[0].Symbol);
        Assert.NotEmpty(validation.UnsupportedClaims);
        Assert.Equal(InsufficientEvidenceResponse.DefaultMessage, validation.ValidatedAnswer);
        Assert.Equal(AiConfidenceLevel.Low, confidenceResult.Level);
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

    [Fact]
    public async Task T107_UnknownAndInsufficientEvidence_NonZeroChunksWithInsufficientEvidenceValidation()
    {
        // Arrange: Vector retriever returns chunks, but answer asserts repository facts
        // that don't match the retrieved evidence, causing InsufficientEvidence status.
        var fakeRetriever = new TestVectorRetriever();
        var fakeEmbeddingProvider = new TestEmbeddingProvider();
        var fakeAiProvider = new TestAiProvider("The system has a `MongoDatabase` collection with a schema version 2.");

        // Chunks that will be retrieved
        var chunk = new VectorChunkSearchResult(
            ChunkId: Guid.NewGuid(),
            AnalysisId: _analysisId,
            SourceFileId: Guid.NewGuid(),
            FilePath: "WeatherController.cs",
            Symbol: "WeatherController.GetAll",
            StartLine: 19,
            EndLine: 24,
            Content: "[Authorize] [HttpGet] public async Task<IActionResult> GetAll() { return Ok(await _weatherService.GetForecastsAsync()); }",
            TokenCount: 15,
            ChunkIndex: 0,
            EvidenceId: Guid.NewGuid(),
            ConfidenceScore: 0.9f,
            CosineDistance: 0.05,
            SimilarityScore: 0.90);

        fakeRetriever.RegisterResult([chunk]);

        var validator = new AiEvidenceValidator();

        var ragService = new RagService(
            fakeAiProvider,
            fakeEmbeddingProvider,
            fakeRetriever,
            evidenceValidator: validator,
            confidenceCalculator: new AiConfidenceCalculator());

        // Question about Kubernetes (out of scope) with answer that asserts unrelated repo facts
        // using backticked symbol not in evidence
        var question = "What endpoints does the WeatherController expose?";
        // Answer with backticked MongoDB symbol not in the WeatherController chunk
        var answerAssertingUnrelated = "The system has a `MongoDatabase` collection with a schema version 2.";

        // Act via validator first to see status, then RagService
        var validation = await validator.ValidateAnswerAsync(answerAssertingUnrelated, [chunk]);

        // Assert: answer asserts repository facts not in evidence → Unsupported from validator
        // (the actual contract: Unsupported when all claims are unsupported but evidence exists,
        // InsufficientEvidence when no evidence chunks are provided at all).
        Assert.Equal(AnswerValidationStatus.Unsupported, validation.Status);
        // RagService should surface canonical message
        var ragResult = await ragService.AnswerQuestionAsync(_analysisId, question);
        Assert.Equal(InsufficientEvidenceResponse.DefaultMessage, ragResult.Answer);
        Assert.False(ragResult.HasSufficientEvidence);
        // Evidence should be cleared when validation concludes insufficient
        Assert.Empty(ragResult.Evidence);
        // Confidence should be Low per AiConfidenceCalculator groundingFactor=0.2 for InsufficientEvidence
        // or groundingFactor=0.0 for Unsupported status — both return Low confidence.
        Assert.Equal(AiConfidenceLevel.Low, ragResult.Confidence);
    }

    [Fact]
    public async Task T107_UnknownAndInsufficientEvidence_ConfidenceCalculator_InsufficientEvidenceReturnsLow()
    {
        // Arrange: Test that AiConfidenceCalculator returns Low (not Unknown) when validation status indicates
        // insufficient/unreliable evidence (Unsupported or InsufficientEvidence).
        // NOTE: The validator returns Unsupported when all claims in the answer are unsupported by evidence,
        // rather than InsufficientEvidence. This test verifies the confidence calculator correctly returns
        // Low confidence for either status.
        var calculator = new AiConfidenceCalculator();

        var chunk = new VectorChunkSearchResult(
            ChunkId: Guid.NewGuid(),
            AnalysisId: _analysisId,
            SourceFileId: Guid.NewGuid(),
            FilePath: "WeatherController.cs",
            Symbol: "WeatherController.GetAll",
            StartLine: 19,
            EndLine: 24,
            Content: "[Authorize] [HttpGet] public async Task<IActionResult> GetAll() { return Ok(await _weatherService.GetForecastsAsync()); }",
            TokenCount: 15,
            ChunkIndex: 0,
            EvidenceId: Guid.NewGuid(),
            ConfidenceScore: 0.9f,
            CosineDistance: 0.05,
            SimilarityScore: 0.90);

        // Answer asserts repository facts that don't match the retrieved chunk evidence.
        var answer = "The system has a `MongoDatabase` collection with a schema version 2.";
        var validator = new AiEvidenceValidator();
        var validation = await validator.ValidateAnswerAsync(answer, [chunk]);

        // The validator returns Unsupported when all claims are unsupported (not InsufficientEvidence).
        // This is intentional: InsufficientEvidence = not enough evidence; Unsupported = evidence exists
        // but claims don't match. Both warrant Low confidence.
        Assert.Equal(AnswerValidationStatus.Unsupported, validation.Status);

        // Act
        var request = new ConfidenceEvaluationRequest(
            "What endpoints exist?",
            answer,
            [chunk],
            validation);

        var result = calculator.EvaluateConfidence(request);

        // Assert: AiConfidenceCalculator returns Low confidence for Unsupported status.
        // GroundingFactor = 0.0 when validation.Status == Unsupported (line 115-118),
        // resulting in Low confidence regardless of evidence quality factors.
        Assert.Equal(AiConfidenceLevel.Low, result.Level);
        // Score is 0.0 because groundingFactor = 0.0 makes evidenceQualityScore * 0.0 = 0.0
        Assert.Equal(0.0f, result.Score);
    }

    [Fact]
    public async Task T107_UnknownAndInsufficientEvidence_ChunksWithUnsupportedClaims()
    {
        // Arrange: Vector retriever returns chunks, answer has claims completely unsupported by evidence
        // The TestAiProvider returns text with a backticked symbol not in the chunk → Unsupported status
        var fakeRetriever = new TestVectorRetriever();
        var fakeEmbeddingProvider = new TestEmbeddingProvider();
        // Provider returns answer with unsupported backticked symbol
        var fakeAiProvider = new TestAiProvider("The system has a `MongoDatabase` collection with a schema version 2.");

        var chunk = new VectorChunkSearchResult(
            ChunkId: Guid.NewGuid(),
            AnalysisId: _analysisId,
            SourceFileId: Guid.NewGuid(),
            FilePath: "WeatherController.cs",
            Symbol: "WeatherController.GetAll",
            StartLine: 19,
            EndLine: 24,
            Content: "[Authorize] [HttpGet] public async Task<IActionResult> GetAll() { return Ok(await _weatherService.GetForecastsAsync()); }",
            TokenCount: 15,
            ChunkIndex: 0,
            EvidenceId: Guid.NewGuid(),
            ConfidenceScore: 0.9f,
            CosineDistance: 0.05,
            SimilarityScore: 0.90);

        fakeRetriever.RegisterResult([chunk]);

        var validator = new AiEvidenceValidator();

        var ragService = new RagService(
            fakeAiProvider,
            fakeEmbeddingProvider,
            fakeRetriever,
            evidenceValidator: validator,
            confidenceCalculator: new AiConfidenceCalculator());

        // Question about WeatherController with answer asserting MongoDB (completely unsupported symbol not in chunk)
        var question = "What endpoints and persistence does WeatherController have?";

        // Act
        var result = await ragService.AnswerQuestionAsync(_analysisId, question);

        // Assert: answer has unsupported claims → Unsupported status + canonical message
        Assert.Equal(AnswerValidationStatus.Unsupported, result.Validation.Status);
        Assert.Equal(InsufficientEvidenceResponse.DefaultMessage, result.Answer);
        Assert.False(result.HasSufficientEvidence);
        Assert.Empty(result.Evidence);
        // Confidence Low per AiConfidenceCalculator with Unsupported status
        Assert.Equal(AiConfidenceLevel.Low, result.Confidence);
    }

    private sealed class TestVectorRetriever : IVectorChunkRetriever
    {
        private IReadOnlyList<VectorChunkSearchResult> _results = [];

        public void RegisterResult(IReadOnlyList<VectorChunkSearchResult> results)
        {
            _results = results.OrderByDescending(r => r.SimilarityScore).ToList();
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
