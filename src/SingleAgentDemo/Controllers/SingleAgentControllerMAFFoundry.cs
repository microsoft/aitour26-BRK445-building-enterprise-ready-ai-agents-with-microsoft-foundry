using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using SharedEntities;
using SingleAgentDemo.Services;
using System.ComponentModel;
using System.Text.Json;
using ZavaAgentsMetadata;
using ZavaMAFFoundry;

namespace SingleAgentDemo.Controllers;

/// <summary>
/// Controller for single agent analysis using Microsoft Agent Framework with Foundry Agent.
/// Uses a single agent from Foundry with MCP tools that call HTTP services for photo analysis, customer info, reasoning, and inventory.
/// </summary>
[ApiController]
[Route("api/singleagent/maf_foundry")]  // Using constant AgentMetadata.FrameworkIdentifiers.MafFoundry
public class SingleAgentControllerMAFFoundry : ControllerBase
{
    private readonly ILogger<SingleAgentControllerMAFFoundry> _logger;
    private readonly AIAgent _orchestratorAgent;
    private readonly AnalyzePhotoService _analyzePhotoService;
    private readonly CustomerInformationService _customerInformationService;
    private readonly ToolReasoningService _toolReasoningService;
    private readonly InventoryService _inventoryService;
    private readonly IConfiguration _config;
    private readonly List<AITool> _tools;
        
    public SingleAgentControllerMAFFoundry(
        ILogger<SingleAgentControllerMAFFoundry> logger,
        IConfiguration config,
        MAFFoundryAgentProvider foundryAgentProvider,
        AnalyzePhotoService analyzePhotoService,
        CustomerInformationService customerInformationService,
        ToolReasoningService toolReasoningService,
        InventoryService inventoryService)
    {
        _logger = logger;
        _analyzePhotoService = analyzePhotoService;
        _customerInformationService = customerInformationService;
        _toolReasoningService = toolReasoningService;
        _inventoryService = inventoryService;

        // Set framework for all services to use maf endpoints
        _analyzePhotoService.SetFramework(AgentMetadata.FrameworkIdentifiers.MafFoundry);
        _customerInformationService.SetFramework(AgentMetadata.FrameworkIdentifiers.MafFoundry);
        _toolReasoningService.SetFramework(AgentMetadata.FrameworkIdentifiers.MafFoundry);
        _inventoryService.SetFramework(AgentMetadata.FrameworkIdentifiers.MafFoundry);

        // Create tools that call HTTP services
        _tools =
        [
            AIFunctionFactory.Create(PerformPhotoAnalysis),
            AIFunctionFactory.Create(GetCustomerInformation),
            AIFunctionFactory.Create(PerformToolReasoning),
            AIFunctionFactory.Create(PerformInventoryCheck)
        ];

        var agentName = AgentMetadata.GetAgentName(AgentType.ZavaSingleAgent);
        var chatDeploymentName = config["AI_ChatDeploymentName"] ?? "gpt-5-mini";
        var instructions = AgentMetadata.GetAgentInstructions(AgentType.ZavaSingleAgent);

        // Get the agent from Microsoft Foundry
        _orchestratorAgent = foundryAgentProvider.GetOrCreateAIAgent(
            agentName: agentName, 
            model: chatDeploymentName, 
            agentInstructions: instructions,
            tools: _tools);

        _logger.LogInformation($"SingleAgentControllerMAFFoundry initialized with Foundry agent '{agentName}' and {_tools.Count} MCP tools");
    }

    /// <summary>
    /// Analyze an image using a single Foundry agent with MCP tools that call HTTP services.
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<SingleAgentAnalysisResponse>> AnalyzeAsync(
        [FromForm] IFormFile image,
        [FromForm] string prompt,
        [FromForm] string customerId)
    {
        try
        {
            _logger.LogInformation(
                "Starting analysis for customer {CustomerId} using MAF Foundry Agent with MCP Tools",
                customerId);

            // Store image data in a temporary field for tool access
            _currentImage = image;
            _currentPrompt = prompt;
            _currentCustomerId = customerId;

            // Create thread and chat options with tools
            var agentThread = _orchestratorAgent.GetNewThread();
            //var chatOptions = new ChatOptions
            //{
            //    Tools = [.. _tools]
            //};

            var analysisPrompt = $@"Analyze the uploaded image for customer {customerId} with the task: {prompt}

Please follow these steps:
1. Use PerformPhotoAnalysis to analyze the image and identify materials
2. Use GetCustomerInformation to retrieve the customer's profile
3. Use PerformToolReasoning to determine what tools are needed
4. Use PerformInventoryCheck to check availability and pricing

Provide a comprehensive analysis based on all tool results.";

            _logger.LogInformation("Executing Foundry orchestrator agent with 4 MCP tools");

            // Execute the agent with all tools
            var response = await _orchestratorAgent.RunAsync(
                message: analysisPrompt,
                thread: agentThread);
                //, options: new ChatClientAgentRunOptions(chatOptions));

            var result = response.Text;

            _logger.LogInformation("Agent execution completed, result length: {Length}", result.Length);

            // Build response from collected tool outputs
            var analysisResponse = BuildResponseFromToolOutputs(result, prompt);

            _logger.LogInformation("Analysis complete for customer {CustomerId} using MAF Foundry Agent with MCP Tools", customerId);

            return Ok(analysisResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in analysis for customer {CustomerId} using MAF Foundry Agent with MCP Tools", customerId);
            
            // Return fallback response on error
            return Ok(BuildFallbackResponse(prompt, customerId));
        }
        finally
        {
            // Clean up temporary data
            _currentImage = null;
            _currentPrompt = null;
            _currentCustomerId = null;
        }
    }

    // Temporary storage for current request data (used by tools)
    private IFormFile? _currentImage;
    private string? _currentPrompt;
    private string? _currentCustomerId;

    // ===== MCP Tool Implementations =====
    // Each tool calls an HTTP service to perform its task

    [Description("Analyzes an uploaded photo to identify materials, surfaces, and project requirements.")]
    private async Task<string> PerformPhotoAnalysis(
        [Description("The description of the analysis task")]
        string taskDescription)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("MCP Tool: PerformPhotoAnalysis called for task: {Task}", taskDescription);

        try
        {
            if (_currentImage == null || _currentPrompt == null)
            {
                _logger.LogWarning("PerformPhotoAnalysis: Missing image or prompt data");
                _photoAnalysisResult = new PhotoAnalysisResult
                {
                    Description = $"Analysis for task: {taskDescription}. Detected typical DIY project.",
                    DetectedMaterials = ["paint", "wall", "surface"]
                };
                return JsonSerializer.Serialize(_photoAnalysisResult);
            }

            _photoAnalysisResult = await _analyzePhotoService.AnalyzePhotoAsync(_currentImage, _currentPrompt);
            var duration = DateTime.UtcNow - startTime;
            
            _logger.LogInformation(
                "MCP Tool: PerformPhotoAnalysis completed in {Duration}ms. Detected {Count} materials",
                duration.TotalMilliseconds, _photoAnalysisResult.DetectedMaterials.Length);

            return JsonSerializer.Serialize(_photoAnalysisResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP Tool: PerformPhotoAnalysis failed");
            _photoAnalysisResult = new PhotoAnalysisResult
            {
                Description = $"Fallback analysis for task: {taskDescription}",
                DetectedMaterials = ["paint", "wall", "surface"]
            };
            return JsonSerializer.Serialize(_photoAnalysisResult);
        }
    }

    [Description("Retrieves customer profile information including owned tools and skills.")]
    private async Task<string> GetCustomerInformation(
        [Description("The customer ID to retrieve information for")]
        string customerId)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("MCP Tool: GetCustomerInformation called for customer: {CustomerId}", customerId);

        try
        {
            var customerIdToUse = string.IsNullOrEmpty(customerId) ? _currentCustomerId : customerId;
            if (string.IsNullOrEmpty(customerIdToUse))
            {
                _logger.LogWarning("GetCustomerInformation: Missing customer ID");
                _customerInfo = new CustomerInformation
                {
                    Id = "unknown",
                    Name = "Unknown Customer",
                    OwnedTools = ["hammer", "screwdriver"],
                    Skills = ["basic DIY"]
                };
                return JsonSerializer.Serialize(_customerInfo);
            }

            _customerInfo = await _customerInformationService.GetCustomerInformationAsync(customerIdToUse);
            var duration = DateTime.UtcNow - startTime;
            
            _logger.LogInformation(
                "MCP Tool: GetCustomerInformation completed in {Duration}ms. Customer has {Count} tools",
                duration.TotalMilliseconds, _customerInfo.OwnedTools.Length);

            return JsonSerializer.Serialize(_customerInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP Tool: GetCustomerInformation failed");
            _customerInfo = new CustomerInformation
            {
                Id = customerId ?? "unknown",
                Name = $"Customer {customerId}",
                OwnedTools = ["hammer", "screwdriver", "measuring tape"],
                Skills = ["basic DIY"]
            };
            return JsonSerializer.Serialize(_customerInfo);
        }
    }

    [Description("Determines what tools are needed based on photo analysis and customer information.")]
    private async Task<string> PerformToolReasoning(
        [Description("Context about the photo analysis and customer")]
        string context)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("MCP Tool: PerformToolReasoning called");

        try
        {
            // Use stored photo analysis and customer info, or provide defaults
            var photoAnalysis = _photoAnalysisResult ?? new PhotoAnalysisResult
            {
                Description = "Default analysis",
                DetectedMaterials = ["paint", "wall"]
            };

            var customer = _customerInfo ?? new CustomerInformation
            {
                Id = _currentCustomerId ?? "unknown",
                Name = "Customer",
                OwnedTools = ["hammer"],
                Skills = ["basic DIY"]
            };

            var reasoningRequest = new ReasoningRequest
            {
                PhotoAnalysis = photoAnalysis,
                Customer = customer,
                Prompt = _currentPrompt ?? "DIY project"
            };

            _toolReasoningResult = await _toolReasoningService.GenerateReasoningAsync(reasoningRequest);
            var duration = DateTime.UtcNow - startTime;
            
            _logger.LogInformation(
                "MCP Tool: PerformToolReasoning completed in {Duration}ms, result length: {Length}",
                duration.TotalMilliseconds, _toolReasoningResult.Length);

            return _toolReasoningResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP Tool: PerformToolReasoning failed");
            _toolReasoningResult = $"Based on the analysis, additional tools are recommended for {_currentPrompt ?? "the project"}.";
            return _toolReasoningResult;
        }
    }

    [Description("Checks inventory availability and pricing for recommended tools.")]
    private async Task<string> PerformInventoryCheck(
        [Description("List of tool SKUs or names to check, comma-separated")]
        string tools)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("MCP Tool: PerformInventoryCheck called for tools: {Tools}", tools);

        try
        {
            // Parse tools from comma-separated string or extract from reasoning result
            var toolArray = tools.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            
            if (toolArray.Length == 0)
            {
                // Extract tool names from reasoning result as fallback
                toolArray = ["PAINT-ROLLER-9IN", "BRUSH-SET-3PC", "DROP-CLOTH-9X12"];
            }

            var toolRecommendations = toolArray.Select(tool => new ToolRecommendation 
            { 
                Sku = tool,
                Name = tool 
            }).ToArray();

            _inventoryResult = await _inventoryService.EnrichWithInventoryAsync(toolRecommendations);
            var duration = DateTime.UtcNow - startTime;
            
            _logger.LogInformation(
                "MCP Tool: PerformInventoryCheck completed in {Duration}ms. Checked {Count} tools",
                duration.TotalMilliseconds, _inventoryResult.Length);

            return JsonSerializer.Serialize(_inventoryResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP Tool: PerformInventoryCheck failed");
            _inventoryResult =
            [
                new ToolRecommendation { Name = "Paint Roller", Sku = "PAINT-ROLLER-9IN", IsAvailable = true, Price = 12.99m, Description = "9-inch paint roller" },
                new ToolRecommendation { Name = "Paint Brush Set", Sku = "BRUSH-SET-3PC", IsAvailable = true, Price = 24.99m, Description = "3-piece brush set" }
            ];
            return JsonSerializer.Serialize(_inventoryResult);
        }
    }

    // Storage for tool outputs
    private PhotoAnalysisResult? _photoAnalysisResult;
    private CustomerInformation? _customerInfo;
    private string? _toolReasoningResult;
    private ToolRecommendation[]? _inventoryResult;

    // ===== Helper Methods =====

    private SingleAgentAnalysisResponse BuildResponseFromToolOutputs(string agentOutput, string prompt)
    {
        return new SingleAgentAnalysisResponse
        {
            Analysis = _photoAnalysisResult?.Description ?? "Photo analysis completed",
            ReusableTools = _customerInfo?.OwnedTools ?? ["hammer", "screwdriver"],
            RecommendedTools = _inventoryResult ?? 
            [
                new ToolRecommendation { Name = "Paint Roller", Sku = "PAINT-ROLLER-9IN", IsAvailable = true, Price = 12.99m, Description = "9-inch paint roller" },
                new ToolRecommendation { Name = "Paint Brush Set", Sku = "BRUSH-SET-3PC", IsAvailable = true, Price = 24.99m, Description = "3-piece brush set" }
            ],
            Reasoning = GenerateSummary(agentOutput, prompt)
        };
    }

    private SingleAgentAnalysisResponse BuildFallbackResponse(string prompt, string customerId)
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

    private string GenerateSummary(string agentOutput, string prompt)
    {
        return string.Join(Environment.NewLine,
            "=== Microsoft Agent Framework Analysis (Foundry Agent with MCP Tools) ===",
            "",
            "Execution Mode: Single Foundry Agent (ZavaSingleAgent) with 4 MCP Tools",
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
            "This analysis used a single Foundry agent (ZavaSingleAgent) with MCP tools that call HTTP services.",
            "Each tool makes an HTTP request to the appropriate microservice for specialized processing.");
    }
}
