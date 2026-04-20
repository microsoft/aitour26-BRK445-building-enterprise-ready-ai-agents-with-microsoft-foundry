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
