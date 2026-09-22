using RepoLens.Analysis.CSharp;

namespace RepoLens.AnalysisTests.CSharp;

public class ApiEndpointExtractorTests
{
    private readonly CSharpFileParser _parser = new();

    [Fact]
    public void ExtractFromTree_WithControllersAndMinimalApis_DetectsAllEndpoints()
    {
        // Arrange
        var code = """
            using Microsoft.AspNetCore.Mvc;

            [ApiController]
            [Route("api/[controller]")]
            public class UsersController : ControllerBase
            {
                [HttpGet("{id}")]
                public IActionResult GetUser(string id) => Ok();
            }

            public class Program
            {
                public static void Main(string[] args)
                {
                    var app = WebApplication.Create();
                    app.MapPost("/api/orders", () => "order created");
                }
            }
            """;

        var tree = _parser.ParseText(code, "Api.cs");

        // Act
        var endpoints = ApiEndpointExtractor.ExtractFromTree(tree, "Api.cs");

        // Assert
        Assert.Contains(endpoints, e => !e.IsMinimalApi && e.HttpMethod == "GET" && e.RouteTemplate.Contains("Users/{id}"));
        Assert.Contains(endpoints, e => e.IsMinimalApi && e.HttpMethod == "POST" && e.RouteTemplate == "/api/orders");
    }

    [Fact]
    public void ExtractFromTree_WhenClassHasNoHttpAttributesOrMapCalls_ReturnsEmptyEndpoints()
    {
        // Arrange (Negative case)
        var code = """
            namespace MyNamespace;

            public class BusinessLogicService
            {
                public string CalculateDiscount(decimal amount)
                {
                    return amount > 100 ? "10%" : "0%";
                }
            }
            """;

        var tree = _parser.ParseText(code, "BusinessLogic.cs");

        // Act
        var endpoints = ApiEndpointExtractor.ExtractFromTree(tree, "BusinessLogic.cs");

        // Assert
        Assert.Empty(endpoints);
    }
}
