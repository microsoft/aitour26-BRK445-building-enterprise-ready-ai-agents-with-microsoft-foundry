using Microsoft.AspNetCore.Mvc;
using SharedEntities;
using ZavaAgentsMetadata;

namespace CustomerInformationService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomerController : ControllerBase
{
    private readonly ILogger<CustomerController> _logger;
    private readonly DataServiceClient.DataServiceClient _dataServiceClient;

    public CustomerController(
        ILogger<CustomerController> logger,
        DataServiceClient.DataServiceClient dataServiceClient)
    {
        _logger = logger;
        _dataServiceClient = dataServiceClient;
    }

    [HttpGet("{customerId}/llm")]
    public async Task<ActionResult<CustomerInformation>> GetCustomerLlmAsync(string customerId, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AgentMetadata.LogPrefixes.Llm} Getting customer information for ID: {{CustomerId}}", customerId);

        // Use DataServiceClient directly instead of LLM/Agent
        return await GetCustomerFromDataServiceAsync(customerId, AgentMetadata.LogPrefixes.Llm, cancellationToken);
    }

    [HttpGet("{customerId}/maf_local")]  // Using constant AgentMetadata.FrameworkIdentifiers.MafLocal
    public async Task<ActionResult<CustomerInformation>> GetCustomerMAFLocalAsync(string customerId, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AgentMetadata.LogPrefixes.MafLocal} Getting customer information for ID: {{CustomerId}}", customerId);

        // Use DataServiceClient directly instead of Agent
        return await GetCustomerFromDataServiceAsync(customerId, AgentMetadata.LogPrefixes.MafLocal, cancellationToken);
    }

    [HttpGet("{customerId}/maf_foundry")]  // Using constant AgentMetadata.FrameworkIdentifiers.MafFoundry
    public async Task<ActionResult<CustomerInformation>> GetCustomerMAFFoundryAsync(string customerId, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AgentMetadata.LogPrefixes.MafFoundry} Getting customer information for ID: {{CustomerId}}", customerId);

        // Use DataServiceClient directly instead of Agent
        return await GetCustomerFromDataServiceAsync(customerId, AgentMetadata.LogPrefixes.MafFoundry, cancellationToken);
    }

    [HttpPost("match-tools/llm")]
    public async Task<ActionResult<ToolMatchResult>> MatchToolsLlm([FromBody] ToolMatchRequest request)
    {
        _logger.LogInformation($"{AgentMetadata.LogPrefixes.Llm} Matching tools for customer {{CustomerId}}", request.CustomerId);
        // Use DataServiceClient to get customer information
        return await MatchToolsInternal(request, AgentMetadata.LogPrefixes.Llm);
    }

    [HttpPost("match-tools/maf_local")]  // Using constant AgentMetadata.FrameworkIdentifiers.MafLocal
    public async Task<ActionResult<ToolMatchResult>> MatchToolsMAF([FromBody] ToolMatchRequest request)
    {
        _logger.LogInformation($"{AgentMetadata.LogPrefixes.MafLocal} Matching tools for customer {{CustomerId}}", request.CustomerId);
        // Use DataServiceClient to get customer information
        return await MatchToolsInternal(request, AgentMetadata.LogPrefixes.MafLocal);
    }

    [HttpGet("{customerId}/directcall")]
    public async Task<ActionResult<CustomerInformation>> GetCustomerDirectCallAsync(string customerId)
    {
        _logger.LogInformation($"{AgentMetadata.LogPrefixes.DirectCall} Getting customer information for ID: {{CustomerId}}", customerId);

        // add a sleep of 1 seconds to emulate the analysis time
        Thread.Sleep(1000);

        // Use DataServiceClient to get customer information
        return await GetCustomerFromDataServiceAsync(customerId, AgentMetadata.LogPrefixes.DirectCall, CancellationToken.None);
    }

    [HttpPost("match-tools/directcall")]
    public async Task<ActionResult<ToolMatchResult>> MatchToolsDirectCall([FromBody] ToolMatchRequest request)
    {
        _logger.LogInformation($"{AgentMetadata.LogPrefixes.DirectCall} Matching tools for customer {{CustomerId}}", request.CustomerId);
        // Use DataServiceClient to get customer information
        return await MatchToolsInternal(request, AgentMetadata.LogPrefixes.DirectCall);
    }

    private async Task<ActionResult<CustomerInformation>> GetCustomerFromDataServiceAsync(
        string customerId,
        string logPrefix,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("{Prefix} Retrieving customer {CustomerId} from DataService", logPrefix, customerId);
            
            var customer = await _dataServiceClient.GetCustomerByIdAsync(customerId, cancellationToken);
            
            if (customer != null)
            {
                _logger.LogInformation("{Prefix} Successfully retrieved customer {CustomerId} from DataService", logPrefix, customer.Id);
                return Ok(customer);
            }
            
            _logger.LogWarning("{Prefix} Customer {CustomerId} not found in DataService, returning fallback", logPrefix, customerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Prefix} Error retrieving customer {CustomerId} from DataService", logPrefix, customerId);
        }

        return Ok(await GetFallbackCustomer(customerId));
    }

    private async Task<ActionResult<ToolMatchResult>> MatchToolsInternal(ToolMatchRequest request, string logPrefix)
    {
        try
        {
            _logger.LogInformation("{Prefix} Retrieving customer {CustomerId} from DataService for tool matching", logPrefix, request.CustomerId);
            
            // Get customer from DataServiceClient
            var customer = await _dataServiceClient.GetCustomerByIdAsync(request.CustomerId);
            
            if (customer == null)
            {
                _logger.LogWarning("{Prefix} Customer {CustomerId} not found, using fallback", logPrefix, request.CustomerId);
                customer = await GetFallbackCustomer(request.CustomerId);
            }
            
            var reusableTools = DetermineReusableTools(customer.OwnedTools, request.DetectedMaterials, request.Prompt);
            var missingTools = DetermineMissingTools(customer.OwnedTools, request.DetectedMaterials, request.Prompt);

            _logger.LogInformation("{Prefix} Tool matching completed for customer {CustomerId}: {ReusableCount} reusable, {MissingCount} missing", 
                logPrefix, request.CustomerId, reusableTools.Length, missingTools.Length);

            return Ok(new ToolMatchResult
            {
                ReusableTools = reusableTools,
                MissingTools = missingTools
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Prefix} Error matching tools for customer {CustomerId}", logPrefix, request.CustomerId);
            return StatusCode(500, "An error occurred while matching tools");
        }
    }

    private async Task<CustomerInformation> GetFallbackCustomer(string customerId)
    {
        // Try to get from DataService first
        try
        {
            var customer = await _dataServiceClient.GetCustomerByIdAsync(customerId);
            if (customer != null)
            {
                _logger.LogInformation("Retrieved customer {CustomerId} from DataService", customerId);
                return customer;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve customer from DataService, using default");
        }

        // Return default if not found
        return new CustomerInformation
        {
            Id = customerId,
            Name = $"Customer {customerId}",
            OwnedTools = new[] { "measuring tape", "basic hand tools" },
            Skills = new[] { "basic DIY" }
        };
    }



    private static string[] DetermineReusableTools(string[] ownedTools, string[] detectedMaterials, string prompt)
    {
        var reusable = new List<string>();
        var promptLower = prompt.ToLowerInvariant();

        foreach (var tool in ownedTools)
        {
            var toolLower = tool.ToLowerInvariant();

            if (toolLower.Contains("measuring tape") || toolLower.Contains("screwdriver") || toolLower.Contains("hammer"))
            {
                reusable.Add(tool);
            }

            if (promptLower.Contains("paint") && toolLower.Contains("brush"))
            {
                reusable.Add(tool);
            }

            if (promptLower.Contains("wood") && (toolLower.Contains("saw") || toolLower.Contains("drill")))
            {
                reusable.Add(tool);
            }
        }

        return reusable.ToArray();
    }

    private static ToolRecommendation[] DetermineMissingTools(string[] ownedTools, string[] detectedMaterials, string prompt)
    {
        var missing = new List<ToolRecommendation>();
        var promptLower = prompt.ToLowerInvariant();
        var ownedToolsLower = ownedTools.Select(t => t.ToLowerInvariant()).ToArray();

        if (promptLower.Contains("paint") || detectedMaterials.Any(m => m.Contains("paint", StringComparison.OrdinalIgnoreCase)))
        {
            if (!ownedToolsLower.Any(t => t.Contains("roller")))
            {
                missing.Add(new ToolRecommendation { Name = "Paint Roller", Sku = "PAINT-ROLLER-9IN", IsAvailable = true, Price = 12.99m, Description = "9-inch paint roller for smooth walls" });
            }

            if (!ownedToolsLower.Any(t => t.Contains("brush")))
            {
                missing.Add(new ToolRecommendation { Name = "Paint Brush Set", Sku = "BRUSH-SET-3PC", IsAvailable = true, Price = 24.99m, Description = "3-piece brush set for detail work" });
            }

            missing.Add(new ToolRecommendation { Name = "Drop Cloth", Sku = "DROP-CLOTH-9X12", IsAvailable = true, Price = 8.99m, Description = "Plastic drop cloth protection" });
        }

        if (promptLower.Contains("wood") || detectedMaterials.Any(m => m.Contains("wood", StringComparison.OrdinalIgnoreCase)))
        {
            if (!ownedToolsLower.Any(t => t.Contains("saw")))
            {
                missing.Add(new ToolRecommendation { Name = "Circular Saw", Sku = "SAW-CIRCULAR-7IN", IsAvailable = true, Price = 89.99m, Description = "7.25-inch circular saw for wood cutting" });
            }

            missing.Add(new ToolRecommendation { Name = "Wood Stain", Sku = "STAIN-WOOD-QT", IsAvailable = true, Price = 15.99m, Description = "1-quart wood stain in natural color" });
        }

        if (missing.Count == 0)
        {
            missing.Add(new ToolRecommendation { Name = "Safety Glasses", Sku = "SAFETY-GLASSES", IsAvailable = true, Price = 5.99m, Description = "Safety glasses for eye protection" });
            missing.Add(new ToolRecommendation { Name = "Work Gloves", Sku = "GLOVES-WORK-L", IsAvailable = true, Price = 7.99m, Description = "Heavy-duty work gloves" });
        }

        return missing.ToArray();
    }


}
