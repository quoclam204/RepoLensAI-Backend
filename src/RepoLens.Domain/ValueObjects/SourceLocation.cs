using RepoLens.Domain.Exceptions;

namespace RepoLens.Domain.ValueObjects;

public readonly record struct SourceLocation
{
    public string FilePath { get; }
    public int StartLine { get; }
    public int EndLine { get; }

    public SourceLocation(string filePath, int startLine, int endLine)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new DomainException("File path cannot be null, empty, or whitespace.");
        }

        if (startLine < 1)
        {
            throw InvalidEvidenceRangeException.ForNegativeOrZeroStartLine(startLine);
        }

        if (endLine < startLine)
        {
            throw InvalidEvidenceRangeException.ForEndLineBeforeStartLine(startLine, endLine);
        }

        FilePath = filePath.Replace('\\', '/').Trim();
        StartLine = startLine;
        EndLine = endLine;
    }

    public override string ToString() => $"{FilePath}:{StartLine}-{EndLine}";
}
