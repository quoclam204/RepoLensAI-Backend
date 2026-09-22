namespace RepoLens.Application.DTOs.Persistence;

/// <summary>
/// Data transfer model for persisting an API endpoint (T059).
/// </summary>
public record ApiEndpointPersistenceModel(
    Guid? Id,
    string? ProjectPath,
    Guid? ProjectId,
    string Method,
    string Route,
    string? Controller,
    string? Action,
    string? SymbolKey,
    string? SymbolFullName,
    Guid? SymbolId,
    string? EvidenceKey,
    Guid? EvidenceId);
