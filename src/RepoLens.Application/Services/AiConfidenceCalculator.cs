using System.Globalization;
using RepoLens.Application.Abstractions.AI;
using RepoLens.Application.Abstractions.AI.Models;
using RepoLens.Application.Models.RAG;

namespace RepoLens.Application.Services;

/// <summary>
/// Configuration thresholds and weights for evidence-based confidence evaluation (T089).
/// </summary>
public sealed record ConfidenceCalculatorOptions
{
    public static ConfidenceCalculatorOptions Default { get; } = new();

    public float HighThreshold { get; init; } = 0.75f;
    public float MediumThreshold { get; init; } = 0.45f;

    public float StaticEvidenceWeight { get; init; } = 0.40f;
    public float RetrievalSimilarityWeight { get; init; } = 0.40f;
    public float CoverageWeight { get; init; } = 0.20f;
}

/// <summary>
/// Service implementing deterministic, explainable AI confidence evaluation grounded in repository evidence (T089 / NFR-AI-001).
/// Enforces that confidence reflects actual evidence grounding, retrieval metrics, static analysis confidence,
/// and answer validation outcomes rather than arbitrary model assertions.
/// </summary>
public class AiConfidenceCalculator : IAiConfidenceCalculator
{
    private readonly ConfidenceCalculatorOptions _options;

    public AiConfidenceCalculator(ConfidenceCalculatorOptions? options = null)
    {
        _options = options ?? ConfidenceCalculatorOptions.Default;
    }

    /// <inheritdoc />
    public Task<ConfidenceEvaluationResult> EvaluateConfidenceAsync(
        ConfidenceEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(EvaluateConfidence(request));
    }

    /// <inheritdoc />
    public ConfidenceEvaluationResult EvaluateConfidence(ConfidenceEvaluationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Indeterminate / Unknown cases (e.g. null/whitespace question and answer)
        if (string.IsNullOrWhiteSpace(request.Question) || string.IsNullOrWhiteSpace(request.Answer))
        {
            return new ConfidenceEvaluationResult(
                Level: AiConfidenceLevel.Unknown,
                Score: 0.0f,
                Rationale: "Cannot evaluate confidence: question or answer text is empty or indeterminate.",
                Factors: new ConfidenceFactorBreakdown(
                    GroundingFactor: 0.0f,
                    RetrievalSimilarityFactor: 0.0f,
                    StaticEvidenceFactor: 0.0f,
                    CoverageFactor: 0.0f));
        }

        var chunks = request.RetrievedChunks ?? [];
        var validation = request.ValidationResult;

        // 2. Zero evidence chunks retrieved
        if (chunks.Count == 0)
        {
            var isAdmittingNoEvidence =
                InsufficientEvidenceResponse.ContainsInsufficientEvidenceAcknowledgment(request.Answer);

            var noEvidenceRationale = isAdmittingNoEvidence
                ? "Low confidence: No repository evidence chunks were retrieved; answer acknowledges lack of evidence."
                : "Low confidence: No repository evidence chunks were retrieved to support the asserted statements.";

            return new ConfidenceEvaluationResult(
                Level: AiConfidenceLevel.Low,
                Score: 0.1f,
                Rationale: noEvidenceRationale,
                Factors: new ConfidenceFactorBreakdown(
                    GroundingFactor: isAdmittingNoEvidence ? 1.0f : 0.0f,
                    RetrievalSimilarityFactor: 0.0f,
                    StaticEvidenceFactor: 0.0f,
                    CoverageFactor: 0.0f));
        }

        // 3. Compute factor scores
        // A. Static Analysis Confidence Factor
        var staticFactor = (float)Math.Clamp(chunks.Average(c => c.ConfidenceScore), 0.0, 1.0);

        // B. Retrieval Semantic Similarity Factor
        var similarityFactor = (float)Math.Clamp(chunks.Average(c => c.SimilarityScore), 0.0, 1.0);

        // C. Evidence Coverage Factor (rewards having multiple distinct supporting chunks)
        var coverageFactor = chunks.Count switch
        {
            >= 3 => 1.0f,
            2 => 0.9f,
            1 => 0.8f,
            _ => 0.0f
        };

        // Combine evidence quality factors
        var evidenceQualityScore =
            (_options.StaticEvidenceWeight * staticFactor) +
            (_options.RetrievalSimilarityWeight * similarityFactor) +
            (_options.CoverageWeight * coverageFactor);

        // D. Grounding & Validation Factor
        float groundingFactor = 1.0f;
        if (validation != null)
        {
            if (validation.Status == AnswerValidationStatus.Unsupported)
            {
                groundingFactor = 0.0f;
            }
            else if (validation.Status == AnswerValidationStatus.PartiallySupported)
            {
                var totalClaims = validation.ClaimDetails.Count;
                var supportedClaims = validation.ClaimDetails.Count(c => c.IsSupported);
                var claimRatio = totalClaims > 0 ? (float)supportedClaims / totalClaims : 0.5f;

                // Grounding factor scales based on supported claims ratio and citation validity
                var citationPenalty = validation.RejectedCitations.Count > 0 ? 0.9f : 1.0f;
                groundingFactor = (0.5f + (0.4f * claimRatio)) * citationPenalty;
            }
            else if (validation.Status == AnswerValidationStatus.FullySupported)
            {
                groundingFactor = validation.RejectedCitations.Count > 0 ? 0.85f : 1.0f;
            }
            else if (validation.Status == AnswerValidationStatus.InsufficientEvidence)
            {
                groundingFactor = 0.2f;
            }
        }

        // Final composite score
        var finalScore = (float)Math.Clamp(evidenceQualityScore * groundingFactor, 0.0, 1.0);

        // 4. Map composite score to AiConfidenceLevel with safety constraints
        AiConfidenceLevel level;
        string rationale;

        if (validation != null && validation.Status == AnswerValidationStatus.Unsupported)
        {
            level = AiConfidenceLevel.Low;
            rationale = $"Low confidence ({finalScore:P0}): Answer contains statements completely unsupported by retrieved repository evidence.";
        }
        else if (validation != null && (validation.Status == AnswerValidationStatus.PartiallySupported || validation.RejectedCitations.Count > 0))
        {
            // Partially supported answers or answers with rejected citations cannot be High confidence (capped at Medium)
            finalScore = Math.Min(finalScore, _options.HighThreshold - 0.05f);

            if (finalScore >= _options.MediumThreshold)
            {
                level = AiConfidenceLevel.Medium;
                rationale = string.Format(
                    CultureInfo.InvariantCulture,
                    "Medium confidence ({0:P0}): Answer is partially supported by {1} evidence chunks, but contains ungrounded claims or rejected citations.",
                    finalScore,
                    chunks.Count);
            }
            else
            {
                level = AiConfidenceLevel.Low;
                rationale = string.Format(
                    CultureInfo.InvariantCulture,
                    "Low confidence ({0:P0}): Multiple claims in the answer are unsupported by retrieved evidence.",
                    finalScore);
            }
        }
        else if (finalScore >= _options.HighThreshold)
        {
            level = AiConfidenceLevel.High;
            rationale = string.Format(
                CultureInfo.InvariantCulture,
                "High confidence ({0:P0}): All claims are fully grounded in {1} repository evidence chunks with high static confidence ({2:F2}) and strong semantic similarity ({3:P0}).",
                finalScore,
                chunks.Count,
                staticFactor,
                similarityFactor);
        }
        else if (finalScore >= _options.MediumThreshold)
        {
            level = AiConfidenceLevel.Medium;
            rationale = string.Format(
                CultureInfo.InvariantCulture,
                "Medium confidence ({0:P0}): Answer is supported by {1} evidence chunks with moderate retrieval similarity ({2:P0}) and static confidence ({3:F2}).",
                finalScore,
                chunks.Count,
                similarityFactor,
                staticFactor);
        }
        else
        {
            level = AiConfidenceLevel.Low;
            rationale = string.Format(
                CultureInfo.InvariantCulture,
                "Low confidence ({0:P0}): Evidence grounding is weak (similarity: {1:P0}, static confidence: {2:F2}).",
                finalScore,
                similarityFactor,
                staticFactor);
        }

        var factors = new ConfidenceFactorBreakdown(
            GroundingFactor: groundingFactor,
            RetrievalSimilarityFactor: similarityFactor,
            StaticEvidenceFactor: staticFactor,
            CoverageFactor: coverageFactor);

        return new ConfidenceEvaluationResult(
            Level: level,
            Score: finalScore,
            Rationale: rationale,
            Factors: factors);
    }
}
