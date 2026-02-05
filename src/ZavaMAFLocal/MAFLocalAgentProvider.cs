using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZavaAgentsMetadata;

namespace ZavaMAFLocal;

/// <summary>
/// Provides locally-created agents using the Microsoft Agent Framework.
/// Agents are created with instructions and tools configured locally using IChatClient.
/// </summary>
public class MAFLocalAgentProvider
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the MAFLocalAgentProvider.
    /// </summary>
    /// <param name="chatClient">The chat client to use for creating agents.</param>
    public MAFLocalAgentProvider(
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

    public AIAgent GetLocalAgentByName(AgentType agent)
    {
        return GetAgentByName(AgentMetadata.GetLocalAgentName(agent));
    }

    public Workflow GetLocalWorkflowByName(string workflowName)
    {
        return _serviceProvider.GetRequiredKeyedService<Workflow>(workflowName);
    }
}

/// <summary>
/// Extension methods for registering local MAF agents in dependency injection.
/// Follows the pattern of AddAIAgent(name, factory) for individual agent registration.
/// </summary>
public static class MAFLocalAgentExtensions
{
    /// <summary>
    /// Registers all local MAF agents to the service collection using the recommended pattern.
    /// Each agent is registered as a keyed singleton AIAgent service.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    public static WebApplicationBuilder AddMAFLocalAgents(
        this WebApplicationBuilder builder)
    {
        using var provider = builder.Services.BuildServiceProvider();
        var logger = provider.GetService<ILoggerFactory>()?
            .CreateLogger("MAFLocalAgentExtensions");
        logger?.LogInformation("Registering MAF Local agents using IChatClient");

        // Register the provider as singleton
        builder.Services.AddSingleton<MAFLocalAgentProvider>(sp =>
        {
            var serviceLogger = sp.GetService<ILoggerFactory>()?
                .CreateLogger("MAFLocalAgentExtensions");
            serviceLogger?.LogInformation("Creating MAFLocalAgentProvider with IChatClient");

            var chatClient = sp.GetRequiredService<IChatClient>();
            return new MAFLocalAgentProvider(sp, chatClient);
        });

        // Register each agent individually as keyed singleton
        foreach (var agentType in AgentMetadata.AllAgents)
        {
            var agentName = AgentMetadata.GetLocalAgentName(agentType);
            var instructions = AgentMetadata.GetAgentInstructions(agentType);

            logger?.LogInformation(
                "Creating MAF Local agent: {AgentName} - Type: {AgentType}",
                agentName,
                agentType);

            // Registration logic for each agent would go here if needed
            builder.AddAIAgent(agentName, (sp, key) =>
            {
                // create agent
                var chatClient = sp.GetRequiredService<IChatClient>();
                return chatClient.CreateAIAgent(
                    name: agentName,
                    instructions: instructions);
            });


            logger?.LogDebug($"Registered MAF Local agent: {agentName} as keyed singleton");
        }

        logger?.LogInformation("Completed registration of {Count} MAF Local agents", AgentMetadata.AllAgents.Count());
        return builder;
    }

    /// <summary>
    /// Registers all local MAF agents to the service collection using the recommended pattern.
    /// Each agent is registered as a keyed singleton AIAgent service.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    public static WebApplicationBuilder AddMAFLocalWorkflows(
        this WebApplicationBuilder builder)
    {
        using var provider = builder.Services.BuildServiceProvider();
        var logger = provider.GetService<ILoggerFactory>()?
            .CreateLogger("MAFLocalAgentExtensions");
        logger?.LogInformation("Registering MAF Local agents using IChatClient");

        // Register the workflow as a keyed singleton
        builder.AddWorkflow("SequentialWorkflow", (sp, key) =>
            {
                var workFlowName = "SequentialWorkflow";
                logger?.LogInformation($"Creating MAF Local workflow: {workFlowName} - Type: Sequential");
                // create agent
                var localAgentProvider = sp.GetRequiredService<MAFLocalAgentProvider>();
                var productSearchAgent = localAgentProvider.GetLocalAgentByName(AgentType.ProductSearchAgent);
                var productMatchmakingAgent = localAgentProvider.GetLocalAgentByName(AgentType.ProductMatchmakingAgent);
                var locationServiceAgent = localAgentProvider.GetLocalAgentByName(AgentType.LocationServiceAgent);
                var navigationAgent = localAgentProvider.GetLocalAgentByName(AgentType.NavigationAgent);

                var workflow = AgentWorkflowBuilder.BuildSequential(workFlowName,
                    [productSearchAgent,
                    productMatchmakingAgent,
                    locationServiceAgent,
                    navigationAgent]);
                return workflow;
            });

        
        // Register the Concurrent workflow as a keyed singleton
        builder.AddWorkflow("ConcurrentWorkflow", (sp, key) =>
        {
            var workFlowName = "ConcurrentWorkflow";

            logger?.LogInformation($"Creating MAF Local workflow: {workFlowName} - Type: Concurrent");

            // create agent
            var localAgentProvider = sp.GetRequiredService<MAFLocalAgentProvider>();
            var productSearchAgent = localAgentProvider.GetLocalAgentByName(AgentType.ProductSearchAgent);
            var productMatchmakingAgent = localAgentProvider.GetLocalAgentByName(AgentType.ProductMatchmakingAgent);
            var locationServiceAgent = localAgentProvider.GetLocalAgentByName(AgentType.LocationServiceAgent);
            var navigationAgent = localAgentProvider.GetLocalAgentByName(AgentType.NavigationAgent);

            var workflow = AgentWorkflowBuilder.BuildConcurrent(workFlowName, 
                [productSearchAgent,
                productMatchmakingAgent,
                locationServiceAgent,
                navigationAgent]);
            return workflow;
        });

        // Register the HandOff workflow as a keyed singleton
        builder.AddWorkflow("HandOffWorkflow", (sp, key) =>
        {
            var workFlowName = "HandOffWorkflow";

            logger?.LogInformation($"Creating MAF Local workflow: {workFlowName} - Type: HandOff");

            // create agent
            var localAgentProvider = sp.GetRequiredService<MAFLocalAgentProvider>();
            var productSearchAgent = localAgentProvider.GetLocalAgentByName(AgentType.ProductSearchAgent);
            var productMatchmakingAgent = localAgentProvider.GetLocalAgentByName(AgentType.ProductMatchmakingAgent);
            var locationServiceAgent = localAgentProvider.GetLocalAgentByName(AgentType.LocationServiceAgent);
            var navigationAgent = localAgentProvider.GetLocalAgentByName(AgentType.NavigationAgent);

            var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(productSearchAgent)
                .WithHandoff(productSearchAgent, productMatchmakingAgent)
                .WithHandoff(productMatchmakingAgent, locationServiceAgent)
                .WithHandoff(locationServiceAgent, navigationAgent)
                .Build();

            // hack to avoid DevUI triggering exception about missing name in workflow
            workflow.SetName(workFlowName);

            return workflow;
        });

        // Register the GroupChat workflow as a keyed singleton
        builder.AddWorkflow("GroupChatWorkflow", (sp, key) =>
        {
            var workFlowName = "GroupChatWorkflow";

            logger?.LogInformation($"Creating MAF Local workflow: {workFlowName} - Type: GroupChat");

            // create agent
            var localAgentProvider = sp.GetRequiredService<MAFLocalAgentProvider>();
            var productSearchAgent = localAgentProvider.GetLocalAgentByName(AgentType.ProductSearchAgent);
            var productMatchmakingAgent = localAgentProvider.GetLocalAgentByName(AgentType.ProductMatchmakingAgent);
            var locationServiceAgent = localAgentProvider.GetLocalAgentByName(AgentType.LocationServiceAgent);
            var navigationAgent = localAgentProvider.GetLocalAgentByName(AgentType.NavigationAgent);

            var agentList = new List<AIAgent>
            {
                productSearchAgent,
                productMatchmakingAgent,
                locationServiceAgent,
                navigationAgent
            };

            var workflow = AgentWorkflowBuilder.CreateGroupChatBuilderWith(
                _ => new RoundRobinGroupChatManager(agentList) { MaximumIterationCount = 5 })
                .AddParticipants(agentList)
                .Build();

            // hack to avoid DevUI triggering exception about missing name in workflow
            workflow.SetName(workFlowName);

            return workflow;
        });

        return builder;
    }
}

