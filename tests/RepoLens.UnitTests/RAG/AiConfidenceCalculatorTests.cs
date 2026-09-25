using RepoLens.Application.Abstractions.AI.Models;
using RepoLens.Application.Models.RAG;
using RepoLens.Application.Services;

namespace RepoLens.UnitTests.RAG;

public class AiConfidenceCalculatorTests
{
    private static VectorChunkSearchResult MakeChunk(
        string filePath = "src/Services/AuthService.cs",
        string? symbol = "AuthService.Authenticate",
        int startLine = 10,
        int endLine = 30,
        float confidence = 0.95f,
        double similarity = 0.90)
    {
        return new VectorChunkSearchResult(
            ChunkId: Guid.NewGuid(),
            AnalysisId: Guid.NewGuid(),
            SourceFileId: Guid.NewGuid(),
            FilePath: filePath,
            Symbol: symbol,
            StartLine: startLine,
            EndLine: endLine,
            Content: "public bool Authenticate(string user, string pass) => true;",
            TokenCount: 15,
            ChunkIndex: 0,
            EvidenceId: Guid.NewGuid(),
            ConfidenceScore: confidence,
            CosineDistance: 1.0 - similarity,
            SimilarityScore: similarity);
    }

    [Fact]
    public void EvaluateConfidence_HighQualityEvidenceAndFullySupported_ReturnsHighConfidence()
    {
        // 1. High confidence: Answer claims fully grounded in multiple high-confidence chunks with high semantic similarity (>0.85)
        var calculator = new AiConfidenceCalculator();

        var chunks = new List<VectorChunkSearchResult>
        {
            MakeChunk("src/Auth/TokenService.cs", "TokenService", 1, 50, confidence: 0.95f, similarity: 0.92),
            MakeChunk("src/Auth/IAuthService.cs", "IAuthService", 1, 30, confidence: 0.90f, similarity: 0.88),
            MakeChunk("src/Controllers/AuthController.cs", "AuthController", 1, 60, confidence: 0.95f, similarity: 0.89)
        };

        var validationResult = new AnswerValidationResult(
            IsValid: true,
            Status: AnswerValidationStatus.FullySupported,
            ValidatedAnswer: "Auth tokens are issued by `TokenService.cs`.",
            ValidatedEvidence:
            [
                new AiEvidenceItem { File = "src/Auth/TokenService.cs", Symbol = "TokenService" }
            ],
            ClaimDetails:
            [
                new ClaimValidationItem("Auth tokens are issued by `TokenService.cs`.", true, "src/Auth/TokenService.cs", "TokenService", "Fully supported")
            ],
            UnsupportedClaims: [],
            RejectedCitations: []);

        var request = new ConfidenceEvaluationRequest(
            Question: "How are auth tokens generated?",
            Answer: "Auth tokens are issued by `TokenService.cs`.",
            RetrievedChunks: chunks,
            ValidationResult: validationResult);

        var result = calculator.EvaluateConfidence(request);

        Assert.Equal(AiConfidenceLevel.High, result.Level);
        Assert.True(result.Score >= 0.75f, $"Expected score >= 0.75 but got {result.Score}");
        Assert.Contains("High confidence", result.Rationale);
        Assert.True(result.Factors.GroundingFactor >= 0.99f);
        Assert.True(result.Factors.StaticEvidenceFactor >= 0.90f);
        Assert.True(result.Factors.RetrievalSimilarityFactor >= 0.85f);
        Assert.Equal(1.0f, result.Factors.CoverageFactor);
    }

    [Fact]
    public void EvaluateConfidence_ModerateSimilarityAndConfidence_ReturnsMediumConfidence()
    {
        // 2. Medium confidence: Moderate similarity or moderate chunk confidence (0.60), all grounded -> Score in [0.45, 0.75), Level = Medium
        var calculator = new AiConfidenceCalculator();

        var chunks = new List<VectorChunkSearchResult>
        {
            MakeChunk("src/Common/Helper.cs", "Helper", 1, 20, confidence: 0.60f, similarity: 0.60),
            MakeChunk("src/Common/Utils.cs", "Utils", 1, 25, confidence: 0.55f, similarity: 0.62)
        };

        var validationResult = new AnswerValidationResult(
            IsValid: true,
            Status: AnswerValidationStatus.FullySupported,
            ValidatedAnswer: "Helper utilities are located in `Helper.cs`.",
            ValidatedEvidence: [],
            ClaimDetails:
            [
                new ClaimValidationItem("Helper utilities are located in `Helper.cs`.", true, "src/Common/Helper.cs", null, "Supported")
            ],
            UnsupportedClaims: [],
            RejectedCitations: []);

        var request = new ConfidenceEvaluationRequest(
            Question: "Where are common utilities?",
            Answer: "Helper utilities are located in `Helper.cs`.",
            RetrievedChunks: chunks,
            ValidationResult: validationResult);

        var result = calculator.EvaluateConfidence(request);

        Assert.Equal(AiConfidenceLevel.Medium, result.Level);
        Assert.InRange(result.Score, 0.45f, 0.7499f);
        Assert.Contains("Medium confidence", result.Rationale);
    }

    [Fact]
    public void EvaluateConfidence_WeakEvidenceMetrics_ReturnsLowConfidence()
    {
        // 3. Low confidence: Low similarity / weak static evidence (<0.40) -> Score < 0.45, Level = Low
        var calculator = new AiConfidenceCalculator();

        var chunks = new List<VectorChunkSearchResult>
        {
            MakeChunk("src/Legacy/OldCode.cs", "OldCode", 1, 10, confidence: 0.30f, similarity: 0.35)
        };

        var request = new ConfidenceEvaluationRequest(
            Question: "How does legacy billing work?",
            Answer: "Legacy billing might be in OldCode.cs.",
            RetrievedChunks: chunks);

        var result = calculator.EvaluateConfidence(request);

        Assert.Equal(AiConfidenceLevel.Low, result.Level);
        Assert.True(result.Score < 0.45f, $"Expected score < 0.45 but got {result.Score}");
        Assert.Contains("Low confidence", result.Rationale);
    }

    [Fact]
    public void EvaluateConfidence_UnsupportedClaims_CappedAtLowConfidence()
    {
        // 4. Unsupported claims override: Answer has claims marked unsupported by T088 validation -> Confidence capped at Low
        var calculator = new AiConfidenceCalculator();

        // Chunks have high similarity and confidence, but claims are hallucinated/unsupported
        var chunks = new List<VectorChunkSearchResult>
        {
            MakeChunk("src/Auth/TokenService.cs", "TokenService", 1, 50, confidence: 0.99f, similarity: 0.95),
            MakeChunk("src/Auth/IAuthService.cs", "IAuthService", 1, 50, confidence: 0.99f, similarity: 0.95),
            MakeChunk("src/Auth/Hasher.cs", "Hasher", 1, 50, confidence: 0.99f, similarity: 0.95)
        };

        var validationResult = new AnswerValidationResult(
            IsValid: false,
            Status: AnswerValidationStatus.Unsupported,
            ValidatedAnswer: "[Uncertain - unsupported by retrieved evidence] Payment processing happens in `StripeGateway.cs`.",
            ValidatedEvidence: [],
            ClaimDetails:
            [
                new ClaimValidationItem("Payment processing happens in `StripeGateway.cs`.", false, null, null, "Not found in chunks")
            ],
            UnsupportedClaims: ["Payment processing happens in `StripeGateway.cs`."],
            RejectedCitations:
            [
                new AiEvidenceItem { File = "StripeGateway.cs", Reason = "Not found in retrieved chunks" }
            ]);

        var request = new ConfidenceEvaluationRequest(
            Question: "Where is payment processed?",
            Answer: "Payment processing happens in `StripeGateway.cs`.",
            RetrievedChunks: chunks,
            ValidationResult: validationResult);

        var result = calculator.EvaluateConfidence(request);

        Assert.Equal(AiConfidenceLevel.Low, result.Level);
        Assert.Equal(0.0f, result.Score);
        Assert.Equal(0.0f, result.Factors.GroundingFactor);
        Assert.Contains("unsupported by retrieved repository evidence", result.Rationale);
    }

    [Fact]
    public void EvaluateConfidence_PartiallySupportedClaims_CappedAtMediumConfidenceEvenWithHighSimilarity()
    {
        // 5. Partially supported claims override: Answer has some unsupported claims or rejected citations -> Confidence capped at Medium
        var calculator = new AiConfidenceCalculator();

        var chunks = new List<VectorChunkSearchResult>
        {
            MakeChunk("src/Auth/TokenService.cs", "TokenService", 1, 50, confidence: 0.99f, similarity: 0.98),
            MakeChunk("src/Auth/IAuthService.cs", "IAuthService", 1, 50, confidence: 0.99f, similarity: 0.98),
            MakeChunk("src/Auth/Hasher.cs", "Hasher", 1, 50, confidence: 0.99f, similarity: 0.98)
        };

        var validationResult = new AnswerValidationResult(
            IsValid: false,
            Status: AnswerValidationStatus.PartiallySupported,
            ValidatedAnswer: "Auth is in `TokenService.cs`. [Uncertain] Billing is in `BillingService.cs`.",
            ValidatedEvidence:
            [
                new AiEvidenceItem { File = "src/Auth/TokenService.cs" }
            ],
            ClaimDetails:
            [
                new ClaimValidationItem("Auth is in `TokenService.cs`.", true, "src/Auth/TokenService.cs", "TokenService", "Supported"),
                new ClaimValidationItem("Billing is in `BillingService.cs`.", false, null, null, "Not found")
            ],
            UnsupportedClaims: ["Billing is in `BillingService.cs`."],
            RejectedCitations:
            [
                new AiEvidenceItem { File = "BillingService.cs" }
            ]);

        var request = new ConfidenceEvaluationRequest(
            Question: "How are auth and billing implemented?",
            Answer: "Auth is in `TokenService.cs`. Billing is in `BillingService.cs`.",
            RetrievedChunks: chunks,
            ValidationResult: validationResult);

        var result = calculator.EvaluateConfidence(request);

        // Cannot be High confidence even though chunks were 0.99!
        Assert.Equal(AiConfidenceLevel.Medium, result.Level);
        Assert.Contains("partially supported", result.Rationale, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EvaluateConfidence_ZeroRetrievedChunks_ReturnsLowConfidenceWithExplainableRationale()
    {
        // 6. Empty evidence chunks retrieved: Returns Low confidence with explicit rationale
        var calculator = new AiConfidenceCalculator();

        var request = new ConfidenceEvaluationRequest(
            Question: "What database does this repository use?",
            Answer: "Insufficient evidence in the analyzed repository to answer this question.",
            RetrievedChunks: []);

        var result = calculator.EvaluateConfidence(request);

        Assert.Equal(AiConfidenceLevel.Low, result.Level);
        Assert.Equal(0.1f, result.Score);
        Assert.Contains("No repository evidence chunks were retrieved", result.Rationale);
        Assert.Equal(0.0f, result.Factors.RetrievalSimilarityFactor);
        Assert.Equal(0.0f, result.Factors.StaticEvidenceFactor);
        Assert.Equal(0.0f, result.Factors.CoverageFactor);
    }

    [Theory]
    [InlineData("", "Valid answer")]
    [InlineData("Valid question", "")]
    [InlineData("   ", "   ")]
    public void EvaluateConfidence_EmptyQuestionOrAnswer_ReturnsUnknownConfidence(string question, string answer)
    {
        // 7. Empty question/answer: Returns Unknown confidence with score 0.0 and clear rationale
        var calculator = new AiConfidenceCalculator();

        var request = new ConfidenceEvaluationRequest(
            Question: question,
            Answer: answer,
            RetrievedChunks: [MakeChunk()]);

        var result = calculator.EvaluateConfidence(request);

        Assert.Equal(AiConfidenceLevel.Unknown, result.Level);
        Assert.Equal(0.0f, result.Score);
        Assert.Contains("empty or indeterminate", result.Rationale);
    }

    [Fact]
    public void EvaluateConfidence_DeterministicOutput_ProducesIdenticalResultForIdenticalInputs()
    {
        // 8. Deterministic calculation: Same inputs produce identical numeric score, level, and rationale across multiple calls
        var calculator = new AiConfidenceCalculator();

        var chunks = new List<VectorChunkSearchResult>
        {
            MakeChunk("src/API/Program.cs", "Main", 1, 20, 0.85f, 0.82),
            MakeChunk("src/API/Startup.cs", "ConfigureServices", 1, 40, 0.90f, 0.86)
        };

        var request = new ConfidenceEvaluationRequest(
            Question: "How is the app initialized?",
            Answer: "The app initializes services in `Program.cs` and `Startup.cs`.",
            RetrievedChunks: chunks);

        var result1 = calculator.EvaluateConfidence(request);
        var result2 = calculator.EvaluateConfidence(request);

        Assert.Equal(result1.Level, result2.Level);
        Assert.Equal(result1.Score, result2.Score);
        Assert.Equal(result1.Rationale, result2.Rationale);
        Assert.Equal(result1.Factors.GroundingFactor, result2.Factors.GroundingFactor);
        Assert.Equal(result1.Factors.RetrievalSimilarityFactor, result2.Factors.RetrievalSimilarityFactor);
        Assert.Equal(result1.Factors.StaticEvidenceFactor, result2.Factors.StaticEvidenceFactor);
        Assert.Equal(result1.Factors.CoverageFactor, result2.Factors.CoverageFactor);
    }

    [Fact]
    public void EvaluateConfidence_FactorBreakdown_AllFactorsPresentAndClampedBetweenZeroAndOne()
    {
        // 9. Factor breakdown accuracy: All 4 factors are present and in [0.0, 1.0]
        var calculator = new AiConfidenceCalculator();

        var chunks = new List<VectorChunkSearchResult>
        {
            MakeChunk("src/Models/User.cs", "User", 1, 15, 0.70f, 0.75)
        };

        var request = new ConfidenceEvaluationRequest(
            Question: "Where is User defined?",
            Answer: "User is in `User.cs`.",
            RetrievedChunks: chunks);

        var result = calculator.EvaluateConfidence(request);

        Assert.NotNull(result.Factors);
        Assert.InRange(result.Factors.GroundingFactor, 0.0f, 1.0f);
        Assert.InRange(result.Factors.RetrievalSimilarityFactor, 0.0f, 1.0f);
        Assert.InRange(result.Factors.StaticEvidenceFactor, 0.0f, 1.0f);
        Assert.InRange(result.Factors.CoverageFactor, 0.0f, 1.0f);
        Assert.InRange(result.Score, 0.0f, 1.0f);
    }

    [Fact]
    public async Task EvaluateConfidenceAsync_WhenCancellationTokenCanceled_ThrowsOperationCanceledException()
    {
        // 10. Async cancellation support: evaluate confidence async respects CancellationToken
        var calculator = new AiConfidenceCalculator();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var request = new ConfidenceEvaluationRequest(
            Question: "Valid question",
            Answer: "Valid answer",
            RetrievedChunks: [MakeChunk()]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            calculator.EvaluateConfidenceAsync(request, cts.Token));
    }
}
