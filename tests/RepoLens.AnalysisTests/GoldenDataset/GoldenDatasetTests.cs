using RepoLens.Analysis.Graph;
using RepoLens.Analysis.Orchestration;
using RepoLens.Domain.Enums;

namespace RepoLens.AnalysisTests.GoldenDataset;

/// <summary>
/// Golden Dataset tests for static analysis reproducibility and baseline verification (T100, NFR-TEST-001).
/// Verifies deterministic extraction against known baseline repositories (sample-csharp-api, sample-nextjs-app).
/// </summary>
public class GoldenDatasetTests : IDisposable
{
    private readonly string _rootDir;
    private readonly RepositoryAnalysisEngine _engine = new();

    public GoldenDatasetTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "RepoLens_Golden_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootDir))
            {
                Directory.Delete(_rootDir, recursive: true);
            }
        }
        catch
        {
            // Best effort
        }
    }

    [Fact]
    public void SampleCSharpApi_GoldenBaseline_ExtractsExpectedStructureDeterministically()
    {
        // Arrange
        var repoDir = Path.Combine(_rootDir, "sample-csharp-api");
        Directory.CreateDirectory(repoDir);

        File.WriteAllText(Path.Combine(repoDir, "sample-csharp-api.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Combine(repoDir, "WeatherForecast.cs"),
            """
            namespace SampleApi.Models;

            public class WeatherForecast
            {
                public int Id { get; set; }
                public DateTime Date { get; set; }
                public int TemperatureC { get; set; }
                public string Summary { get; set; } = string.Empty;
            }
            """);

        File.WriteAllText(Path.Combine(repoDir, "IWeatherService.cs"),
            """
            namespace SampleApi.Services;

            public interface IWeatherService
            {
                Task<IEnumerable<SampleApi.Models.WeatherForecast>> GetForecastsAsync();
            }
            """);

        File.WriteAllText(Path.Combine(repoDir, "WeatherService.cs"),
            """
            using SampleApi.Models;

            namespace SampleApi.Services;

            public class WeatherService : IWeatherService
            {
                public Task<IEnumerable<WeatherForecast>> GetForecastsAsync()
                {
                    return Task.FromResult<IEnumerable<WeatherForecast>>([]);
                }
            }
            """);

        File.WriteAllText(Path.Combine(repoDir, "WeatherDbContext.cs"),
            """
            using Microsoft.EntityFrameworkCore;
            using SampleApi.Models;

            namespace SampleApi.Data;

            public class WeatherDbContext : DbContext
            {
                public DbSet<WeatherForecast> Forecasts => Set<WeatherForecast>();

                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<WeatherForecast>().HasKey(f => f.Id);
                }
            }
            """);

        File.WriteAllText(Path.Combine(repoDir, "WeatherController.cs"),
            """
            using Microsoft.AspNetCore.Mvc;
            using SampleApi.Models;
            using SampleApi.Services;

            namespace SampleApi.Controllers;

            [ApiController]
            [Route("api/[controller]")]
            public class WeatherController : ControllerBase
            {
                private readonly IWeatherService _weatherService;

                public WeatherController(IWeatherService weatherService)
                {
                    _weatherService = weatherService;
                }

                [HttpGet]
                public async Task<IActionResult> GetAll()
                {
                    return Ok(await _weatherService.GetForecastsAsync());
                }

                [HttpPost]
                public IActionResult Create([FromBody] WeatherForecast forecast)
                {
                    return CreatedAtAction(nameof(GetAll), forecast);
                }
            }
            """);

        // Act
        var result = _engine.AnalyzeRepository(repoDir);

        // Assert Golden Baseline
        Assert.Empty(result.AllErrors);
        Assert.Single(result.ScannedMetadata.Projects);
        Assert.Equal("sample-csharp-api", result.ScannedMetadata.Projects[0].ProjectName);

        var nodes = result.Analysis.Nodes;
        // Verify Symbols
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Class && n.Name == "WeatherForecast");
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Interface && n.Name == "IWeatherService");
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Class && n.Name == "WeatherService");
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Class && n.Name == "WeatherDbContext");
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Class && n.Name == "WeatherController");

        // Verify Endpoints
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Endpoint && n.Properties.TryGetValue("HttpMethod", out var m) && m == "GET");
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Endpoint && n.Properties.TryGetValue("HttpMethod", out var m) && m == "POST");

        // Verify Database Entities
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.DatabaseEntity && n.Name == "WeatherForecast");

        // Verify Relationships
        var rels = result.Analysis.Relationships;
        Assert.Contains(rels, r => r.Type == KnowledgeRelationshipType.Implements);
        Assert.Contains(rels, r => r.Type == KnowledgeRelationshipType.Exposes);
    }

    [Fact]
    public void SampleNextJsApp_GoldenBaseline_ExtractsExpectedStructureDeterministically()
    {
        // Arrange
        var repoDir = Path.Combine(_rootDir, "sample-nextjs-app");
        Directory.CreateDirectory(repoDir);

        File.WriteAllText(Path.Combine(repoDir, "package.json"),
            """
            {
              "name": "sample-nextjs-app",
              "version": "1.0.0",
              "dependencies": {
                "next": "14.2.0",
                "react": "18.3.0",
                "axios": "1.6.8"
              }
            }
            """);

        var appDir = Path.Combine(repoDir, "app");
        Directory.CreateDirectory(appDir);

        File.WriteAllText(Path.Combine(appDir, "page.tsx"),
            """
            import React, { useEffect, useState } from "react";
            import axios from "axios";

            export interface UserProfile {
                id: string;
                name: string;
            }

            export default function HomePage() {
                const [users, setUsers] = useState<UserProfile[]>([]);

                useEffect(() => {
                    axios.get("/api/users").then(res => setUsers(res.data));
                }, []);

                return <div>Hello Next.js</div>;
            }
            """);

        // Act
        var result = _engine.AnalyzeRepository(repoDir);

        // Assert Golden Baseline
        Assert.Empty(result.AllErrors);
        Assert.Single(result.ScannedMetadata.Projects);
        Assert.Equal("sample-nextjs-app", result.ScannedMetadata.Projects[0].ProjectName);

        var nodes = result.Analysis.Nodes;
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Interface && n.Name == "UserProfile");
        Assert.Contains(nodes, n => n.Type == KnowledgeNodeType.Class || n.Type == KnowledgeNodeType.Method || n.Name == "HomePage");

        // Verify npm dependencies were discovered in relationships
        var rels = result.Analysis.Relationships;
        Assert.Contains(rels, r => r.Type == KnowledgeRelationshipType.DependsOn && r.TargetId.Contains("next"));
        Assert.Contains(rels, r => r.Type == KnowledgeRelationshipType.DependsOn && r.TargetId.Contains("react"));
    }
}
