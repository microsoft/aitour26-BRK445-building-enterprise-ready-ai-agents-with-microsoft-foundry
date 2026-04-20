using Microsoft.AspNetCore.Mvc;
using SharedEntities;
using SingleAgentDemo.Services;
using ZavaAgentsMetadata;

namespace SingleAgentDemo.Endpoints;

public static class SingleAgentDirectCallEndpoints
{
    public static void MapSingleAgentDirectCallEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/singleagent/directcall");
        group.MapPost("/analyze", AnalyzeAsync).DisableAntiforgery();
    }

    public static async Task<IResult> AnalyzeAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] AnalyzePhotoService analyzePhotoService,
        [FromServices] CustomerInformationService customerInformationService,
        [FromServices] ToolReasoningService toolReasoningService,
        [FromServices] InventoryService inventoryService,
        [FromForm] IFormFile image,
        [FromForm] string prompt,
        [FromForm] string customerId)
    {
        try
        {
            analyzePhotoService.SetFramework(AgentMetadata.FrameworkIdentifiers.DirectCall);
            customerInformationService.SetFramework(AgentMetadata.FrameworkIdentifiers.DirectCall);
            toolReasoningService.SetFramework(AgentMetadata.FrameworkIdentifiers.DirectCall);
            inventoryService.SetFramework(AgentMetadata.FrameworkIdentifiers.DirectCall);

            logger.LogInformation("Starting analysis workflow for customer {CustomerId} using Direct HTTP Calls", customerId);

            logger.LogInformation("DirectCall Workflow: Step 1 - Photo Analysis");
            var photoAnalysis = await analyzePhotoService.AnalyzePhotoAsync(image, prompt);

            logger.LogInformation("DirectCall Workflow: Step 2 - Customer Information Retrieval");
            var customerInfo = await customerInformationService.GetCustomerInformationAsync(customerId);

            logger.LogInformation("DirectCall Workflow: Step 3 - Tool Reasoning");
            var reasoningRequest = new ReasoningRequest
            {
                PhotoAnalysis = photoAnalysis,
                Customer = customerInfo,
                Prompt = prompt
            };
            var reasoning = await toolReasoningService.GenerateReasoningAsync(reasoningRequest);

            logger.LogInformation("DirectCall Workflow: Step 4 - Tool Matching");
            var toolMatch = await customerInformationService.MatchToolsAsync(customerId, photoAnalysis.DetectedMaterials, prompt);

            logger.LogInformation("DirectCall Workflow: Step 5 - Inventory Enrichment");
            var enrichedTools = await inventoryService.EnrichWithInventoryAsync(toolMatch.MissingTools);

            logger.LogInformation("DirectCall Workflow: Complete - Synthesizing results");
            var response = new SingleAgentAnalysisResponse
            {
                Analysis = photoAnalysis.Description,
                ReusableTools = toolMatch.ReusableTools,
                RecommendedTools = enrichedTools.Select(t => new ToolRecommendation
                {
                    Name = t.Name,
                    Sku = t.Sku,
                    IsAvailable = t.IsAvailable,
                    Price = t.Price,
                    Description = t.Description
                }).ToArray(),
                Reasoning = $"[Direct HTTP Call Mode]\n{reasoning}"
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in analysis workflow for customer {CustomerId} using DirectCall", customerId);
            return Results.Text("An error occurred while processing your request", statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
