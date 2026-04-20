#pragma warning disable MAAIW001
using Microsoft.Agents.AI;
using ZavaAgentsMetadata;
using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using SharedEntities;

namespace MultiAgentDemo.Controllers;

/// <summary>
/// Controller for multi-agent orchestration using Microsoft Agent Framework with Foundry Agents.
/// Supports multiple orchestration patterns: Sequential, Concurrent, Handoff, GroupChat, and Magentic.
/// </summary>
[ApiController]
[Route("api/multiagent/maf_foundry")]  // Using constant AgentMetadata.FrameworkIdentifiers.MafFoundry
public class MultiAgentControllerMAFFoundry(
    ILogger<MultiAgentControllerMAFFoundry> logger,
    IServiceProvider serviceProvider) : ControllerBase
{
    /// <summary>
    /// Routes to the appropriate orchestration pattern based on request type.
    /// </summary>
    [HttpPost("assist")]
    public async Task<ActionResult<MultiAgentResponse>> AssistAsync([FromBody] MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return BadRequest("Request body is required and must include a ProductQuery.");
        }

        logger.LogInformation(
            "Starting {OrchestrationTypeName} orchestration for query: {ProductQuery} using Microsoft Agent Framework (Foundry)",
            request.Orchestration, request.ProductQuery);

        try
        {
            return request.Orchestration switch
            {
                OrchestrationType.Sequential => await AssistSequentialAsync(request),
                OrchestrationType.Concurrent => await AssistConcurrentAsync(request),
                OrchestrationType.Handoff => await AssistHandoffAsync(request),
                OrchestrationType.GroupChat => await AssistGroupChatAsync(request),
                OrchestrationType.Magentic => await AssistMagenticAsync(request),
                _ => await AssistSequentialAsync(request)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in {OrchestrationTypeName} orchestration using Microsoft Agent Framework (Foundry)", request.Orchestration);
            return StatusCode(500, "An error occurred during orchestration processing.");
        }
    }

    /// <summary>
    /// Sequential workflow - executes agents in order, with output feeding into subsequent steps.
    /// </summary>
    [HttpPost("assist/sequential")]
    public async Task<ActionResult<MultiAgentResponse>> AssistSequentialAsync([FromBody] MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return BadRequest("Request body is required and must include a ProductQuery.");
        }

        logger.LogInformation("Starting sequential workflow for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var (ProductSearch, ProductMatchmaking, LocationService, Navigation) = GetAgents();
            var workflow = AgentWorkflowBuilder.BuildSequential([
                ProductSearch,
                ProductMatchmaking,
                LocationService,
                Navigation
            ]);

            var workflowResponse = await RunWorkflowAsync(request, workflow);
            return Ok(workflowResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in sequential workflow");
            return StatusCode(500, "An error occurred during sequential workflow processing.");
        }
    }

    /// <summary>
    /// Concurrent workflow - executes all agents in parallel for independent analysis.
    /// </summary>
    [HttpPost("assist/concurrent")]
    public async Task<ActionResult<MultiAgentResponse>> AssistConcurrentAsync([FromBody] MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return BadRequest("Request body is required and must include a ProductQuery.");
        }

        logger.LogInformation("Starting concurrent workflow for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var (ProductSearch, ProductMatchmaking, LocationService, Navigation) = GetAgents();
            var workflow = AgentWorkflowBuilder.BuildConcurrent([
                ProductSearch,
                ProductMatchmaking,
                LocationService,
                Navigation
            ]);

            var workflowResponse = await RunWorkflowAsync(request, workflow);
            return Ok(workflowResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in concurrent workflow");
            return StatusCode(500, "An error occurred during concurrent workflow processing.");
        }
    }

    /// <summary>
    /// Handoff workflow - dynamically passes control between agents based on branching logic.
    /// </summary>
    [HttpPost("assist/handoff")]
    public async Task<ActionResult<MultiAgentResponse>> AssistHandoffAsync([FromBody] MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return BadRequest("Request body is required and must include a ProductQuery.");
        }

        logger.LogInformation("Starting handoff workflow with branching logic for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var (ProductSearch, ProductMatchmaking, LocationService, Navigation) = GetAgents();
            var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(ProductSearch)
                .WithHandoff(ProductSearch, ProductMatchmaking)
                .WithHandoff(ProductMatchmaking, LocationService)
                .WithHandoff(LocationService, Navigation)
                .Build();

            var workflowResponse = await RunWorkflowAsync(request, workflow);
            return Ok(workflowResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in handoff workflow");
            return StatusCode(500, "An error occurred during handoff workflow processing.");
        }
    }

    /// <summary>
    /// Group chat workflow - agents collaborate in a round-robin conversation pattern.
    /// </summary>
    [HttpPost("assist/groupchat")]
    public async Task<ActionResult<MultiAgentResponse>> AssistGroupChatAsync([FromBody] MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return BadRequest("Request body is required and must include a ProductQuery.");
        }

        logger.LogInformation("Starting group chat workflow for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var (ProductSearch, ProductMatchmaking, LocationService, Navigation) = GetAgents();
            var agentList = new List<AIAgent>
            {
                ProductSearch,
                ProductMatchmaking,
                LocationService,
                Navigation
            };

            var workflow = AgentWorkflowBuilder.CreateGroupChatBuilderWith(
                _ => new RoundRobinGroupChatManager(agentList) { MaximumIterationCount = 5 })
                .AddParticipants(agentList)
                .Build();

            var workflowResponse = await RunWorkflowAsync(request, workflow);
            return Ok(workflowResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in group chat workflow");
            return StatusCode(500, "An error occurred during group chat workflow processing.");
        }
    }

    /// <summary>
    /// Magentic workflow - complex multi-agent collaboration (not yet implemented).
    /// </summary>
    [HttpPost("assist/magentic")]
    public async Task<ActionResult<MultiAgentResponse>> AssistMagenticAsync([FromBody] MultiAgentRequest? request)
    {
        logger.LogInformation("MagenticOne workflow requested for query: {ProductQuery}", request?.ProductQuery);

        return StatusCode(501, 
            "The MagenticOne workflow is not yet implemented in the MAF Foundry framework. " +
            "Please use another orchestration type or the LLM direct call mode.");
    }

    /// <summary>
    /// Executes a workflow and processes the streaming events.
    /// </summary>
    private async Task<MultiAgentResponse> RunWorkflowAsync(MultiAgentRequest request, Workflow workflow)
    {
        var orchestrationId = Guid.NewGuid().ToString();
        var steps = new List<AgentStep>();
        string? lastExecutorId = null;

        var run = await InProcessExecution.OpenStreamingAsync(workflow, request.ProductQuery);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        await foreach (var evt in run.WatchStreamAsync().ConfigureAwait(false))
        {
            ProcessWorkflowEvent(evt, steps, request, ref lastExecutorId);
        }

        var mermaidChart = workflow.ToMermaidString();
        var (_, ProductMatchmaking, _, Navigation) = GetAgents();
        
        var alternatives = await StepsProcessor.GetProductAlternativesFromStepsAsync(
            steps, ProductMatchmaking, logger);
        var navigationInstructions = await StepsProcessor.GenerateNavigationInstructionsAsync(
            steps, Navigation, request.Location, request.ProductQuery, logger);

        return new MultiAgentResponse
        {
            OrchestrationId = orchestrationId,
            OrchestationType = request.Orchestration,
            OrchestrationDescription = GetOrchestrationDescription(request.Orchestration),
            Steps = [.. steps],
            MermaidWorkflowRepresentation = mermaidChart,
            Alternatives = alternatives,
            NavigationInstructions = navigationInstructions
        };
    }

    /// <summary>
    /// Processes workflow events and extracts agent steps.
    /// </summary>
    private void ProcessWorkflowEvent(
        WorkflowEvent evt,
        List<AgentStep> steps,
        MultiAgentRequest request,
        ref string? lastExecutorId)
    {
        switch (evt)
        {
            case AgentResponseUpdateEvent updateEvent:
                if (updateEvent.ExecutorId != lastExecutorId)
                {
                    lastExecutorId = updateEvent.ExecutorId;
                    logger.LogDebug("ExecutorId changed to: {ExecutorId}", updateEvent.ExecutorId);
                }
                break;

            case WorkflowOutputEvent outputEvent:
                logger.LogDebug("WorkflowOutput - SourceId: {SourceId}", outputEvent.ExecutorId);
                var messages = outputEvent.As<List<ChatMessage>>() ?? [];
                
                foreach (var message in messages)
                {
                    steps.Add(new AgentStep
                    {
                        Agent = GetAgentDisplayName(message.AuthorName),
                        AgentId = message.AuthorName ?? string.Empty,
                        Action = $"Processing - {request.ProductQuery}",
                        Result = message.Text,
                        Timestamp = message.CreatedAt?.UtcDateTime ?? DateTime.UtcNow
                    });
                }
                break;
        }
    }

    /// <summary>
    /// Retrieves all required agents from dependency injection.
    /// </summary>
    private (AIAgent ProductSearch, AIAgent ProductMatchmaking, AIAgent LocationService, AIAgent Navigation) GetAgents()
    {
        return (
            ProductSearch: serviceProvider.GetRequiredKeyedService<AIAgent>(
                AgentMetadata.GetAgentName(AgentType.ProductSearchAgent)),
            ProductMatchmaking: serviceProvider.GetRequiredKeyedService<AIAgent>(
                AgentMetadata.GetAgentName(AgentType.ProductMatchmakingAgent)),
            LocationService: serviceProvider.GetRequiredKeyedService<AIAgent>(
                AgentMetadata.GetAgentName(AgentType.LocationServiceAgent)),
            Navigation: serviceProvider.GetRequiredKeyedService<AIAgent>(
                AgentMetadata.GetAgentName(AgentType.NavigationAgent))
        );
    }

    /// <summary>
    /// Converts agent ID to human-readable display name.
    /// </summary>
    private string GetAgentDisplayName(string? agentId)
    {
        if (string.IsNullOrEmpty(agentId))
            return "Unknown Agent";

        var (ProductSearch, ProductMatchmaking, LocationService, Navigation) = GetAgents();
        
        return agentId switch
        {
            _ when agentId == LocationService.Id => "Location Service Agent",
            _ when agentId == Navigation.Id => "Navigation Agent",
            _ when agentId == ProductMatchmaking.Id => "Product Matchmaking Agent",
            _ when agentId == ProductSearch.Id => "Product Search Agent",
            _ => agentId
        };
    }

    /// <summary>
    /// Returns a description for the orchestration type.
    /// </summary>
    private static string GetOrchestrationDescription(OrchestrationType orchestration) => orchestration switch
    {
        OrchestrationType.Sequential => 
            "Sequential workflow using Microsoft Agent Framework (Foundry). Each agent step executes in order, with output feeding into subsequent steps.",
        OrchestrationType.Concurrent => 
            "Concurrent workflow using Microsoft Agent Framework (Foundry). All agents execute in parallel for independent analysis.",
        OrchestrationType.Handoff => 
            "Handoff workflow using Microsoft Agent Framework (Foundry). Agents dynamically pass control based on context and branching logic.",
        OrchestrationType.GroupChat => 
            "Group chat workflow using Microsoft Agent Framework (Foundry). Agents collaborate in a round-robin conversation pattern.",
        OrchestrationType.Magentic => 
            "MagenticOne-inspired workflow for complex multi-agent collaboration.",
        _ => 
            "Multi-agent workflow using Microsoft Agent Framework (Foundry)."
    };
}