#pragma warning disable CA2252, OPENAI001

using Azure.AI.OpenAI;
using Azure.AI.Projects;
using Azure.Core;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZavaAgentsMetadata;

namespace ZavaMAFFoundry;

/// <summary>
/// Provides access to agents from Microsoft Foundry.
/// Agents are pre-deployed and managed in Microsoft Foundry.
/// </summary>
public class MAFFoundryAgentProvider
{
    private readonly AIProjectClient _projectClient;
    private readonly IConfiguration _configuration;
    private readonly string _tenantId;
    public MAFFoundryAgentProvider(string microsoftFoundryProjectEndpoint, IConfiguration configuration, string tenantId = "")
    {
        _configuration = configuration;
        _tenantId = tenantId;

        DefaultAzureCredential tokenCredential = GetAzureCredentials();

        _projectClient = new(
            endpoint: new Uri(microsoftFoundryProjectEndpoint),
            tokenProvider: tokenCredential);
    }

    /// <summary>
    /// Gets an AI agent by its agent ID from Microsoft Foundry (synchronous).
    /// </summary>
    public AIAgent GetAIAgent(string agentName, List<AITool> tools = null)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            throw new ArgumentException("Agent Name cannot be null or empty", nameof(agentName));
        }

        return _projectClient.GetAIAgent(name: agentName, tools: tools);
    }

    public AIAgent GetOrCreateAIAgent(string agentName,
        string model = "",
        string agentInstructions = "", List<AITool> tools = null)
    {
        if (string.IsNullOrWhiteSpace(agentName))
        {
            throw new ArgumentException("Agent Name cannot be null or empty", nameof(agentName));
        }
        AIAgent agent = null;

        try
        {
            agent = _projectClient.GetAIAgent(name: agentName, tools: tools);
        }
        catch
        {
        }

        agent ??= _projectClient.CreateAIAgent(
                name: agentName,
                model: model,
                instructions: agentInstructions,
                tools: tools);

        return agent;
    }

    public IChatClient GetChatClient(string deploymentName = "")
    {
        if (string.IsNullOrEmpty(deploymentName))
        {
            deploymentName = _configuration["AI_ChatDeploymentName"] ?? "gpt-5-mini";
        }

        var azureOpenAIChatClient =  _projectClient.GetAzureOpenAIChatClient(deploymentName);

        // get credentials        
        DefaultAzureCredential tokenCredential = GetAzureCredentials();
        var endpoint = new Uri(NormalizeEndpoint(azureOpenAIChatClient.Endpoint.AbsoluteUri));
        var azureOpenAIClient = new AzureOpenAIClient(
            endpoint: endpoint,
            credential: tokenCredential);

        return azureOpenAIClient
            .GetChatClient(deploymentName)
            .AsIChatClient();
    }

    public IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(string deploymentName = "")
    {
        if (string.IsNullOrEmpty(deploymentName))
        {
            deploymentName = _configuration["AI_embeddingsDeploymentName"] ?? "text-embedding-3-small";
        }
        var azureOpenAIEmbeddingClient = _projectClient.GetAzureOpenAIEmbeddingClient(deploymentName);

        // get credentials        
        DefaultAzureCredential tokenCredential = GetAzureCredentials();
        var endpoint = new Uri(NormalizeEndpoint(azureOpenAIEmbeddingClient.Endpoint.AbsoluteUri));
        var azureOpenAIClient = new AzureOpenAIClient(
            endpoint: endpoint,
            credential: tokenCredential);

        return azureOpenAIClient
            .GetEmbeddingClient(deploymentName)
            .AsIEmbeddingGenerator();
    }


    private DefaultAzureCredential GetAzureCredentials()
    {
        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrEmpty(_tenantId))
        {
            credentialOptions = new DefaultAzureCredentialOptions()
            { TenantId = _tenantId };
        }
        var tokenCredential = new DefaultAzureCredential(options: credentialOptions);
        return tokenCredential;
    }

    internal static string NormalizeEndpoint(string endpoint)
    {
        // If the endpoint contains ".services.ai.azure.com/api/projects/", replace with ".cognitiveservices.azure.com"
        if (endpoint.Contains(".services.ai.azure.com/api/projects/"))
        {
            var idx = endpoint.IndexOf(".services.ai.azure.com/api/projects/", StringComparison.OrdinalIgnoreCase);
            var prefix = endpoint.Substring(0, idx);
            return $"{prefix}.cognitiveservices.azure.com";
        }
        return endpoint;
    }
}

/// <summary>
/// Extension methods for registering MAF Foundry agents in dependency injection.
/// Follows the pattern of AddAIAgent(name, factory) for individual agent registration.
/// </summary>
public static class MAFFoundryAgentExtensions
{
    /// <summary>
    /// Registers all MAF Foundry agents to the service collection using the recommended pattern.
    /// Each agent is registered as a keyed singleton AIAgent service.
    /// Agents are retrieved from Microsoft Foundry by their agent IDs.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    public static WebApplicationBuilder AddMAFFoundryAgents(
        this WebApplicationBuilder builder)
    {

        // Register MAF agent providers using new extension methods
        var projectEndpoint = builder.Configuration.GetConnectionString("microsoftfoundryproject");
        var tenantId = builder.Configuration.GetConnectionString("tenantId");

        var logger = builder.Services.BuildServiceProvider().GetService<ILoggerFactory>()?.
            CreateLogger("MAFFoundryAgentExtensions");

        // If no Foundry project endpoint is configured, skip registration to allow local runs
        if (string.IsNullOrWhiteSpace(projectEndpoint))
        {
            logger?.LogWarning("Microsoft Foundry project endpoint not configured; skipping Foundry agent registration.");
            return builder;
        }

        // Register the MAFFoundryAgentProvider as singleton
        MAFFoundryAgentProvider mafFoundryAgentProvider = new(projectEndpoint, builder.Configuration, tenantId);
        builder.Services.AddSingleton(_ => mafFoundryAgentProvider);

        // Register the IChatClient as is used in other scenarios
        IChatClient chatClient = mafFoundryAgentProvider.GetChatClient();        
        builder.Services.AddChatClient(chatClient);

        // register the IEmbeddingGenerator as is used in other scenarios
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = mafFoundryAgentProvider.GetEmbeddingGenerator();        
        builder.Services.AddSingleton(embeddingGenerator);

        logger?.LogInformation("Registering MAF Foundry agents from endpoint: {Endpoint}", projectEndpoint);

        logger?.LogInformation("Creating [DefaultAzureCredential]");
        TokenCredential tokenCredential = new DefaultAzureCredential();

        if (!string.IsNullOrEmpty(tenantId))
        {
            logger?.LogInformation($"Creating [DefaultAzureCredential] Using tenant ID: {tenantId} for Azure credentials");
            var credentialOptions = new DefaultAzureCredentialOptions();
            credentialOptions = new DefaultAzureCredentialOptions()
            { TenantId = tenantId };
            tokenCredential = new DefaultAzureCredential(options: credentialOptions);
        }

        AIProjectClient projectClient = new(
            endpoint: new Uri(projectEndpoint),
            tokenProvider: tokenCredential);

        foreach (var agentType in AgentMetadata.AllAgents)
        {
            var agentName = AgentMetadata.GetAgentName(agentType);
            var instructions = AgentMetadata.GetAgentInstructions(agentType);

            // Registration logic for each agent would go here if needed
            builder.AddAIAgent(agentName, (sp, key) =>
            {
                return projectClient.GetAIAgent(agentName);
            });

            logger?.LogDebug($"Registered MAF Foundry agent: {agentName} as keyed singleton");

        }

        logger?.LogInformation($"Completed registration of {AgentMetadata.AllAgents.Count()} MAF Foundry agents");
        return builder;
    }
}

