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
    General = 6,
    Declaration = 7,
    Invocation = 8,
    Configuration = 9,
    Dependency = 10,
    Route = 11,
    Database = 12
}
