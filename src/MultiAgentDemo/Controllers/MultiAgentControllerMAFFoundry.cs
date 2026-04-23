using Microsoft.Agents.AI;
using ZavaAgentsMetadata;
using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using MultiAgentDemo.Tracing;
using SharedEntities;
using System.Text;
using System.Diagnostics;

namespace MultiAgentDemo.Controllers;

/// <summary>
/// Controller for multi-agent orchestration using Microsoft Agent Framework with Foundry Agents.
/// Supports multiple orchestration patterns: Sequential, Concurrent, Handoff, GroupChat, and Magentic.
/// </summary>
[ApiController]
[Route("api/multiagent/maf_foundry")]  // Using constant AgentMetadata.FrameworkIdentifiers.MafFoundry
public class MultiAgentControllerMAFFoundry : ControllerBase
{
    private readonly ILogger<MultiAgentControllerMAFFoundry> _logger;
    private readonly IServiceProvider _serviceProvider;

    public MultiAgentControllerMAFFoundry(
        ILogger<MultiAgentControllerMAFFoundry> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

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

        _logger.LogInformation(
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
            _logger.LogError(ex, "Error in {OrchestrationTypeName} orchestration using Microsoft Agent Framework (Foundry)", request.Orchestration);
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

        _logger.LogInformation("Starting sequential workflow for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var agents = GetAgents();
            // NOTE: Hosted Foundry agents call the Responses API and reject orphan
            // tool/function-call items at the root. AgentWorkflowBuilder.BuildSequential
            // forwards the prior agent's raw ChatMessage list, which leaks those items
            // and produces HTTP 400 invalid_payload on the second agent. We use a
            // sanitizing builder that converts each hand-off to a plain-text user
            // message. See MAFFoundrySequentialBuilder for details.
            var workflow = MAFFoundrySequentialBuilder.BuildSequentialForFoundry(
            [
                agents.ProductSearch,
                agents.ProductMatchmaking,
                agents.LocationService,
                agents.Navigation
            ]);

            var workflowResponse = await RunWorkflowAsync(request, workflow);
            return Ok(workflowResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in sequential workflow");
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

        _logger.LogInformation("Starting concurrent workflow for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var agents = GetAgents();
            var workflow = AgentWorkflowBuilder.BuildConcurrent([
                agents.ProductSearch,
                agents.ProductMatchmaking,
                agents.LocationService,
                agents.Navigation
            ]);

            var workflowResponse = await RunWorkflowAsync(request, workflow);
            return Ok(workflowResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in concurrent workflow");
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

        _logger.LogInformation("Starting handoff workflow with branching logic for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var agents = GetAgents();
            var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(agents.ProductSearch)
                .WithHandoff(agents.ProductSearch, agents.ProductMatchmaking)
                .WithHandoff(agents.ProductMatchmaking, agents.LocationService)
                .WithHandoff(agents.LocationService, agents.Navigation)
                .Build();

            var workflowResponse = await RunWorkflowAsync(request, workflow);
            return Ok(workflowResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in handoff workflow");
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

        _logger.LogInformation("Starting group chat workflow for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var agents = GetAgents();
            var agentList = new List<AIAgent>
            {
                agents.ProductSearch,
                agents.ProductMatchmaking,
                agents.LocationService,
                agents.Navigation
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
            _logger.LogError(ex, "Error in group chat workflow");
            return StatusCode(500, "An error occurred during group chat workflow processing.");
        }
    }

    /// <summary>
    /// Magentic workflow - complex multi-agent collaboration (not yet implemented).
    /// </summary>
    [HttpPost("assist/magentic")]
    public async Task<ActionResult<MultiAgentResponse>> AssistMagenticAsync([FromBody] MultiAgentRequest? request)
    {
        _logger.LogInformation("MagenticOne workflow requested for query: {ProductQuery}", request?.ProductQuery);

        return StatusCode(501,
            "The MagenticOne workflow is not yet implemented in the MAF Foundry framework. " +
            "Please use another orchestration type or the LLM direct call mode.");
    }

    /// <summary>
    /// Executes a workflow and processes the streaming events.
    /// </summary>
    private const string OrchestratorIdentityBanner =
        "🌐 Using MAF Foundry orchestrator (hosted Microsoft Foundry agents)";

    private async Task<MultiAgentResponse> RunWorkflowAsync(MultiAgentRequest request, Workflow workflow)
    {
        using var orchestrationActivity = MultiAgentWorkflowTracing.ActivitySource.StartActivity(
            $"Scenario2.{request.Orchestration}.Orchestration",
            ActivityKind.Internal);

        orchestrationActivity?.SetTag("zava.scenario", 2);
        orchestrationActivity?.SetTag("zava.orchestration", request.Orchestration.ToString().ToLowerInvariant());
        orchestrationActivity?.SetTag("zava.product_query", request.ProductQuery);

        var orchestrationId = Guid.NewGuid().ToString();
        var startedAt = DateTime.UtcNow;
        _logger.LogInformation(
            "{Banner} | OrchestrationId={OrchestrationId} | Pattern={Orchestration}",
            OrchestratorIdentityBanner, orchestrationId, request.Orchestration);

        orchestrationActivity?.SetTag("zava.orchestration_id", orchestrationId);

        var steps = new List<AgentStep>
        {
            new()
            {
                Agent = "Orchestrator",
                AgentId = "maf-foundry-orchestrator",
                Action = $"Routing {request.Orchestration} request",
                Result = OrchestratorIdentityBanner,
                Timestamp = startedAt
            }
        };
        string? lastExecutorId = null;

        var run = await InProcessExecution.StreamAsync(workflow, request.ProductQuery);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        await foreach (var evt in run.WatchStreamAsync().ConfigureAwait(false))
        {
            ProcessWorkflowEvent(evt, steps, request, ref lastExecutorId);
        }

        var mermaidChart = workflow.ToMermaidString();
        var agents = GetAgents();

        var alternatives = await StepsProcessor.GetProductAlternativesFromStepsAsync(
            steps, agents.ProductMatchmaking, _logger);
        var navigationInstructions = await StepsProcessor.GenerateNavigationInstructionsAsync(
            steps, agents.Navigation, request.Location, request.ProductQuery, _logger);

        return new MultiAgentResponse
        {
            OrchestrationId = orchestrationId,
            OrchestationType = request.Orchestration,
            OrchestrationDescription = GetOrchestrationDescription(request.Orchestration),
            Steps = steps.ToArray(),
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
            case AgentRunUpdateEvent updateEvent:
                if (updateEvent.ExecutorId != lastExecutorId)
                {
                    lastExecutorId = updateEvent.ExecutorId;
                    _logger.LogInformation("Workflow step → executor: {ExecutorId}", updateEvent.ExecutorId);
                }
                break;

            case WorkflowOutputEvent outputEvent:
                _logger.LogInformation("Workflow output received from: {SourceId}", outputEvent.SourceId);
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

            case ExecutorFailedEvent failedEvent:
                {
                    var ex = failedEvent.Data as Exception;
                    _logger.LogError(ex,
                        "Workflow ExecutorFailedEvent — ExecutorId: {ExecutorId} | Message: {Message} | Inner: {Inner} | Detail: {Detail}",
                        failedEvent.ExecutorId,
                        ex?.Message ?? "(no exception)",
                        ex?.InnerException?.Message ?? "(none)",
                        ex?.ToString() ?? failedEvent.ToString());
                    break;
                }

            case WorkflowErrorEvent errorEvent:
                {
                    var ex = errorEvent.Data as Exception;
                    _logger.LogError(ex,
                        "Workflow WorkflowErrorEvent — Message: {Message} | Inner: {Inner} | Detail: {Detail}",
                        ex?.Message ?? "(no exception)",
                        ex?.InnerException?.Message ?? "(none)",
                        ex?.ToString() ?? errorEvent.ToString());
                    break;
                }

            default:
                _logger.LogInformation("Workflow event: {EventType}", evt.GetType().Name);
                break;
        }
    }

    /// <summary>
    /// Retrieves all required agents from dependency injection.
    /// </summary>
    private (AIAgent ProductSearch, AIAgent ProductMatchmaking, AIAgent LocationService, AIAgent Navigation) GetAgents()
    {
        return (
            ProductSearch: _serviceProvider.GetRequiredKeyedService<AIAgent>(
                AgentMetadata.GetAgentName(AgentType.ProductSearchAgent)),
            ProductMatchmaking: _serviceProvider.GetRequiredKeyedService<AIAgent>(
                AgentMetadata.GetAgentName(AgentType.ProductMatchmakingAgent)),
            LocationService: _serviceProvider.GetRequiredKeyedService<AIAgent>(
                AgentMetadata.GetAgentName(AgentType.LocationServiceAgent)),
            Navigation: _serviceProvider.GetRequiredKeyedService<AIAgent>(
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

        var agents = GetAgents();

        return agentId switch
        {
            _ when agentId == agents.LocationService.Id => "Location Service Agent",
            _ when agentId == agents.Navigation.Id => "Navigation Agent",
            _ when agentId == agents.ProductMatchmaking.Id => "Product Matchmaking Agent",
            _ when agentId == agents.ProductSearch.Id => "Product Search Agent",
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