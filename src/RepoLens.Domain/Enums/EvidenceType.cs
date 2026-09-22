namespace RepoLens.Domain.Enums;

/// <summary>
/// Categorization of source code evidence backing an architectural or domain finding.
/// </summary>
public enum EvidenceType
{
    ArchitectureEdge = 1,
    ProjectDependency = 2,
    ApiEndpoint = 3,
    DatabaseModel = 4,
    CodeSymbol = 5,
    General = 6
}
