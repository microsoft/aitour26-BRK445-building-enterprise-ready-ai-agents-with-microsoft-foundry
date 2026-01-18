using Microsoft.AspNetCore.Mvc;
using SharedEntities;
using ZavaAgentsMetadata;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly ILogger<InventoryController> _logger;
    private readonly DataServiceClient.DataServiceClient _dataServiceClient;

    public InventoryController(
        ILogger<InventoryController> logger,
        DataServiceClient.DataServiceClient dataServiceClient)
    {
        _logger = logger;
        _dataServiceClient = dataServiceClient;
    }

    [HttpPost("search_llm")]
    public async Task<ActionResult<ToolRecommendation[]>> SearchInventoryLlmAsync([FromBody] InventorySearchRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AgentMetadata.LogPrefixes.Llm} Searching inventory for query: {{SearchQuery}}", request.SearchQuery);

        // Use DataServiceClient directly instead of LLM/Agent
        return await SearchInventoryFromDataServiceAsync(request, AgentMetadata.LogPrefixes.Llm, cancellationToken);
    }
           
    [HttpPost("searchmaf_local")]  // Using constant AgentMetadata.FrameworkIdentifiers.MafLocal
    public async Task<ActionResult<ToolRecommendation[]>> SearchInventoryMAFLocalAsync([FromBody] InventorySearchRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AgentMetadata.LogPrefixes.MafLocal} Searching inventory for query: {{SearchQuery}}", request.SearchQuery);

        // Use DataServiceClient directly instead of Agent
        return await SearchInventoryFromDataServiceAsync(request, AgentMetadata.LogPrefixes.MafLocal, cancellationToken);
    }

    [HttpPost("searchmaf_foundry")]  // Using constant AgentMetadata.FrameworkIdentifiers.MafFoundry
    public async Task<ActionResult<ToolRecommendation[]>> SearchInventoryMAFFoundryAsync([FromBody] InventorySearchRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AgentMetadata.LogPrefixes.MafFoundry} Searching inventory for query: {{SearchQuery}}", request.SearchQuery);

        // Use DataServiceClient directly instead of Agent
        return await SearchInventoryFromDataServiceAsync(request, AgentMetadata.LogPrefixes.MafFoundry, cancellationToken);
    }

    [HttpPost("search_directcall")]
    public async Task<ActionResult<ToolRecommendation[]>> SearchInventoryDirectCallAsync([FromBody] InventorySearchRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AgentMetadata.LogPrefixes.DirectCall} Searching inventory for query: {{SearchQuery}}", request.SearchQuery);

        // Use DataServiceClient to search inventory
        return await SearchInventoryFromDataServiceAsync(request, AgentMetadata.LogPrefixes.DirectCall, cancellationToken);
    }

    [HttpGet("search/{sku}")]
    public async Task<ActionResult<ToolRecommendation>> GetItem(string sku, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting inventory item for SKU: {Sku}", sku);

            // Try to get from DataService first
            var item = await _dataServiceClient.GetToolBySkuAsync(sku, cancellationToken);
            if (item != null)
            {
                return Ok(item);
            }

            return NotFound($"Item with SKU {sku} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inventory item for SKU: {Sku}", sku);
            return StatusCode(500, "An error occurred while retrieving the item");
        }
    }

    [HttpGet("available")]
    public async Task<ActionResult<ToolRecommendation[]>> GetAvailableItems(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting all available inventory items");

            // Get from DataService
            var availableItems = await _dataServiceClient.GetAvailableToolsAsync(cancellationToken);
            return Ok(availableItems.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available inventory items");
            return StatusCode(500, "An error occurred while retrieving available items");
        }
    }

    [HttpPost("check-availability")]
    public async Task<ActionResult<Dictionary<string, bool>>> CheckAvailabilityAsync([FromBody] string[] skus, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Checking availability for {Count} SKUs", skus.Length);

            var availability = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var sku in skus)
            {
                var tool = await _dataServiceClient.GetToolBySkuAsync(sku, cancellationToken);
                availability[sku] = tool != null && tool.IsAvailable;
            }

            return Ok(availability);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking availability");
            return StatusCode(500, "An error occurred while checking availability");
        }
    }

    private async Task<ActionResult<ToolRecommendation[]>> SearchInventoryFromDataServiceAsync(
        InventorySearchRequest request,
        string logPrefix,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("{Prefix} Searching inventory from DataService for query: {SearchQuery}", logPrefix, request.SearchQuery);

            // Get all available tools from DataService
            var allTools = await _dataServiceClient.GetAvailableToolsAsync(cancellationToken);
            
            if (allTools == null || allTools.Count == 0)
            {
                _logger.LogWarning("{Prefix} No tools available from DataService, using fallback", logPrefix);
                return Ok(await BuildFallbackRecommendations(request.SearchQuery));
            }

            // Filter tools based on search query
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
                _logger.LogInformation("{Prefix} Found {Count} matching tools from DataService", logPrefix, matchedTools.Length);
                return Ok(matchedTools);
            }

            // If no direct matches, use fallback heuristics
            _logger.LogInformation("{Prefix} No direct matches found, using heuristic fallback", logPrefix);
            var fallbackSkus = GetFallbackInventorySkus(request.SearchQuery);
            
            if (fallbackSkus.Length > 0)
            {
                return Ok(await BuildRecommendationsFromSkus(fallbackSkus, request.SearchQuery));
            }

            // Return empty result if no matches found
            return Ok(Array.Empty<ToolRecommendation>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Prefix} Error searching inventory from DataService", logPrefix);
            return Ok(await BuildFallbackRecommendations(request.SearchQuery));
        }
    }

    private async Task<ToolRecommendation[]> BuildRecommendationsFromSkus(string[] skus, string searchQuery)
    {
        var recommendations = new List<ToolRecommendation>();

        foreach (var sku in skus)
        {
            if (string.IsNullOrWhiteSpace(sku))
            {
                continue;
            }

            recommendations.Add(await GetToolRecommendation(sku));
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

    private async Task<ToolRecommendation[]> BuildFallbackRecommendations(string searchQuery)
        => await BuildRecommendationsFromSkus(GetFallbackInventorySkus(searchQuery), searchQuery);

    #region Utility helpers

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

    private async Task<ToolRecommendation> GetToolRecommendation(string sku)
    {
        try
        {
            var item = await _dataServiceClient.GetToolBySkuAsync(sku);
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
            _logger.LogWarning(ex, "Failed to get tool from DataService for SKU {Sku}", sku);
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

    #endregion
}
