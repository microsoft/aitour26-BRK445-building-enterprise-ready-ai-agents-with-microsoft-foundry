using Azure.AI.Projects;
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
    public MAFFoundryAgentProvider(string microsoftFoundryProjectEndpoint, string tenantId = "")
    {
        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrEmpty(tenantId))
        {
            credentialOptions = new DefaultAzureCredentialOptions()
            { TenantId = tenantId };
        }
        var tokenCredential = new DefaultAzureCredential(options: credentialOptions);

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
    /// <param name="projectEndpoint">The Microsoft Foundry project endpoint.</param>
    public static WebApplicationBuilder AddMAFFoundryAgents(
        this WebApplicationBuilder builder)
        // , string projectEndpoint, string tenantId = "")
    {

        // Register MAF agent providers using new extension methods
        var projectEndpoint = builder.Configuration.GetConnectionString("microsoftfoundryproject");
        var tenantId = builder.Configuration.GetConnectionString("tenantId");

        // Register the provider as singleton
        builder.Services.AddSingleton(_ =>
        new MAFFoundryAgentProvider(projectEndpoint, tenantId));

        var logger = builder.Services.BuildServiceProvider().GetService<ILoggerFactory>()?.
            CreateLogger("MAFFoundryAgentExtensions");

        logger?.LogInformation("Registering MAF Foundry agents from endpoint: {Endpoint}", projectEndpoint);

        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrEmpty(tenantId))
        { 
            credentialOptions = new DefaultAzureCredentialOptions() 
            { TenantId = tenantId }; 
        }
        var tokenCredential = new DefaultAzureCredential(options: credentialOptions);

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

