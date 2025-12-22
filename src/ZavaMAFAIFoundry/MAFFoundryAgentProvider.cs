using Azure.AI.Agents.Persistent;
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
    private readonly PersistentAgentsClient _persistentAgentClient;
    public MAFFoundryAgentProvider(
        string aiFoundryProjectEndpoint, 
        string tenantId = "")
    {
        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrEmpty(tenantId))
        {
            credentialOptions = new DefaultAzureCredentialOptions()
            { TenantId = tenantId };
        }
        var tokenCredential = new DefaultAzureCredential(options: credentialOptions);

        _persistentAgentClient = new PersistentAgentsClient(
            aiFoundryProjectEndpoint!,
            tokenCredential);
    }

    /// <summary>
    /// Gets an AI agent by its agent ID from Microsoft AI Foundry (synchronous).
    /// </summary>
    public AIAgent GetAIAgent(string agentId, List<AITool> tools = null)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentException("Agent Name cannot be null or empty", nameof(agentId));
        }

        ChatClientAgentOptions options = new();
        options.ChatOptions.Tools = tools;

        return _persistentAgentClient.GetAIAgent(agentId: agentId, options: options);
    }

    public AIAgent GetOrCreateAIAgent(string agentId,
        string agentName = "",
        string model = "",
        string agentInstructions = "", 
        List<AITool> tools = null)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            throw new ArgumentException("Agent ID cannot be null or empty", nameof(agentId));
        }
        AIAgent agent = null;
        try
        {
            ChatClientAgentOptions options = new();
            options.ChatOptions.Tools = tools;
            agent = _persistentAgentClient.GetAIAgent(agentId: agentId, options: options);
        }
        catch
        {
        }

        agent ??= _persistentAgentClient.CreateAIAgent(
                name: agentName,
                model: model,
                instructions: agentInstructions);

        return agent;
    }
}