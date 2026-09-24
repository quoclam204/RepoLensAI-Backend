using RepoLens.Analysis.CSharp;
using RepoLens.Domain.ValueObjects;

namespace RepoLens.AnalysisTests.CSharp;

public class ProjectDependencyExtractorTests
{
    private readonly ProjectDependencyExtractor _extractor = new();

    [Fact]
    public void Analyze_WithLayeredProjects_ExtractsMultiProjectChainAndPackageWithExactLine()
    {
        // Arrange: Api.csproj referencing Infrastructure and an external package
        var apiCsproj = """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <ProjectReference Include="..\Infrastructure\Infrastructure.csproj" />
              </ItemGroup>
              <ItemGroup>
                <PackageReference Include="Example.Package" Version="1.2.3" />
              </ItemGroup>
            </Project>
            """;

        // Arrange: Infrastructure.csproj referencing Domain
        var infraCsproj = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Domain\Domain.csproj" />
              </ItemGroup>
            </Project>
            """;

        // Act
        var apiResult = _extractor.Analyze("src/Api/Api.csproj", apiCsproj);
        var infraResult = _extractor.Analyze("src/Infrastructure/Infrastructure.csproj", infraCsproj);

        // Assert: Api -> Infrastructure
        Assert.Single(apiResult.ProjectReferences);
        var apiToInfra = apiResult.ProjectReferences[0];
        Assert.Equal("Infrastructure", apiToInfra.TargetProjectName);
        Assert.Equal("../Infrastructure/Infrastructure.csproj", apiToInfra.RelativePath);
        Assert.Equal(ConfidenceScore.High, apiToInfra.Confidence);

        // Assert: PackageReference
        Assert.Single(apiResult.PackageReferences);
        var pkg = apiResult.PackageReferences[0];
        Assert.Equal("Example.Package", pkg.PackageName);
        Assert.Equal("1.2.3", pkg.Version);
        Assert.Equal(ConfidenceScore.High, pkg.Confidence);
        Assert.True(pkg.Location.StartLine > 0);
        Assert.Equal("src/Api/Api.csproj", pkg.Location.FilePath);

        // Assert: Infrastructure -> Domain
        Assert.Single(infraResult.ProjectReferences);
        var infraToDomain = infraResult.ProjectReferences[0];
        Assert.Equal("Domain", infraToDomain.TargetProjectName);
        Assert.Equal("../Domain/Domain.csproj", infraToDomain.RelativePath);
        Assert.Equal(ConfidenceScore.High, infraToDomain.Confidence);
    }

    [Fact]
    public void Analyze_WithValidCsproj_ExtractsProjectAndPackageReferencesWithHighConfidence()
    {
        // Arrange
        var csprojContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>

              <ItemGroup>
                <ProjectReference Include="..\RepoLens.Domain\RepoLens.Domain.csproj" />
              </ItemGroup>

              <ItemGroup>
                <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.9.0" />
              </ItemGroup>
            </Project>
            """;

        // Act
        var result = _extractor.Analyze("src/RepoLens.Analysis/RepoLens.Analysis.csproj", csprojContent);

        // Assert
        Assert.Single(result.ProjectReferences);
        var projRef = result.ProjectReferences[0];
        Assert.Equal("RepoLens.Domain", projRef.TargetProjectName);
        Assert.Equal("../RepoLens.Domain/RepoLens.Domain.csproj", projRef.RelativePath);
        Assert.Equal(ConfidenceScore.High, projRef.Confidence);
        Assert.True(projRef.Location.StartLine > 0);

        Assert.Single(result.PackageReferences);
        var pkgRef = result.PackageReferences[0];
        Assert.Equal("Microsoft.CodeAnalysis.CSharp", pkgRef.PackageName);
        Assert.Equal("5.9.0", pkgRef.Version);
        Assert.Equal(ConfidenceScore.High, pkgRef.Confidence);
        Assert.True(pkgRef.Location.StartLine > 0);
    }

    [Fact]
    public void Analyze_WhenCsprojHasOnlyPropertyGroups_ReturnsEmptyResults()
    {
        // Arrange (Negative Case 1)
        var csprojContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """;

        // Act
        var result = _extractor.Analyze("MyProject.csproj", csprojContent);

        // Assert
        Assert.Empty(result.ProjectReferences);
        Assert.Empty(result.PackageReferences);
    }

    [Fact]
    public void Analyze_WhenContentIsEmptyOrMalformedXml_ReturnsEmptyGracefully()
    {
        // Arrange (Negative Case 2)
        var emptyContent = "   \t\n  ";
        var malformedContent = "<Project><UnclosedTag>";

        // Act
        var resultEmpty = _extractor.Analyze("Empty.csproj", emptyContent);
        var resultMalformed = _extractor.Analyze("Malformed.csproj", malformedContent);

        // Assert
        Assert.Empty(resultEmpty.ProjectReferences);
        Assert.Empty(resultEmpty.PackageReferences);
        Assert.Empty(resultMalformed.ProjectReferences);
        Assert.Empty(resultMalformed.PackageReferences);
    }
}
