using Microsoft.Agents.AI;
using ZavaAgentsMetadata;
using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using SharedEntities;
using ZavaMAFLocal;
using System.Diagnostics.Tracing;

namespace MultiAgentDemo.Controllers;

/// <summary>
/// Controller for multi-agent orchestration using Microsoft Agent Framework with locally created agents.
/// Agents are created with gpt-5-mini model and configured locally with instructions and tools.
/// </summary>
[ApiController]
[Route("api/multiagent/maf_local")]  // Using constant AgentMetadata.FrameworkIdentifiers.MafLocal
public class MultiAgentControllerMAFLocal : ControllerBase
{
    private readonly ILogger<MultiAgentControllerMAFLocal> _logger;
    private readonly AIAgent _productSearchAgent;
    private readonly AIAgent _productMatchmakingAgent;
    private readonly AIAgent _locationServiceAgent;
    private readonly AIAgent _navigationAgent;

    private readonly Workflow _sequentialWorkflow;
    private readonly Workflow _concurrentWorkflow;
    private readonly Workflow _handOffWorkflow;

    public MultiAgentControllerMAFLocal(
        ILogger<MultiAgentControllerMAFLocal> logger,
        MAFLocalAgentProvider localAgentProvider)
    {
        _logger = logger;

        // agents
        _productSearchAgent = localAgentProvider.GetLocalAgentByName(AgentType.ProductSearchAgent);
        _productMatchmakingAgent = localAgentProvider.GetLocalAgentByName(AgentType.ProductMatchmakingAgent);
        _locationServiceAgent = localAgentProvider.GetLocalAgentByName(AgentType.LocationServiceAgent);
        _navigationAgent = localAgentProvider.GetLocalAgentByName(AgentType.NavigationAgent);

        // get workflows
        _sequentialWorkflow = localAgentProvider.GetLocalWorkflowByName("SequentialWorkflow");
        _concurrentWorkflow = localAgentProvider.GetLocalWorkflowByName("ConcurrentWorkflow");
        _handOffWorkflow = localAgentProvider.GetLocalWorkflowByName("HandOffWorkflow");
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
            "Starting {OrchestrationTypeName} orchestration for query: {ProductQuery} using MAF Local Agents",
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
            _logger.LogError(ex, "Error in {OrchestrationTypeName} orchestration using MAF Local Agents", request.Orchestration);
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

        _logger.LogInformation("Starting sequential workflow with local agents for query: {ProductQuery}", request.ProductQuery);

        try
        {
            // sample to show how the workflow can be built programmatically, but in this case we are using a pre-built workflow from the provider
            //var workflow = AgentWorkflowBuilder.BuildSequential([
            //    _productSearchAgent,
            //    _productMatchmakingAgent,
            //    _locationServiceAgent,
            //    _navigationAgent
            //]);

            var workflowResponse = await RunWorkflowAsync(request, _sequentialWorkflow);
            return Ok(workflowResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in sequential workflow with local agents");
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

        _logger.LogInformation("Starting concurrent workflow with local agents for query: {ProductQuery}", request.ProductQuery);

        try
        {
            // sample to show how the workflow can be built programmatically, but in this case we are using a pre-built workflow from the provider
            //var workflow = AgentWorkflowBuilder.BuildConcurrent([
            //    _productSearchAgent,
            //    _productMatchmakingAgent,
            //    _locationServiceAgent,
            //    _navigationAgent
            //]);

            var workflowResponse = await RunWorkflowAsync(request, _concurrentWorkflow);
            return Ok(workflowResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in concurrent workflow with local agents");
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

        _logger.LogInformation("Starting handoff workflow with local agents for query: {ProductQuery}", request.ProductQuery);

        try
        {
            //var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(_productSearchAgent)
            //    .WithHandoff(_productSearchAgent, _productMatchmakingAgent)
            //    .WithHandoff(_productMatchmakingAgent, _locationServiceAgent)
            //    .WithHandoff(_locationServiceAgent, _navigationAgent)
            //    .Build();

            var workflowResponse = await RunWorkflowAsync(request, _handOffWorkflow);
            return Ok(workflowResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in handoff workflow with local agents");
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

        _logger.LogInformation("Starting group chat workflow with local agents for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var agentList = new List<AIAgent>
            {
                _productSearchAgent,
                _productMatchmakingAgent,
                _locationServiceAgent,
                _navigationAgent
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
            _logger.LogError(ex, "Error in group chat workflow with local agents");
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
            "The MagenticOne workflow is not yet implemented in the MAF Local framework. " +
            "Please use another orchestration type.");
    }

    /// <summary>
    /// Executes a workflow and processes the streaming events.
    /// </summary>
    private async Task<MultiAgentResponse> RunWorkflowAsync(
        MultiAgentRequest request,
        Workflow workflow)
    {
        var orchestrationId = Guid.NewGuid().ToString();
        var steps = new List<AgentStep>();
        string? lastExecutorId = null;

        var run = await InProcessExecution.StreamAsync(workflow, request.ProductQuery);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        await foreach (var evt in run.WatchStreamAsync().ConfigureAwait(false))
        {
            ProcessWorkflowEvent(evt, steps, request, ref lastExecutorId);
        }

        var mermaidChart = workflow.ToMermaidString();

        var alternatives = await StepsProcessor.GetProductAlternativesFromStepsAsync(
            steps, _productMatchmakingAgent, _logger);
        var navigationInstructions = await StepsProcessor.GenerateNavigationInstructionsAsync(
            steps, _navigationAgent, request.Location, request.ProductQuery, _logger);

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
                    _logger.LogDebug("ExecutorId changed to: {ExecutorId}", updateEvent.ExecutorId);
                }
                break;

            case WorkflowOutputEvent outputEvent:
                _logger.LogDebug("WorkflowOutput - SourceId: {SourceId}", outputEvent.SourceId);
                var messages = outputEvent.As<List<ChatMessage>>() ?? [];

                foreach (var message in messages)
                {
                    steps.Add(new AgentStep
                    {
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
    /// Converts agent ID to human-readable display name.
    /// </summary>
    private string GetAgentDisplayName(
        string? agentId)
    {
        if (string.IsNullOrEmpty(agentId))
            return "Unknown Agent";

        return agentId switch
        {
            _ when agentId == _locationServiceAgent.Id => "Location Service Agent (Local)",
            _ when agentId == _navigationAgent.Id => "Navigation Agent (Local)",
            _ when agentId == _productMatchmakingAgent.Id => "Product Matchmaking Agent (Local)",
            _ when agentId == _productSearchAgent.Id => "Product Search Agent (Local)",
            _ => agentId
        };
    }

    /// <summary>
    /// Returns a description for the orchestration type.
    /// </summary>
    private static string GetOrchestrationDescription(OrchestrationType orchestration) => orchestration switch
    {
        OrchestrationType.Sequential =>
            "Sequential workflow using MAF Local Agents (gpt-5-mini). Each agent step executes in order, with output feeding into subsequent steps.",
        OrchestrationType.Concurrent =>
            "Concurrent workflow using MAF Local Agents (gpt-5-mini). All agents execute in parallel for independent analysis.",
        OrchestrationType.Handoff =>
            "Handoff workflow using MAF Local Agents (gpt-5-mini). Agents dynamically pass control based on context and branching logic.",
        OrchestrationType.GroupChat =>
            "Group chat workflow using MAF Local Agents (gpt-5-mini). Agents collaborate in a round-robin conversation pattern.",
        OrchestrationType.Magentic =>
            "MagenticOne-inspired workflow for complex multi-agent collaboration.",
        _ =>
            "Multi-agent workflow using MAF Local Agents (gpt-5-mini)."
    };
}
