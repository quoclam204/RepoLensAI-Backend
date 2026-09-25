using RepoLens.Application.Abstractions;
using RepoLens.Application.Abstractions.AI;
using RepoLens.Application.Abstractions.AI.Models;
using RepoLens.Application.Models.RAG;

namespace RepoLens.Application.Services;

/// <summary>
/// Configuration options for the RAG service.
/// </summary>
public sealed record RagServiceOptions
{
    public static RagServiceOptions Default { get; } = new();

    /// <summary>
    /// Required dimension of query embedding vector (must match text-embedding-3-small and pgvector column).
    /// </summary>
    public int ExpectedEmbeddingDimension { get; init; } = 1536;

    /// <summary>
    /// Default top-K chunks to retrieve.
    /// </summary>
    public int DefaultTopK { get; init; } = 5;

    /// <summary>
    /// Maximum allowed top-K chunks.
    /// </summary>
    public int MaxTopK { get; init; } = 100;
}

/// <summary>
/// Application service implementing Evidence-Grounded RAG (T087 / FR-009 / NFR-AI-001).
/// Orchestrates the pipeline:
/// Question -> Embed -> Retrieve evidence chunks -> Build grounded context -> Call AI provider -> Return traceable response.
/// </summary>
public class RagService : IRagService, IEvidenceGroundedRagService
{
    private readonly IAiProvider _aiProvider;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IVectorChunkRetriever _vectorRetriever;
    private readonly RagServiceOptions _options;

    public RagService(
        IAiProvider aiProvider,
        IEmbeddingProvider embeddingProvider,
        IVectorChunkRetriever vectorRetriever,
        RagServiceOptions? options = null)
    {
        _aiProvider = aiProvider ?? throw new ArgumentNullException(nameof(aiProvider));
        _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        _vectorRetriever = vectorRetriever ?? throw new ArgumentNullException(nameof(vectorRetriever));
        _options = options ?? RagServiceOptions.Default;
    }

    /// <inheritdoc />
    public Task<RagResult> AnswerQuestionAsync(
        Guid analysisId,
        string question,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var request = new RagRequest(analysisId, question, topK);
        return AnswerQuestionAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RagResult> AnswerQuestionAsync(
        RagRequest request,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate inputs
        ArgumentNullException.ThrowIfNull(request);

        if (request.AnalysisId == Guid.Empty)
        {
            throw new ArgumentException("AnalysisId cannot be empty.", nameof(request));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(request.Question, nameof(request));

        if (request.TopK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "TopK must be greater than zero.");
        }

        if (request.TopK > _options.MaxTopK)
        {
            throw new ArgumentOutOfRangeException(nameof(request), $"TopK cannot exceed {_options.MaxTopK}.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 2. Generate query embedding
        var embeddings = await _embeddingProvider.EmbedAsync([request.Question], cancellationToken);
        if (embeddings == null || embeddings.Count == 0 || embeddings[0] == null)
        {
            throw new InvalidOperationException("Embedding provider failed to generate an embedding for the query.");
        }

        var queryVector = embeddings[0];
        if (queryVector.Length != _options.ExpectedEmbeddingDimension)
        {
            throw new InvalidOperationException(
                $"Query embedding dimension mismatch: expected {_options.ExpectedEmbeddingDimension}, but received {queryVector.Length}.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 3. Retrieve relevant chunks via IVectorChunkRetriever (strictly scoped to AnalysisId)
        var retrievedChunks = await _vectorRetriever.RetrieveSimilarChunksAsync(
            request.AnalysisId,
            queryVector,
            request.TopK,
            cancellationToken);

        retrievedChunks ??= [];

        cancellationToken.ThrowIfCancellationRequested();

        // 4. Build context
        var context = RagPromptBuilder.BuildContext(retrievedChunks, request.AnalysisId);

        // 5. Build AI request
        var aiRequest = new AiRequest
        {
            Prompt = request.Question,
            SystemPrompt = RagPromptBuilder.DefaultSystemPrompt,
            Context = context
        };

        // 6. Call AI provider for generation
        var aiResponse = await _aiProvider.GenerateAsync(aiRequest, cancellationToken);
        if (aiResponse == null)
        {
            throw new InvalidOperationException("AI provider returned a null response.");
        }

        // 7. Evidence validation & traceability
        var evidenceItems = new List<AiEvidenceItem>(retrievedChunks.Count);
        foreach (var chunk in retrievedChunks)
        {
            evidenceItems.Add(new AiEvidenceItem
            {
                File = chunk.FilePath,
                Symbol = chunk.Symbol,
                StartLine = chunk.StartLine,
                EndLine = chunk.EndLine,
                Reason = $"Retrieved chunk {chunk.ChunkIndex} (similarity: {chunk.SimilarityScore:P0}, confidence: {chunk.ConfidenceScore:F2})"
            });
        }

        // Incorporate any citations returned by AI provider not already captured
        if (aiResponse.Evidence is { Count: > 0 })
        {
            foreach (var aiItem in aiResponse.Evidence)
            {
                if (!evidenceItems.Any(e => e.File == aiItem.File && e.StartLine == aiItem.StartLine && e.EndLine == aiItem.EndLine))
                {
                    evidenceItems.Add(aiItem);
                }
            }
        }

        // Determine confidence
        var hasSufficientEvidence = retrievedChunks.Count > 0;
        var confidence = aiResponse.Confidence;
        if (confidence == AiConfidenceLevel.Unknown)
        {
            if (!hasSufficientEvidence)
            {
                confidence = AiConfidenceLevel.Low;
            }
            else
            {
                var avgConfidence = retrievedChunks.Average(c => c.ConfidenceScore);
                confidence = avgConfidence switch
                {
                    >= 0.8f => AiConfidenceLevel.High,
                    >= 0.5f => AiConfidenceLevel.Medium,
                    _ => AiConfidenceLevel.Low
                };
            }
        }

        var answer = aiResponse.Answer;
        if (string.IsNullOrWhiteSpace(answer) && !hasSufficientEvidence)
        {
            answer = "Insufficient evidence in the analyzed repository to answer this question.";
        }

        return new RagResult(
            Question: request.Question,
            Answer: answer,
            RetrievedChunks: retrievedChunks,
            Evidence: evidenceItems.AsReadOnly(),
            Confidence: confidence,
            HasSufficientEvidence: hasSufficientEvidence,
            ContextPrompt: context);
    }
}
