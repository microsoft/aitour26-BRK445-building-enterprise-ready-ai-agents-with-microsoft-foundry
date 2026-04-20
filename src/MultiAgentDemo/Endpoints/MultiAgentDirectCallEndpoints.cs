using Microsoft.AspNetCore.Mvc;
using SharedEntities;

namespace MultiAgentDemo.Endpoints;

public static class MultiAgentDirectCallEndpoints
{
    public static void MapMultiAgentDirectCallEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/multiagent/directcall");

        group.MapPost("/assist", AssistAsync);
        group.MapPost("/assist/sequential", AssistSequentialAsync);
        group.MapPost("/assist/concurrent", AssistConcurrentAsync);
        group.MapPost("/assist/handoff", AssistHandoffAsync);
        group.MapPost("/assist/groupchat", AssistGroupChatAsync);
        group.MapPost("/assist/magentic", AssistMagenticAsync);
    }

    public static Task<IResult> AssistAsync(
        [FromServices] ILogger<Program> logger,
        [FromBody] MultiAgentRequest? request)
        => AssistCoreAsync(logger, request);

    public static Task<IResult> AssistSequentialAsync(
        [FromServices] ILogger<Program> logger,
        [FromBody] MultiAgentRequest? request)
        => AssistCoreAsync(logger, request);

    public static Task<IResult> AssistConcurrentAsync(
        [FromServices] ILogger<Program> logger,
        [FromBody] MultiAgentRequest? request)
        => AssistCoreAsync(logger, request);

    public static Task<IResult> AssistHandoffAsync(
        [FromServices] ILogger<Program> logger,
        [FromBody] MultiAgentRequest? request)
        => AssistCoreAsync(logger, request);

    public static Task<IResult> AssistGroupChatAsync(
        [FromServices] ILogger<Program> logger,
        [FromBody] MultiAgentRequest? request)
        => AssistCoreAsync(logger, request);

    public static Task<IResult> AssistMagenticAsync(
        [FromServices] ILogger<Program> logger,
        [FromBody] MultiAgentRequest? request)
        => AssistCoreAsync(logger, request);

    private static Task<IResult> AssistCoreAsync(ILogger logger, MultiAgentRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ProductQuery))
        {
            return Task.FromResult<IResult>(Results.BadRequest("Request body is required and must include a ProductQuery."));
        }

        logger.LogInformation(
            "Starting direct HTTP call orchestration for query: {ProductQuery}",
            request.ProductQuery);

        try
        {
            return Task.FromResult<IResult>(Results.Ok(CreateDirectCallResponse(request)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in direct call orchestration");
            return Task.FromResult<IResult>(
                Results.Text("An error occurred during direct call processing.", statusCode: StatusCodes.Status500InternalServerError));
        }
    }

    private static MultiAgentResponse CreateDirectCallResponse(MultiAgentRequest request)
    {
        var orchestrationId = Guid.NewGuid().ToString("N")[..8];
        var baseTime = DateTime.UtcNow;

        var steps = new[]
        {
            new AgentStep
            {
                Agent = "DirectCall - Product Search",
                Action = $"HTTP call to search products for '{request.ProductQuery}'",
                Result = $"[Direct HTTP] Found products matching '{request.ProductQuery}'",
                Timestamp = baseTime
            },
            new AgentStep
            {
                Agent = "DirectCall - Product Matchmaking",
                Action = $"HTTP call to find alternatives for '{request.ProductQuery}'",
                Result = "[Direct HTTP] Identified product alternatives",
                Timestamp = baseTime.AddSeconds(1)
            },
            new AgentStep
            {
                Agent = "DirectCall - Location Service",
                Action = $"HTTP call to locate '{request.ProductQuery}' in store",
                Result = "[Direct HTTP] Located products in store aisles",
                Timestamp = baseTime.AddSeconds(2)
            },
            new AgentStep
            {
                Agent = "DirectCall - Navigation Service",
                Action = $"HTTP call to generate route for '{request.ProductQuery}'",
                Result = "[Direct HTTP] Generated optimal navigation path",
                Timestamp = baseTime.AddSeconds(3)
            }
        };

        var alternatives = new List<ProductAlternative>
        {
            new()
            {
                Name = $"Premium {request.ProductQuery}",
                Sku = "PREM-" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
                Price = 189.99m,
                InStock = true,
                Location = "Aisle 5",
                Aisle = 5,
                Section = "A"
            },
            new()
            {
                Name = $"Standard {request.ProductQuery}",
                Sku = "STD-" + Guid.NewGuid().ToString("N")[..8].ToUpper(),
                Price = 89.99m,
                InStock = true,
                Location = "Aisle 7",
                Aisle = 7,
                Section = "B"
            }
        };

        NavigationInstructions? navigationInstructions = null;
        if (request.Location != null)
        {
            navigationInstructions = new NavigationInstructions
            {
                StartLocation = $"Entrance ({request.Location.Lat:F4}, {request.Location.Lon:F4})",
                EstimatedTime = "3-5 minutes",
                Steps = new[]
                {
                    new NavigationStep
                    {
                        Direction = "Head straight",
                        Description = "[Direct HTTP] Walk towards main hardware section",
                        Landmark = new NavigationLandmark { Description = "Customer Service Desk" }
                    },
                    new NavigationStep
                    {
                        Direction = "Turn left",
                        Description = "[Direct HTTP] Enter Aisle 5 for products",
                        Landmark = new NavigationLandmark { Description = "Power Tools display" }
                    }
                }
            };
        }

        return new MultiAgentResponse
        {
            OrchestrationId = orchestrationId,
            OrchestationType = request.Orchestration,
            OrchestrationDescription = "[Direct HTTP Call Mode] Processing using direct service calls without AI orchestration.",
            Steps = steps,
            Alternatives = alternatives,
            NavigationInstructions = navigationInstructions
        };
    }
}
