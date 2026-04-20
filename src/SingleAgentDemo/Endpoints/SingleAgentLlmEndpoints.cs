using Microsoft.AspNetCore.Mvc;
using SharedEntities;
using SingleAgentDemo.Services;
using ZavaAgentsMetadata;

namespace SingleAgentDemo.Endpoints;

public static class SingleAgentLlmEndpoints
{
    public static void MapSingleAgentLlmEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/singleagent/llm");
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
            analyzePhotoService.SetFramework(AgentMetadata.FrameworkIdentifiers.Llm);
            customerInformationService.SetFramework(AgentMetadata.FrameworkIdentifiers.Llm);
            toolReasoningService.SetFramework(AgentMetadata.FrameworkIdentifiers.Llm);
            inventoryService.SetFramework(AgentMetadata.FrameworkIdentifiers.Llm);

            logger.LogInformation("Starting analysis workflow for customer {CustomerId} using LLM Direct Call", customerId);

            logger.LogInformation("LLM Workflow: Step 1 - Photo Analysis");
            var photoAnalysis = await analyzePhotoService.AnalyzePhotoAsync(image, prompt);

            logger.LogInformation("LLM Workflow: Step 2 - Customer Information Retrieval");
            var customerInfo = await customerInformationService.GetCustomerInformationAsync(customerId);

            logger.LogInformation("LLM Workflow: Step 3 - AI-Powered Tool Reasoning");
            var reasoningRequest = new ReasoningRequest
            {
                PhotoAnalysis = photoAnalysis,
                Customer = customerInfo,
                Prompt = prompt
            };
            var reasoning = await toolReasoningService.GenerateReasoningAsync(reasoningRequest);

            logger.LogInformation("LLM Workflow: Step 4 - Tool Matching");
            var toolMatch = await customerInformationService.MatchToolsAsync(customerId, photoAnalysis.DetectedMaterials, prompt);

            logger.LogInformation("LLM Workflow: Step 5 - Inventory Enrichment");
            var enrichedTools = await inventoryService.EnrichWithInventoryAsync(toolMatch.MissingTools);

            logger.LogInformation("LLM Workflow: Complete - Synthesizing results");
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
                Reasoning = reasoning
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in analysis workflow for customer {CustomerId} using LLM", customerId);
            return Results.Text("An error occurred while processing your request", statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
