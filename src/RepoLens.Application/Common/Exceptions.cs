namespace RepoLens.Application.Common;

public class AnalysisNotFoundException : Exception
{
    public Guid AnalysisId { get; }

    public AnalysisNotFoundException(Guid analysisId)
        : base($"The requested analysis '{analysisId}' does not exist.")
    {
        AnalysisId = analysisId;
    }
}

public class AnalysisNotReadyException : Exception
{
    public string Status { get; }

    public AnalysisNotReadyException(string status)
        : base("The analysis has not completed yet.")
    {
        Status = status;
    }
}

public class AnalysisFailedException : Exception
{
    public string? Stage { get; }

    public AnalysisFailedException(string? stage)
        : base("The repository analysis failed.")
    {
        Stage = stage;
    }
}
