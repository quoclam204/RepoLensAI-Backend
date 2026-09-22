namespace RepoLens.Domain.Exceptions;

public class InvalidConfidenceScoreException : DomainException
{
    public float Value { get; }

    public InvalidConfidenceScoreException(float value)
        : base($"Confidence score must be between 0.0 and 1.0. Provided: {value}.")
    {
        Value = value;
    }
}
