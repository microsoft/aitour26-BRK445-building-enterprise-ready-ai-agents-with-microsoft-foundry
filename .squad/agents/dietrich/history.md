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

(Empty — first session.)

### 2026-04-20: MAF 1.1.0 Blocker — Breaking API Changes
- **Fact:** MAF 1.1.0 removed/relocated core agent instantiation methods (`IChatClient.CreateAIAgent()`, `AIProjectClient.GetAIAgent()`, `AIProjectClient.CreateAIAgent()`). This is an architectural shift, not a simple rename or deprecation.
- **Status:** Repo remains on 1.0.0-preview.251219.1 until migration docs published or code is refactored.
- **Implication:** Dietrich must track MAF 1.1.0 migration docs closely. Day-1 awareness: do not attempt upgrades without refactoring plan.
- **Alignment action (completed 2026-04-20):** `infra/Brk445-Console-DeployAgents.csproj` bumped from 1.0.0-preview.251204.1 to 1.0.0-preview.251219.1 to match src/ projects. Both build targets now pass clean.
