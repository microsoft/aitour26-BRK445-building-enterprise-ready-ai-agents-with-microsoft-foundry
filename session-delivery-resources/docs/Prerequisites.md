# Prerequisites

This file lists the minimum tooling and access required to run the aspiredemo (Zava‑Aspire) locally and to provision the cloud resources used by the demos.

## Required tooling

- .NET SDK 10.x (verify with `dotnet --info` or `dotnet --list-sdks`) — [Download .NET](https://dotnet.microsoft.com/en-us/download)
- Aspire CLI — Install with `curl -sSL https://aspire.dev/install.sh | bash` (Linux/macOS) or PowerShell `iex "& { $(irm https://aspire.dev/install.ps1) }"` (Windows). Verify with `aspire --version`. See https://aspire.dev/get-started/install-cli/ for details.
- Visual Studio 2022 (recommended 17.14.13+) or Visual Studio Code with C#/.NET extensions
- Docker Desktop (required only for containerized runs)
- Git

## Optional (only if deploying to Azure or using cloud features)

- Azure subscription with permission to create resources
- Azure CLI (`az`) and optionally Azure Developer CLI (`azd`)

## Notes

- The solution is cross-platform — it runs on Windows, macOS, and Linux.

---

See also:

- `./02.NeededCloudResources.md` — instructions to create the Microsoft Foundry project, models and agents used by the demos.
- `./03.HowToRunDemoLocally.md` — step-by-step instructions to build and run the demo locally.
