using RepoLens.Analysis.Scanning;

namespace RepoLens.AnalysisTests.Scanning;

public class RepositoryScannerTests : IDisposable
{
    private readonly string _testDir;
    private readonly RepositoryScanner _scanner = new();

    public RepositoryScannerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "RepoLens_Scanner_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void Scan_WithRealisticDirectoryStructure_DiscoversProjectsAndSourceFilesWhileIgnoringBuildArtifacts()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDir, "MyApp.sln"), "Microsoft Visual Studio Solution File...");

        var apiDir = Path.Combine(_testDir, "src", "MyApp.Api");
        Directory.CreateDirectory(apiDir);
        File.WriteAllText(Path.Combine(apiDir, "MyApp.Api.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        File.WriteAllText(Path.Combine(apiDir, "Program.cs"), "var builder = WebApplication.CreateBuilder(args);");
        File.WriteAllText(Path.Combine(apiDir, "appsettings.json"), "{}");

        var webDir = Path.Combine(_testDir, "src", "MyApp.Web");
        Directory.CreateDirectory(webDir);
        File.WriteAllText(Path.Combine(webDir, "package.json"), "{\"name\": \"myapp-web\"}");
        File.WriteAllText(Path.Combine(webDir, "App.tsx"), "export function App() { return <div>App</div>; }");
        File.WriteAllText(Path.Combine(webDir, "index.ts"), "export * from './App';");

        // Ignored directories and files
        var binDir = Path.Combine(apiDir, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(binDir);
        File.WriteAllText(Path.Combine(binDir, "MyApp.Api.dll"), "fake binary");

        var objDir = Path.Combine(apiDir, "obj", "Debug");
        Directory.CreateDirectory(objDir);
        File.WriteAllText(Path.Combine(objDir, "MyApp.Api.AssemblyInfo.cs"), "fake generated");
        File.WriteAllText(Path.Combine(objDir, "MyApp.Api.g.cs"), "fake generated");

        var nodeModules = Path.Combine(webDir, "node_modules", "react");
        Directory.CreateDirectory(nodeModules);
        File.WriteAllText(Path.Combine(nodeModules, "index.js"), "module.exports = {}");

        var gitDir = Path.Combine(_testDir, ".git", "objects");
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Combine(gitDir, "dummy"), "git data");

        // Act
        var result = _scanner.Scan(_testDir);

        // Assert
        Assert.Empty(result.ScanErrors);
        Assert.Single(result.SolutionFiles);
        Assert.Equal("MyApp.sln", result.SolutionFiles[0]);

        Assert.Equal(2, result.Projects.Count);
        Assert.Contains(result.Projects, p => p.ProjectName == "MyApp.Api" && p.ProjectType == "CSharp");
        Assert.Contains(result.Projects, p => p.ProjectName == "MyApp.Web" && p.ProjectType == "Node");

        Assert.Contains(result.SourceFiles, f => f.RelativePath == "src/MyApp.Api/Program.cs" && f.Category == "CSharp");
        Assert.Contains(result.SourceFiles, f => f.RelativePath == "src/MyApp.Web/App.tsx" && f.Category == "TypeScript");
        Assert.Contains(result.SourceFiles, f => f.RelativePath == "src/MyApp.Web/index.ts" && f.Category == "TypeScript");

        Assert.DoesNotContain(result.SourceFiles, f => f.RelativePath.Contains("bin/"));
        Assert.DoesNotContain(result.SourceFiles, f => f.RelativePath.Contains("obj/"));
        Assert.DoesNotContain(result.SourceFiles, f => f.RelativePath.Contains("node_modules/"));
        Assert.DoesNotContain(result.SourceFiles, f => f.RelativePath.Contains(".git/"));
        Assert.DoesNotContain(result.SourceFiles, f => f.RelativePath.EndsWith(".AssemblyInfo.cs"));
        Assert.DoesNotContain(result.SourceFiles, f => f.RelativePath.EndsWith(".g.cs"));

        Assert.Contains(result.ConfigurationFiles, f => f.RelativePath == "src/MyApp.Api/appsettings.json");
    }

    [Fact]
    public void Scan_DiscoversAllSupportedFileTypesAcrossStructure()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDir, "TestApp.sln"), "Microsoft Visual Studio Solution File...");

        var srcDir = Path.Combine(_testDir, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "TestApp.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(srcDir, "Program.cs"), "Console.WriteLine();");
        File.WriteAllText(Path.Combine(srcDir, "Order.cs"), "public class Order {}");
        File.WriteAllText(Path.Combine(srcDir, "app.ts"), "export const a = 1;");
        File.WriteAllText(Path.Combine(srcDir, "App.tsx"), "export function App() {}");
        File.WriteAllText(Path.Combine(srcDir, "app.js"), "const b = 2;");
        File.WriteAllText(Path.Combine(srcDir, "App.jsx"), "export function AppJsx() {}");
        File.WriteAllText(Path.Combine(srcDir, "package.json"), "{\"name\":\"test\"}");
        File.WriteAllText(Path.Combine(srcDir, "appsettings.json"), "{}");
        File.WriteAllText(Path.Combine(srcDir, "tsconfig.json"), "{}");

        // Act
        var result = _scanner.Scan(_testDir);

        // Assert
        Assert.Empty(result.ScanErrors);
        Assert.Contains("TestApp.sln", result.SolutionFiles);
        Assert.Contains(result.Projects, p => p.ProjectName == "TestApp" && p.ProjectType == "CSharp");
        Assert.Contains(result.SourceFiles, f => f.RelativePath == "src/Program.cs" && f.Category == "CSharp");
        Assert.Contains(result.SourceFiles, f => f.RelativePath == "src/Order.cs" && f.Category == "CSharp");
        Assert.Contains(result.SourceFiles, f => f.RelativePath == "src/app.ts" && f.Category == "TypeScript");
        Assert.Contains(result.SourceFiles, f => f.RelativePath == "src/App.tsx" && f.Category == "TypeScript");
        Assert.Contains(result.SourceFiles, f => f.RelativePath == "src/app.js" && f.Category == "JavaScript");
        Assert.Contains(result.SourceFiles, f => f.RelativePath == "src/App.jsx" && f.Category == "JavaScript");
        Assert.Contains(result.ConfigurationFiles, f => f.RelativePath == "src/package.json");
        Assert.Contains(result.ConfigurationFiles, f => f.RelativePath == "src/appsettings.json");
        Assert.Contains(result.ConfigurationFiles, f => f.RelativePath == "src/tsconfig.json");
    }

    [Fact]
    public void Scan_IgnoresAllArtifactDirectories()
    {
        // Arrange
        var ignoredDirs = new[]
        {
            "bin", "obj", "node_modules", ".git", ".vs", "dist", "build", "coverage", ".idea", ".vscode"
        };

        foreach (var dir in ignoredDirs)
        {
            var dirPath = Path.Combine(_testDir, dir);
            Directory.CreateDirectory(dirPath);
            File.WriteAllText(Path.Combine(dirPath, "Artifact.cs"), "public class Ignored {}");
            File.WriteAllText(Path.Combine(dirPath, "script.ts"), "export const ignored = true;");
        }

        // Add one valid source file
        File.WriteAllText(Path.Combine(_testDir, "Valid.cs"), "public class Valid {}");

        // Act
        var result = _scanner.Scan(_testDir);

        // Assert
        Assert.Single(result.SourceFiles);
        Assert.Equal("Valid.cs", result.SourceFiles[0].RelativePath);
    }

    [Fact]
    public void Scan_IgnoresGeneratedFiles()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDir, "Generated.g.cs"), "// generated");
        File.WriteAllText(Path.Combine(_testDir, "Something.g.i.cs"), "// generated");
        File.WriteAllText(Path.Combine(_testDir, "Something.AssemblyInfo.cs"), "// generated");
        File.WriteAllText(Path.Combine(_testDir, "Something.AssemblyAttributes.cs"), "// generated");
        File.WriteAllText(Path.Combine(_testDir, "Form.Designer.cs"), "// generated");
        File.WriteAllText(Path.Combine(_testDir, "Real.cs"), "public class Real {}");

        // Act
        var result = _scanner.Scan(_testDir);

        // Assert
        Assert.Single(result.SourceFiles);
        Assert.Equal("Real.cs", result.SourceFiles[0].RelativePath);
    }

    [Fact]
    public void Scan_ProducesDeterministicOrdering()
    {
        // Arrange
        var subDirB = Path.Combine(_testDir, "b");
        var subDirA = Path.Combine(_testDir, "a");
        Directory.CreateDirectory(subDirB);
        Directory.CreateDirectory(subDirA);

        File.WriteAllText(Path.Combine(subDirB, "z.cs"), "class Z {}");
        File.WriteAllText(Path.Combine(subDirB, "a.cs"), "class A {}");
        File.WriteAllText(Path.Combine(subDirA, "m.cs"), "class M {}");
        File.WriteAllText(Path.Combine(_testDir, "Root.cs"), "class Root {}");

        // Act
        var run1 = _scanner.Scan(_testDir);
        var run2 = _scanner.Scan(_testDir);

        // Assert
        var paths1 = run1.SourceFiles.Select(f => f.RelativePath).ToList();
        var paths2 = run2.SourceFiles.Select(f => f.RelativePath).ToList();

        Assert.Equal(paths1, paths2);
        // Verify sorted ordering
        Assert.True(paths1.SequenceEqual(paths1.OrderBy(p => p, StringComparer.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Scan_NormalizesRelativePathsConsistently()
    {
        // Arrange
        var nested = Path.Combine(_testDir, "src", "Sub", "Module");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "Item.cs"), "class Item {}");

        // Act
        var result = _scanner.Scan(_testDir);

        // Assert
        var file = Assert.Single(result.SourceFiles);
        Assert.Equal("src/Sub/Module/Item.cs", file.RelativePath);
        Assert.DoesNotContain("\\", file.RelativePath);
        Assert.DoesNotContain("//", file.RelativePath);
    }

    [Fact]
    public void Scan_WhenRepositoryIsEmpty_ReturnsEmptyResultWithoutException()
    {
        // Act
        var result = _scanner.Scan(_testDir);

        // Assert
        Assert.Empty(result.ScanErrors);
        Assert.Empty(result.Projects);
        Assert.Empty(result.SourceFiles);
        Assert.Empty(result.ConfigurationFiles);
        Assert.Empty(result.SolutionFiles);
    }

    [Fact]
    public void Scan_WhenDirectoryDoesNotExist_ReturnsSafeErrorResult()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistent_" + Guid.NewGuid().ToString("N"));

        // Act
        var result = _scanner.Scan(nonExistentPath);

        // Assert
        Assert.Single(result.ScanErrors);
        Assert.Contains(nonExistentPath, result.ScanErrors[0]);
        Assert.Empty(result.Projects);
        Assert.Empty(result.SourceFiles);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Scan_WhenArgumentIsNullOrWhitespace_ThrowsArgumentException(string? invalidPath)
    {
        Assert.ThrowsAny<ArgumentException>(() => _scanner.Scan(invalidPath!));
    }
}
