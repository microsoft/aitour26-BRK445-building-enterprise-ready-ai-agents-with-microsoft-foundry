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
