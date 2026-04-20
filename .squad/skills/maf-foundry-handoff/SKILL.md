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
