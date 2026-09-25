using System.Diagnostics.CodeAnalysis;

namespace RepoLens.Application.Models.RAG;

/// <summary>
/// Request parameters for evidence-grounded RAG question answering (T087 / FR-009).
/// </summary>
public sealed record RagRequest
{
    /// <summary>
    /// The unique identifier of the analysis run, enforcing strict repository tenant isolation.
    /// </summary>
    public required Guid AnalysisId { get; init; }

    /// <summary>
    /// The user's natural language question regarding the analyzed repository.
    /// </summary>
    public required string Question { get; init; }

    /// <summary>
    /// Maximum number of semantically relevant document chunks to retrieve (default is 5).
    /// </summary>
    public int TopK { get; init; } = 5;

    public RagRequest() { }

    [SetsRequiredMembers]
    public RagRequest(Guid analysisId, string question, int topK = 5)
    {
        AnalysisId = analysisId;
        Question = question;
        TopK = topK;
    }
}
