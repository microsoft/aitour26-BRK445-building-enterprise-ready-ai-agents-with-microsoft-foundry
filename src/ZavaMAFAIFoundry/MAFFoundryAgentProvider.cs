using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ZavaMAFAIFoundry;

/// <summary>
/// Provides access to agents from Microsoft AI Foundry.
/// Agents are pre-deployed and managed in Microsoft AI Foundry.
/// </summary>
public class MAFFoundryAgentProvider
{
    private readonly AIProjectClient _persistentAgentClient;
    public MAFFoundryAgentProvider(
        string aiFoundryProjectEndpoint,
        string tenantId = "")
    {
        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrEmpty(tenantId))
        {
            credentialOptions = new DefaultAzureCredentialOptions()
            {
                TenantId = tenantId
            };
        }
        var tokenCredential = new DefaultAzureCredential(options: credentialOptions);

        _persistentAgentClient = new AIProjectClient(
            new Uri(aiFoundryProjectEndpoint),
            tokenCredential);
    }

    /// <summary>
    /// Gets an AI agent by its agent ID from Microsoft AI Foundry (synchronous).
    /// </summary>
    public AIAgent GetAIAgent(string agentId, List<AITool>? tools = null)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentException("Agent Name cannot be null or empty", nameof(agentId));
        }

        AgentRecord agentRecord = _persistentAgentClient.Agents.GetAgent(agentName: agentId);
        return _persistentAgentClient.AsAIAgent(agentRecord, tools);
    }

    public AIAgent GetOrCreateAIAgent(
        string agentId,
        string agentName = "",
        string model = "",
        string agentInstructions = "",
        List<AITool>? tools = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId, nameof(agentId));
        ArgumentException.ThrowIfNullOrWhiteSpace(agentName, nameof(agentName));
        ArgumentException.ThrowIfNullOrWhiteSpace(model, nameof(model));

        try
        {
            AgentRecord agentRecord = _persistentAgentClient.Agents.GetAgent(agentName: agentId);
            return _persistentAgentClient.AsAIAgent(agentRecord, tools);
        }
        catch
        {
        }

        var agentVersion = _persistentAgentClient.Agents.CreateAgentVersion(
                agentName,
                new AgentVersionCreationOptions(
                    new PromptAgentDefinition(model: model)
                    {
                        Instructions = "You are good at telling jokes.",
                    }));

        return _persistentAgentClient.AsAIAgent(agentVersion, tools);
    }
}