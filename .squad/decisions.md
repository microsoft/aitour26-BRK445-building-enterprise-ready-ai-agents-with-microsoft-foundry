# Squad Decisions

## Active Decisions

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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
