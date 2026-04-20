#pragma warning disable MAAIW001

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using SharedEntities;
using ZavaAgentsMetadata;

namespace MultiAgentDemo.Endpoints;

public static class MultiAgentMafFoundryEndpoints
{
    public static void MapMultiAgentMafFoundryEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/multiagent/maf_foundry");

        group.MapPost("/assist", AssistAsync);
        group.MapPost("/assist/sequential", AssistSequentialAsync);
        group.MapPost("/assist/concurrent", AssistConcurrentAsync);
        group.MapPost("/assist/handoff", AssistHandoffAsync);
        group.MapPost("/assist/groupchat", AssistGroupChatAsync);
        group.MapPost("/assist/magentic", AssistMagenticAsync);
    }

    public static Task<IResult> AssistAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] IServiceProvider serviceProvider,
        [FromBody] MultiAgentRequest? request)
        => AssistAsyncCore(logger, serviceProvider, request);

    public static Task<IResult> AssistSequentialAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] IServiceProvider serviceProvider,
        [FromBody] MultiAgentRequest? request)
        => AssistSequentialAsyncCore(logger, serviceProvider, request);

    public static Task<IResult> AssistConcurrentAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] IServiceProvider serviceProvider,
        [FromBody] MultiAgentRequest? request)
        => AssistConcurrentAsyncCore(logger, serviceProvider, request);

    public static Task<IResult> AssistHandoffAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] IServiceProvider serviceProvider,
        [FromBody] MultiAgentRequest? request)
        => AssistHandoffAsyncCore(logger, serviceProvider, request);

    public static Task<IResult> AssistGroupChatAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] IServiceProvider serviceProvider,
        [FromBody] MultiAgentRequest? request)
        => AssistGroupChatAsyncCore(logger, serviceProvider, request);

    public static Task<IResult> AssistMagenticAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] IServiceProvider serviceProvider,
        [FromBody] MultiAgentRequest? request)
        => AssistMagenticAsyncCore(logger, request);

    private static async Task<IResult> AssistAsyncCore(
        ILogger logger,
        IServiceProvider serviceProvider,
        MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return Results.BadRequest("Request body is required and must include a ProductQuery.");
        }

        logger.LogInformation(
            "Starting {OrchestrationTypeName} orchestration for query: {ProductQuery} using Microsoft Agent Framework (Foundry)",
            request.Orchestration,
            request.ProductQuery);

        try
        {
            return request.Orchestration switch
            {
                OrchestrationType.Sequential => await AssistSequentialAsyncCore(logger, serviceProvider, request),
                OrchestrationType.Concurrent => await AssistConcurrentAsyncCore(logger, serviceProvider, request),
                OrchestrationType.Handoff => await AssistHandoffAsyncCore(logger, serviceProvider, request),
                OrchestrationType.GroupChat => await AssistGroupChatAsyncCore(logger, serviceProvider, request),
                OrchestrationType.Magentic => await AssistMagenticAsyncCore(logger, request),
                _ => await AssistSequentialAsyncCore(logger, serviceProvider, request)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error in {OrchestrationTypeName} orchestration using Microsoft Agent Framework (Foundry)",
                request.Orchestration);
            return Results.Text("An error occurred during orchestration processing.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> AssistSequentialAsyncCore(
        ILogger logger,
        IServiceProvider serviceProvider,
        MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return Results.BadRequest("Request body is required and must include a ProductQuery.");
        }

        logger.LogInformation("Starting sequential workflow for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var (productSearch, productMatchmaking, locationService, navigation) = GetAgents(serviceProvider);
            var workflow = AgentWorkflowBuilder.BuildSequential([
                productSearch,
                productMatchmaking,
                locationService,
                navigation
            ]);

            var workflowResponse = await RunWorkflowAsync(logger, serviceProvider, request, workflow);
            return Results.Ok(workflowResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in sequential workflow");
            return Results.Text("An error occurred during sequential workflow processing.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> AssistConcurrentAsyncCore(
        ILogger logger,
        IServiceProvider serviceProvider,
        MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return Results.BadRequest("Request body is required and must include a ProductQuery.");
        }

        logger.LogInformation("Starting concurrent workflow for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var (productSearch, productMatchmaking, locationService, navigation) = GetAgents(serviceProvider);
            var workflow = AgentWorkflowBuilder.BuildConcurrent([
                productSearch,
                productMatchmaking,
                locationService,
                navigation
            ]);

            var workflowResponse = await RunWorkflowAsync(logger, serviceProvider, request, workflow);
            return Results.Ok(workflowResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in concurrent workflow");
            return Results.Text("An error occurred during concurrent workflow processing.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> AssistHandoffAsyncCore(
        ILogger logger,
        IServiceProvider serviceProvider,
        MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return Results.BadRequest("Request body is required and must include a ProductQuery.");
        }

        logger.LogInformation("Starting handoff workflow with branching logic for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var (productSearch, productMatchmaking, locationService, navigation) = GetAgents(serviceProvider);
            var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(productSearch)
                .WithHandoff(productSearch, productMatchmaking)
                .WithHandoff(productMatchmaking, locationService)
                .WithHandoff(locationService, navigation)
                .Build();

            var workflowResponse = await RunWorkflowAsync(logger, serviceProvider, request, workflow);
            return Results.Ok(workflowResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in handoff workflow");
            return Results.Text("An error occurred during handoff workflow processing.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> AssistGroupChatAsyncCore(
        ILogger logger,
        IServiceProvider serviceProvider,
        MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return Results.BadRequest("Request body is required and must include a ProductQuery.");
        }

        logger.LogInformation("Starting group chat workflow for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var (productSearch, productMatchmaking, locationService, navigation) = GetAgents(serviceProvider);
            var agentList = new List<AIAgent>
            {
                productSearch,
                productMatchmaking,
                locationService,
                navigation
            };

            var workflow = AgentWorkflowBuilder.CreateGroupChatBuilderWith(
                    _ => new RoundRobinGroupChatManager(agentList) { MaximumIterationCount = 5 })
                .AddParticipants(agentList)
                .Build();

            var workflowResponse = await RunWorkflowAsync(logger, serviceProvider, request, workflow);
            return Results.Ok(workflowResponse);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in group chat workflow");
            return Results.Text("An error occurred during group chat workflow processing.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static Task<IResult> AssistMagenticAsyncCore(ILogger logger, MultiAgentRequest? request)
    {
        logger.LogInformation("MagenticOne workflow requested for query: {ProductQuery}", request?.ProductQuery);

        return Task.FromResult<IResult>(
            Results.Text(
                "The MagenticOne workflow is not yet implemented in the MAF Foundry framework. Please use another orchestration type or the LLM direct call mode.",
                statusCode: StatusCodes.Status501NotImplemented));
    }

    private static async Task<MultiAgentResponse> RunWorkflowAsync(
        ILogger logger,
        IServiceProvider serviceProvider,
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
            ProcessWorkflowEvent(logger, serviceProvider, evt, steps, request, ref lastExecutorId);
        }

        var mermaidChart = workflow.ToMermaidString();
        var (_, productMatchmaking, _, navigation) = GetAgents(serviceProvider);

        var alternatives = await StepsProcessor.GetProductAlternativesFromStepsAsync(
            steps,
            productMatchmaking,
            logger);
        var navigationInstructions = await StepsProcessor.GenerateNavigationInstructionsAsync(
            steps,
            navigation,
            request.Location,
            request.ProductQuery,
            logger);

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

    private static void ProcessWorkflowEvent(
        ILogger logger,
        IServiceProvider serviceProvider,
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
                        Agent = GetAgentDisplayName(serviceProvider, message.AuthorName),
                        AgentId = message.AuthorName ?? string.Empty,
                        Action = $"Processing - {request.ProductQuery}",
                        Result = message.Text,
                        Timestamp = message.CreatedAt?.UtcDateTime ?? DateTime.UtcNow
                    });
                }
                break;

            case WorkflowErrorEvent errorEvent:
                logger.LogError("WorkflowError - ExecutorId: {ExecutorId}, Error: {ErrorMessage}", lastExecutorId, errorEvent.Data);
                break;
        }
    }

    private static (AIAgent ProductSearch, AIAgent ProductMatchmaking, AIAgent LocationService, AIAgent Navigation) GetAgents(IServiceProvider serviceProvider)
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

    private static string GetAgentDisplayName(IServiceProvider serviceProvider, string? agentId)
    {
        if (string.IsNullOrEmpty(agentId))
        {
            return "Unknown Agent";
        }

        var (productSearch, productMatchmaking, locationService, navigation) = GetAgents(serviceProvider);

        return agentId switch
        {
            _ when agentId == locationService.Id => "Location Service Agent",
            _ when agentId == navigation.Id => "Navigation Agent",
            _ when agentId == productMatchmaking.Id => "Product Matchmaking Agent",
            _ when agentId == productSearch.Id => "Product Search Agent",
            _ => agentId
        };
    }

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
