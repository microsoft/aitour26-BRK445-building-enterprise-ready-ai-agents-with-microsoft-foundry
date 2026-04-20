using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;
using SharedEntities;
using System.Text.Json;
using ZavaAgentsMetadata;
using ZavaMAFFoundry;
using ZavaMAFLocal;

namespace AnalyzePhotoService.Endpoints;

public static class PhotoAnalysisEndpoints
{
    public static void MapPhotoAnalysisEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/PhotoAnalysis");

        group.MapPost("/analyzellm", AnalyzeLLMAsync).DisableAntiforgery();
        group.MapPost("/analyzemaf_local", AnalyzeMAFLocalAsync).DisableAntiforgery();
        group.MapPost("/analyzemaf_foundry", AnalyzeMAFFoundryAsync).DisableAntiforgery();
        group.MapPost("/analyzedirectcall", AnalyzeDirectCallAsync).DisableAntiforgery();
    }

    public static async Task<IResult> AnalyzeLLMAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] MAFLocalAgentProvider localAgentProvider,
        [FromForm] IFormFile image,
        [FromForm] string prompt,
        CancellationToken cancellationToken = default)
    {
        if (image is null)
        {
            return Results.BadRequest("No image file was provided.");
        }

        logger.LogInformation($"{AgentMetadata.LogPrefixes.Llm} Analyzing photo. Prompt: {{Prompt}}", prompt);

        var agent = localAgentProvider.GetAgentByName(AgentMetadata.GetAgentName(AgentType.PhotoAnalyzerAgent));
        // LLM endpoint uses MAF under the hood since we removed SK
        return await AnalyzeWithAgentAsync(
            logger,
            prompt,
            image.FileName,
            async (analysisPrompt) => await GetAgentFxResponseAsync(agent, analysisPrompt),
            AgentMetadata.LogPrefixes.Llm,
            cancellationToken);
    }

    public static async Task<IResult> AnalyzeMAFLocalAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] MAFLocalAgentProvider localAgentProvider,
        [FromForm] IFormFile image,
        [FromForm] string prompt,
        CancellationToken cancellationToken = default)
    {
        if (image is null)
        {
            return Results.BadRequest("No image file was provided.");
        }

        logger.LogInformation($"{AgentMetadata.LogPrefixes.MafLocal} Analyzing photo. Prompt: {{Prompt}}", prompt);

        var agent = localAgentProvider.GetLocalAgentByName(AgentType.PhotoAnalyzerAgent);
        return await AnalyzeWithAgentAsync(
            logger,
            prompt,
            image.FileName,
            async (analysisPrompt) => await GetAgentFxResponseAsync(agent, analysisPrompt),
            AgentMetadata.LogPrefixes.MafLocal,
            cancellationToken);
    }

    public static async Task<IResult> AnalyzeMAFFoundryAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] MAFFoundryAgentProvider localAgentProvider,
        [FromForm] IFormFile image,
        [FromForm] string prompt,
        CancellationToken cancellationToken = default)
    {
        if (image is null)
        {
            return Results.BadRequest("No image file was provided.");
        }

        logger.LogInformation($"{AgentMetadata.LogPrefixes.MafFoundry} Analyzing photo. Prompt: {{Prompt}}", prompt);

        var agent = localAgentProvider.GetAIAgent(AgentMetadata.GetAgentName(AgentType.PhotoAnalyzerAgent));
        return await AnalyzeWithAgentAsync(
            logger,
            prompt,
            image.FileName,
            async (analysisPrompt) => await GetAgentFxResponseAsync(agent, analysisPrompt),
            AgentMetadata.LogPrefixes.MafFoundry,
            cancellationToken);
    }

    public static async Task<IResult> AnalyzeDirectCallAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] MAFLocalAgentProvider localAgentProvider,
        [FromForm] IFormFile image,
        [FromForm] string prompt,
        CancellationToken cancellationToken = default)
    {
        if (image is null)
        {
            return Results.BadRequest("No image file was provided.");
        }

        logger.LogInformation("[DirectCall] Analyzing photo. Prompt: {Prompt}", prompt);

        // add a sleep of 3 seconds to emulate the image analysis time
        await Task.Delay(3000);

        // Fallback path.
        var fallbackDescription = BuildFallbackDescription(prompt);
        var fallback = new PhotoAnalysisResult
        {
            Description = fallbackDescription,
            DetectedMaterials = DetermineDetectedMaterials(prompt, image.FileName)
        };
        return Results.Ok(fallback);
    }

    // Shared high-level analysis routine for both endpoints.
    private static async Task<IResult> AnalyzeWithAgentAsync(
        ILogger logger,
        string userPrompt,
        string fileName,
        Func<string, Task<string>> invokeAgent,
        string logPrefix,
        CancellationToken cancellationToken)
    {
        var analysisPrompt = BuildAnalysisPrompt(userPrompt, fileName);
        var fallbackDescription = BuildFallbackDescription(userPrompt);

        try
        {
            var agentRawResponse = await invokeAgent(analysisPrompt);
            logger.LogInformation("{Prefix} Raw agent response length: {Length}", logPrefix, agentRawResponse.Length);

            if (TryParsePhotoAnalysis(agentRawResponse, out var parsed))
            {
                return Results.Ok(parsed);
            }

            logger.LogWarning("{Prefix} Parsed result invalid or incomplete. Using heuristic fallback. Raw: {Raw}", logPrefix, TrimForLog(agentRawResponse));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Prefix} Invocation failed. Using heuristic fallback.", logPrefix);
        }

        // Fallback path.
        var fallback = new PhotoAnalysisResult
        {
            Description = fallbackDescription,
            DetectedMaterials = DetermineDetectedMaterials(userPrompt, fileName)
        };
        return Results.Ok(fallback);
    }

    // Agent invocation helper
    private static async Task<string> GetAgentFxResponseAsync(AIAgent agent, string prompt)
    {
        var session = await agent.CreateSessionAsync();
        var response = await agent.RunAsync(prompt, session);
        return response?.Text ?? string.Empty;
    }

    private static string BuildAnalysisPrompt(string prompt, string fileName)
    {
        return $@"You are an AI assistant that analyzes photos of rooms for renovation and home-improvement projects.
Given the image filename and the user's short prompt, return a JSON object with exactly two fields:
  - description: a brief natural-language description of what the image shows and what renovation tasks are likely required
  - detectedMaterials: an array of short strings naming materials, finishes or items that appear relevant (e.g. 'paint', 'tile', 'wood', 'grout')

Return only valid JSON. Do not include any surrounding markdown or explanatory text.

ImageFileName: {fileName}
UserPrompt: {prompt}
";
    }

    #region JSON parsing logic centralized.
    private static bool TryParsePhotoAnalysis(string agentResponse, out PhotoAnalysisResult result)
    {
        result = default!;
        if (string.IsNullOrWhiteSpace(agentResponse)) return false;

        var json = ExtractJson(agentResponse);
        if (json is null) return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("description", out var descProp) || descProp.ValueKind != JsonValueKind.String)
                return false;

            if (!root.TryGetProperty("detectedMaterials", out var materialsProp) || materialsProp.ValueKind != JsonValueKind.Array)
                return false;

            var description = descProp.GetString() ?? string.Empty;
            var materials = new List<string>();
            foreach (var item in materialsProp.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                {
                    materials.Add(item.GetString()!);
                }
            }

            if (string.IsNullOrWhiteSpace(description) || materials.Count == 0)
                return false;

            result = new PhotoAnalysisResult
            {
                Description = description,
                DetectedMaterials = materials.ToArray()
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // Extracts first valid JSON object substring from a response.
    private static string? ExtractJson(string raw)
    {
        var first = raw.IndexOf('{');
        var last = raw.LastIndexOf('}');
        if (first < 0 || last <= first) return null;
        return raw.Substring(first, last - first + 1);
    }
    #endregion

    #region Fallback description and logging
    private static string BuildFallbackDescription(string prompt) =>
        $"Photo analysis for prompt: '{prompt}'. Detected a room that needs renovation work. The image shows surfaces that require preparation and finishing.";

    private static string TrimForLog(string value, int max = 500)
        => value.Length <= max ? value : value.Substring(0, max) + "...";

    // Simple heuristic detector.
    private static string[] DetermineDetectedMaterials(string prompt, string? fileName)
    {
        var materials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var promptLower = prompt.ToLowerInvariant();
        var fileNameLower = fileName?.ToLowerInvariant() ?? string.Empty;

        bool Contains(params string[] keys) => keys.Any(k => promptLower.Contains(k) || fileNameLower.Contains(k));

        if (Contains("paint", "wall"))
            AddRange(materials, "paint", "wall", "surface preparation");

        if (Contains("wood", "deck"))
            AddRange(materials, "wood", "stain", "sanding");

        if (Contains("tile", "bathroom"))
            AddRange(materials, "tile", "grout", "adhesive");

        if (Contains("garden", "landscape"))
            AddRange(materials, "soil", "plants", "tools");

        if (materials.Count == 0)
            AddRange(materials, "general tools", "measuring", "safety equipment");

        return materials.ToArray();
    }

    private static void AddRange(HashSet<string> set, params string[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v)) set.Add(v);
        }
    }

    // Simple DTO used to deserialize the AI's JSON response (kept for potential future direct deserialization).
    private record AiPhotoAnalysisResult(string Description, string[] DetectedMaterials);
    #endregion
}
