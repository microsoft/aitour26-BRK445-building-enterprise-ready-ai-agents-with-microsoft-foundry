## How to deliver this session

🥇 Thanks for delivering this session!

Prior to delivering the workshop please:

1. Read this document and all included resources included in their entirety.
2. Watch the video presentation
3. Ask questions of the content leads! We're here to help!

## 📁 File Summary

| Resource | Link | Type | Description |
|---|---|---:|---|
| Session Delivery Deck | [Deck](https://aka.ms/AAxri1f) | External | Main slide deck for the session |
| Full Session Recording | [Recording CodeSpaces](https://youtu.be/ReWkkXbnF7I) | External | Full train-the-trainer recorded session |
| Demo source code | [`/src` demo source](../src) | Internal | Demo source code used in the live demos |
| Prerequisites | [`Prerequisites`](./docs/Prerequisites.md) | Internal | Tooling and access required to run the demos |
| Installation | [`Installation`](./docs/01.Installation.md) | Internal | Prepare environment and architecture overview |
| Cloud Resources | [`Needed Cloud Resources`](./docs/02.NeededCloudResources.md) | Internal | Create cloud resources and deploy agents via console app |
| How to run demo locally | [`03.HowToRunDemoLocally`](./docs/03.HowToRunDemoLocally.md) | Internal | Step-by-step instructions to build and run the demo locally |
| How to setup demo environment using CodeSpaces | [SetupCodespaces](https://aka.ms/AAyd4kq) | External | Step-by-step instructions to run the demo using codespaces |

## 🚀Get Started

The workshop mixes short live demos with recorded segments for reference.

### 🕐Timing

> Note: Times are approximate. Use the `Mode` guidance to decide whether to run a demo live or play the recorded fallback.

| Time | Segment | Mode | Notes |
|---:|---|---|---|
| 07 mins | Introduction & content | Live | Presenter: lead — slides 1–3 |
| 06 mins | Demo — AI Foundry Agents | Live (recorded fallback) | [Recorded demo](https://aka.ms/AAz3su8) |
| 04 mins | Content | Live | Key point: agent overview |
| 06 mins | Demo — Aspire + Single Agent | Recorded (recommended) | [Recorded demo CodeSpaces](https://aka.ms/AAz480e) |
| 03 mins | Content | Live | Transition & Q&A |
| 07 mins | Demo — Multi-Agent Orchestration | Live (recorded fallback) | [Recorded demo CodeSpaces](https://aka.ms/AAz408f) |
| 04 mins | Content | Live | Observability & tracing |
| 02 mins | Demo — Azure Monitor & Diagnostics | Recorded | [Recorded demo](https://aka.ms/AAz3su9) |
| 04 mins | Content / Q&A | Live | Wrap-up & next steps |

### 🏋️Preparation (presenter quick-check)

Purpose: a short, actionable checklist to get a presenter ready for a live session. The repo contains more detailed setup steps in `./docs/01.Installation.md` — use the checklist below as the final pre-session verification.

Pre-session checklist (30–60 minutes before)

- [ ] Clone the repository (or pull latest if already cloned)
- [ ] Install prerequisites — follow `session-delivery-resources/docs/Prerequisites.md`
- [ ] Deploy required cloud resources (see `session-delivery-resources/docs/02.NeededCloudResources.md`) or confirm they already exist
- [ ] Follow the instructions in `session-delivery-resources/docs/03.HowToRunDemoLocally.md` to run the demo locally and verify health endpoints

### ⚡ T-30min Speaker Recovery Checklist

Atomic recovery actions for the most common pre-talk failures. Each row is one command you can paste.

| Symptom | Recovery |
|---|---|
| Aspire stuck / won't start | `bash src/cleanup-aspire.sh` then `aspire run` |
| Build fails CS0009 / corrupt ref DLL | `dotnet build-server shutdown; Remove-Item -Recurse -Force src\*\obj, src\*\bin; dotnet build src\ZavaAppHost\ZavaAppHost.csproj` |
| Build permission error (Linux/macOS) | `./infra/fix_permissions.sh` |
| Agent auth fails | `az account show` → if wrong tenant: `az login --tenant <tenant-id>` |
| Demo 2 running >10 min | Open Store → **Settings** → switch to "MAF - Local Agents" (30–60% faster) |
| Foundry HTTP 400 invalid_payload / missing input | Confirm latest `git pull` (sanitizer + entry-point adapter must be in place) |

If none of these apply, see `docs/03.HowToRunDemoLocally.md` Troubleshooting.

Fallback & recording guidance

- If a live demo fails (service doesn't start, index not ready, or external resource is inaccessible) — play the recorded demo clip for that segment and mark the runbook with the issue.
- For fragile demos (search/indexing, external APIs), prefer the recorded fallback during high-risk sessions.

