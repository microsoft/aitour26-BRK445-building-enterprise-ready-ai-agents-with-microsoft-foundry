# Project Context

- **Owner:** Bruno Capuano
- **Project:** aitour26-BRK445 — Microsoft AI Tour 2026 session "Building enterprise-ready AI Agents with Microsoft Foundry". A .NET Aspire solution (Zava) demonstrating multi-service AI agent patterns with Microsoft Foundry / MAF.
- **Stack:** C# / .NET 10, .NET Aspire, Microsoft Agent Framework (MAF), Microsoft Foundry Agents, Blazor (Store frontend), xUnit, Bicep (infra), Docker, devcontainer
- **Key entry points:** `src/BRK445-Zava-Aspire.slnx`, `src/ZavaAppHost`, `src/ZavaWorkingModes/WorkingMode.cs`, `src/Store` (settings page switches working modes; persisted to `localStorage`)
- **Created:** 2026-04-20T14:52:17Z

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-04-20: Aspire CLI Install Standardization (cross-agent note)
- **Fact:** Aspire CLI install replaced `dotnet workload install aspire` — the workload command is obsolete. Official install method via https://aspire.dev/get-started/install-cli/.
- **DevRel impact:** All prerequisites and installation docs now reference Aspire CLI. When documenting setup for future sessions, use only the official CLI install scripts (bash or PowerShell), not the deprecated workload command.

### 2026-04-20: Brand Rename Rule — "Microsoft Foundry" (Lambert)
- **Scope:** Product text references only (e.g., "Create a Microsoft Foundry project"). DO NOT apply to:
  - Repo folder paths (`aitour26-BRK445-building-enterprise-ready-ai-agents-with-azure-ai-foundry`)
  - Code identifiers (`MafAIFoundry`, `maf_ai_foundry`)
  - Azure resource types (`Microsoft.CognitiveServices/accounts/projects`)
  - URLs (`ai.azure.com` stays as-is)
- **Pattern:** Search docs for "Azure AI Foundry" and "AI Foundry" (product strings); replace with "Microsoft Foundry". Excluded `MafAIFoundry` code mode names and embedded file path references.
- **DevRel impact:** When updating presenter materials, apply this rule to all product mentions. Keeps branding consistent in narrative text while preserving code and infrastructure identifiers.

### 2026-04-20: Sequential Docs Navigation Pattern (Lambert)
- **Pattern:** Numbered prerequisite docs (Prerequisites.md → 01.Installation.md → 02.NeededCloudResources.md → 03.HowToRunDemoLocally.md) need prev/next footer links.
- **Format:**
  - First doc: `[🏠 Session Delivery Resources](../readme.md) | [Next: <Title> ➡](./<file>.md)`
  - Middle docs: `[⬅ Previous: <Title>](./<file>.md) | [🏠 Session Delivery Resources](../readme.md) | [Next: <Title> ➡](./<file>.md)`
  - Last doc: `[⬅ Previous: <Title>](./<file>.md) | [🏠 Session Delivery Resources](../readme.md)`
  - Side-referenced docs (e.g., ManualAgentDeployment.md): Back-links only (parent + home), no forward.
- **DevRel impact:** Reusable pattern for any future multi-step setup docs. Improves presenter flow when reading sequentially.
