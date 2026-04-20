# Squad Decisions

## Active Decisions

### MAF Foundry Sequential Workflow — Hosted Hand-off Payload Sanitization (2026-04-20)

**Author:** Hicks (Backend / Microservices)  
**Status:** Active  
**Scope:** `src/MultiAgentDemo/Controllers/MultiAgentControllerMAFFoundry.cs`

**Context:** Demo 2 (Foundry mode → Sequential) failed on agent #2 with `HTTP 400 (invalid_payload)` from Foundry's `/responses` endpoint. Hosted Foundry agents (created with `HostedFileSearchTool` + `HostedCodeInterpreterTool`) receive strict validation: `function_call`, `tool_call`, `reasoning` items must be paired with their `*_output` siblings. `AgentWorkflowBuilder.BuildSequential` chains agents by passing the prior agent's full `List<ChatMessage>` as input to the next agent, which includes orphan items that poison the Foundry request.

**Decision:** New `MAFFoundrySequentialBuilder.BuildSequentialForFoundry(IReadOnlyList<AIAgent>)` inserts a `BindAsExecutor<List<ChatMessage>, List<ChatMessage>>` sanitizer between every pair of hosted Foundry agents. The sanitizer collapses the prior output to a single plain-text `User` `ChatMessage`, dropping all tool/function/reasoning items. Only `MultiAgentControllerMAFFoundry.AssistSequentialAsync` uses this builder; Concurrent, Handoff, GroupChat remain unchanged.

**Rationale:** Minimal, safe-by-construction solution that preserves demo narrative (still hosted Foundry agents end-to-end). MAF Local is unaffected because Azure OpenAI Chat-Completions tolerates orphan items; only the Responses API enforces strict pairing.

**Files Changed:**
- `src/MultiAgentDemo/Controllers/MAFFoundrySequentialBuilder.cs` (new)
- `src/MultiAgentDemo/Controllers/MultiAgentControllerMAFFoundry.cs` (AssistSequentialAsync call site)

**Verification:** ✅ Compile clean (0 errors, 0 warnings); pending Bruno's Aspire verification run.

### Workflow Error Event Logging Strategy (2026-04-20)

**Author:** Hicks (Backend / Microservices)  
**Status:** Active

**Context:** Demo 2 failed with `ExecutorFailedEvent` + `WorkflowErrorEvent` but exception detail didn't surface because the default case logged only `evt.GetType().Name`.

**Decision:** Added explicit `case` arms for `ExecutorFailedEvent` and `WorkflowErrorEvent` in `ProcessWorkflowEvent` (both `MultiAgentControllerMAFFoundry` and `MultiAgentControllerMAFLocal`), logged at `LogError` level.

**Property Mapping** (verified vs. MAF 1.0.0-preview.251219.1 XML doc):
- `WorkflowEvent.Data` → contains object payload or `Exception` for error events
- `ExecutorFailedEvent : ExecutorEvent` — ctor `(string executorId, Exception ex)` with exception in `Data`
- `WorkflowErrorEvent : WorkflowEvent` — ctor `(Exception ex)` with exception in `Data`

**Implementation Pattern:**
```csharp
var ex = failedEvent.Data as Exception;
_logger.LogError(ex, "... | Message: {Message} | Inner: {Inner} | Detail: {Detail}",
    ex?.Message ?? "(no exception)",
    ex?.InnerException?.Message ?? "(none)",
    ex?.ToString() ?? failedEvent.ToString());
```

Passing `Exception` as first arg ensures structured logging captures full stack trace separately; message template provides one-line summary in console. Falls back to `evt.ToString()` if shape changes.

### Demo 2 Timing Expectations — Documented with Empirical Data (2026-04-20)

**Author:** Lambert (DevRel / Presenter Enablement)  
**Status:** Active  
**Scope:** `session-delivery-resources/docs/03.HowToRunDemoLocally.md`

**Context:** Presenter walkthrough revealed Demo 2 needed clear, empirically-backed timing guidance. Bruno measured live test runs.

**Decision:** Expanded Demo 2 section (lines 114–134) to match Demo 1 depth:
- **~8 minutes end-to-end** (normal, not a bug)
- **Detailed breakdown table:** 4 hosted Foundry agents (~60–90s each) + 2 post-processing LLM calls (~60s each)
- **Speaker tip callout:** Educates on executor-logging quirk (logs quiet after first transition), new "Workflow event: {EventType}" lines for visibility
- **Troubleshooting hint:** Runs exceeding ~10 minutes indicate credential stalls or throttling
- **MAF Local fallback:** 30–60% speedup option for time-constrained presentations

**Rationale:** Foundry envelope overhead is dominant cost. Presenters needed reassurance that orchestration silence ≠ hang.

**Verification:** ✅ Empirically measured; no changes to other sections.

### Copilot Auto-Push Directive (2026-04-20)

**Author:** Bruno Capuano (via Copilot)  
**Status:** Active

After every commit, always push to remote tracking branch (`git push`). No "commit but don't push" — always sync branch to origin.

**Rationale:** User request — enables Bruno to pull from any machine.

**Enforcement:** Scribe MUST include `git push` as final step after commit (step 6) in every spawn. Coordinator checks `git status` at end of each turn to verify nothing ahead of origin.

### Aspire Package Versioning Standard (2026-04-20)

**Author:** Hicks (Backend / Microservices)  
**Status:** Active  
**Date:** 2026-04-20

All .NET Aspire packages in this repository now track the latest stable Aspire release (13.2.2 as of this upgrade). Aspire.* packages must be kept aligned and upgraded together whenever a new Aspire CLI version is released.

**Target Versions (13.2.2 Release):**
- Aspire.AppHost.Sdk: 13.2.2 (Stable)
- Aspire.Azure.AI.OpenAI: 13.2.2-preview.1.26207.2 (Preview-only)
- Aspire.Azure.AI.Inference: 13.2.2-preview.1.26207.2 (Preview-only)
- Aspire.Hosting.Azure.AIFoundry: 13.1.3-preview.1.26166.8 (Latest available)
- Aspire.Hosting.Azure.ApplicationInsights: 13.2.2 (Stable)
- Aspire.Hosting.Azure.CognitiveServices: 13.2.2 (Stable)
- CommunityToolkit.Aspire.Hosting.Sqlite: 13.1.1 (Stable)

**Key Notes:**
- Azure AI packages are preview-only and lag behind core Aspire releases
- Aspire.Hosting.Azure.AIFoundry has no 13.2.x version yet on NuGet; use latest available preview
- When upgrading, bump all Aspire packages together and validate with full restore + build
- Document any transitive dependency conflicts and their resolutions

**Validation:** ✅ Build success with 0 errors (src/ZavaAppHost/ZavaAppHost.csproj)

### Aspire CLI Install Standard (2026-04-20)

**Author:** Hicks (Backend / Microservices)  
**Status:** Active

The Aspire team deprecated `dotnet workload install aspire` in favor of a standalone CLI installed via install scripts. Standard install method for this repo:

- **Bash (Linux/macOS):** `curl -sSL https://aspire.dev/install.sh | bash`
- **PowerShell (Windows):** `iex "& { $(irm https://aspire.dev/install.ps1) }"`
- **Validation:** `aspire --version` (should return `13.2.0+{commitSHA}` or similar)

The workload command is deprecated and **must not appear in docs**. All documentation, devcontainer setup, and prerequisite guides now reference the install script method. This aligns with official Aspire documentation (https://aspire.dev/get-started/install-cli/) and ensures compatibility with future releases.

**Files Updated:** `docs/ARCHITECTURE_DEPLOYMENT.md`, `README.MD`, `session-delivery-resources/docs/Prerequisites.md`, `session-delivery-resources/docs/01.Installation.md`, `.devcontainer/devcontainer.json`

### MAF Version Upgrade Blocker (2026-04-20)

**Author:** Bishop (AI Agents Engineer)  
**Status:** BLOCKED  

Cannot upgrade to Microsoft Agent Framework (MAF) 1.1.0 due to breaking API changes. Repository remains on MAF 1.0.0-preview.251219.1 (December 19, 2025 build).

**Latest Available Versions (queried 2026-04-20):**
| Package | Current | Latest | Status |
|---------|---------|--------|--------|
| Microsoft.Agents.AI | 1.0.0-preview.251219.1 | 1.1.0 | Stable released |
| Microsoft.Agents.AI.Abstractions | 1.0.0-preview.251219.1 | 1.1.0 | Stable released |
| Microsoft.Agents.AI.Workflows | 1.0.0-preview.251219.1 | 1.1.0 | Stable released |
| Microsoft.Agents.AI.AzureAI | 1.0.0-preview.251219.1 | 1.0.0-rc5 | RC released |
| Microsoft.Agents.AI.AzureAI.Persistent | 1.0.0-preview.251219.1 | 1.1.0-preview.260410.1 | Preview released |
| Microsoft.Agents.AI.Hosting | 1.0.0-preview.251219.1 | 1.1.0-preview.260410.1 | Preview released |
| Microsoft.Agents.AI.Hosting.OpenAI | 1.0.0-alpha.251219.1 | 1.1.0-alpha.260410.1 | Alpha released |
| Microsoft.Agents.AI.DevUI | 1.0.0-preview.251219.1 | 1.1.0-preview.260410.1 | Preview released |

**Breaking Changes in MAF 1.1.0:**
- `IChatClient.CreateAIAgent()` — removed or relocated
- `AIProjectClient.GetAIAgent()` — removed or relocated
- `AIProjectClient.CreateAIAgent()` — removed or relocated

These methods are **core** to agent instantiation in `ZavaMAFLocal/MAFLocalAgentProvider.cs` and `ZavaMAFFoundry/MAFFoundryAgentProvider.cs`. The changes appear to be an architectural shift, not a simple rename.

**Resolution:** All upgrade changes reverted. Repo stable on 1.0.0-preview.251219.1.

**Action Items:**
- Monitor MAF GitHub / release notes for 1.1.0 migration guide
- If features are urgent, investigate new API via decompilation or sample code
- ✅ **COMPLETED:** Aligned `infra/Brk445-Console-DeployAgents.csproj` to match src/ on 1.0.0-preview.251219.1 (was on older 1.0.0-preview.251204.1)

**Recommendation:** Do NOT upgrade until:
1. MAF team publishes official migration guide for 1.1.0 API changes, OR
2. Agent code is refactored to use new 1.1.0 instantiation pattern.

### MAF Version Alignment Rule (2026-04-20)

**Author:** Bishop (AI Agents Engineer)  
**Status:** Active  

**Principle:** All `Microsoft.Agents.AI.*` packages in this repo MUST track the same preview/stable date stamp. Never mix stamps (e.g., don't mix `.251219.1` and `.260410.1` across projects).

**Current State:** All packages on `1.0.0-preview.251219.1` (with `Microsoft.Agents.AI.Hosting.OpenAI` on matching `1.0.0-alpha.251219.1`). Infra project aligned as of 2026-04-20.

**Why:** Mixing date stamps causes assembly version conflicts at runtime and prevents consistent agent instantiation across working modes (Local, Foundry, AIFoundry).

**Enforcement:** Dietrich (MAF & Foundry Freshness Watcher) audits all MAF package versions quarterly. Any drift triggers alignment sync before deploy.

**Upgrade Path:** Blocked until MAF 1.1.0 migration docs published. When upgrade becomes viable, upgrade all packages together to same stamp.

### Brand Rename to "Microsoft Foundry" + Sequential Doc Navigation (2026-04-20)

**Author:** Lambert (DevRel / Presenter Enablement)  
**Status:** Active  
**Scope:** Documentation

**Context:** Presenter walkthrough revealed two gaps: (1) "Azure AI Foundry" needed brand rename to "Microsoft Foundry" across docs, (2) sequential setup docs lacked prev/next navigation and console-app instructions weren't discoverable.

**Decisions:**

1. **Brand Rename Rule** — Update product references in narrative text; preserve code identifiers, paths, and URLs.
   - Replace "Azure AI Foundry" / "AI Foundry" → "Microsoft Foundry" (product text only)
   - DO NOT apply to repo names, code vars, URLs, Bicep types
   - Files: README.MD (2), 02.NeededCloudResources.md (2), 03.HowToRunDemoLocally.md (2), ManualAgentDeployment.md (5), 01.Installation.md (1) = 12 total replacements

2. **Sequential Docs Navigation Footer** — Link prev/next across Prerequisites → 01 → 02 → 03 sequence
   - Prerequisites.md: Next-only
   - 01–03: Full prev/next/home
   - ManualAgentDeployment.md: Back-link only (branches off, no forward)
   - Files: All 5 docs updated

3. **Console App Discoverability** — Add forward pointer in 01.Installation.md Architecture section
   - Forward pointer: "You'll run this console app as part of [Step 02: Create agents](./02.NeededCloudResources.md#4-create-agents-using-the-console-application)"
   - session-delivery-resources/readme.md: Added Installation row; updated Cloud Resources description
   - Anchor verified in 02.NeededCloudResources.md

**Rationale:** Docs speak to presenters (narrative) not compilers (code). Explicit forward pointers + navigation reduce presenter friction. Product name in text should reflect current branding.

**Verification:** ✅ No "Azure AI Foundry" product strings remain in docs (code identifiers excluded); all nav blocks present and linked; forward pointer validated; index updated.

**Reusability:** These patterns apply to any future doc rebrand or multi-step tutorial.

### Bruno Directive: Drop --use-device-code from az login (2026-04-20)

**Author:** Bruno Capuano (via Copilot)  
**Status:** Active  
**Scope:** All docs, scripts, and demos

Never use `--use-device-code` parameter on `az login` commands. Use plain `az login --tenant <tenant>` instead.

**Files Updated:**
- `session-delivery-resources/docs/02.NeededCloudResources.md` (1 occurrence)
- `session-delivery-resources/docs/03.HowToRunDemoLocally.md` (3 occurrences)

**Rationale:** User request — simplifies auth flow for demo/presenter scenarios.

**DevRel impact:** When writing or updating docs with Azure CLI authentication, use only plain `az login --tenant <tenant>`. Never include `--use-device-code`.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
