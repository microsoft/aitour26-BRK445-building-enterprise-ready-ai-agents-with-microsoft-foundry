using Microsoft.AspNetCore.Mvc;
using MultiAgentDemo.Services;
using SharedEntities;

namespace MultiAgentDemo.Endpoints;

public static class MultiAgentLlmEndpoints
{
    public static void MapMultiAgentLlmEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/multiagent/llm");

        group.MapPost("/assist", AssistAsync);
        group.MapPost("/assist/sequential", AssistSequentialAsync);
        group.MapPost("/assist/concurrent", AssistConcurrentAsync);
        group.MapPost("/assist/handoff", AssistHandoffAsync);
        group.MapPost("/assist/groupchat", AssistGroupChatAsync);
        group.MapPost("/assist/magentic", AssistMagenticAsync);
    }

    public static async Task<IResult> AssistAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] InventoryAgentService inventoryAgentService,
        [FromServices] MatchmakingAgentService matchmakingAgentService,
        [FromServices] LocationAgentService locationAgentService,
        [FromServices] NavigationAgentService navigationAgentService,
        [FromServices] SequentialOrchestrationService sequentialOrchestration,
        [FromServices] ConcurrentOrchestrationService concurrentOrchestration,
        [FromServices] HandoffOrchestrationService handoffOrchestration,
        [FromServices] GroupChatOrchestrationService groupChatOrchestration,
        [FromServices] MagenticOrchestrationService magenticOrchestration,
        [FromBody] MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return Results.BadRequest("Request body is required and must include a ProductQuery.");
        }

        ConfigureFramework(
            inventoryAgentService,
            matchmakingAgentService,
            locationAgentService,
            navigationAgentService,
            "llm");

        logger.LogInformation(
            "Starting {OrchestrationTypeName} orchestration for query: {ProductQuery} using LLM",
            request.Orchestration,
            request.ProductQuery);

        try
        {
            var orchestrationService = GetOrchestrationService(
                request.Orchestration,
                sequentialOrchestration,
                concurrentOrchestration,
                handoffOrchestration,
                groupChatOrchestration,
                magenticOrchestration);

            var response = await orchestrationService.ExecuteAsync(request);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in {OrchestrationTypeName} orchestration using LLM", request.Orchestration);
            return Results.Text("An error occurred during orchestration processing.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public static async Task<IResult> AssistSequentialAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] InventoryAgentService inventoryAgentService,
        [FromServices] MatchmakingAgentService matchmakingAgentService,
        [FromServices] LocationAgentService locationAgentService,
        [FromServices] NavigationAgentService navigationAgentService,
        [FromServices] SequentialOrchestrationService sequentialOrchestration,
        [FromBody] MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return Results.BadRequest("Request body is required and must include a ProductQuery.");
        }

        request.Orchestration = OrchestrationType.Sequential;
        ConfigureFramework(inventoryAgentService, matchmakingAgentService, locationAgentService, navigationAgentService, "llm");
        logger.LogInformation("Starting sequential orchestration for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var response = await sequentialOrchestration.ExecuteAsync(request);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in sequential orchestration using LLM");
            return Results.Text("An error occurred during sequential processing.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public static async Task<IResult> AssistConcurrentAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] InventoryAgentService inventoryAgentService,
        [FromServices] MatchmakingAgentService matchmakingAgentService,
        [FromServices] LocationAgentService locationAgentService,
        [FromServices] NavigationAgentService navigationAgentService,
        [FromServices] ConcurrentOrchestrationService concurrentOrchestration,
        [FromBody] MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return Results.BadRequest("Request body is required and must include a ProductQuery.");
        }

        request.Orchestration = OrchestrationType.Concurrent;
        ConfigureFramework(inventoryAgentService, matchmakingAgentService, locationAgentService, navigationAgentService, "llm");
        logger.LogInformation("Starting concurrent orchestration for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var response = await concurrentOrchestration.ExecuteAsync(request);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in concurrent orchestration using LLM");
            return Results.Text("An error occurred during concurrent processing.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public static async Task<IResult> AssistHandoffAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] InventoryAgentService inventoryAgentService,
        [FromServices] MatchmakingAgentService matchmakingAgentService,
        [FromServices] LocationAgentService locationAgentService,
        [FromServices] NavigationAgentService navigationAgentService,
        [FromServices] HandoffOrchestrationService handoffOrchestration,
        [FromBody] MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return Results.BadRequest("Request body is required and must include a ProductQuery.");
        }

        request.Orchestration = OrchestrationType.Handoff;
        ConfigureFramework(inventoryAgentService, matchmakingAgentService, locationAgentService, navigationAgentService, "llm");
        logger.LogInformation("Starting handoff orchestration for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var response = await handoffOrchestration.ExecuteAsync(request);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in handoff orchestration using LLM");
            return Results.Text("An error occurred during handoff processing.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public static async Task<IResult> AssistGroupChatAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] InventoryAgentService inventoryAgentService,
        [FromServices] MatchmakingAgentService matchmakingAgentService,
        [FromServices] LocationAgentService locationAgentService,
        [FromServices] NavigationAgentService navigationAgentService,
        [FromServices] GroupChatOrchestrationService groupChatOrchestration,
        [FromBody] MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return Results.BadRequest("Request body is required and must include a ProductQuery.");
        }

        request.Orchestration = OrchestrationType.GroupChat;
        ConfigureFramework(inventoryAgentService, matchmakingAgentService, locationAgentService, navigationAgentService, "llm");
        logger.LogInformation("Starting group chat orchestration for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var response = await groupChatOrchestration.ExecuteAsync(request);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in group chat orchestration using LLM");
            return Results.Text("An error occurred during group chat processing.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public static async Task<IResult> AssistMagenticAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] InventoryAgentService inventoryAgentService,
        [FromServices] MatchmakingAgentService matchmakingAgentService,
        [FromServices] LocationAgentService locationAgentService,
        [FromServices] NavigationAgentService navigationAgentService,
        [FromServices] MagenticOrchestrationService magenticOrchestration,
        [FromBody] MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return Results.BadRequest("Request body is required and must include a ProductQuery.");
        }

        request.Orchestration = OrchestrationType.Magentic;
        ConfigureFramework(inventoryAgentService, matchmakingAgentService, locationAgentService, navigationAgentService, "llm");
        logger.LogInformation("Starting MagenticOne orchestration for query: {ProductQuery}", request.ProductQuery);

        try
        {
            var response = await magenticOrchestration.ExecuteAsync(request);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in MagenticOne orchestration using LLM");
            return Results.Text("An error occurred during MagenticOne processing.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static void ConfigureFramework(
        InventoryAgentService inventoryAgentService,
        MatchmakingAgentService matchmakingAgentService,
        LocationAgentService locationAgentService,
        NavigationAgentService navigationAgentService,
        string framework)
    {
        inventoryAgentService.SetFramework(framework);
        matchmakingAgentService.SetFramework(framework);
        locationAgentService.SetFramework(framework);
        navigationAgentService.SetFramework(framework);
    }

    private static IAgentOrchestrationService GetOrchestrationService(
        OrchestrationType orchestrationType,
        SequentialOrchestrationService sequentialOrchestration,
        ConcurrentOrchestrationService concurrentOrchestration,
        HandoffOrchestrationService handoffOrchestration,
        GroupChatOrchestrationService groupChatOrchestration,
        MagenticOrchestrationService magenticOrchestration) => orchestrationType switch
    {
        OrchestrationType.Sequential => sequentialOrchestration,
        OrchestrationType.Concurrent => concurrentOrchestration,
        OrchestrationType.Handoff => handoffOrchestration,
        OrchestrationType.GroupChat => groupChatOrchestration,
        OrchestrationType.Magentic => magenticOrchestration,
        _ => sequentialOrchestration
    };
}
