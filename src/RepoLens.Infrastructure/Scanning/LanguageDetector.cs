namespace RepoLens.Infrastructure.Scanning;

/// <summary>
/// Detects programming and markup languages for discovered files (T035, FR-003).
/// In MVP, primary analysis languages are C#, TypeScript, and JavaScript.
/// </summary>
public sealed class LanguageDetector
{
    private static readonly Dictionary<string, string> ExtensionToLanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // C#
        [".cs"] = "C#",

        // TypeScript
        [".ts"] = "TypeScript",
        [".tsx"] = "TypeScript",
        [".mts"] = "TypeScript",
        [".cts"] = "TypeScript",

        // JavaScript
        [".js"] = "JavaScript",
        [".jsx"] = "JavaScript",
        [".mjs"] = "JavaScript",
        [".cjs"] = "JavaScript",

        // Configuration & Data
        [".json"] = "JSON",
        [".xml"] = "XML",
        [".csproj"] = "XML",
        [".props"] = "XML",
        [".targets"] = "XML",
        [".yaml"] = "YAML",
        [".yml"] = "YAML",
        [".toml"] = "TOML",

        // Web
        [".html"] = "HTML",
        [".htm"] = "HTML",
        [".css"] = "CSS",
        [".scss"] = "SCSS",
        [".less"] = "Less",

        // Database
        [".sql"] = "SQL",

        // Documentation
        [".md"] = "Markdown",
        [".txt"] = "Text",

        // Shell
        [".sh"] = "Shell",
        [".bash"] = "Shell",
        [".ps1"] = "PowerShell",

        // Other common languages
        [".py"] = "Python",
        [".go"] = "Go",
        [".java"] = "Java",
        [".rs"] = "Rust",
        [".cpp"] = "C++",
        [".c"] = "C"
    };

    /// <summary>
    /// Detects the language of a file given its filename or path.
    /// </summary>
    /// <param name="filePath">Relative or absolute file path.</param>
    /// <returns>Detected language name, or "Unknown" if not recognized.</returns>
    public string DetectLanguage(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "Unknown";
        }

        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(extension))
        {
            // Check specific well-known extensionless files
            var fileName = Path.GetFileName(filePath);
            if (string.Equals(fileName, "Dockerfile", StringComparison.OrdinalIgnoreCase))
            {
                return "Dockerfile";
            }

            return "Unknown";
        }

        if (ExtensionToLanguageMap.TryGetValue(extension, out var language))
        {
            return language;
        }

        return "Unknown";
    }
}
