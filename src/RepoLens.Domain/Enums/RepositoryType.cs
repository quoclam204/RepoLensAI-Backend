namespace RepoLens.Domain.Enums;

/// <summary>
/// Classification of repository type determined by static analysis evidence.
/// </summary>
public enum RepositoryType
{
    /// <summary>Backend API project (e.g., ASP.NET Core Web API).</summary>
    ApiBackend = 1,

    /// <summary>Frontend application (e.g., Next.js, React SPA).</summary>
    Frontend = 2,

    /// <summary>Monorepo or full-stack project containing multiple sub-projects.</summary>
    Monorepo = 3,

    /// <summary>Reusable library (no entry point, only public classes/interfaces).</summary>
    Library = 4,

    /// <summary>Console or CLI application (has entry point but no API controllers).</summary>
    Cli = 5,

    /// <summary>Unsupported language or unable to classify.</summary>
    Unsupported = 6
}
