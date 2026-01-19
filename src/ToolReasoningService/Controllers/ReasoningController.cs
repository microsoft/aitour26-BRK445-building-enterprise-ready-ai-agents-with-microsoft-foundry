using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;
using SharedEntities;
using System.Text;
using ZavaAgentsMetadata;
using ZavaMAFLocal;
using ZavaMAFOllama;

namespace ToolReasoningService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReasoningController : ControllerBase
{
    private readonly ILogger<ReasoningController> _logger;
    private readonly AIAgent _agentFxAgent;
    private readonly AIAgent _ollamaAgent;

    public ReasoningController(
        ILogger<ReasoningController> logger,
        MAFLocalAgentProvider localAgentProvider,
        MAFOllamaAgentProvider ollamaAgentProvider)
    {
        _logger = logger;
        _agentFxAgent = localAgentProvider.GetAgentByName(AgentMetadata.GetLocalAgentName(AgentType.ToolReasoningAgent));
        _ollamaAgent = ollamaAgentProvider.GetAgentByName(AgentMetadata.GetAgentName(AgentType.ToolReasoningAgent));
    }

    [HttpPost("analyze_generate/llm")]
    public async Task<ActionResult<string>> GenerateReasoningLlmAsync([FromBody] ReasoningRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AgentMetadata.LogPrefixes.Llm} Generating reasoning for prompt");

        // LLM endpoint uses MAF under the hood since we removed SK
        return await GenerateReasoningAsync(
            request,
            async (prompt, token) => await InvokeAgentFrameworkAsync(prompt, token),
            AgentMetadata.LogPrefixes.Llm,
            cancellationToken);
    }

    [HttpPost("analyze_generate/maf_local")]  // Using constant AgentMetadata.FrameworkIdentifiers.MafLocal
    public async Task<ActionResult<string>> GenerateReasoningMAFLocalAsync([FromBody] ReasoningRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AgentMetadata.LogPrefixes.MafLocal} Generating reasoning for prompt");

        return await GenerateReasoningAsync(
            request,
            async (prompt, token) => await InvokeAgentFrameworkAsync(prompt, token),
            AgentMetadata.LogPrefixes.MafLocal,
            cancellationToken);
    }

    [HttpPost("analyze_generate/maf_foundry")]  // Using constant AgentMetadata.FrameworkIdentifiers.MafFoundry
    public async Task<ActionResult<string>> GenerateReasoningMAFFoundryAsync([FromBody] ReasoningRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AgentMetadata.LogPrefixes.MafFoundry} Generating reasoning for prompt");

        return await GenerateReasoningAsync(
            request,
            async (prompt, token) => await InvokeAgentFrameworkAsync(prompt, token),
            AgentMetadata.LogPrefixes.MafFoundry,
            cancellationToken);
    }

    [HttpPost("analyze_generate/maf_ollama")]  // Using constant AgentMetadata.FrameworkIdentifiers.MafOllama
    public async Task<ActionResult<string>> GenerateReasoningMAFOllamaAsync([FromBody] ReasoningRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"{AgentMetadata.LogPrefixes.MafOllama} Generating reasoning for prompt");

        return await GenerateReasoningAsync(
            request,
            async (prompt, token) => await InvokeOllamaAgentAsync(prompt, token),
            AgentMetadata.LogPrefixes.MafOllama,
            cancellationToken);
    }

    [HttpPost("analyze_generate/direct_call")]
    public ActionResult<string> GenerateReasoningDirectCallAsync([FromBody] ReasoningRequest request)
    {
        _logger.LogInformation("[DirectCall] Generating reasoning for prompt");

        // add a sleep of 1 seconds to emulate the analysis time
        Thread.Sleep(1000);

        // DirectCall mode bypasses AI orchestration and returns fallback response immediately
        return Ok(GenerateFallbackReasoning(request));
    }

    private async Task<ActionResult<string>> GenerateReasoningAsync(
        ReasoningRequest request,
        Func<string, CancellationToken, Task<string>> invokeAgent,
        string logPrefix,
        CancellationToken cancellationToken)
    {
        var reasoningPrompt = BuildReasoningPrompt(request);

        try
        {
            var agentResponse = await invokeAgent(reasoningPrompt, cancellationToken);
            _logger.LogInformation("{Prefix} Raw agent response length: {Length}", logPrefix, agentResponse.Length);

            if (!string.IsNullOrWhiteSpace(agentResponse))
            {
                return Ok(agentResponse);
            }

            _logger.LogWarning("{Prefix} Empty response received. Falling back to heuristic reasoning.", logPrefix);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Prefix} Agent invocation failed. Falling back to heuristic reasoning.", logPrefix);
        }

        return Ok(GenerateFallbackReasoning(request));
    }

    private async Task<string> InvokeAgentFrameworkAsync(string prompt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var thread = _agentFxAgent.GetNewThread();
        var response = await _agentFxAgent.RunAsync(prompt, thread);
        return response?.Text ?? string.Empty;
    }

    private async Task<string> InvokeOllamaAgentAsync(string prompt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var thread = _ollamaAgent.GetNewThread();
        var response = await _ollamaAgent.RunAsync(prompt, thread);
        return response?.Text ?? string.Empty;
    }

    #region Prompt & fallback helpers

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

    #endregion
}
