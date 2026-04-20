# Project Context

- **Owner:** Bruno Capuano
- **Project:** aitour26-BRK445 — Microsoft AI Tour 2026 session "Building enterprise-ready AI Agents with Microsoft Foundry". A .NET Aspire solution (Zava) demonstrating multi-service AI agent patterns with Microsoft Foundry / MAF.
- **Stack:** C# / .NET 10, .NET Aspire, Microsoft Agent Framework (MAF), Microsoft Foundry Agents, Blazor (Store frontend), xUnit, Bicep (infra), Docker, devcontainer
- **Key entry points:** `src/BRK445-Zava-Aspire.slnx`, `src/ZavaAppHost`, `src/ZavaWorkingModes/WorkingMode.cs`, `src/Store` (settings page switches working modes; persisted to `localStorage`)
- **Created:** 2026-04-20T14:52:17Z

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-04-20: Aspire CLI Install Migration
- **Fact:** Aspire CLI install replaced `dotnet workload install aspire` — the workload command is obsolete. Official install script is at https://aspire.dev/install.sh (bash) or https://aspire.dev/install.ps1 (PowerShell).
- **Files updated:**
  - `docs/ARCHITECTURE_DEPLOYMENT.md` — replaced workload step with CLI install step (bash + PowerShell)
  - `README.MD` — removed "Aspire workloads" terminology
  - `session-delivery-resources/docs/Prerequisites.md` — added Aspire CLI to required tooling
  - `session-delivery-resources/docs/01.Installation.md` — added Aspire CLI to prerequisites
  - `.devcontainer/devcontainer.json` — updated comment to reference official docs (command was already correct)

### 2026-04-20: Aspire Package Upgrade to 13.2.2
- **Fact:** Upgraded all .NET Aspire packages from 13.1.0 to latest stable/preview versions aligned with Aspire CLI 13.2.2.
- **Version Table:**
  | Package | From | To | Notes |
  |---------|------|----|----- |
  | Aspire.AppHost.Sdk | 13.1.0 | 13.2.2 | Stable |
  | Aspire.Azure.AI.OpenAI | 13.1.0-preview.1.25616.3 | 13.2.2-preview.1.26207.2 | Preview-only |
  | Aspire.Azure.AI.Inference | 13.1.0-preview.1.25616.3 | 13.2.2-preview.1.26207.2 | Preview-only |
  | Aspire.Hosting.Azure.AIFoundry | 13.1.0-preview.1.25616.3 | 13.1.3-preview.1.26166.8 | Preview-only (latest available) |
  | Aspire.Hosting.Azure.ApplicationInsights | 13.1.0 | 13.2.2 | Stable |
  | Aspire.Hosting.Azure.CognitiveServices | 13.1.0 | 13.2.2 | Stable |
  | CommunityToolkit.Aspire.Hosting.Sqlite | 13.0.1-beta.468 | 13.1.1 | Stable |
  | Microsoft.Extensions.AI.Abstractions | 10.1.1 | 10.2.0 | Bumped to resolve transitive conflict |
- **Quirks:**
  - Aspire.Azure.AI.* packages are still preview-only as of 13.2.2 (AI integration packages lag behind core Aspire).
  - Aspire.Hosting.Azure.AIFoundry is on 13.1.3-preview (not yet 13.2.x) — used latest available.
  - Microsoft.Extensions.AI.Abstractions required bump from 10.1.1 to 10.2.0 to resolve package downgrade error (new Aspire.Azure.AI.Inference depends on Microsoft.Extensions.AI 10.2.0).
- **Build Outcome:** SUCCESS — `dotnet build src\ZavaAppHost\ZavaAppHost.csproj` completed with 0 errors, 2 warnings (NU1901: low severity vulnerabilities in transitive NuGet packages — acceptable). Restore took 32.9s.
- **Files Changed:** 17 .csproj files across all services + ZavaAppHost + DataService (for Microsoft.Extensions.AI.Abstractions bump).

### 2026-04-20: Preview-Feature Warning Pattern (CA2252)
- **Fact:** MAF packages (and other preview-feature libs) trigger CA2252 compiler warnings when their types surface in app code. This is expected behavior for preview features.
- **Suppression pattern (project scope):** Add `<NoWarn>$(NoWarn);CA2252</NoWarn>` to the .csproj. Allows clean builds without `#pragma` directives in every file.
- **Alternative (file scope):** Use `#pragma warning disable CA2252` at file top (already present in `src/ZavaMAFFoundry/MAFFoundryAgentProvider.cs:1`).
- **Usage:** Applied to `infra/Brk445-Console-DeployAgents.csproj` when bumping MAF packages to 1.0.0-preview.251219.1 (2026-04-20). Build clean with 0 errors.

### Demo 2: MAF Event Logging for Hosted Sequential Workflows — 2026-04-20

**Finding (Empirical):** MAF's AgentRunUpdateEvent does not change ExecutorId between hosted-agent sequential workflow steps. Only the first executor transition was logged across multiple Demo 2 runs.

**Mitigation:** Default case in ProcessWorkflowEvent now logs all unhandled workflow event types via `evt.GetType().Name`, ensuring every workflow event surfaces in Aspire logs for live presenter visibility. Applied to MultiAgentControllerMAFFoundry.cs and MultiAgentControllerMAFLocal.cs.

**Commit:** 4db8714 — "Demo 2: log all workflow event types for live visibility"

**Follow-up:** Investigate whether ExecutorInvokedEvent / ExecutorCompletedEvent are emitted during hosted sequential workflows.

## Learnings
- 2025: MAF `WorkflowEvent.Data` carries the `Exception` for both `ExecutorFailedEvent` and `WorkflowErrorEvent` (verified against Microsoft.Agents.AI.Workflows 1.0.0-preview.251219.1 XML doc). When adding a fallback `default:` log arm in a workflow event switch, ALWAYS add explicit error-event arms above it at LogError — otherwise failures vanish into an info-level type-name log.

### 2026-04-20: MAF Foundry Sequential Workflow — Hosted Hand-off Payload Bug
- **Symptom:** Demo 2 (MAF Foundry, Sequential) failed on agent #2 with `HTTP 400 invalid_payload` at `param: "/"` from `OpenAI.Responses.ResponsesClient.CreateResponseAsync`.
- **Root cause:** Hosted Foundry agents are created with `HostedFileSearchTool` + `HostedCodeInterpreterTool` (`infra/infra/Services/AgentCreationService.cs`). `AgentWorkflowBuilder.BuildSequential` forwards the previous agent's full `List<ChatMessage>` — including hosted-tool / function-call / reasoning content items — as the next agent's input. Foundry's `/responses` endpoint rejects orphan `*_call` items at root.
- **Fix:** New `MAFFoundrySequentialBuilder.BuildSequentialForFoundry(...)` interposes a `BindAsExecutor<List<ChatMessage>, List<ChatMessage>>` sanitizer between each pair of agents that collapses the prior output to a single plain-text `User` `ChatMessage`. `MultiAgentControllerMAFFoundry.AssistSequentialAsync` uses it instead of `AgentWorkflowBuilder.BuildSequential`.
- **MAF API used:** `WorkflowBuilder` + `ExecutorBindingExtensions.BindAsExecutor<TIn,TOut>(Func<TIn,TOut>, id, options, threadsafe)` + `agent.BindAsExecutor(emitEvents:true)` + `builder.AddEdge(...)` + `builder.WithOutputFrom(...)`. There is no converter overload on `BuildSequential` in MAF `1.0.0-preview.251219.1`.
- **Why MAF Local works:** Local agents call Azure OpenAI Chat-Completions, which tolerates these items; only the Responses API enforces strict pairing.
- **Build status:** Compile clean (0 errors / 0 warnings via `dotnet build ... -t:Compile`); a full `dotnet build` only fails on the post-build copy when an Aspire-hosted `MultiAgentDemo.exe` still holds the file lock — stop Aspire first.
- **Reusable pattern:** Captured in `.squad/skills/maf-foundry-handoff/SKILL.md`.

### 2026-04-20: MAF Foundry Sequential — Entry-Point Adapter Required (Agent #1)
- **Symptom:** Demo 2 (Foundry, Sequential) failed on agent #1 (ProductSearchAgent_1) with `HTTP 400 invalid_request_error: missing_required_parameter` (`Parameter: input`) — i.e. `/responses` body had no `input` field at all.
- **Root cause:** `MAFFoundrySequentialBuilder.BuildSequentialForFoundry` constructed `new WorkflowBuilder(firstAgentBinding)` directly. Hosted-agent executors expect `List<ChatMessage>`, but the controller passes a raw `string` to `InProcessExecution.StreamAsync`. `AgentWorkflowBuilder.BuildSequential` silently inserts a `string → List<ChatMessage>` adapter at the entry; our custom builder skipped it, so the first agent received nothing and emitted a `/responses` request with no `input`.
- **Fix:** Added a `foundry-input-adapter` `BindAsExecutor<string, List<ChatMessage>>` (`query => [new ChatMessage(ChatRole.User, query ?? string.Empty)]`, threadsafe) and made it the workflow root, with an edge to the first agent binding. Inter-agent sanitizer chain unchanged.
- **Build:** Compile clean (0 errors / 0 warnings) on `dotnet build src/MultiAgentDemo/MultiAgentDemo.csproj -t:Compile`.
- **General rule:** Custom `WorkflowBuilder`s over hosted Foundry agents need BOTH (a) entry-point `string → List<ChatMessage>` adapter AND (b) inter-agent sanitizers. Captured in `.squad/skills/maf-foundry-handoff/SKILL.md` (confidence bumped — independently confirmed twice now).

### 2026-04-20: Demo 2 MAF Foundry Sequential — TurnToken Was Silently Dropped
- **Symptom:** Bruno: "the answer came too fast." Demo 2 (MAF Foundry, Sequential) returned ~30ms after request, with default Paint Sprayer alternatives + canned navigation. Aspire log showed 2 SuperSteps with ExecutorInvoked/ExecutorCompleted but ZERO `AgentRunUpdateEvent`, ZERO `WorkflowOutputEvent`, ZERO Foundry HTTP calls — i.e. no agent ever invoked Foundry.
- **Root cause:** `ChatProtocolExecutor.TakeTurnAsync(TurnToken,...)` only invokes the wrapped agent when a `TurnToken` arrives, then forwards the token downstream. Our `MAFFoundrySequentialBuilder` used `ExecutorBindingExtensions.BindAsExecutor<TIn,TOut>(...)` (a `FunctionExecutor`) for both the entry-point adapter AND every inter-agent sanitizer. `FunctionExecutor` registers a route ONLY for its declared `TIn` type — it has no `TurnToken` route, so `StreamingRun.TrySendMessageAsync(new TurnToken(...))` landed on the input adapter and was silently swallowed. No agent ever saw the token, none invoked Foundry, the workflow ended immediately, and `StepsProcessor` filled the response with `GenerateDefaultProductAlternatives()` + `CreateDefaultNavigationInstructions()` — which looked plausible and arrived in milliseconds.
- **Fix:**
  1. Entry-point adapter is now the built-in `ChatForwardingExecutor` (configured with `StringMessageChatRole = ChatRole.User`). It accepts `string`, forwards it as `ChatMessage`, AND has a native `TurnToken` route that forwards downstream.
  2. Each inter-agent sanitizer is now `FoundryHandoffSanitizingExecutor : Executor` with explicit `RouteBuilder` handlers for `List<ChatMessage>`, `IEnumerable<ChatMessage>`, `ChatMessage[]`, `ChatMessage` (sanitize+forward) AND for `TurnToken` (forward as-is).
- **Verification:** `dotnet build src/MultiAgentDemo/MultiAgentDemo.csproj -t:Compile` — 0 errors, 0 warnings.
- **Visibility (regression-protection):** Both `MultiAgentControllerMAFFoundry.RunWorkflowAsync` and `MultiAgentControllerMAFLocal.RunWorkflowAsync` now prepend a synthetic `AgentStep` with `Agent = "Orchestrator"` whose `Result` is "🌐 Using MAF Foundry orchestrator (hosted Microsoft Foundry agents)" or "💻 Using MAF Local orchestrator (gpt-5-mini local agents)". They also log the same banner at INFO with `OrchestrationId` + pattern. Future "answer came too fast" reports can be triaged in 5 seconds: which banner is in the response?
- **General rule (now in skill):** ANY custom executor that sits between hosted-agent (`AIAgentBinding`) executors in a MAF workflow MUST handle `TurnToken` and forward it downstream. `FunctionExecutor` (i.e. `ExecutorBindingExtensions.BindAsExecutor<TIn,TOut>`) is unsafe for this role. Use `ChatForwardingExecutor` or a custom `Executor` subclass with an explicit `TurnToken` route.

### 2026-04-20: TurnToken Propagation Rule (Cross-cutting MAF orchestration)

**Discovery:** Demo 2 executed in ~30ms instead of ~8 minutes because `TurnToken` was silently dropped by the input adapter and sanitizers. Without `TurnToken`, hosted Foundry agents skip API calls and return stub responses via `StepsProcessor` defaults.

**Rule (applies to all MAF-touching agents):** 
- Input adapters in orchestration chains MUST preserve `TurnToken`. `FunctionExecutor`-based adapters lose it (only handle `List<ChatMessage>` type). Use `ChatForwardingExecutor` or document the explicit `TurnToken` route.
- Sanitizers between agent pairs MUST route `TurnToken` explicitly. Derive from `Executor` base, add `RouteBuilder` arm for `TurnToken`, forward downstream as-is.
- If the workflow runs in <100ms instead of expected ~30–60s, first suspect: missing `TurnToken` propagation.

**Reference:** Hicks' 2026-04-20 fix (commit ada9ecc, Orchestration Log `2026-04-20T16-24-21Z-hicks.md`, Skill `.squad/skills/maf-foundry-handoff/`).
