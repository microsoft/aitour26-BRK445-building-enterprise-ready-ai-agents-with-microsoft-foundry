using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;
using SharedEntities;
using ZavaAgentsMetadata;
using ZavaMAFLocal;

namespace ProductSearchService.Endpoints;

public static class ProductSearchEndpoints
{
    private static readonly Dictionary<string, ToolRecommendation> Inventory = new(StringComparer.OrdinalIgnoreCase)
    {
        { "PAINT-ROLLER-9IN", new ToolRecommendation { Name = "Paint Roller", Sku = "PAINT-ROLLER-9IN", IsAvailable = true, Price = 12.99m, Description = "9-inch paint roller for smooth walls" } },
        { "BRUSH-SET-3PC", new ToolRecommendation { Name = "Paint Brush Set", Sku = "BRUSH-SET-3PC", IsAvailable = true, Price = 24.99m, Description = "3-piece brush set for detail work" } },
        { "DROP-CLOTH-9X12", new ToolRecommendation { Name = "Drop Cloth", Sku = "DROP-CLOTH-9X12", IsAvailable = true, Price = 8.99m, Description = "Plastic drop cloth protection" } },
        { "SAW-CIRCULAR-7IN", new ToolRecommendation { Name = "Circular Saw", Sku = "SAW-CIRCULAR-7IN", IsAvailable = true, Price = 89.99m, Description = "7.25-inch circular saw for wood cutting" } },
        { "STAIN-WOOD-QT", new ToolRecommendation { Name = "Wood Stain", Sku = "STAIN-WOOD-QT", IsAvailable = false, Price = 15.99m, Description = "1-quart wood stain in natural color" } },
        { "SAFETY-GLASSES", new ToolRecommendation { Name = "Safety Glasses", Sku = "SAFETY-GLASSES", IsAvailable = true, Price = 5.99m, Description = "Safety glasses for eye protection" } },
        { "GLOVES-WORK-L", new ToolRecommendation { Name = "Work Gloves", Sku = "GLOVES-WORK-L", IsAvailable = true, Price = 7.99m, Description = "Heavy-duty work gloves" } },
        { "DRILL-CORDLESS", new ToolRecommendation { Name = "Cordless Drill", Sku = "DRILL-CORDLESS", IsAvailable = true, Price = 79.99m, Description = "18V cordless drill with battery" } },
        { "LEVEL-2FT", new ToolRecommendation { Name = "Level", Sku = "LEVEL-2FT", IsAvailable = true, Price = 19.99m, Description = "2-foot aluminum level" } },
        { "TILE-CUTTER", new ToolRecommendation { Name = "Tile Cutter", Sku = "TILE-CUTTER", IsAvailable = false, Price = 45.99m, Description = "Manual tile cutting tool" } }
    };

    public static void MapProductSearchEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api");

        group.MapPost("/search/llm", SearchInventoryLlmAsync);
        group.MapPost("/search/maf", SearchInventoryMAFAsync);
        group.MapPost("/search/directcall", SearchInventoryDirectCallAsync);
        group.MapGet("/search/{sku}", GetItem);
        group.MapGet("/available", GetAvailableItems);
        group.MapPost("/check-availability", CheckAvailabilityAsync);
    }

    public static async Task<IResult> SearchInventoryLlmAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] MAFLocalAgentProvider localAgentProvider,
        [FromBody] InventorySearchRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.Llm} Searching inventory for query: {{SearchQuery}}", request.SearchQuery);
        var agent = localAgentProvider.GetAgentByName(AgentMetadata.GetAgentName(AgentType.ProductSearchAgent));
        return await SearchProductsAsync(logger, request, (prompt, token) => InvokeAgentFrameworkAsync(agent, prompt, token), AgentMetadata.LogPrefixes.Llm, cancellationToken);
    }

    public static async Task<IResult> SearchInventoryMAFAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] MAFLocalAgentProvider localAgentProvider,
        [FromBody] InventorySearchRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.Maf} Searching inventory for query: {{SearchQuery}}", request.SearchQuery);
        var agent = localAgentProvider.GetAgentByName(AgentMetadata.GetAgentName(AgentType.ProductSearchAgent));
        return await SearchProductsAsync(logger, request, (prompt, token) => InvokeAgentFrameworkAsync(agent, prompt, token), AgentMetadata.LogPrefixes.Maf, cancellationToken);
    }

    public static IResult SearchInventoryDirectCallAsync(
        [FromServices] ILogger<Program> logger,
        [FromBody] InventorySearchRequest request)
    {
        logger.LogInformation("[DirectCall] Searching inventory for query: {SearchQuery}", request.SearchQuery);
        return Results.Ok(BuildFallbackRecommendations(request.SearchQuery));
    }

    public static IResult GetItem([FromServices] ILogger<Program> logger, string sku)
    {
        try
        {
            logger.LogInformation("Getting inventory item for SKU: {Sku}", sku);

            return Inventory.TryGetValue(sku, out var item)
                ? Results.Ok(item)
                : Results.NotFound($"Item with SKU {sku} not found");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting inventory item for SKU: {Sku}", sku);
            return Results.Text("An error occurred while retrieving the item", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public static IResult GetAvailableItems([FromServices] ILogger<Program> logger)
    {
        try
        {
            logger.LogInformation("Getting all available inventory items");

            var availableItems = Inventory.Values
                .Where(static item => item.IsAvailable)
                .ToArray();
            return Results.Ok(availableItems);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting available inventory items");
            return Results.Text("An error occurred while retrieving available items", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public static async Task<IResult> CheckAvailabilityAsync(
        [FromServices] ILogger<Program> logger,
        [FromBody] string[] skus)
    {
        try
        {
            logger.LogInformation("Checking availability for {Count} SKUs", skus.Length);
            await Task.Delay(300);

            var availability = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var sku in skus)
            {
                availability[sku] = Inventory.TryGetValue(sku, out var item) && item.IsAvailable;
            }

            return Results.Ok(availability);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking availability");
            return Results.Text("An error occurred while checking availability", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> SearchProductsAsync(
        ILogger logger,
        InventorySearchRequest request,
        Func<string, CancellationToken, Task<string>> invokeAgentAsync,
        string logPrefix,
        CancellationToken cancellationToken)
    {
        var prompt = BuildProductSearchPrompt(request.SearchQuery);

        try
        {
            var agentResponse = await invokeAgentAsync(prompt, cancellationToken);
            logger.LogInformation("{Prefix} Raw agent response length: {Length}", logPrefix, agentResponse.Length);

            if (TryParseSkuList(agentResponse, out var skus) && skus.Length > 0)
            {
                return Results.Ok(BuildRecommendationsFromSkus(skus, request.SearchQuery));
            }

            logger.LogWarning("{Prefix} Unable to parse SKU list. Falling back to heuristics. Raw: {Raw}", logPrefix, TrimForLog(agentResponse));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Prefix} Agent invocation failed. Using fallback recommendations.", logPrefix);
        }

        return Results.Ok(BuildFallbackRecommendations(request.SearchQuery));
    }

    private static async Task<string> InvokeAgentFrameworkAsync(AIAgent agent, string prompt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var session = await agent.CreateSessionAsync();
        var response = await agent.RunAsync(prompt, session);
        return response?.Text ?? string.Empty;
    }

    private static ToolRecommendation[] BuildRecommendationsFromSkus(string[] skus, string searchQuery)
    {
        var recommendations = new List<ToolRecommendation>();
        foreach (var sku in skus)
        {
            if (string.IsNullOrWhiteSpace(sku))
            {
                continue;
            }

            recommendations.Add(CreateRecommendationForSku(sku));
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add(new ToolRecommendation
            {
                Name = "No matching products found",
                Sku = string.Empty,
                IsAvailable = false,
                Price = 0m,
                Description = $"No products matched the query: '{searchQuery}'"
            });
        }

        return recommendations.ToArray();
    }

    private static ToolRecommendation[] BuildFallbackRecommendations(string searchQuery)
        => BuildRecommendationsFromSkus(GetFallbackProductSkus(searchQuery), searchQuery);

    private static string BuildProductSearchPrompt(string searchQuery) => @$"
# Context
User Query: {searchQuery}

# Tasks
Search for products that may match the user query.
Analyze the user query and extract the product name or SKU that the user is referring to.

Return ONLY the product SKU identifiers, separated by commas, with no additional text, explanation, or formatting.
If there are NO matching products, return a string that contains only a single comma: ','
Example response: 'PAINT-ROLLER-9IN,BRUSH-SET-3PC,SAW-CIRCULAR-7IN'
";

    private static bool TryParseSkuList(string agentResponse, out string[] skus)
    {
        skus = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(agentResponse))
        {
            return false;
        }

        var normalized = agentResponse.Trim();
        if (string.Equals(normalized, ",", StringComparison.Ordinal))
        {
            return false;
        }

        skus = normalized
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        return skus.Length > 0;
    }

    private static string[] GetFallbackProductSkus(string searchQuery)
    {
        var queryLower = searchQuery.ToLowerInvariant();
        var matchedSkus = new List<string>();

        if (queryLower.Contains("paint") || queryLower.Contains("roller"))
        {
            matchedSkus.Add("PAINT-ROLLER-9IN");
        }
        if (queryLower.Contains("brush"))
        {
            matchedSkus.Add("BRUSH-SET-3PC");
        }
        if (queryLower.Contains("saw") || queryLower.Contains("cut"))
        {
            matchedSkus.Add("SAW-CIRCULAR-7IN");
        }
        if (queryLower.Contains("drill"))
        {
            matchedSkus.Add("DRILL-CORDLESS");
        }

        return matchedSkus.ToArray();
    }

    private static ToolRecommendation CreateRecommendationForSku(string sku)
    {
        if (Inventory.TryGetValue(sku, out var item))
        {
            return new ToolRecommendation
            {
                Name = item.Name,
                Sku = item.Sku,
                IsAvailable = item.IsAvailable && Random.Shared.NextDouble() > 0.1,
                Price = item.Price * (decimal)(0.9 + Random.Shared.NextDouble() * 0.2),
                Description = item.Description
            };
        }

        return new ToolRecommendation
        {
            Name = $"Tool for SKU {sku}",
            Sku = sku,
            IsAvailable = false,
            Price = 29.99m,
            Description = "Product not found in current inventory"
        };
    }

    private static string TrimForLog(string value, int maxLength = 400)
        => value.Length <= maxLength ? value : value[..maxLength] + "...";
}
