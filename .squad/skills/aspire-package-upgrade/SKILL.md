---
name: "aspire-package-upgrade"
description: "Upgrading .NET Aspire packages to latest stable/preview versions"
domain: "aspire, dotnet, nuget"
confidence: "high"
source: "earned (verified via full restore+build on 13.2.2 upgrade, 2026-04-20)"
---

## Context

.NET Aspire releases frequently, and Aspire packages should be kept aligned with the installed Aspire CLI version. This skill documents the process for discovering latest versions, upgrading all Aspire packages together, and validating the upgrade with a build.

**Key Principle:** Aspire.* packages should track the same major.minor version (e.g., all on 13.2.x) to avoid API/behavior mismatches.

## Discovery — Find Latest Versions

### Via NuGet API (PowerShell)

```powershell
$packages = @(
    'aspire.apphost.sdk',
    'aspire.azure.ai.openai',
    'aspire.azure.ai.inference',
    'aspire.hosting.azure.aifoundry',
    'aspire.hosting.azure.applicationinsights',
    'aspire.hosting.azure.cognitiveservices',
    'communitytoolkit.aspire.hosting.sqlite'
)

foreach ($pkg in $packages) {
    $response = Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/$pkg/index.json"
    # Get versions starting with desired major.minor (e.g., 13.2)
    $v13 = $response.versions | Where-Object { $_ -match '^13\.2\.' } | Sort-Object -Descending
    $v13stable = $v13 | Where-Object { $_ -notmatch 'preview|alpha|beta|rc' } | Select-Object -First 1
    
    if ($v13stable) {
        Write-Output "$pkg : $v13stable (stable)"
    } elseif ($v13 | Select-Object -First 1) {
        Write-Output "$pkg : $($v13 | Select-Object -First 1) (preview-only)"
    } else {
        Write-Output "$pkg : No 13.2.x versions found"
    }
}
```

### Via grep (Find Current Versions)

```bash
grep -rn "Aspire\." --include="*.csproj" src/
grep -rn "Aspire\.AppHost\.Sdk" --include="*.csproj" src/
```

Build a version table: project → current version → target version.

## Upgrade Strategy

### 1. Update Aspire.AppHost.Sdk (ZavaAppHost)

The SDK version in `<Project Sdk="Aspire.AppHost.Sdk/X.Y.Z">` should match the Aspire CLI version.

**Before:**
```xml
<Project Sdk="Aspire.AppHost.Sdk/13.1.0">
```

**After:**
```xml
<Project Sdk="Aspire.AppHost.Sdk/13.2.2">
```

### 2. Update All Aspire PackageReferences

Use the `edit` tool to update each `<PackageReference Include="Aspire.*" Version="..." />` in every .csproj under `src/`.

**Target versions (as of 13.2.2 release):**
- Aspire.Hosting.Azure.* packages → 13.2.2 (stable)
- Aspire.Azure.AI.* packages → 13.2.2-preview.1.26207.2 (preview-only)
- CommunityToolkit.Aspire.* → check latest (may lag behind)

**Prefer stable where available.** If only preview versions exist for a package, use the latest preview aligned with the target major.minor.

### 3. Handle Transitive Conflicts

After upgrading Aspire packages, restore may fail with NU1605 (package downgrade) errors. This happens when:
- New Aspire package depends on a newer version of Microsoft.Extensions.AI.* or other shared dependencies
- Your project has a direct reference to an older version

**Resolution:** Bump the conflicting package to match the transitive dependency version.

**Example (from 13.2.2 upgrade):**
```
error NU1605: Detected package downgrade: Microsoft.Extensions.AI.Abstractions from 10.2.0 to 10.1.1.
  DataService -> Aspire.Azure.AI.Inference 13.2.2-preview.1.26207.2 -> Microsoft.Extensions.AI 10.2.0 -> Microsoft.Extensions.AI.Abstractions (>= 10.2.0)
  DataService -> Microsoft.Extensions.AI.Abstractions (>= 10.1.1)
```

**Fix:** Bump DataService's direct reference from 10.1.1 to 10.2.0.

### 4. Validate with Restore + Build

```bash
dotnet restore src\ZavaAppHost\ZavaAppHost.csproj
dotnet build src\ZavaAppHost\ZavaAppHost.csproj --no-restore
```

**Success Criteria:**
- Restore completes (warnings for low-severity vulnerabilities in transitive deps are acceptable)
- Build completes with 0 errors (warnings are acceptable)

**On Failure:**
- Read the first error fully
- If it's a package downgrade (NU1605), bump the conflicting package (see step 3)
- Retry restore + build
- If it still fails, STOP and revert changes (`git checkout -- src/`)

## Files to Touch

1. **Every .csproj under src/** that has `<PackageReference Include="Aspire.*"` or `CommunityToolkit.Aspire.*`
2. **src/ZavaAppHost/ZavaAppHost.csproj** — the `<Project Sdk="Aspire.AppHost.Sdk/...">` line

## Patterns

### Preview-Only Packages

Some Aspire packages (especially Azure AI integrations) don't yet have stable releases. Track the latest preview version aligned with the target Aspire version (e.g., 13.2.2-preview.1.26207.2 for the 13.2.2 release).

**Current preview-only packages (as of 13.2.2):**
- Aspire.Azure.AI.OpenAI
- Aspire.Azure.AI.Inference
- Aspire.Hosting.Azure.AIFoundry (may lag behind — use latest available)

### Version Alignment Rule

**All Aspire.* packages should be on the same major.minor version (e.g., all on 13.2.x).** Mixing versions (e.g., 13.1.0 and 13.2.2) can cause runtime API mismatches and is NOT supported.

## Anti-Patterns

- ❌ Only bumping some Aspire packages (e.g., just ZavaAppHost) — bump ALL together
- ❌ Ignoring NU1605 errors — these WILL cause runtime failures; resolve them before committing
- ❌ Mixing Aspire versions (e.g., AppHost.Sdk 13.2.2 + Hosting.Azure.* 13.1.0) — keep aligned
- ❌ Touching non-Aspire packages unnecessarily — ONLY bump Aspire.* and transitive conflict resolutions
- ❌ Committing without validating the build — always run `dotnet build` before pushing

## Example Workflow (13.2.2 Upgrade)

1. Check Aspire CLI version: `aspire --version` → `13.2.2+25961cf...`
2. Grep for current Aspire versions in repo
3. Query NuGet API for latest 13.2.x versions
4. Build version table (before/after)
5. Edit ZavaAppHost.csproj SDK line: 13.1.0 → 13.2.2
6. Edit all service .csproj files: bump Aspire.Azure.AI.OpenAI, etc.
7. Edit ZavaAppHost.csproj: bump Aspire.Hosting.Azure.* packages
8. Run `dotnet restore src\ZavaAppHost\ZavaAppHost.csproj`
9. If NU1605 error: bump Microsoft.Extensions.AI.Abstractions in DataService.csproj
10. Retry restore → SUCCESS (2 warnings, acceptable)
11. Run `dotnet build src\ZavaAppHost\ZavaAppHost.csproj --no-restore` → SUCCESS (0 errors)
12. Document: update history.md, create decision note
13. Commit with message: "chore: upgrade Aspire packages to 13.2.2"

## Success Indicators

- ✅ All Aspire packages aligned on same major.minor version
- ✅ `dotnet restore` completes (low-severity NU1901 warnings are OK)
- ✅ `dotnet build` completes with 0 errors
- ✅ Version table documented in history or decision note
- ✅ Transitive dependency conflicts resolved and documented
