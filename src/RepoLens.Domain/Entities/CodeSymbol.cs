using RepoLens.Domain.Enums;

namespace RepoLens.Domain.Entities;

/// <summary>
/// Represents a code-level symbol (class, method, interface, property, etc.) extracted by static analysis (T019).
/// </summary>
public class CodeSymbol
{
    public Guid Id { get; set; }

    public Guid SourceFileId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public SymbolType SymbolType { get; set; }

    public int StartLine { get; set; }

    public int EndLine { get; set; }

    #region Navigation Properties

    public SourceFile SourceFile { get; set; } = null!;

    #endregion
}
