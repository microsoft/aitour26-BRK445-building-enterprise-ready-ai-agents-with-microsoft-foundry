using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using SharedEntities;
using SingleAgentDemo.Services;
using System.ComponentModel;
using System.Text.Json;
using ZavaAgentsMetadata;
using ZavaMAFFoundry;

namespace SingleAgentDemo.Endpoints;

public static class SingleAgentMafLocalEndpoints
{
    public static void MapSingleAgentMafLocalEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/singleagent/maf_local");
        group.MapPost("/analyze", AnalyzeAsync).DisableAntiforgery();
    }

    public static async Task<IResult> AnalyzeAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] MAFFoundryAgentProvider foundryAgentProvider,
        [FromServices] AnalyzePhotoService analyzePhotoService,
        [FromServices] CustomerInformationService customerInformationService,
        [FromServices] ToolReasoningService toolReasoningService,
        [FromServices] InventoryService inventoryService,
        [FromForm] IFormFile image,
        [FromForm] string prompt,
        [FromForm] string customerId)
    {
        analyzePhotoService.SetFramework(AgentMetadata.FrameworkIdentifiers.MafLocal);
        customerInformationService.SetFramework(AgentMetadata.FrameworkIdentifiers.MafLocal);
        toolReasoningService.SetFramework(AgentMetadata.FrameworkIdentifiers.MafLocal);
        inventoryService.SetFramework(AgentMetadata.FrameworkIdentifiers.MafLocal);

        IFormFile? currentImage = null;
        string? currentPrompt = null;
        string? currentCustomerId = null;
        PhotoAnalysisResult? photoAnalysisResult = null;
        CustomerInformation? customerInfo = null;
        string? toolReasoningResult = null;
        ToolRecommendation[]? inventoryResult = null;

        async Task<string> PerformPhotoAnalysis(
            [Description("The description of the analysis task")] string taskDescription)
        {
            var startTime = DateTime.UtcNow;
            logger.LogInformation("MCP Tool: PerformPhotoAnalysis called for task: {Task}", taskDescription);

            try
            {
                if (currentImage == null || currentPrompt == null)
                {
                    logger.LogWarning("PerformPhotoAnalysis: Missing image or prompt data");
                    photoAnalysisResult = new PhotoAnalysisResult
                    {
                        Description = $"Analysis for task: {taskDescription}. Detected typical DIY project.",
                        DetectedMaterials = ["paint", "wall", "surface"]
                    };
                    return JsonSerializer.Serialize(photoAnalysisResult);
                }

                photoAnalysisResult = await analyzePhotoService.AnalyzePhotoAsync(currentImage, currentPrompt);
                var duration = DateTime.UtcNow - startTime;

                logger.LogInformation(
                    "MCP Tool: PerformPhotoAnalysis completed in {Duration}ms. Detected {Count} materials",
                    duration.TotalMilliseconds,
                    photoAnalysisResult.DetectedMaterials.Length);

                return JsonSerializer.Serialize(photoAnalysisResult);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MCP Tool: PerformPhotoAnalysis failed");
                photoAnalysisResult = new PhotoAnalysisResult
                {
                    Description = $"Fallback analysis for task: {taskDescription}",
                    DetectedMaterials = ["paint", "wall", "surface"]
                };
                return JsonSerializer.Serialize(photoAnalysisResult);
            }
        }

        async Task<string> GetCustomerInformation(
            [Description("The customer ID to retrieve information for")] string requestedCustomerId)
        {
            var startTime = DateTime.UtcNow;
            logger.LogInformation("MCP Tool: GetCustomerInformation called for customer: {CustomerId}", requestedCustomerId);

            try
            {
                var customerIdToUse = string.IsNullOrEmpty(requestedCustomerId) ? currentCustomerId : requestedCustomerId;
                if (string.IsNullOrEmpty(customerIdToUse))
                {
                    logger.LogWarning("GetCustomerInformation: Missing customer ID");
                    customerInfo = new CustomerInformation
                    {
                        Id = "unknown",
                        Name = "Unknown Customer",
                        OwnedTools = ["hammer", "screwdriver"],
                        Skills = ["basic DIY"]
                    };
                    return JsonSerializer.Serialize(customerInfo);
                }

                customerInfo = await customerInformationService.GetCustomerInformationAsync(customerIdToUse);
                var duration = DateTime.UtcNow - startTime;

                logger.LogInformation(
                    "MCP Tool: GetCustomerInformation completed in {Duration}ms. Customer has {Count} tools",
                    duration.TotalMilliseconds,
                    customerInfo.OwnedTools.Length);

                return JsonSerializer.Serialize(customerInfo);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MCP Tool: GetCustomerInformation failed");
                customerInfo = new CustomerInformation
                {
                    Id = requestedCustomerId ?? "unknown",
                    Name = $"Customer {requestedCustomerId}",
                    OwnedTools = ["hammer", "screwdriver", "measuring tape"],
                    Skills = ["basic DIY"]
                };
                return JsonSerializer.Serialize(customerInfo);
            }
        }

        async Task<string> PerformToolReasoning(
            [Description("Context about the photo analysis and customer")] string context)
        {
            var startTime = DateTime.UtcNow;
            logger.LogInformation("MCP Tool: PerformToolReasoning called");

            try
            {
                var photoAnalysis = photoAnalysisResult ?? new PhotoAnalysisResult
                {
                    Description = "Default analysis",
                    DetectedMaterials = ["paint", "wall"]
                };

                var customer = customerInfo ?? new CustomerInformation
                {
                    Id = currentCustomerId ?? "unknown",
                    Name = "Customer",
                    OwnedTools = ["hammer"],
                    Skills = ["basic DIY"]
                };

                var reasoningRequest = new ReasoningRequest
                {
                    PhotoAnalysis = photoAnalysis,
                    Customer = customer,
                    Prompt = currentPrompt ?? "DIY project"
                };

                toolReasoningResult = await toolReasoningService.GenerateReasoningAsync(reasoningRequest);
                var duration = DateTime.UtcNow - startTime;

                logger.LogInformation(
                    "MCP Tool: PerformToolReasoning completed in {Duration}ms, result length: {Length}",
                    duration.TotalMilliseconds,
                    toolReasoningResult.Length);

                return toolReasoningResult;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MCP Tool: PerformToolReasoning failed");
                toolReasoningResult = $"Based on the analysis, additional tools are recommended for {currentPrompt ?? "the project"}.";
                return toolReasoningResult;
            }
        }

        async Task<string> PerformInventoryCheck(
            [Description("List of tool SKUs or names to check, comma-separated")] string tools)
        {
            var startTime = DateTime.UtcNow;
            logger.LogInformation("MCP Tool: PerformInventoryCheck called for tools: {Tools}", tools);

            try
            {
                var toolArray = tools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (toolArray.Length == 0)
                {
                    toolArray = ["PAINT-ROLLER-9IN", "BRUSH-SET-3PC", "DROP-CLOTH-9X12"];
                }

                var toolRecommendations = toolArray.Select(tool => new ToolRecommendation
                {
                    Sku = tool,
                    Name = tool
                }).ToArray();

                inventoryResult = await inventoryService.EnrichWithInventoryAsync(toolRecommendations);
                var duration = DateTime.UtcNow - startTime;

                logger.LogInformation(
                    "MCP Tool: PerformInventoryCheck completed in {Duration}ms. Checked {Count} tools",
                    duration.TotalMilliseconds,
                    inventoryResult.Length);

                return JsonSerializer.Serialize(inventoryResult);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MCP Tool: PerformInventoryCheck failed");
                inventoryResult =
                [
                    new ToolRecommendation { Name = "Paint Roller", Sku = "PAINT-ROLLER-9IN", IsAvailable = true, Price = 12.99m, Description = "9-inch paint roller" },
                    new ToolRecommendation { Name = "Paint Brush Set", Sku = "BRUSH-SET-3PC", IsAvailable = true, Price = 24.99m, Description = "3-piece brush set" }
                ];
                return JsonSerializer.Serialize(inventoryResult);
            }
        }

        List<AITool> tools =
        [
            AIFunctionFactory.Create(PerformPhotoAnalysis),
            AIFunctionFactory.Create(GetCustomerInformation),
            AIFunctionFactory.Create(PerformToolReasoning),
            AIFunctionFactory.Create(PerformInventoryCheck)
        ];

        var instructions = @"You are an AI assistant that helps analyze images and recommend tools for DIY projects.

CRITICAL: You MUST execute ALL four tools in the exact order specified below. Do not skip any tool.

You have access to four tools:
1. PerformPhotoAnalysis - Analyzes uploaded photos to identify materials and surfaces
2. GetCustomerInformation - Retrieves customer profile including owned tools and skills
3. PerformToolReasoning - Determines what tools are needed based on photo analysis and customer info
4. PerformInventoryCheck - Checks inventory availability and pricing for recommended tools

Mandatory execution order:
STEP 1: Call PerformPhotoAnalysis with the task description to analyze the uploaded image
STEP 2: Call GetCustomerInformation with the customer ID to get their profile
STEP 3: Call PerformToolReasoning with context from steps 1 and 2 to determine needed tools
STEP 4: Call PerformInventoryCheck with the list of tool SKUs from step 3 (comma-separated)

IMPORTANT: 
- Always execute all four steps in order
- Pass specific parameters: task description, customer ID, context, and SKU list
- Do not make assumptions; always get data from tools
- Return the complete analysis from all tool results

After calling all tools, provide a comprehensive summary including photo analysis, customer profile, recommended tools, and pricing information.";

        IChatClient chatClient = foundryAgentProvider.GetChatClient();
        var orchestratorAgent = new ChatClientAgent(
            chatClient,
            name: "OrchestratorAgent",
            instructions: instructions,
            description: "Orchestrates the analysis process using multiple tools.",
            tools: tools);

        logger.LogInformation("SingleAgentMafLocalEndpoints initialized with orchestrator agent and 4 MCP tools");

        try
        {
            logger.LogInformation("Starting analysis for customer {CustomerId} using Single Agent with MCP Tools", customerId);

            currentImage = image;
            currentPrompt = prompt;
            currentCustomerId = customerId;

            var session = await orchestratorAgent.CreateSessionAsync();
            var analysisPrompt = $@"Analyze the uploaded image for customer {customerId} with the task: {prompt}

Please follow these steps:
1. Use PerformPhotoAnalysis to analyze the image and identify materials
2. Use GetCustomerInformation to retrieve the customer's profile
3. Use PerformToolReasoning to determine what tools are needed
4. Use PerformInventoryCheck to check availability and pricing

Provide a comprehensive analysis based on all tool results.";

            logger.LogInformation("Executing orchestrator agent with 4 MCP tools");

            var response = await orchestratorAgent.RunAsync(message: analysisPrompt, session: session);
            var result = response.Text;

            logger.LogInformation("Agent execution completed, result length: {Length}", result.Length);

            var analysisResponse = BuildResponseFromToolOutputs(result, prompt, photoAnalysisResult, customerInfo, inventoryResult);

            logger.LogInformation("Analysis complete for customer {CustomerId} using Single Agent with MCP Tools", customerId);

            return Results.Ok(analysisResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in analysis for customer {CustomerId} using Single Agent with MCP Tools", customerId);
            return Results.Ok(BuildFallbackResponse(prompt, customerId));
        }
    }

    private static SingleAgentAnalysisResponse BuildResponseFromToolOutputs(
        string agentOutput,
        string prompt,
        PhotoAnalysisResult? photoAnalysisResult,
        CustomerInformation? customerInfo,
        ToolRecommendation[]? inventoryResult)
    {
        return new SingleAgentAnalysisResponse
        {
            Analysis = photoAnalysisResult?.Description ?? "Photo analysis completed",
            ReusableTools = customerInfo?.OwnedTools ?? ["hammer", "screwdriver"],
            RecommendedTools = inventoryResult ??
            [
                new ToolRecommendation { Name = "Paint Roller", Sku = "PAINT-ROLLER-9IN", IsAvailable = true, Price = 12.99m, Description = "9-inch paint roller" },
                new ToolRecommendation { Name = "Paint Brush Set", Sku = "BRUSH-SET-3PC", IsAvailable = true, Price = 24.99m, Description = "3-piece brush set" }
            ],
            Reasoning = GenerateSummary(agentOutput, prompt)
        };
    }

    private static SingleAgentAnalysisResponse BuildFallbackResponse(string prompt, string customerId)
    {
        return new SingleAgentAnalysisResponse
        {
            Analysis = $"Image analysis completed for task: {prompt}. Detected typical DIY project requirements.",
            ReusableTools = ["hammer", "screwdriver", "measuring tape"],
            RecommendedTools =
            [
                new ToolRecommendation { Name = "Paint Roller", Sku = "PAINT-ROLLER-9IN", IsAvailable = true, Price = 12.99m, Description = "9-inch paint roller for smooth walls" },
                new ToolRecommendation { Name = "Paint Brush Set", Sku = "BRUSH-SET-3PC", IsAvailable = true, Price = 24.99m, Description = "3-piece brush set for detail work" },
                new ToolRecommendation { Name = "Drop Cloth", Sku = "DROP-CLOTH-9X12", IsAvailable = true, Price = 8.99m, Description = "Plastic drop cloth protection" }
            ],
            Reasoning = $"[Fallback Response]\nAnalysis completed for customer {customerId} with task: {prompt}\nRecommended tools are available."
        };
    }

    private static string GenerateSummary(string agentOutput, string prompt)
    {
        return string.Join(
            Environment.NewLine,
            "=== Microsoft Agent Framework Analysis (Single Agent with MCP Tools) ===",
            "",
            "Execution Mode: Single Agent with 4 MCP Tools",
            "- Tool 1: PerformPhotoAnalysis (via HTTP to AnalyzePhotoService)",
            "- Tool 2: GetCustomerInformation (via HTTP to CustomerInformationService)",
            "- Tool 3: PerformToolReasoning (via HTTP to ToolReasoningService)",
            "- Tool 4: PerformInventoryCheck (via HTTP to InventoryService)",
            "",
            $"Project Task: {prompt}",
            "",
            "Agent Output:",
            agentOutput,
            "",
            "Summary:",
            "This analysis used a single orchestrator agent with MCP tools that call HTTP services.",
            "Each tool makes an HTTP request to the appropriate microservice for specialized processing.");
    }
}
