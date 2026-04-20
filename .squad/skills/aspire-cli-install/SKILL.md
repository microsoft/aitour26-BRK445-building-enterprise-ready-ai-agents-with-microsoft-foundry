---
name: "aspire-cli-install"
description: "Migrating .NET Aspire repos from workload install to CLI script install"
domain: "aspire, dotnet, devops"
confidence: "high"
source: "earned (verified against https://aspire.dev/get-started/install-cli/ on 2026-04-20; package upgrade to 13.2.2 validated)"
---

## Context

The .NET Aspire team deprecated the `dotnet workload install aspire` command and replaced it with a standalone CLI installed via install scripts. This affects all .NET Aspire repositories that document installation steps or configure devcontainers.

## Patterns

### Installation Commands

**Bash (Linux/macOS):**
```bash
curl -sSL https://aspire.dev/install.sh | bash
```

**PowerShell (Windows):**
```powershell
iex "& { $(irm https://aspire.dev/install.ps1) }"
```

**Validation:**
```bash
aspire --version  # Should return 13.2.0+{commitSHA} or similar
```

### Where to Look in a Repo

1. **README / main docs** — Check for references to "Aspire workload" or `dotnet workload install aspire`
2. **Deployment / architecture docs** — Often contain prerequisite sections with install steps
3. **Session / workshop docs** — `Prerequisites.md`, `Installation.md`, etc.
4. **Devcontainer config** — `.devcontainer/devcontainer.json` `postCreateCommand` or `onCreateCommand`
5. **CI/CD pipelines** — GitHub Actions, Azure Pipelines may have setup steps

### Migration Checklist

- [ ] Replace `dotnet workload install aspire` with install script command
- [ ] Show both bash and PowerShell variants where appropriate
- [ ] Include `aspire --version` validation step
- [ ] Link to https://aspire.dev/get-started/install-cli/ for details
- [ ] Remove "Aspire workload" terminology (obsolete)
- [ ] Update devcontainer `postCreateCommand` if needed (and add a comment explaining why)
- [ ] Check CI/CD scripts for workload install commands

## Examples

### Documentation Update (Markdown)

**Before:**
```markdown
2. **.NET Aspire Workload**
   ```bash
   dotnet workload install aspire
   ```
```

**After:**
```markdown
2. **Aspire CLI**
   
   The Aspire CLI is installed via an install script (the old `dotnet workload install aspire` is obsolete).
   
   **Bash (Linux/macOS):**
   ```bash
   curl -sSL https://aspire.dev/install.sh | bash
   ```
   
   **PowerShell (Windows):**
   ```powershell
   iex "& { $(irm https://aspire.dev/install.ps1) }"
   ```
   
   Verify installation:
   ```bash
   aspire --version  # Should return 13.2.0+{commitSHA} or similar
   ```
   
   See https://aspire.dev/get-started/install-cli/ for details.
```

### Devcontainer Update

**Before:**
```json
"postCreateCommand": "dotnet workload install aspire"
```

**After:**
```json
// Installs Aspire CLI per https://aspire.dev/get-started/install-cli/
"postCreateCommand": "curl -sSL https://aspire.dev/install.sh | bash"
```

## Anti-Patterns

- ❌ Leaving `dotnet workload install aspire` in docs or devcontainer — it's obsolete
- ❌ Only showing one OS variant (bash or PowerShell) in cross-platform docs — show both
- ❌ Not including `aspire --version` validation step — developers need to confirm the install worked
- ❌ Not linking to https://aspire.dev/get-started/install-cli/ — official source is authoritative
- ❌ Using "Aspire workload" terminology in new docs — the workload concept is deprecated for Aspire
