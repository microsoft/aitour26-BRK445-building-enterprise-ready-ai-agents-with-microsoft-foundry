using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using MultiAgentDemo.Tracing;
using MafRouteBuilder = Microsoft.Agents.AI.Workflows.RouteBuilder;

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
/// <c>HTTP 400 invalid_payload</c>. We therefore interpose a sanitizer between
/// every pair of agents that collapses the prior agent's output to a single
/// plain-text user message.
/// </para>
/// <para>
/// Critical: Both the entry-point adapter AND the sanitizers MUST be
/// <see cref="ChatProtocolExecutor"/>-style executors that propagate the
/// <see cref="TurnToken"/> downstream. Hosted-agent executors
/// (<c>AIAgentHostExecutor</c>) only invoke their underlying agent when they
/// receive a <see cref="TurnToken"/>; they batch chat messages until the token
/// arrives. Earlier versions of this builder used
/// <c>ExecutorBindingExtensions.BindAsExecutor</c> (a <c>FunctionExecutor</c>)
/// for both adapters, which has no route registered for <see cref="TurnToken"/>
/// and therefore silently swallowed it. The token sent via
/// <c>StreamingRun.TrySendMessageAsync(new TurnToken(...))</c> would land on
/// the input adapter, get dropped, and no agent would ever invoke Foundry —
/// the workflow finished in ~10 ms with empty steps and the controller
/// returned a stub response made of fallback alternatives + default navigation.
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

        // Entry-point adapter. ChatForwardingExecutor is a built-in
        // ChatProtocolExecutor: when configured with StringMessageChatRole it
        // accepts the controller's raw string query, converts it to a
        // ChatMessage(User), and forwards it. It also natively forwards
        // TurnToken downstream — which is what makes the agents downstream
        // actually invoke Foundry.
        var inputAdapter = new ChatForwardingExecutor(
            id: "foundry-input-adapter",
            options: new ChatForwardingExecutorOptions { StringMessageChatRole = ChatRole.User });
        ExecutorBinding inputAdapterBinding = inputAdapter.BindExecutor();

        var step1Executor = new FoundryTracedStepExecutor(
            id: "foundry-step-trace-1",
            stepIndex: 1,
            stepName: "ProductSearch",
            agentDisplayName: "Product Search Agent",
            agentKey: "product-search");
        ExecutorBinding step1Binding = step1Executor.BindExecutor();

        ExecutorBinding firstAgentBinding = agents[0].BindAsExecutor(emitEvents: true);

        var builder = new WorkflowBuilder(inputAdapterBinding);
        builder.AddEdge(inputAdapterBinding, step1Binding);
        builder.AddEdge(step1Binding, firstAgentBinding);

        ExecutorBinding previous = firstAgentBinding;
        for (int i = 1; i < agents.Count; i++)
        {
            var sanitizer = new FoundryHandoffSanitizingExecutor(id: $"foundry-handoff-sanitizer-{i}");
            ExecutorBinding sanitizerBinding = sanitizer.BindExecutor();

            var stepExecutor = new FoundryTracedStepExecutor(
                id: $"foundry-step-trace-{i + 1}",
                stepIndex: i + 1,
                stepName: i switch
                {
                    1 => "ProductMatchmaking",
                    2 => "LocationService",
                    3 => "Navigation",
                    _ => $"Step{i + 1}"
                },
                agentDisplayName: i switch
                {
                    1 => "Product Matchmaking Agent",
                    2 => "Location Service Agent",
                    3 => "Navigation Agent",
                    _ => $"Agent Step {i + 1}"
                },
                agentKey: i switch
                {
                    1 => "product-matchmaking",
                    2 => "location-service",
                    3 => "navigation",
                    _ => $"agent-step-{i + 1}"
                });
            ExecutorBinding stepBinding = stepExecutor.BindExecutor();

            ExecutorBinding nextAgentBinding = agents[i].BindAsExecutor(emitEvents: true);

            builder.AddEdge(previous, sanitizerBinding);
            builder.AddEdge(sanitizerBinding, stepBinding);
            builder.AddEdge(stepBinding, nextAgentBinding);

            previous = nextAgentBinding;
        }

        builder.WithOutputFrom(previous);
        if (!string.IsNullOrWhiteSpace(workflowName))
        {
            builder.WithName(workflowName);
        }

        return builder.Build(validateOrphans: false);
    }
}

/// <summary>
/// Pass-through executor that sits between two hosted Foundry agents in a
/// sequential workflow. Collapses the prior agent's output messages into a
/// single plain-text User <see cref="ChatMessage"/> (stripping any orphan
/// function-call / tool-call / reasoning items that would make Foundry's
/// /responses endpoint return HTTP 400) AND forwards the
/// <see cref="TurnToken"/> downstream so the next agent actually executes.
/// </summary>
internal sealed class FoundryHandoffSanitizingExecutor : Executor, IResettableExecutor
{
    public FoundryHandoffSanitizingExecutor(string id)
        : base(id, options: null, declareCrossRunShareable: true)
    {
    }

    protected override MafRouteBuilder ConfigureRoutes(MafRouteBuilder routeBuilder) =>
        routeBuilder
            .AddHandler<List<ChatMessage>>(ForwardSanitizedAsync)
            .AddHandler<IEnumerable<ChatMessage>>((messages, ctx, ct) => ForwardSanitizedAsync(messages.ToList(), ctx, ct))
            .AddHandler<ChatMessage[]>((messages, ctx, ct) => ForwardSanitizedAsync(messages.ToList(), ctx, ct))
            .AddHandler<ChatMessage>((message, ctx, ct) => ForwardSanitizedAsync(new List<ChatMessage> { message }, ctx, ct))
            .AddHandler<TurnToken>((token, ctx, ct) => ctx.SendMessageAsync(token, ct));

    private static ValueTask ForwardSanitizedAsync(List<ChatMessage> messages, IWorkflowContext context, CancellationToken cancellationToken)
    {
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

        var combined = sb.Length > 0 ? sb.ToString() : "(previous step produced no textual output)";
        var sanitized = new List<ChatMessage> { new(ChatRole.User, combined) };
        return context.SendMessageAsync(sanitized, cancellationToken);
    }

    public ValueTask ResetAsync() => default;
}

/// <summary>
/// Executor that creates a named activity span for each sequential scenario-2 step
/// so Aspire traces show explicit cascaded workflow steps with meaningful names.
/// </summary>
internal sealed class FoundryTracedStepExecutor : Executor, IResettableExecutor
{
    private readonly int _stepIndex;
    private readonly string _stepName;
    private readonly string _agentDisplayName;
    private readonly string _agentKey;

    public FoundryTracedStepExecutor(
        string id,
        int stepIndex,
        string stepName,
        string agentDisplayName,
        string agentKey)
        : base(id, options: null, declareCrossRunShareable: true)
    {
        _stepIndex = stepIndex;
        _stepName = stepName;
        _agentDisplayName = agentDisplayName;
        _agentKey = agentKey;
    }

    protected override MafRouteBuilder ConfigureRoutes(MafRouteBuilder routeBuilder) =>
        routeBuilder
            .AddHandler<List<ChatMessage>>((messages, ctx, ct) => ctx.SendMessageAsync(messages, ct))
            .AddHandler<IEnumerable<ChatMessage>>((messages, ctx, ct) => ctx.SendMessageAsync(messages.ToList(), ct))
            .AddHandler<ChatMessage[]>((messages, ctx, ct) => ctx.SendMessageAsync(messages.ToList(), ct))
            .AddHandler<ChatMessage>((message, ctx, ct) => ctx.SendMessageAsync(new List<ChatMessage> { message }, ct))
            .AddHandler<TurnToken>(ForwardTokenWithTracingAsync);

    private ValueTask ForwardTokenWithTracingAsync(TurnToken token, IWorkflowContext context, CancellationToken cancellationToken)
    {
        using var activity = MultiAgentWorkflowTracing.ActivitySource.StartActivity(
            $"Scenario2.Sequential.Step{_stepIndex}.{_stepName}",
            ActivityKind.Internal);

        activity?.SetTag("zava.scenario", 2);
        activity?.SetTag("zava.orchestration", "sequential");
        activity?.SetTag("zava.step.index", _stepIndex);
        activity?.SetTag("zava.step.name", _stepName);
        activity?.SetTag("zava.agent.display_name", _agentDisplayName);
        activity?.SetTag("zava.agent.key", _agentKey);

        return context.SendMessageAsync(token, cancellationToken);
    }

    public ValueTask ResetAsync() => default;
}
