# Skill: MAF Hosted-Foundry Sequential Hand-off Sanitization

## When to use

You are building a sequential (or any chained) MAF workflow whose participants
are **hosted Microsoft Foundry agents** — i.e. agents created via
`AIProjectClient.CreateAIAgent(...)` that carry hosted tools such as
`HostedFileSearchTool`, `HostedCodeInterpreterTool`, or any other tool whose
results materialise as non-text content items in the agent's output.

## Symptom you're avoiding

```
HTTP 400 invalid_payload  (param: "/")
The provided data does not match the expected schema
```
on the **second** agent of the chain, surfaced from
`OpenAI.Responses.ResponsesClient.CreateResponseAsync` →
`AzureAIProjectChatClient.GetStreamingResponseAsync`.

## Why it happens

`AgentWorkflowBuilder.BuildSequential([...])` forwards the previous agent's
full `List<ChatMessage>` (including `FunctionCallContent`,
`FunctionResultContent`, hosted-tool call/result items, reasoning items) as
the next agent's input. The Foundry `/responses` endpoint requires every
`function_call` / `tool_call` / `reasoning` item to be paired with its
matching `*_output`. The previous agent's tool outputs live on its
server-side thread, not in the new request, so the items are orphans and the
whole body is rejected at root.

## Pattern

Build the workflow with `WorkflowBuilder` and insert a one-line sanitizer
executor between every pair of agents that collapses the prior output to a
single plain-text user `ChatMessage`:

```csharp
ExecutorBinding sanitizer = ExecutorBindingExtensions.BindAsExecutor<List<ChatMessage>, List<ChatMessage>>(
    messageHandler: messages =>
    {
        var sb = new StringBuilder();
        foreach (var m in messages)
        {
            if (string.IsNullOrWhiteSpace(m.Text)) continue;
            if (sb.Length > 0) sb.AppendLine().AppendLine();
            sb.Append(m.Text.Trim());
        }
        return [new ChatMessage(ChatRole.User, sb.Length > 0 ? sb.ToString() : "(no output)")];
    },
    id: $"foundry-handoff-sanitizer-{i}",
    options: null,
    threadsafe: true);
```

Then `builder.AddEdge(prevAgent, sanitizer)` →
`builder.AddEdge(sanitizer, nextAgent)`. Mark the final agent with
`builder.WithOutputFrom(lastAgent)`.

See `src/MultiAgentDemo/Controllers/MAFFoundrySequentialBuilder.cs` for the
full reference implementation.

## Entry-point adapter (also required)

When you build the workflow yourself with `WorkflowBuilder` (instead of
`AgentWorkflowBuilder.BuildSequential`), you also lose the implicit
`string → List<ChatMessage>` adapter that MAF would have wired at the workflow
root. The controller calls `InProcessExecution.StreamAsync(workflow, request.ProductQuery)`
with a raw `string`, but the first hosted-agent executor expects
`List<ChatMessage>`. Without an adapter, the first agent's call to Foundry's
`/responses` endpoint is sent with **no `input` field** and fails with:

```
HTTP 400 invalid_request_error: missing_required_parameter
Parameter: input
```

Fix it by adding an entry-point executor and using it as the workflow root:

```csharp
ExecutorBinding inputAdapter = ExecutorBindingExtensions.BindAsExecutor<string, List<ChatMessage>>(
    messageHandler: query => [new ChatMessage(ChatRole.User, query ?? string.Empty)],
    id: "foundry-input-adapter",
    options: null,
    threadsafe: true);

var builder = new WorkflowBuilder(inputAdapter);
builder.AddEdge(inputAdapter, firstAgentBinding);
// ... then the inter-agent sanitizer chain as above.
```

**Rule of thumb:** custom `WorkflowBuilder`s over hosted Foundry agents need
BOTH (a) the entry-point `string → List<ChatMessage>` adapter AND (b) the
inter-agent sanitizers. Skipping either produces a Foundry HTTP 400, just at
different points in the chain (first agent vs. second agent).

## TurnToken propagation rule (CRITICAL)

`AIAgentHostExecutor` (the executor produced by `agent.BindAsExecutor(emitEvents:true)`
for any hosted MAF agent) is a `ChatProtocolExecutor`. It batches incoming
`ChatMessage` payloads and only **invokes the underlying agent** when it
receives a `TurnToken`, after which it forwards the token downstream. The
controller starts the run with:

```csharp
var run = await InProcessExecution.StreamAsync(workflow, request.ProductQuery);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
```

The `TurnToken` lands on the workflow's start executor. If that executor
doesn't have a route for `TurnToken`, the token is **silently dropped** —
none of the downstream agents ever invoke Foundry, the workflow ends in a
few milliseconds with no `AgentRunUpdateEvent` / `WorkflowOutputEvent`, and
the controller returns whatever the post-processing layer defaulted to
(in our case, fallback alternatives + canned navigation steps that look
real). Symptom for the user: "the answer came back instantly."

**Rule:** every custom executor placed between hosted-agent executors —
including the entry-point adapter and every inter-agent sanitizer — MUST
handle `TurnToken` and forward it downstream. Two safe shapes:

- **Entry-point string→ChatMessage adapter:** use the built-in
  `ChatForwardingExecutor` with
  `new ChatForwardingExecutorOptions { StringMessageChatRole = ChatRole.User }`.
  It already forwards `TurnToken`.
- **Inter-agent sanitizer:** subclass `Executor` directly and register
  `routeBuilder.AddHandler<TurnToken>((t, ctx, ct) => ctx.SendMessageAsync(t, ct))`
  alongside your chat-message handlers.

`ExecutorBindingExtensions.BindAsExecutor<TIn,TOut>(...)` (`FunctionExecutor`)
**is unsafe** for these roles — it registers exactly one route (for `TIn`) and
will swallow `TurnToken` without warning. The MAF runtime emits
`ExecutorInvokedEvent` / `ExecutorCompletedEvent` either way, so the
silent-drop bug is invisible from the event stream alone. The reliable signal
is "no `AgentRunUpdateEvent` events" + "no Foundry HTTP traffic".

See `src/MultiAgentDemo/Controllers/MAFFoundrySequentialBuilder.cs` for the
fully-corrected reference implementation (`ChatForwardingExecutor` entry +
`FoundryHandoffSanitizingExecutor` inter-agent).

## Always emit an orchestrator-identity step

Independently of the workflow shape, the first `AgentStep` returned to the
caller should be a synthetic "Orchestrator" step whose `Result` names the
orchestrator (e.g. "🌐 Using MAF Foundry orchestrator …" /
"💻 Using MAF Local orchestrator …"), and the controller should log the same
banner at INFO with the `OrchestrationId`. This makes future "wrong
orchestrator" / "too fast" / "too slow" reports diagnosable in seconds from
either the JSON payload or the Aspire console — without re-instrumenting.

## When NOT to use

- MAF Local agents — they hit the Chat-Completions endpoint and tolerate
  these items.
- Concurrent workflows — agents don't consume each other's output.
- Single-agent calls.

## Trade-off

You lose the multi-turn context-sharing you'd get with a shared
chat-completions thread. Each agent in the chain only sees a flattened
text summary of the previous one, which is usually what you want for a
sequential pipeline.

## Verified against

- MAF `1.0.0-preview.251219.1`
- Foundry endpoint shape `*.services.ai.azure.com/api/projects/<project>`
- Agents created with `HostedCodeInterpreterTool` + `HostedFileSearchTool`

## Confidence

**High (independently confirmed twice).** First confirmation: inter-agent
sanitizer fixed the agent-#2 hand-off (2026-04-20). Second confirmation:
entry-point adapter fixed the agent-#1 missing-`input` 400 (2026-04-20).
Both failures share the same root cause shape: hosted Foundry agents'
`/responses` endpoint enforces strict request shape that MAF's stock
`AgentWorkflowBuilder.BuildSequential` papers over with implicit adapters
the custom builder must reinstate.
