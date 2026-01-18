using SharedEntities;
using ZavaAgentsMetadata;

namespace SingleAgentDemo.Services;

public class ProductSearchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductSearchService> _logger;
    private string _framework = AgentMetadata.FrameworkIdentifiers.MafLocal; // Default to MAF Local

    public ProductSearchService(HttpClient httpClient, ILogger<ProductSearchService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Sets the agent framework to use for service calls
    /// </summary>
    /// <param name="framework">"llm" for LLM Direct Call, or "maf" for Microsoft Agent Framework</param>
    public void SetFramework(string framework)
    {
        _framework = framework?.ToLowerInvariant() ?? AgentMetadata.FrameworkIdentifiers.MafLocal;
        _logger.LogInformation($"[ProductSearchService] Framework set to: {_framework}");
    }

    public async Task<ToolRecommendation[]> EnrichWithInventoryAsync(ToolRecommendation[] tools)
    {
        try
        {
            var skus = tools.Select(t => t.Sku).ToArray();

            // create a prompt to search on the inventory service for the given SKUs
            var searchQuery = $"Search for the following SKUs: {string.Join(", ", skus)}";

            var searchRequest = new InventorySearchRequest { SearchQuery = searchQuery };
            
            var endpoint = $"/api/analyze_search/{_framework}";
            _logger.LogInformation($"[ProductSearchService] Calling endpoint: {endpoint}");
            var response = await _httpClient.PostAsJsonAsync(endpoint, searchRequest);
            
            _logger.LogInformation($"ProductSearchService HTTP status code: {response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var inventoryResults = await response.Content.ReadFromJsonAsync<ToolRecommendation[]>();
                return inventoryResults ?? tools;
            }
            
            _logger.LogWarning("ProductSearchService returned non-success status: {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling ProductSearchService");
        }

        return tools; // Return original tools if product search service fails
    }
}