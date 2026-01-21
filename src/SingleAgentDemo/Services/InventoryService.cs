using SharedEntities;
using ZavaAgentsMetadata;

namespace SingleAgentDemo.Services;

public class InventoryService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<InventoryService> _logger;
    private string _framework = AgentMetadata.FrameworkIdentifiers.MafLocal; // Default to MAF Local

    public InventoryService(HttpClient httpClient, ILogger<InventoryService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Sets the agent framework to use for service calls
    /// </summary>
    /// <param name="framework">"llm" for LLM Direct Call, "maf_local" for MAF Local, "maf_foundry" for MAF Foundry, or "directcall" for Direct Call</param>
    public void SetFramework(string framework)
    {
        _framework = framework?.ToLowerInvariant() ?? AgentMetadata.FrameworkIdentifiers.MafLocal;
        _logger.LogInformation($"[InventoryService] Framework set to: {_framework}");
    }

    /// <summary>
    /// Maps framework identifier to the corresponding controller endpoint name
    /// </summary>
    private string GetEndpointForFramework(string framework)
    {
        return framework switch
        {
            AgentMetadata.FrameworkIdentifiers.MafLocal => "analyze_search_maf_local",
            AgentMetadata.FrameworkIdentifiers.MafFoundry => "analyze_search_maf_foundry",
            AgentMetadata.FrameworkIdentifiers.MafOllama => "analyze_search_maf_ollama",
            AgentMetadata.FrameworkIdentifiers.Llm => "analyze_search_llm",
            AgentMetadata.FrameworkIdentifiers.DirectCall => "analyze_search_direct_call",
            _ => "analyze_search_maf_local" // Default fallback
        };
    }

    public async Task<ToolRecommendation[]> EnrichWithInventoryAsync(ToolRecommendation[] tools)
    {
        try
        {
            var skus = tools.Select(t => t.Sku).ToArray();

            // create a prompt to search on the inventory service for the given SKUs
            var searchQuery = $"Search for the following SKUs: {string.Join(", ", skus)}";

            var searchRequest = new InventorySearchRequest { SearchQuery = searchQuery };
            
            var endpointName = GetEndpointForFramework(_framework);
            var endpoint = $"/api/inventory/{endpointName}";
            _logger.LogInformation($"[InventoryService] Calling endpoint: {endpoint}");
            
            //// Create a cancellation token with a 15-second timeout
            //using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await _httpClient.PostAsJsonAsync(endpoint, searchRequest);
            
            _logger.LogInformation($"InventoryService HTTP status code: {response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var inventoryResults = await response.Content.ReadFromJsonAsync<ToolRecommendation[]>();
                return inventoryResults ?? tools;
            }
            
            _logger.LogWarning("InventoryService returned non-success status: {StatusCode}", response.StatusCode);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "InventoryService call timed out after 15 seconds");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling InventoryService");
        }

        return tools; // Return original tools if inventory service fails or times out
    }
}