using RepoLens.Analysis.CSharp;

namespace RepoLens.AnalysisTests.CSharp;

public class ApiEndpointExtractorTests
{
    private readonly CSharpFileParser _parser = new();

    [Fact]
    public void ExtractFromTree_WithOrdersControllerAndSeparateRouteAttribute_DetectsAllEndpoints()
    {
        // Arrange
        var code = """
            using Microsoft.AspNetCore.Mvc;

            namespace Demo;

            [ApiController]
            [Route("api/[controller]")]
            public class OrdersController : ControllerBase
            {
                [HttpGet]
                public IActionResult GetOrders()
                {
                    return Ok();
                }

                [HttpGet("{id}")]
                public IActionResult GetOrder(int id)
                {
                    return Ok();
                }

                [HttpPost]
                [Route("create")]
                public IActionResult CreateOrder()
                {
                    return Ok();
                }
            }
            """;

        var tree = _parser.ParseText(code, "OrdersController.cs");

        // Act
        var endpoints = ApiEndpointExtractor.ExtractFromTree(tree, "OrdersController.cs");

        // Assert
        Assert.Equal(3, endpoints.Count);

        var getOrders = endpoints.Single(e => e.HandlerSymbol == "OrdersController.GetOrders");
        Assert.Equal("GET", getOrders.HttpMethod);
        Assert.Equal("/api/Orders", getOrders.RouteTemplate);
        Assert.False(getOrders.IsMinimalApi);
        Assert.Equal("OrdersController.cs", getOrders.Location.FilePath);
        Assert.True(getOrders.Location.StartLine > 0);

        var getOrderById = endpoints.Single(e => e.HandlerSymbol == "OrdersController.GetOrder");
        Assert.Equal("GET", getOrderById.HttpMethod);
        Assert.Equal("/api/Orders/{id}", getOrderById.RouteTemplate);
        Assert.False(getOrderById.IsMinimalApi);

        var postOrder = endpoints.Single(e => e.HandlerSymbol == "OrdersController.CreateOrder");
        Assert.Equal("POST", postOrder.HttpMethod);
        Assert.Equal("/api/Orders/create", postOrder.RouteTemplate);
        Assert.False(postOrder.IsMinimalApi);
    }

    [Fact]
    public void ExtractFromTree_WithMinimalApis_DetectsAllEndpoints()
    {
        // Arrange
        var code = """
            using Microsoft.AspNetCore.Builder;

            var app = WebApplication.Create(args);

            app.MapGet("/api/orders", () => "list");
            app.MapPost("/api/orders", () => "created");
            """;

        var tree = _parser.ParseText(code, "Program.cs");

        // Act
        var endpoints = ApiEndpointExtractor.ExtractFromTree(tree, "Program.cs");

        // Assert
        Assert.Equal(2, endpoints.Count);
        Assert.Contains(endpoints, e => e.IsMinimalApi && e.HttpMethod == "GET" && e.RouteTemplate == "/api/orders");
        Assert.Contains(endpoints, e => e.IsMinimalApi && e.HttpMethod == "POST" && e.RouteTemplate == "/api/orders");
    }

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
