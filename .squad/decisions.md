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

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
