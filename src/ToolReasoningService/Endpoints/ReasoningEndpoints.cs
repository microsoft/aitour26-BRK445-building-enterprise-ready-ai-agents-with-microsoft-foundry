using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;
using SharedEntities;
using System.Text;
using ZavaAgentsMetadata;
using ZavaMAFLocal;

namespace ToolReasoningService.Endpoints;

public static class ReasoningEndpoints
{
    public static void MapReasoningEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Reasoning");

        group.MapPost("/generate/llm", GenerateReasoningLlmAsync);
        group.MapPost("/generate/maf_local", GenerateReasoningMAFLocalAsync);
        group.MapPost("/generate/maf_foundry", GenerateReasoningMAFFoundryAsync);
        group.MapPost("/generate/directcall", GenerateReasoningDirectCallAsync);
    }

    public static async Task<IResult> GenerateReasoningLlmAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] MAFLocalAgentProvider localAgentProvider,
        [FromBody] ReasoningRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.Llm} Generating reasoning for prompt");
        var agent = localAgentProvider.GetAgentByName(AgentMetadata.GetLocalAgentName(AgentType.ToolReasoningAgent));
        return await GenerateReasoningAsync(logger, request, (prompt, token) => InvokeAgentFrameworkAsync(agent, prompt, token), AgentMetadata.LogPrefixes.Llm, cancellationToken);
    }

    public static async Task<IResult> GenerateReasoningMAFLocalAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] MAFLocalAgentProvider localAgentProvider,
        [FromBody] ReasoningRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.MafLocal} Generating reasoning for prompt");
        var agent = localAgentProvider.GetAgentByName(AgentMetadata.GetLocalAgentName(AgentType.ToolReasoningAgent));
        return await GenerateReasoningAsync(logger, request, (prompt, token) => InvokeAgentFrameworkAsync(agent, prompt, token), AgentMetadata.LogPrefixes.MafLocal, cancellationToken);
    }

    public static async Task<IResult> GenerateReasoningMAFFoundryAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] MAFLocalAgentProvider localAgentProvider,
        [FromBody] ReasoningRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.MafFoundry} Generating reasoning for prompt");
        var agent = localAgentProvider.GetAgentByName(AgentMetadata.GetLocalAgentName(AgentType.ToolReasoningAgent));
        return await GenerateReasoningAsync(logger, request, (prompt, token) => InvokeAgentFrameworkAsync(agent, prompt, token), AgentMetadata.LogPrefixes.MafFoundry, cancellationToken);
    }

    public static async Task<IResult> GenerateReasoningDirectCallAsync(
        [FromServices] ILogger<Program> logger,
        [FromBody] ReasoningRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[DirectCall] Generating reasoning for prompt");
        await Task.Delay(1000, cancellationToken);
        return Results.Ok(GenerateFallbackReasoning(request));
    }

    private static async Task<IResult> GenerateReasoningAsync(
        ILogger logger,
        ReasoningRequest request,
        Func<string, CancellationToken, Task<string>> invokeAgent,
        string logPrefix,
        CancellationToken cancellationToken)
    {
        var reasoningPrompt = BuildReasoningPrompt(request);

        try
        {
            var agentResponse = await invokeAgent(reasoningPrompt, cancellationToken);
            logger.LogInformation("{Prefix} Raw agent response length: {Length}", logPrefix, agentResponse.Length);

            if (!string.IsNullOrWhiteSpace(agentResponse))
            {
                return Results.Ok(agentResponse);
            }

            logger.LogWarning("{Prefix} Empty response received. Falling back to heuristic reasoning.", logPrefix);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Prefix} Agent invocation failed. Falling back to heuristic reasoning.", logPrefix);
        }

        return Results.Ok(GenerateFallbackReasoning(request));
    }

    private static async Task<string> InvokeAgentFrameworkAsync(AIAgent agent, string prompt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var session = await agent.CreateSessionAsync();
        var response = await agent.RunAsync(prompt, session);
        return response?.Text ?? string.Empty;
    }

    private static string BuildReasoningPrompt(ReasoningRequest request) => $@"
You are an expert DIY consultant. Based on the following information, provide detailed reasoning for tool recommendations:

**Project Task:** {request.Prompt}
**Image Analysis:** {request.PhotoAnalysis.Description}
**Detected Materials:** {string.Join(", ", request.PhotoAnalysis.DetectedMaterials)}
**Customer's Existing Tools:** {string.Join(", ", request.Customer.OwnedTools)}
**Customer's Skills:** {string.Join(", ", request.Customer.Skills)}

Please provide:
1. A brief analysis of the project requirements
2. Assessment of the customer's current capabilities
3. Specific reasoning for each recommended tool
4. Safety considerations
5. Tips for success based on their skill level

Format your response with clear sections and be encouraging while being practical about safety and skill requirements.
";

    private static string GenerateFallbackReasoning(ReasoningRequest request)
    {
        var promptLower = request.Prompt.ToLowerInvariant();
        var materials = request.PhotoAnalysis.DetectedMaterials;
        var ownedTools = request.Customer.OwnedTools;
        var skills = request.Customer.Skills;

        var reasoning = new StringBuilder();
        reasoning.AppendLine($"Based on your project '{request.Prompt}' and the provided photo analysis, here's my reasoning for tool recommendations:\n");
        reasoning.AppendLine("**Task Analysis:**");
        reasoning.AppendLine($"- The image analysis highlights: {request.PhotoAnalysis.Description}");
        reasoning.AppendLine($"- Key materials involved: {string.Join(", ", materials)}\n");

        reasoning.AppendLine("**Your Profile:**");
        reasoning.AppendLine($"- Available tools: {string.Join(", ", ownedTools)}");
        reasoning.AppendLine($"- Skill level: {string.Join(", ", skills)}\n");

        reasoning.AppendLine("**Recommendations:**");

        if (promptLower.Contains("paint"))
        {
            reasoning.AppendLine("- A paint roller offers efficient coverage for large surfaces, while brushes are essential for edges and trim.");
            reasoning.AppendLine("- Drop cloths will protect surrounding areas from splatter.");
        }
        else if (promptLower.Contains("wood"))
        {
            reasoning.AppendLine("- A quality saw supports precise cuts, and wood stain finishes the project while adding protection.");
            reasoning.AppendLine("- Measuring tools remain critical for accurate results.");
        }
        else if (promptLower.Contains("tile"))
        {
            reasoning.AppendLine("- Tile cutters, spacers, and proper adhesive are necessary for a clean installation.");
            reasoning.AppendLine("- Grout and leveling tools help achieve professional results.");
        }
        else
        {
            reasoning.AppendLine("- Safety equipment (glasses, gloves) should accompany any DIY effort.");
            reasoning.AppendLine("- General-purpose tools cover most common adjustments during execution.");
        }

        reasoning.AppendLine("\n**Safety Note:** Always wear appropriate protective gear and work at a comfortable pace to avoid mistakes.");
        return reasoning.ToString();
    }
}
