using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;
using SharedEntities;
using System.Text.Json;
using ZavaAgentsMetadata;
using ZavaMAFLocal;

namespace LocationService.Endpoints;

public static class LocationEndpoints
{
    public static void MapLocationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Location");

        group.MapGet("/find/llm", FindProductLocationLlmAsync);
        group.MapGet("/find/maf", FindProductLocationMAFAsync);
        group.MapGet("/health", Health);
    }

    public static async Task<IResult> FindProductLocationLlmAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] DataServiceClient.DataServiceClient dataServiceClient,
        [FromServices] MAFLocalAgentProvider localAgentProvider,
        [FromQuery] string product,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.Llm} Finding location for product: {{Product}}", product);
        var agent = localAgentProvider.GetAgentByName(AgentMetadata.GetAgentName(AgentType.LocationServiceAgent));
        return await FindProductLocationAsync(
            logger,
            dataServiceClient,
            product,
            (prompt, token) => InvokeAgentFrameworkAsync(agent, prompt, token),
            AgentMetadata.LogPrefixes.Llm,
            cancellationToken);
    }

    public static async Task<IResult> FindProductLocationMAFAsync(
        [FromServices] ILogger<Program> logger,
        [FromServices] DataServiceClient.DataServiceClient dataServiceClient,
        [FromServices] MAFLocalAgentProvider localAgentProvider,
        [FromQuery] string product,
        CancellationToken cancellationToken)
    {
        logger.LogInformation($"{AgentMetadata.LogPrefixes.Maf} Finding location for product: {{Product}}", product);
        var agent = localAgentProvider.GetAgentByName(AgentMetadata.GetAgentName(AgentType.LocationServiceAgent));
        return await FindProductLocationAsync(
            logger,
            dataServiceClient,
            product,
            (prompt, token) => InvokeAgentFrameworkAsync(agent, prompt, token),
            AgentMetadata.LogPrefixes.Maf,
            cancellationToken);
    }

    public static IResult Health() => Results.Ok(new { Status = "Healthy", Service = "LocationService" });

    private static async Task<IResult> FindProductLocationAsync(
        ILogger logger,
        DataServiceClient.DataServiceClient dataServiceClient,
        string product,
        Func<string, CancellationToken, Task<string>> invokeAgentAsync,
        string logPrefix,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(product))
        {
            return Results.BadRequest("Product query is required.");
        }

        var prompt = BuildLocationPrompt(product);

        try
        {
            var agentResponse = await invokeAgentAsync(prompt, cancellationToken);
            logger.LogInformation("{Prefix} Raw agent response length: {Length}", logPrefix, agentResponse.Length);

            if (TryParseLocationResult(agentResponse, out var parsed))
            {
                return Results.Ok(parsed);
            }

            logger.LogWarning("{Prefix} Unable to parse agent response. Using fallback locations. Raw: {Raw}", logPrefix, TrimForLog(agentResponse));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Prefix} Agent invocation failed. Using fallback locations.", logPrefix);
        }

        return Results.Ok(await BuildFallbackResult(logger, dataServiceClient, product));
    }

    private static async Task<string> InvokeAgentFrameworkAsync(AIAgent agent, string prompt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var session = await agent.CreateSessionAsync();
        var response = await agent.RunAsync(prompt, session);
        return response?.Text ?? string.Empty;
    }

    private static async Task<LocationResult> BuildFallbackResult(
        ILogger logger,
        DataServiceClient.DataServiceClient dataServiceClient,
        string product)
    {
        try
        {
            var locations = await dataServiceClient.SearchLocationsAsync(product);
            if (locations != null && locations.Count > 0)
            {
                logger.LogInformation("Retrieved {Count} locations from DataService for product: {Product}", locations.Count, product);
                return new LocationResult { StoreLocations = locations.ToArray() };
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve locations from DataService");
        }

        return new LocationResult
        {
            StoreLocations = GenerateLocationsByProduct(product)
        };
    }

    private static StoreLocation[] GenerateLocationsByProduct(string product)
    {
        var productLower = product.ToLowerInvariant();

        if (productLower.Contains("tool") || productLower.Contains("drill") || productLower.Contains("hammer"))
        {
            return
            [
                new StoreLocation
                {
                    Section = "Hardware Tools",
                    Aisle = "A1",
                    Shelf = "Middle",
                    Description = $"Hand and power tools section - {product}"
                }
            ];
        }
        else if (productLower.Contains("paint") || productLower.Contains("brush"))
        {
            return
            [
                new StoreLocation
                {
                    Section = "Paint & Supplies",
                    Aisle = "B3",
                    Shelf = "Top",
                    Description = $"Paint and painting supplies - {product}"
                }
            ];
        }
        else if (productLower.Contains("garden") || productLower.Contains("plant"))
        {
            return
            [
                new StoreLocation
                {
                    Section = "Garden Center",
                    Aisle = "Outside",
                    Shelf = "Ground Level",
                    Description = $"Outdoor garden section - {product}"
                }
            ];
        }
        else
        {
            return
            [
                new StoreLocation
                {
                    Section = "General Merchandise",
                    Aisle = "C2",
                    Shelf = "Middle",
                    Description = $"General location for {product}"
                }
            ];
        }
    }

    private static string BuildLocationPrompt(string product) => @$"
You are an assistant that helps customers locate DIY products within a hardware store.

Return a JSON object with the following structure:
{{
    ""storeLocations"": [
        {{ ""section"": string, ""aisle"": string, ""shelf"": string, ""description"": string }},
        ...
    ]
}}

Provide at least one location. Use concise descriptions and keep aisle identifiers short (e.g., 'A1').
If a specific detail is unknown, omit the property or leave it as an empty string.

Product query: ""{product}""
";

    private static bool TryParseLocationResult(string agentResponse, out LocationResult result)
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
            if (!document.RootElement.TryGetProperty("storeLocations", out var locationsElement))
            {
                return false;
            }

            var locations = ParseStoreLocations(locationsElement);
            if (locations.Length == 0)
            {
                return false;
            }

            result = new LocationResult
            {
                StoreLocations = locations
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static StoreLocation[] ParseStoreLocations(JsonElement arrayElement)
    {
        if (arrayElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<StoreLocation>();
        }

        var results = new List<StoreLocation>();

        foreach (var item in arrayElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var section = item.TryGetProperty("section", out var sectionProp) && sectionProp.ValueKind == JsonValueKind.String
                ? sectionProp.GetString() ?? string.Empty
                : string.Empty;

            var aisle = item.TryGetProperty("aisle", out var aisleProp) && aisleProp.ValueKind == JsonValueKind.String
                ? aisleProp.GetString() ?? string.Empty
                : string.Empty;

            var shelf = item.TryGetProperty("shelf", out var shelfProp) && shelfProp.ValueKind == JsonValueKind.String
                ? shelfProp.GetString() ?? string.Empty
                : string.Empty;

            var description = item.TryGetProperty("description", out var descriptionProp) && descriptionProp.ValueKind == JsonValueKind.String
                ? descriptionProp.GetString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrWhiteSpace(section) && string.IsNullOrWhiteSpace(description))
            {
                continue;
            }

            results.Add(new StoreLocation
            {
                Section = section,
                Aisle = aisle,
                Shelf = shelf,
                Description = description
            });
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
