using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;
using SharedEntities;
using System.Text.Json;
using ZavaAgentsMetadata;
using ZavaMAFLocal;

namespace MatchmakingService.Endpoints;

public static class MatchmakingEndpoints
{
    public static void MapMatchmakingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Matchmaking");

        group.MapPost("/alternatives/llm", FindAlternativesLlmAsync);
        group.MapPost("/alternatives/maf", FindAlternativesMAFAsync);
        group.MapPost("/alternatives/directcall", FindAlternativesDirectCallAsync);
        group.MapGet("/health", Health);
    }

    public static async Task<IResult> FindAlternativesLlmAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] MAFLocalAgentProvider localAgentProvider,
        [FromBody] AlternativesRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.Llm} Finding alternatives for product: {{ProductQuery}}, User: {{UserId}}", request.ProductQuery, request.UserId);
        var agent = localAgentProvider.GetAgentByName(AgentMetadata.GetAgentName(AgentType.ProductMatchmakingAgent));
        return await FindAlternativesAsync(logger, request, (prompt, token) => InvokeAgentFrameworkAsync(agent, prompt, token), AgentMetadata.LogPrefixes.Llm, cancellationToken);
    }

    public static async Task<IResult> FindAlternativesMAFAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] MAFLocalAgentProvider localAgentProvider,
        [FromBody] AlternativesRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.Maf} Finding alternatives for product: {{ProductQuery}}, User: {{UserId}}", request.ProductQuery, request.UserId);
        var agent = localAgentProvider.GetAgentByName(AgentMetadata.GetAgentName(AgentType.ProductMatchmakingAgent));
        return await FindAlternativesAsync(logger, request, (prompt, token) => InvokeAgentFrameworkAsync(agent, prompt, token), AgentMetadata.LogPrefixes.Maf, cancellationToken);
    }

    public static IResult FindAlternativesDirectCallAsync(
        [FromServices] ILogger<Program> logger,
        [FromBody] AlternativesRequest request)
    {
        logger.LogInformation("[DirectCall] Finding alternatives for product: {ProductQuery}, User: {UserId}", request.ProductQuery, request.UserId);
        return Results.Ok(BuildFallbackResult(request));
    }

    public static IResult Health()
        => Results.Ok(new { Status = "Healthy", Service = "MatchmakingService" });

    private static async Task<IResult> FindAlternativesAsync(
        ILogger logger,
        AlternativesRequest request,
        Func<string, CancellationToken, Task<string>> invokeAgentAsync,
        string logPrefix,
        CancellationToken cancellationToken)
    {
        var prompt = BuildMatchmakingPrompt(request);

        try
        {
            var agentResponse = await invokeAgentAsync(prompt, cancellationToken);
            logger.LogInformation("{Prefix} Raw agent response length: {Length}", logPrefix, agentResponse.Length);

            if (TryParseMatchmakingResult(agentResponse, out var result))
            {
                return Results.Ok(result);
            }

            logger.LogWarning("{Prefix} Unable to parse agent response. Using fallback recommendations. Raw: {Raw}", logPrefix, TrimForLog(agentResponse));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Prefix} Agent invocation failed. Falling back to heuristic recommendations.", logPrefix);
        }

        return Results.Ok(BuildFallbackResult(request));
    }

    private static async Task<string> InvokeAgentFrameworkAsync(AIAgent agent, string prompt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var session = await agent.CreateSessionAsync();
        var response = await agent.RunAsync(prompt, session);
        return response?.Text ?? string.Empty;
    }

    private static MatchmakingResult BuildFallbackResult(AlternativesRequest request)
    {
        var normalizedQuery = request.ProductQuery.Trim();
        var baseName = string.IsNullOrEmpty(normalizedQuery) ? "your project" : normalizedQuery;

        var alternatives = new List<ProductInfo>
        {
            new()
            {
                Name = $"Alternative tool for {baseName}",
                Sku = "ALT-001",
                Price = 19.99m,
                IsAvailable = true,
                InStock = true,
                Section = "Hardware Tools",
                Location = "Aisle A1"
            },
            new()
            {
                Name = $"Premium alternative for {baseName}",
                Sku = "ALT-002",
                Price = 29.99m,
                IsAvailable = true,
                InStock = true,
                Section = "Hardware Tools",
                Location = "Aisle A1"
            }
        };

        var similarProducts = new List<ProductInfo>
        {
            new()
            {
                Name = $"Similar item related to {baseName}",
                Sku = "SIM-001",
                Price = 24.99m,
                IsAvailable = true,
                InStock = true,
                Section = "Hardware Tools",
                Location = "Aisle A2"
            }
        };

        return new MatchmakingResult
        {
            Alternatives = alternatives.ToArray(),
            SimilarProducts = similarProducts.ToArray()
        };
    }

    private static string BuildMatchmakingPrompt(AlternativesRequest request) => @$"
You are an AI assistant that recommends alternative DIY tools and similar products for store customers.

Return a JSON object with the following structure:
{{
    ""alternatives"": [
        {{ ""name"": string, ""sku"": string, ""price"": number, ""isAvailable"": boolean, ""section""?: string, ""location""?: string }},
    ...
  ],
    ""similarProducts"": [
        {{ ""name"": string, ""sku"": string, ""price"": number, ""isAvailable"": boolean, ""section""?: string, ""location""?: string }},
    ...
  ]
}}

Ensure all SKUs are uppercase with dashes, prices use decimals, and include at least one item in each array.
Avoid extra commentary or markdown.

Product query: ""{request.ProductQuery}""
User identifier: ""{request.UserId}""
";

    private static bool TryParseMatchmakingResult(string agentResponse, out MatchmakingResult result)
    {
        result = default!;
        if (string.IsNullOrWhiteSpace(agentResponse))
        {
            return false;
        }

        var json = ExtractFirstJsonObject(agentResponse);
        if (json is null)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var alternatives = root.TryGetProperty("alternatives", out var alternativesProp)
                ? ParseProductArray(alternativesProp)
                : Array.Empty<ProductInfo>();

            var similarProducts = root.TryGetProperty("similarProducts", out var similarProp)
                ? ParseProductArray(similarProp)
                : Array.Empty<ProductInfo>();

            if (alternatives.Length == 0 && similarProducts.Length == 0)
            {
                return false;
            }

            result = new MatchmakingResult
            {
                Alternatives = alternatives,
                SimilarProducts = similarProducts
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ProductInfo[] ParseProductArray(JsonElement arrayElement)
    {
        if (arrayElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<ProductInfo>();
        }

        var results = new List<ProductInfo>();
        foreach (var item in arrayElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var hasAvailability = item.TryGetProperty("isAvailable", out var availableProp) && availableProp.ValueKind is JsonValueKind.True or JsonValueKind.False;
            var hasInStock = item.TryGetProperty("inStock", out var inStockProp) && inStockProp.ValueKind is JsonValueKind.True or JsonValueKind.False;

            var product = new ProductInfo
            {
                Name = item.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
                    ? nameProp.GetString() ?? string.Empty
                    : string.Empty,
                Sku = item.TryGetProperty("sku", out var skuProp) && skuProp.ValueKind == JsonValueKind.String
                    ? skuProp.GetString() ?? string.Empty
                    : string.Empty,
                Price = item.TryGetProperty("price", out var priceProp) && priceProp.ValueKind is JsonValueKind.Number && priceProp.TryGetDecimal(out var price)
                    ? price
                    : 0m,
                IsAvailable = hasAvailability && availableProp.GetBoolean(),
                Section = item.TryGetProperty("section", out var sectionProp) && sectionProp.ValueKind == JsonValueKind.String
                    ? sectionProp.GetString() ?? string.Empty
                    : string.Empty,
                Location = item.TryGetProperty("location", out var locationProp) && locationProp.ValueKind == JsonValueKind.String
                    ? locationProp.GetString() ?? string.Empty
                    : string.Empty
            };

            product.InStock = hasInStock ? inStockProp.GetBoolean() : product.IsAvailable;

            if (!string.IsNullOrWhiteSpace(product.Name) && !string.IsNullOrWhiteSpace(product.Sku))
            {
                results.Add(product);
            }
        }

        return results.ToArray();
    }

    private static string? ExtractFirstJsonObject(string input)
    {
        var start = input.IndexOf('{');
        if (start < 0)
        {
            return null;
        }

        var depth = 0;
        for (var i = start; i < input.Length; i++)
        {
            var c = input[i];
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
            }

            if (depth == 0)
            {
                return input.Substring(start, i - start + 1).Trim();
            }
        }

        return null;
    }

    private static string TrimForLog(string value, int maxLength = 400)
        => value.Length <= maxLength ? value : value[..maxLength] + "...";
}
