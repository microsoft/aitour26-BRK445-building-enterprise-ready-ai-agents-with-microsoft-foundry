using System.Text;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace MultiAgentDemo.Controllers;

/// <summary>
/// Builds a sequential workflow over hosted Microsoft Foundry agents that sanitizes
/// the messages handed off between agents.
/// <para>
/// Hosted Foundry agents (e.g. those with <c>HostedFileSearchTool</c> /
/// <c>HostedCodeInterpreterTool</c>) call Foundry's Responses API. When MAF's
/// default <c>AgentWorkflowBuilder.BuildSequential</c> forwards the previous
/// agent's full <c>List&lt;ChatMessage&gt;</c> output (which can contain
/// hosted-tool call items, function-call items, or reasoning items) as the
/// <c>input</c> of the next agent, the Responses API rejects the request with
/// <c>HTTP 400 invalid_payload</c> at <c>param: "/"</c> because those items are
/// orphans (their matching tool-output siblings live on the previous agent's
/// server-side thread, not in the new request).
/// </para>
/// <para>
/// This builder inserts a tiny pass-through executor between each pair of agents
/// that collapses the prior agent's output to a single plain-text user message,
/// eliminating any tool/function-call/reasoning items before they reach the next
/// hosted agent.
/// </para>
/// </summary>
internal static class MAFFoundrySequentialBuilder
{
    public static Workflow BuildSequentialForFoundry(IReadOnlyList<AIAgent> agents, string? workflowName = null)
    {
        ArgumentNullException.ThrowIfNull(agents);
        if (agents.Count == 0)
        {
            throw new ArgumentException("At least one agent is required.", nameof(agents));
        }

        ExecutorBinding firstBinding = agents[0].BindAsExecutor(emitEvents: true);
        var builder = new WorkflowBuilder(firstBinding);

        ExecutorBinding previous = firstBinding;
        for (int i = 1; i < agents.Count; i++)
        {
            string sanitizerId = $"foundry-handoff-sanitizer-{i}";
            ExecutorBinding sanitizer = ExecutorBindingExtensions.BindAsExecutor<List<ChatMessage>, List<ChatMessage>>(
                messageHandler: SanitizeForFoundryHandoff,
                id: sanitizerId,
                options: null,
                threadsafe: true);

            ExecutorBinding agentBinding = agents[i].BindAsExecutor(emitEvents: true);

            builder.AddEdge(previous, sanitizer);
            builder.AddEdge(sanitizer, agentBinding);

            previous = agentBinding;
        }

        builder.WithOutputFrom(previous);
        if (!string.IsNullOrWhiteSpace(workflowName))
        {
            builder.WithName(workflowName);
        }

        return builder.Build(validateOrphans: false);
    }

    /// <summary>
    /// Collapses the prior agent's output messages into a single plain-text
    /// user message that the next hosted Foundry agent can safely accept.
    /// Strips function-call / tool-call / reasoning items by keeping only
    /// the textual portion of each message.
    /// </summary>
    private static List<ChatMessage> SanitizeForFoundryHandoff(List<ChatMessage> messages)
    {
        if (messages is null || messages.Count == 0)
        {
            return [new ChatMessage(ChatRole.User, "(previous step produced no output)")];
        }

        var sb = new StringBuilder();
        foreach (var msg in messages)
        {
            var text = msg.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }
            if (sb.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine();
            }
            sb.Append(text.Trim());
        }

        var combined = sb.Length > 0
            ? sb.ToString()
            : "(previous step produced no textual output)";

        return [new ChatMessage(ChatRole.User, combined)];
    }
}
