#pragma warning disable MAAIW001

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using SharedEntities;
using ZavaAgentsMetadata;
using ZavaMAFLocal;

namespace MultiAgentDemo.Endpoints;

public static class MultiAgentMafLocalEndpoints
{
    public static void MapMultiAgentMafLocalEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/multiagent/maf_local");

        group.MapPost("/assist", AssistAsync);
        group.MapPost("/assist/sequential", AssistSequentialAsync);
        group.MapPost("/assist/concurrent", AssistConcurrentAsync);
        group.MapPost("/assist/handoff", AssistHandoffAsync);
        group.MapPost("/assist/groupchat", AssistGroupChatAsync);
        group.MapPost("/assist/magentic", AssistMagenticAsync);
    }

    public static Task<IResult> AssistAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] MAFLocalAgentProvider localAgentProvider,
        [FromBody] MultiAgentRequest? request)
        => AssistAsyncCore(logger, localAgentProvider, request);

    public static Task<IResult> AssistSequentialAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] MAFLocalAgentProvider localAgentProvider,
        [FromBody] MultiAgentRequest? request)
        => AssistSequentialAsyncCore(logger, localAgentProvider, request);

    public static Task<IResult> AssistConcurrentAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] MAFLocalAgentProvider localAgentProvider,
        [FromBody] MultiAgentRequest? request)
        => AssistConcurrentAsyncCore(logger, localAgentProvider, request);

    public static Task<IResult> AssistHandoffAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] MAFLocalAgentProvider localAgentProvider,
        [FromBody] MultiAgentRequest? request)
        => AssistHandoffAsyncCore(logger, localAgentProvider, request);

    public static Task<IResult> AssistGroupChatAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] MAFLocalAgentProvider localAgentProvider,
        [FromBody] MultiAgentRequest? request)
        => AssistGroupChatAsyncCore(logger, localAgentProvider, request);

    public static Task<IResult> AssistMagenticAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] MAFLocalAgentProvider localAgentProvider,
        [FromBody] MultiAgentRequest? request)
        => AssistMagenticAsyncCore(logger, request);

    private static async Task<IResult> AssistAsyncCore(
        ILogger logger,
        MAFLocalAgentProvider localAgentProvider,
        MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return Results.BadRequest("Request body is required and must include a ProductQuery.");
        }

        logger.LogInformation(
            "Starting {OrchestrationTypeName} orchestration for query: {ProductQuery} using MAF Local Agents",
            request.Orchestration,
            request.ProductQuery);

        try
        {
            return request.Orchestration switch
            {
                OrchestrationType.Sequential => await AssistSequentialAsyncCore(logger, localAgentProvider, request),
                OrchestrationType.Concurrent => await AssistConcurrentAsyncCore(logger, localAgentProvider, request),
                OrchestrationType.Handoff => await AssistHandoffAsyncCore(logger, localAgentProvider, request),
                OrchestrationType.GroupChat => await AssistGroupChatAsyncCore(logger, localAgentProvider, request),
                OrchestrationType.Magentic => await AssistMagenticAsyncCore(logger, request),
                _ => await AssistSequentialAsyncCore(logger, localAgentProvider, request)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in {OrchestrationTypeName} orchestration using MAF Local Agents", request.Orchestration);
            return Results.Text("An error occurred during orchestration processing.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> AssistSequentialAsyncCore(
        ILogger logger,
        MAFLocalAgentProvider localAgentProvider,
        MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return Results.BadRequest("Request body is required and must include a ProductQuery.");
        }

        logger.LogInformation("Starting sequential workflow with local agents for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var workflowResponse = await RunWorkflowAsync(
                logger,
                localAgentProvider,
                request,
                localAgentProvider.GetLocalWorkflowByName("SequentialWorkflow"));
            return Results.Ok(workflowResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in sequential workflow with local agents");
            return Results.Text("An error occurred during sequential workflow processing.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> AssistConcurrentAsyncCore(
        ILogger logger,
        MAFLocalAgentProvider localAgentProvider,
        MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return Results.BadRequest("Request body is required and must include a ProductQuery.");
        }

        logger.LogInformation("Starting concurrent workflow with local agents for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var workflowResponse = await RunWorkflowAsync(
                logger,
                localAgentProvider,
                request,
                localAgentProvider.GetLocalWorkflowByName("ConcurrentWorkflow"));
            return Results.Ok(workflowResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in concurrent workflow with local agents");
            return Results.Text("An error occurred during concurrent workflow processing.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> AssistHandoffAsyncCore(
        ILogger logger,
        MAFLocalAgentProvider localAgentProvider,
        MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return Results.BadRequest("Request body is required and must include a ProductQuery.");
        }

        logger.LogInformation("Starting handoff workflow with local agents for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var (productSearchAgent, productMatchmakingAgent, locationServiceAgent, navigationAgent) = GetAgents(localAgentProvider);
            var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(productSearchAgent)
                .WithHandoff(productSearchAgent, productMatchmakingAgent)
                .WithHandoff(productMatchmakingAgent, locationServiceAgent)
                .WithHandoff(locationServiceAgent, navigationAgent)
                .Build();

            var workflowResponse = await RunWorkflowAsync(logger, localAgentProvider, request, workflow);
            return Results.Ok(workflowResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in handoff workflow with local agents");
            return Results.Text("An error occurred during handoff workflow processing.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> AssistGroupChatAsyncCore(
        ILogger logger,
        MAFLocalAgentProvider localAgentProvider,
        MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return Results.BadRequest("Request body is required and must include a ProductQuery.");
        }

        logger.LogInformation("Starting group chat workflow with local agents for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var (productSearchAgent, productMatchmakingAgent, locationServiceAgent, navigationAgent) = GetAgents(localAgentProvider);
            var agentList = new List<AIAgent>
            {
                productSearchAgent,
                productMatchmakingAgent,
                locationServiceAgent,
                navigationAgent
            };

            var workflow = AgentWorkflowBuilder.CreateGroupChatBuilderWith(
                    _ => new RoundRobinGroupChatManager(agentList) { MaximumIterationCount = 5 })
                .AddParticipants(agentList)
                .Build();

            var workflowResponse = await RunWorkflowAsync(logger, localAgentProvider, request, workflow);
            return Results.Ok(workflowResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in group chat workflow with local agents");
            return Results.Text("An error occurred during group chat workflow processing.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static Task<IResult> AssistMagenticAsyncCore(ILogger logger, MultiAgentRequest? request)
    {
        logger.LogInformation("MagenticOne workflow requested for query: {ProductQuery}", request?.ProductQuery);

        return Task.FromResult<IResult>(
            Results.Text(
                "The MagenticOne workflow is not yet implemented in the MAF Local framework. Please use another orchestration type.",
                statusCode: StatusCodes.Status501NotImplemented));
    }

    private static async Task<MultiAgentResponse> RunWorkflowAsync(
        ILogger logger,
        MAFLocalAgentProvider localAgentProvider,
        MultiAgentRequest request,
        Workflow workflow)
    {
        var orchestrationId = Guid.NewGuid().ToString();
        var steps = new List<AgentStep>();
        string? lastExecutorId = null;

        await using var run = await InProcessExecution.RunStreamingAsync(workflow, new ChatMessage(ChatRole.User, request.ProductQuery));
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        await foreach (var evt in run.WatchStreamAsync().ConfigureAwait(false))
        {
            ProcessWorkflowEvent(logger, evt, steps, request, ref lastExecutorId);
        }

        var mermaidChart = workflow.ToMermaidString();
        var (_, productMatchmakingAgent, _, navigationAgent) = GetAgents(localAgentProvider);

        var alternatives = await StepsProcessor.GetProductAlternativesFromStepsAsync(
            steps,
            productMatchmakingAgent,
            logger);
        var navigationInstructions = await StepsProcessor.GenerateNavigationInstructionsAsync(
            steps,
            navigationAgent,
            request.Location,
            request.ProductQuery,
            logger);

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

    private static void ProcessWorkflowEvent(
        ILogger logger,
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
                logger.LogDebug("WorkflowOutput - ExecutorId: {ExecutorId}", outputEvent.ExecutorId);
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

    private static (AIAgent ProductSearch, AIAgent ProductMatchmaking, AIAgent LocationService, AIAgent Navigation) GetAgents(MAFLocalAgentProvider localAgentProvider)
    {
        return (
            ProductSearch: localAgentProvider.GetLocalAgentByName(AgentType.ProductSearchAgent),
            ProductMatchmaking: localAgentProvider.GetLocalAgentByName(AgentType.ProductMatchmakingAgent),
            LocationService: localAgentProvider.GetLocalAgentByName(AgentType.LocationServiceAgent),
            Navigation: localAgentProvider.GetLocalAgentByName(AgentType.NavigationAgent)
        );
    }

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
