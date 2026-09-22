using RepoLens.Domain.Enums;

namespace RepoLens.Application.DTOs.Persistence;

/// <summary>
/// Data transfer model for persisting a code symbol (T059).
/// </summary>
public record CodeSymbolPersistenceModel(
    Guid? Id,
    string? SymbolKey,
    string? FilePath,
    Guid? SourceFileId,
    string Name,
    string FullName,
    SymbolType SymbolType,
    int StartLine,
    int EndLine);
