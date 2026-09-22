namespace RepoLens.Domain.Exceptions;

public class InvalidEvidenceRangeException : DomainException
{
    public int StartLine { get; }
    public int EndLine { get; }

    public InvalidEvidenceRangeException(int startLine, int endLine, string message)
        : base(message)
    {
        StartLine = startLine;
        EndLine = endLine;
    }

    public static InvalidEvidenceRangeException ForNegativeOrZeroStartLine(int startLine) =>
        new(startLine, 0, $"Start line must be greater than or equal to 1. Provided: {startLine}.");

    public static InvalidEvidenceRangeException ForEndLineBeforeStartLine(int startLine, int endLine) =>
        new(startLine, endLine, $"End line ({endLine}) must be greater than or equal to start line ({startLine}).");
}
