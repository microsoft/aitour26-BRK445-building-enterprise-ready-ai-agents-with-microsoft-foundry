using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using ZavaAgentsMetadata;

namespace ZavaMAFOllama;

/// <summary>
/// Provides locally-created agents using the Microsoft Agent Framework with Ollama as the backend.
/// Agents are created with instructions and tools configured locally using IChatClient from Ollama.
/// </summary>
public class MAFOllamaAgentProvider
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the MAFOllamaAgentProvider.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="chatClient">The chat client to use for agent creation (used by agent factories).</param>
    public MAFOllamaAgentProvider(
        IServiceProvider serviceProvider,
        IChatClient chatClient)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Gets an agent by name string.
    /// </summary>
    public AIAgent GetAgentByName(string agentName)
    {
        return _serviceProvider.GetRequiredKeyedService<AIAgent>(agentName);
    }

    /// <summary>
    /// Gets a local agent by agent type.
    /// </summary>
    public AIAgent GetLocalAgentByName(AgentType agent)
    {
        return GetAgentByName(AgentMetadata.GetOllamaAgentName(agent));
    }

    /// <summary>
    /// Gets a local workflow by name.
    /// </summary>
    public Workflow GetLocalWorkflowByName(string workflowName)
    {
        return _serviceProvider.GetRequiredKeyedService<Workflow>(workflowName);
    }
}

/// <summary>
/// Extension methods for registering Ollama MAF agents in dependency injection.
/// Follows the pattern of AddAIAgent(name, factory) for individual agent registration.
/// </summary>
public static class MAFOllamaAgentExtensions
{
    /// <summary>
    /// Registers all Ollama MAF agents to the service collection using the recommended pattern.
    /// Each agent is registered as a keyed singleton AIAgent service.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    public static WebApplicationBuilder AddMAFOllamaAgents(
        this WebApplicationBuilder builder)
    {
        using var provider = builder.Services.BuildServiceProvider();
        var logger = provider.GetService<ILoggerFactory>()?
            .CreateLogger("MAFOllamaAgentExtensions");

        // Get Ollama configuration
        var ollamaEndpoint = builder.Configuration["Ollama__Endpoint"] 
            ?? builder.Configuration.GetConnectionString("ollamaEndpoint")
            ?? "http://localhost:11434";
        var chatModel = builder.Configuration["Ollama__ChatModel"] ?? "ministral-3";

        logger?.LogInformation("Registering MAF Ollama agents using endpoint: {Endpoint}, model: {Model}", 
            ollamaEndpoint, chatModel);

        // Create Ollama client - OllamaApiClient directly implements IChatClient
        var ollamaClient = new OllamaApiClient(new Uri(ollamaEndpoint), chatModel);
        builder.Services.AddKeyedSingleton<IChatClient>("ollama", ollamaClient);

        // Register the provider as singleton
        builder.Services.AddSingleton<MAFOllamaAgentProvider>(sp =>
        {
            var serviceLogger = sp.GetService<ILoggerFactory>()?
                .CreateLogger("MAFOllamaAgentExtensions");
            serviceLogger?.LogInformation("Creating MAFOllamaAgentProvider with Ollama IChatClient");

            var ollamaChatClient = sp.GetRequiredKeyedService<IChatClient>("ollama");
            return new MAFOllamaAgentProvider(sp, ollamaChatClient);
        });

        // Register each agent individually as keyed singleton
        foreach (var agentType in AgentMetadata.AllAgents)
        {
            var agentName = AgentMetadata.GetOllamaAgentName(agentType);
            var instructions = AgentMetadata.GetAgentInstructions(agentType);

            logger?.LogInformation(
                "Creating MAF Ollama agent: {AgentName} - Type: {AgentType}",
                agentName,
                agentType);

            // Registration logic for each agent
            builder.AddAIAgent(agentName, (sp, key) =>
            {
                // create agent using the keyed Ollama chat client
                var ollamaChatClient = sp.GetRequiredKeyedService<IChatClient>("ollama");
                return ollamaChatClient.CreateAIAgent(
                    name: agentName,
                    instructions: instructions);
            });

            logger?.LogDebug($"Registered MAF Ollama agent: {agentName} as keyed singleton");
        }

        logger?.LogInformation("Completed registration of {Count} MAF Ollama agents", AgentMetadata.AllAgents.Count());
        return builder;
    }

    /// <summary>
    /// Registers all Ollama MAF workflows to the service collection using the recommended pattern.
    /// Each workflow is registered as a keyed singleton Workflow service.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    public static WebApplicationBuilder AddMAFOllamaWorkflows(
        this WebApplicationBuilder builder)
    {
        using var provider = builder.Services.BuildServiceProvider();
        var logger = provider.GetService<ILoggerFactory>()?
            .CreateLogger("MAFOllamaAgentExtensions");
        logger?.LogInformation("Registering MAF Ollama workflows");

        // Register the workflow as a keyed singleton
        builder.AddWorkflow("OllamaSequentialWorkflow", (sp, key) =>
            {
                var workFlowName = "OllamaSequentialWorkflow";
                logger?.LogInformation($"Creating MAF Ollama workflow: {workFlowName} - Type: Sequential");
                
                // create agent
                var ollamaAgentProvider = sp.GetRequiredService<MAFOllamaAgentProvider>();
                var productSearchAgent = ollamaAgentProvider.GetLocalAgentByName(AgentType.ProductSearchAgent);
                var productMatchmakingAgent = ollamaAgentProvider.GetLocalAgentByName(AgentType.ProductMatchmakingAgent);
                var locationServiceAgent = ollamaAgentProvider.GetLocalAgentByName(AgentType.LocationServiceAgent);
                var navigationAgent = ollamaAgentProvider.GetLocalAgentByName(AgentType.NavigationAgent);

                var workflow = AgentWorkflowBuilder.BuildSequential(workFlowName,
                    [productSearchAgent,
                    productMatchmakingAgent,
                    locationServiceAgent,
                    navigationAgent]);
                return workflow;
            });

        // Register the workflow as a keyed singleton
        builder.AddWorkflow("OllamaConcurrentWorkflow", (sp, key) =>
        {
            var workFlowName = "OllamaConcurrentWorkflow";

            logger?.LogInformation($"Creating MAF Ollama workflow: {workFlowName} - Type: Concurrent");

            // create agent
            var ollamaAgentProvider = sp.GetRequiredService<MAFOllamaAgentProvider>();
            var productSearchAgent = ollamaAgentProvider.GetLocalAgentByName(AgentType.ProductSearchAgent);
            var productMatchmakingAgent = ollamaAgentProvider.GetLocalAgentByName(AgentType.ProductMatchmakingAgent);
            var locationServiceAgent = ollamaAgentProvider.GetLocalAgentByName(AgentType.LocationServiceAgent);
            var navigationAgent = ollamaAgentProvider.GetLocalAgentByName(AgentType.NavigationAgent);

            var workflow = AgentWorkflowBuilder.BuildConcurrent(workFlowName, 
                [productSearchAgent,
                productMatchmakingAgent,
                locationServiceAgent,
                navigationAgent]);
            return workflow;
        });

        logger?.LogInformation("Completed registration of MAF Ollama workflows");
        return builder;
    }
}
