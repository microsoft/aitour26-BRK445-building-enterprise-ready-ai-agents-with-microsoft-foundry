# Squad Decisions

## Active Decisions

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
