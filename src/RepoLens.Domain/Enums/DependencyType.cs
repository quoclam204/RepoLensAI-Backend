namespace RepoLens.Domain.Enums;

/// <summary>
/// Type of dependency relationship between projects, files, or symbols.
/// </summary>
public enum DependencyType
{
    ProjectReference = 1,
    PackageReference = 2,
    Inherits = 3,
    Implements = 4,
    Calls = 5,
    DependsOn = 6,
    Contains = 7,
    Defines = 8,
    Exposes = 9,
    MapsTo = 10,
    Reads = 11,
    Writes = 12
}
