# Microsoft AI Tour 2026 — BRK445

![AI Tour banner](img/Banner-MS-AI-Tour-26.png)

[![Microsoft Foundry Discord](https://dcbadge.limes.pink/api/server/Pwpvf3TWaw)](https://aka.ms/MicrosoftFoundryDiscord-AITour26)
[![Microsoft Foundry Developer Forum](https://img.shields.io/badge/GitHub-Microsoft_Foundry_Developer_Forum-blue?style=for-the-badge&logo=github&color=adff2f&logoColor=fff)](https://aka.ms/MicrosoftFoundryForum-AITour26)

Microsoft Foundry Agent Service: Building enterprise-ready AI Agents with Microsoft Foundry

This repository contains the sample code, services, and materials used in BRK445: "Building enterprise-ready AI Agents with Microsoft Foundry" presented at Microsoft AI Tour 2026. The solution demonstrates patterns for designing, implementing, and deploying multi-service AI agents with governance, reuse, and responsible-AI considerations.

If you are delivering this session, check the `session-delivery-resources/` folder for slides, scripts, and demos.

## Quick links

- Source code: `src/`
- Session delivery resources: `session-delivery-resources/`

## What you'll learn

In this session you will learn practical techniques to:

- Design agent architectures that separate reasoning, tools, and orchestration
- Integrate Microsoft Foundry (Azure AI Studio) services with local microservices
- Add safety, evaluation, and observability to generative AI agents
- Package and run multi-service demos locally and in Azure

### Target audience

Developers, architects, and AI engineers who build production-grade agentic applications with Azure AI services and need guidance on patterns for security, governance, and reliability.

## What's in this repository

- `src/` — Example source code used by the demos (C#, .NET 10).  
- `session-delivery-resources/` — slides, demos, and presenter notes

## 💻 Development Setup

### Devcontainer Notes

The devcontainer is configured to:

- ✅ Install .NET 10 SDK and Aspire workloads
- ✅ Install Aspire CLI tool

#### Troubleshooting (Windows + WSL + Docker Desktop)

If starting the devcontainer fails with an error like:

`accessing specified distro mount service: stat /run/guest-services/distro-services/ubuntu.sock: no such file or directory`

it means Docker Desktop can’t bind-mount a WSL path (for example `\\wsl.localhost\Ubuntu\...`) because the Docker Desktop WSL “distro mount service” is not available.

Fix options:

- **Preferred:** In **Docker Desktop** → **Settings** → **Resources** → **WSL integration**:
  - Enable WSL integration
  - Enable integration for your distro (for example **Ubuntu**)
  - Apply & Restart Docker Desktop

- **Workaround:** Disable Dev Containers’ WSLg/Wayland socket mounting (this repo sets workspace defaults in `.vscode/settings.json`), since the BRK445 devcontainer does not require GUI forwarding.

After applying the fix, you can verify the mount service exists by running in a terminal:

- `wsl -d docker-desktop -e sh -lc "ls -la /run/guest-services/distro-services"`

## Technologies used

-- C# / .NET 10 / .NET Aspire for orchestration
- Microsoft Foundry / Microsoft Foundry Agents
- Docker

### Agent Frameworks and Working modes

This solution demonstrates multiple working modes and agent integration patterns. The available working modes are implemented in `src/ZavaWorkingModes/WorkingMode.cs` and include direct HTTP calls, LLM direct call, Microsoft Agent Framework (MAF) with Microsoft Foundry, AI Foundry variants, and local agent modes.

The recommended developer flow uses the Aspire AppHost and the solution `src/BRK445-Zava-Aspire.slnx`. Use the Store frontend Settings page to switch working modes; selections are saved to browser `localStorage` and take effect immediately.

See `src/readme.md` for details on controller routes and how the Store routes requests according to working mode.

## 🔗 Session Resources

Check the [Session Delivery Resources folder](./session-delivery-resources/) for the whole set of materials.

| Resource | Path |
|---|---:|
| Presenter Guide (full) | [session-delivery-resources/readme.md](session-delivery-resources/readme.md)
| Slides | [EN-US_BRK445_Tech_FY26.pptx](https://aka.ms/AAxri1f) |

## Content Owners

<table>
<tr>
    <td align="center"><a href="http://github.com/mikekinsman">
        <img src="https://github.com/elbruno.png" width="100px;" alt="Bruno Capuano"/>
        <br />
        <sub><b>Bruno Capuano</b></sub></a>
        <br />
        <a href="https://github.com/elbruno" title="talk">📢</a>
    </td>
    <td align="center"><a href="https://github.com/kinfey">
        <img src="https://github.com/kinfey.png" width="100px;" alt="Bruno Capuano"/>
        <br />
        <sub><b>Kinfey Lo</b></sub></a>
        <br />
        <a href="https://github.com/kinfey" title="talk">📢</a>
    </td>

</tr></table>

## Responsible AI

Microsoft is committed to helping our customers use our AI products responsibly, sharing our learnings, and building trust-based partnerships through tools like Transparency Notes and Impact Assessments. Many of these resources can be found at [https://aka.ms/RAI](https://aka.ms/RAI).
Microsoft’s approach to responsible AI is grounded in our AI principles of fairness, reliability and safety, privacy and security, inclusiveness, transparency, and accountability.

Large-scale natural language, image, and speech models - like the ones used in this sample - can potentially behave in ways that are unfair, unreliable, or offensive, in turn causing harms. Please consult the [Azure OpenAI service Transparency note](https://learn.microsoft.com/legal/cognitive-services/openai/transparency-note?tabs=text) to be informed about risks and limitations.

The recommended approach to mitigating these risks is to include a safety system in your architecture that can detect and prevent harmful behavior. [Azure AI Content Safety](https://learn.microsoft.com/azure/ai-services/content-safety/overview) provides an independent layer of protection, able to detect harmful user-generated and AI-generated content in applications and services. Azure AI Content Safety includes text and image APIs that allow you to detect material that is harmful. Within Microsoft Foundry portal, the Content Safety service allows you to view, explore and try out sample code for detecting harmful content across different modalities. The following [quickstart documentation](https://learn.microsoft.com/azure/ai-services/content-safety/quickstart-text?tabs=visual-studio%2Clinux&pivots=programming-language-rest) guides you through making requests to the service.

Another aspect to take into account is the overall application performance. With multi-modal and multi-models applications, we consider performance to mean that the system performs as you and your users expect, including not generating harmful outputs. It's important to assess the performance of your overall application using [Performance and Quality and Risk and Safety evaluators](https://learn.microsoft.com/azure/ai-studio/concepts/evaluation-metrics-built-in). You also have the ability to create and evaluate with [custom evaluators](https://learn.microsoft.com/azure/ai-studio/how-to/develop/evaluate-sdk#custom-evaluators).

You can evaluate your AI application in your development environment using the [Azure AI Evaluation SDK](https://microsoft.github.io/promptflow/index.html). Given either a test dataset or a target, your generative AI application generations are quantitatively measured with built-in evaluators or custom evaluators of your choice. To get started with the azure ai evaluation sdk to evaluate your system, you can follow the [quickstart guide](https://learn.microsoft.com/azure/ai-studio/how-to/develop/flow-evaluate-sdk). Once you execute an evaluation run, you can [visualize the results in Microsoft Foundry portal](https://learn.microsoft.com/azure/ai-studio/how-to/evaluate-flow-results).
