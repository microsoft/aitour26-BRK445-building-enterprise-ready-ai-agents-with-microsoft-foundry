# Project Context

- **Owner:** Bruno Capuano
- **Project:** aitour26-BRK445 — Microsoft AI Tour 2026 session "Building enterprise-ready AI Agents with Microsoft Foundry". A .NET Aspire solution (Zava) demonstrating multi-service AI agent patterns with Microsoft Foundry / MAF.
- **Stack:** C# / .NET 10, .NET Aspire, Microsoft Agent Framework (MAF), Microsoft Foundry Agents, Blazor (Store frontend), xUnit, Bicep (infra), Docker, devcontainer
- **Key entry points:** `src/BRK445-Zava-Aspire.slnx`, `src/ZavaAppHost`, `src/ZavaWorkingModes/WorkingMode.cs`, `src/Store` (settings page switches working modes; persisted to `localStorage`)
- **Created:** 2026-04-20T14:52:17Z

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-04-20: MAF Foundry Sequential Workflow — Hosted Hand-off Sanitization (Hicks)
- **Skill:** `.squad/skills/maf-foundry-handoff/` — New `MAFFoundrySequentialBuilder.BuildSequentialForFoundry()` for Foundry-hosted sequential orchestration
- **Problem:** Foundry `/responses` endpoint rejects orphan tool-call/function-call/reasoning items; `AgentWorkflowBuilder.BuildSequential` chains agents by passing prior agent's full output, poisoning next agent's request
- **Solution:** Interpose `BindAsExecutor<List<ChatMessage>, List<ChatMessage>>` sanitizers between agent pairs to collapse output to single plain-text User message
- **Reusability:** Apply when chaining hosted Foundry agents sequentially. Pattern: strip all non-text items before handoff.

