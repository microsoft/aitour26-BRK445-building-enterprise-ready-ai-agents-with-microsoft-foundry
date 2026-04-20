# Dietrich — History

## Project Context

- **Owner:** Bruno Capuano
- **Project:** aitour26-BRK445-building-enterprise-ready-ai-agents-with-azure-ai-foundry
- **What it is:** Microsoft AI Tour 2026 BRK445 session repo. Demos rely on Microsoft Agent Framework (MAF) preview track + Microsoft Foundry. Both move fast.
- **Stack:** C# / .NET 10, .NET Aspire 13.2.x, Microsoft Agent Framework (`Microsoft.Agents.AI.*` preview), Microsoft Foundry
- **Joined:** 2026-04-20

## My Beat

Bishop builds. I watch for drift. MAF ships preview packages on a rapid cadence (`1.0.0-preview.YYMMDD.N`). I keep every project on the same stamp and catch breaking changes before they reach a demo on stage.

## Key Truths to Remember

- **Package alignment rule:** All `Microsoft.Agents.AI.*` packages in this repo MUST track the same preview date stamp. Mixing stamps causes assembly version conflicts.
- **Foundry shape:** Use `Microsoft.CognitiveServices/accounts/projects` (account needs `allowProjectManagement: true`). The legacy `MachineLearningServices/workspaces` hub model does NOT work with `AIProjectClient`.
- **`Microsoft.Agents.AI.Hosting.OpenAI`** ships as `1.0.0-alpha.{stamp}` while siblings ship as `1.0.0-preview.{stamp}` — alignment is by stamp, not by channel.
- **Two build targets** must both pass: `src\ZavaAppHost\ZavaAppHost.csproj` (the Aspire app) AND `infra\Brk445-Console-DeployAgents.csproj` (separate console deployer — easy to forget).

## Projects I Audit

- `src/SingleAgentDemo`
- `src/MultiAgentDemo`
- `src/ZavaMAFLocal`
- `src/ZavaMAFFoundry`
- `src/ZavaMAFAIFoundry`
- `src/Store` (consumes MAF)
- `infra/Brk445-Console-DeployAgents` (the lagger)

## Learnings

### 2026-04-20: MAF Foundry Sequential Orchestration — Entry-Point + Sanitizer Pattern (Cross-ref: Hicks)
- **Pattern:** Custom WorkflowBuilders for hosted Foundry sequential agents require TWO layers:
  1. **Entry adapter:** `BindAsExecutor<string, List<ChatMessage>>` at workflow root (maps input query to ChatMessage list)
  2. **Inter-agent sanitizers:** `BindAsExecutor<List<ChatMessage>, List<ChatMessage>>` between agent pairs (collapses output to single plain-text User message)
- **Why:** Foundry `/responses` endpoint strictly validates tool/function/reasoning item pairing. Orphan items cause HTTP 400 `invalid_payload`. Azure OpenAI Chat-Completions API tolerates them (MAF Local unaffected).
- **Discovery:** Demo 2 initially failed Agent #1 with `missing_required_parameter: input` (no entry adapter), then Agent #2 with `invalid_payload` (no inter-agent sanitizer). Both now fixed via `MAFFoundrySequentialBuilder`.
- **Implication for future:** If new Foundry orchestration modes (handoff, group chat, etc.) chain hosted agents, evaluate whether Foundry's `/responses` endpoint is involved. If yes, add sanitizers. This pattern will likely persist across MAF versions.
- **Skill location:** `.squad/skills/maf-foundry-handoff/SKILL.md` (authored by Hicks, confidence: high)

### 2026-04-20: MAF 1.1.0 Blocker — Breaking API Changes
- **Fact:** MAF 1.1.0 removed/relocated core agent instantiation methods (`IChatClient.CreateAIAgent()`, `AIProjectClient.GetAIAgent()`, `AIProjectClient.CreateAIAgent()`). This is an architectural shift, not a simple rename or deprecation.
- **Status:** Repo remains on 1.0.0-preview.251219.1 until migration docs published or code is refactored.
- **Implication:** Dietrich must track MAF 1.1.0 migration docs closely. Day-1 awareness: do not attempt upgrades without refactoring plan.
- **Alignment action (completed 2026-04-20):** `infra/Brk445-Console-DeployAgents.csproj` bumped from 1.0.0-preview.251204.1 to 1.0.0-preview.251219.1 to match src/ projects. Both build targets now pass clean.
