namespace RepoLens.Domain.Enums;

/// <summary>
/// Type of code-level symbol identified by static analysis.
/// </summary>
public enum SymbolType
{
    Namespace = 1,
    Class = 2,
    Interface = 3,
    Struct = 4,
    Method = 5,
    Property = 6,
    Field = 7,
    Enum = 8
}
