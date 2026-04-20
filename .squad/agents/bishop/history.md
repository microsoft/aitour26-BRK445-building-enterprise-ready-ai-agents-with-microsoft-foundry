# Project Context

- **Owner:** Bruno Capuano
- **Project:** aitour26-BRK445 — Microsoft AI Tour 2026 session "Building enterprise-ready AI Agents with Microsoft Foundry". A .NET Aspire solution (Zava) demonstrating multi-service AI agent patterns with Microsoft Foundry / MAF.
- **Stack:** C# / .NET 10, .NET Aspire, Microsoft Agent Framework (MAF), Microsoft Foundry Agents, Blazor (Store frontend), xUnit, Bicep (infra), Docker, devcontainer
- **Key entry points:** `src/BRK445-Zava-Aspire.slnx`, `src/ZavaAppHost`, `src/ZavaWorkingModes/WorkingMode.cs`, `src/Store` (settings page switches working modes; persisted to `localStorage`)
- **Created:** 2026-04-20T14:52:17Z

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### MAF Version Analysis — 2026-04-20

**Current State:**
| Package | Current Version | Latest Available | Track |
|---------|----------------|------------------|-------|
| Microsoft.Agents.AI | 1.0.0-preview.251219.1 | 1.1.0 | Stable available |
| Microsoft.Agents.AI.Abstractions | 1.0.0-preview.251219.1 | 1.1.0 | Stable available |
| Microsoft.Agents.AI.Workflows | 1.0.0-preview.251219.1 | 1.1.0 | Stable available |
| Microsoft.Agents.AI.AzureAI | 1.0.0-preview.251219.1 | 1.0.0-rc5 | RC |
| Microsoft.Agents.AI.AzureAI.Persistent | 1.0.0-preview.251219.1 | 1.1.0-preview.260410.1 | Preview |
| Microsoft.Agents.AI.Hosting | 1.0.0-preview.251219.1 | 1.1.0-preview.260410.1 | Preview |
| Microsoft.Agents.AI.Hosting.OpenAI | 1.0.0-alpha.251219.1 | 1.1.0-alpha.260410.1 | Alpha |
| Microsoft.Agents.AI.DevUI | 1.0.0-preview.251219.1 | 1.1.0-preview.260410.1 | Preview |

**Infra project misalignment:** `infra/Brk445-Console-DeployAgents.csproj` was on older 1.0.0-preview.251204.1 (Dec 4 vs Dec 19).

**BLOCKER — MAF 1.1.0 Breaking Changes:**

Attempted upgrade to MAF 1.1.0 / 1.0.0-rc5 revealed major API breaking changes:
- `IChatClient.CreateAIAgent()` → removed or relocated
- `AIProjectClient.GetAIAgent()` → removed or relocated  
- `AIProjectClient.CreateAIAgent()` → removed or relocated

These methods are core to `ZavaMAFLocal/MAFLocalAgentProvider.cs` (line 96) and `ZavaMAFFoundry/MAFFoundryAgentProvider.cs` (lines 51, 66, 72, 228).

**Dependency chain fix required:**
- MAF 1.0.0-rc5 (AzureAI) requires **Azure.Identity >= 1.19.0** (stable), but the repo was on 1.18.0-beta.2.
- Aspire 13.2.2-preview.1.26207.2 requires **Microsoft.Extensions.AI.Abstractions 10.4.0**, but DataService was on 10.2.0.
- MAF 1.1.0-preview.260410.1 requires **System.Memory.Data 10.0.3** (was 10.0.1).

**Outcome:** Changes reverted (`git checkout -- src/ infra/`). The MAF 1.1.0 release requires significant refactoring beyond method renames — likely an architectural change in how agents are instantiated. Bruno needs to consult MAF 1.1.0 migration docs or wait for a future MAF release that restores API compatibility.

**Recommendation:** Stay on MAF 1.0.0-preview.251219.1 until:
1. MAF team publishes migration guide for 1.1.0 API changes, OR
2. Agent code is refactored to use the new 1.1.0 instantiation pattern.

## Learnings

- MAF 1.1.0 breaking change: core agent instantiation methods removed/relocated (architectural shift, not rename)
- **Infra alignment completed by Squad (2026-04-20):** `infra/Brk445-Console-DeployAgents.csproj` now on 1.0.0-preview.251219.1, matching src/ projects

### Demo 2: MAF Event Logging for Hosted Sequential Workflows — 2026-04-20

**Finding (Empirical):** MAF's AgentRunUpdateEvent does not change ExecutorId between hosted-agent sequential workflow steps. Only the first executor transition was logged across multiple Demo 2 runs.

**Mitigation:** Default case in ProcessWorkflowEvent now logs all unhandled workflow event types via `evt.GetType().Name`, ensuring every workflow event surfaces in Aspire logs for live presenter visibility. Applied symmetrically to both MAFFoundry and MAFLocal controllers.

**Commit:** 4db8714 — "Demo 2: log all workflow event types for live visibility"

**Follow-up:** Investigate whether ExecutorInvokedEvent / ExecutorCompletedEvent are emitted during hosted sequential workflows.
