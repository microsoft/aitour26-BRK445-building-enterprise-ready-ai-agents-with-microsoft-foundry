using Microsoft.AspNetCore.Mvc;
using SharedEntities;
using ZavaAgentsMetadata;

namespace InventoryService.Endpoints;

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Inventory");

        group.MapPost("/search_llm", SearchInventoryLlmAsync);
        group.MapPost("/searchmaf_local", SearchInventoryMAFLocalAsync);
        group.MapPost("/searchmaf_foundry", SearchInventoryMAFFoundryAsync);
        group.MapPost("/search_directcall", SearchInventoryDirectCallAsync);
        group.MapGet("/search/{sku}", GetItemAsync);
        group.MapGet("/available", GetAvailableItemsAsync);
        group.MapPost("/check-availability", CheckAvailabilityAsync);
    }

    public static async Task<IResult> SearchInventoryLlmAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] DataServiceClient.DataServiceClient dataServiceClient,
        [FromBody] InventorySearchRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.Llm} Searching inventory for query: {{SearchQuery}}", request.SearchQuery);
        return await SearchInventoryFromDataServiceAsync(logger, dataServiceClient, request, AgentMetadata.LogPrefixes.Llm, cancellationToken);
    }

    public static async Task<IResult> SearchInventoryMAFLocalAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] DataServiceClient.DataServiceClient dataServiceClient,
        [FromBody] InventorySearchRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.MafLocal} Searching inventory for query: {{SearchQuery}}", request.SearchQuery);
        return await SearchInventoryFromDataServiceAsync(logger, dataServiceClient, request, AgentMetadata.LogPrefixes.MafLocal, cancellationToken);
    }

    public static async Task<IResult> SearchInventoryMAFFoundryAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] DataServiceClient.DataServiceClient dataServiceClient,
        [FromBody] InventorySearchRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.MafFoundry} Searching inventory for query: {{SearchQuery}}", request.SearchQuery);
        return await SearchInventoryFromDataServiceAsync(logger, dataServiceClient, request, AgentMetadata.LogPrefixes.MafFoundry, cancellationToken);
    }

    public static async Task<IResult> SearchInventoryDirectCallAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] DataServiceClient.DataServiceClient dataServiceClient,
        [FromBody] InventorySearchRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.DirectCall} Searching inventory for query: {{SearchQuery}}", request.SearchQuery);
        return await SearchInventoryFromDataServiceAsync(logger, dataServiceClient, request, AgentMetadata.LogPrefixes.DirectCall, cancellationToken);
    }

    public static async Task<IResult> GetItemAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] DataServiceClient.DataServiceClient dataServiceClient,
        string sku,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Getting inventory item for SKU: {Sku}", sku);

            var item = await dataServiceClient.GetToolBySkuAsync(sku, cancellationToken);
            return item != null ? Results.Ok(item) : Results.NotFound($"Item with SKU {sku} not found");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting inventory item for SKU: {Sku}", sku);
            return Results.Text("An error occurred while retrieving the item", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public static async Task<IResult> GetAvailableItemsAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] DataServiceClient.DataServiceClient dataServiceClient,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Getting all available inventory items");

            var availableItems = await dataServiceClient.GetAvailableToolsAsync(cancellationToken);
            return Results.Ok(availableItems.ToArray());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting available inventory items");
            return Results.Text("An error occurred while retrieving available items", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public static async Task<IResult> CheckAvailabilityAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] DataServiceClient.DataServiceClient dataServiceClient,
        [FromBody] string[] skus,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Checking availability for {Count} SKUs", skus.Length);

            var availability = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var sku in skus)
            {
                var tool = await dataServiceClient.GetToolBySkuAsync(sku, cancellationToken);
                availability[sku] = tool != null && tool.IsAvailable;
            }

            return Results.Ok(availability);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking availability");
            return Results.Text("An error occurred while checking availability", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> SearchInventoryFromDataServiceAsync(
        ILogger logger,
        DataServiceClient.DataServiceClient dataServiceClient,
        InventorySearchRequest request,
        string logPrefix,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("{Prefix} Searching inventory from DataService for query: {SearchQuery}", logPrefix, request.SearchQuery);

            var allTools = await dataServiceClient.GetAvailableToolsAsync(cancellationToken);
            if (allTools == null || allTools.Count == 0)
            {
                logger.LogWarning("{Prefix} No tools available from DataService, using fallback", logPrefix);
                return Results.Ok(await BuildFallbackRecommendations(logger, dataServiceClient, request.SearchQuery));
            }

            var queryLower = request.SearchQuery.ToLowerInvariant();
            var matchedTools = allTools
                .Where(tool =>
                    tool.Name.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ||
                    tool.Description.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ||
                    tool.Sku.Contains(queryLower, StringComparison.OrdinalIgnoreCase))
                .Select(tool => new ToolRecommendation
                {
                    Name = tool.Name,
                    Sku = tool.Sku,
                    IsAvailable = tool.IsAvailable,
                    Price = tool.Price,
                    Description = tool.Description
                })
                .ToArray();

            if (matchedTools.Length > 0)
            {
                logger.LogInformation("{Prefix} Found {Count} matching tools from DataService", logPrefix, matchedTools.Length);
                return Results.Ok(matchedTools);
            }

            logger.LogInformation("{Prefix} No direct matches found, using heuristic fallback", logPrefix);
            var fallbackSkus = GetFallbackInventorySkus(request.SearchQuery);
            if (fallbackSkus.Length > 0)
            {
                return Results.Ok(await BuildRecommendationsFromSkus(logger, dataServiceClient, fallbackSkus, request.SearchQuery));
            }

            return Results.Ok(Array.Empty<ToolRecommendation>());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Prefix} Error searching inventory from DataService", logPrefix);
            return Results.Ok(await BuildFallbackRecommendations(logger, dataServiceClient, request.SearchQuery));
        }
    }

    private static async Task<ToolRecommendation[]> BuildRecommendationsFromSkus(
        ILogger logger,
        DataServiceClient.DataServiceClient dataServiceClient,
        string[] skus,
        string searchQuery)
    {
        var recommendations = new List<ToolRecommendation>();
        foreach (var sku in skus)
        {
            if (!string.IsNullOrWhiteSpace(sku))
            {
                recommendations.Add(await GetToolRecommendation(logger, dataServiceClient, sku));
            }
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

    private static Task<ToolRecommendation[]> BuildFallbackRecommendations(
        ILogger logger,
        DataServiceClient.DataServiceClient dataServiceClient,
        string searchQuery)
        => BuildRecommendationsFromSkus(logger, dataServiceClient, GetFallbackInventorySkus(searchQuery), searchQuery);

    private static string[] GetFallbackInventorySkus(string searchQuery)
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

    private static async Task<ToolRecommendation> GetToolRecommendation(
        ILogger logger,
        DataServiceClient.DataServiceClient dataServiceClient,
        string sku)
    {
        try
        {
            var item = await dataServiceClient.GetToolBySkuAsync(sku);
            if (item != null)
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
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get tool from DataService for SKU {Sku}", sku);
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
}
