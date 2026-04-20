using Microsoft.AspNetCore.Mvc;
using SharedEntities;
using ZavaAgentsMetadata;

namespace CustomerInformationService.Endpoints;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Customer");

        group.MapGet("/{customerId}/llm", GetCustomerLlmAsync);
        group.MapGet("/{customerId}/maf_local", GetCustomerMAFLocalAsync);
        group.MapGet("/{customerId}/maf_foundry", GetCustomerMAFFoundryAsync);
        group.MapGet("/{customerId}/directcall", GetCustomerDirectCallAsync);

        group.MapPost("/match-tools/llm", MatchToolsLlmAsync);
        group.MapPost("/match-tools/maf_local", MatchToolsMAFAsync);
        group.MapPost("/match-tools/directcall", MatchToolsDirectCallAsync);
    }

    public static async Task<IResult> GetCustomerLlmAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] DataServiceClient.DataServiceClient dataServiceClient,
        string customerId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.Llm} Getting customer information for ID: {{CustomerId}}", customerId);
        return await GetCustomerFromDataServiceAsync(logger, dataServiceClient, customerId, AgentMetadata.LogPrefixes.Llm, cancellationToken);
    }

    public static async Task<IResult> GetCustomerMAFLocalAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] DataServiceClient.DataServiceClient dataServiceClient,
        string customerId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.MafLocal} Getting customer information for ID: {{CustomerId}}", customerId);
        return await GetCustomerFromDataServiceAsync(logger, dataServiceClient, customerId, AgentMetadata.LogPrefixes.MafLocal, cancellationToken);
    }

    public static async Task<IResult> GetCustomerMAFFoundryAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] DataServiceClient.DataServiceClient dataServiceClient,
        string customerId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.MafFoundry} Getting customer information for ID: {{CustomerId}}", customerId);
        return await GetCustomerFromDataServiceAsync(logger, dataServiceClient, customerId, AgentMetadata.LogPrefixes.MafFoundry, cancellationToken);
    }

    public static async Task<IResult> GetCustomerDirectCallAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] DataServiceClient.DataServiceClient dataServiceClient,
        string customerId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.DirectCall} Getting customer information for ID: {{CustomerId}}", customerId);
        await Task.Delay(1000, cancellationToken);
        return await GetCustomerFromDataServiceAsync(logger, dataServiceClient, customerId, AgentMetadata.LogPrefixes.DirectCall, CancellationToken.None);
    }

    public static async Task<IResult> MatchToolsLlmAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] DataServiceClient.DataServiceClient dataServiceClient,
        [FromBody] ToolMatchRequest request)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.Llm} Matching tools for customer {{CustomerId}}", request.CustomerId);
        return await MatchToolsInternal(logger, dataServiceClient, request, AgentMetadata.LogPrefixes.Llm);
    }

    public static async Task<IResult> MatchToolsMAFAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] DataServiceClient.DataServiceClient dataServiceClient,
        [FromBody] ToolMatchRequest request)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.MafLocal} Matching tools for customer {{CustomerId}}", request.CustomerId);
        return await MatchToolsInternal(logger, dataServiceClient, request, AgentMetadata.LogPrefixes.MafLocal);
    }

    public static async Task<IResult> MatchToolsDirectCallAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] DataServiceClient.DataServiceClient dataServiceClient,
        [FromBody] ToolMatchRequest request)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.DirectCall} Matching tools for customer {{CustomerId}}", request.CustomerId);
        return await MatchToolsInternal(logger, dataServiceClient, request, AgentMetadata.LogPrefixes.DirectCall);
    }

    private static async Task<IResult> GetCustomerFromDataServiceAsync(
        ILogger logger,
        DataServiceClient.DataServiceClient dataServiceClient,
        string customerId,
        string logPrefix,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("{Prefix} Retrieving customer {CustomerId} from DataService", logPrefix, customerId);

            var customer = await dataServiceClient.GetCustomerByIdAsync(customerId, cancellationToken);
            if (customer != null)
            {
                logger.LogInformation("{Prefix} Successfully retrieved customer {CustomerId} from DataService", logPrefix, customer.Id);
                return Results.Ok(customer);
            }

            logger.LogWarning("{Prefix} Customer {CustomerId} not found in DataService, returning fallback", logPrefix, customerId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Prefix} Error retrieving customer {CustomerId} from DataService", logPrefix, customerId);
        }

        return Results.Ok(await GetFallbackCustomer(logger, dataServiceClient, customerId));
    }

    private static async Task<IResult> MatchToolsInternal(
        ILogger logger,
        DataServiceClient.DataServiceClient dataServiceClient,
        ToolMatchRequest request,
        string logPrefix)
    {
        try
        {
            logger.LogInformation("{Prefix} Retrieving customer {CustomerId} from DataService for tool matching", logPrefix, request.CustomerId);

            var customer = await dataServiceClient.GetCustomerByIdAsync(request.CustomerId);
            if (customer == null)
            {
                logger.LogWarning("{Prefix} Customer {CustomerId} not found, using fallback", logPrefix, request.CustomerId);
                customer = await GetFallbackCustomer(logger, dataServiceClient, request.CustomerId);
            }

            var reusableTools = DetermineReusableTools(customer.OwnedTools, request.DetectedMaterials, request.Prompt);
            var missingTools = DetermineMissingTools(customer.OwnedTools, request.DetectedMaterials, request.Prompt);

            logger.LogInformation(
                "{Prefix} Tool matching completed for customer {CustomerId}: {ReusableCount} reusable, {MissingCount} missing",
                logPrefix,
                request.CustomerId,
                reusableTools.Length,
                missingTools.Length);

            return Results.Ok(new ToolMatchResult
            {
                ReusableTools = reusableTools,
                MissingTools = missingTools
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Prefix} Error matching tools for customer {CustomerId}", logPrefix, request.CustomerId);
            return Results.Text("An error occurred while matching tools", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<CustomerInformation> GetFallbackCustomer(
        ILogger logger,
        DataServiceClient.DataServiceClient dataServiceClient,
        string customerId)
    {
        try
        {
            var customer = await dataServiceClient.GetCustomerByIdAsync(customerId);
            if (customer != null)
            {
                logger.LogInformation("Retrieved customer {CustomerId} from DataService", customerId);
                return customer;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve customer from DataService, using default");
        }

        return new CustomerInformation
        {
            Id = customerId,
            Name = $"Customer {customerId}",
            OwnedTools = ["measuring tape", "basic hand tools"],
            Skills = ["basic DIY"]
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
