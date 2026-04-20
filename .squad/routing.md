# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Architecture, scope, cross-service contracts | Ripley | "Should we add a new working mode?", AppHost composition, RAI posture |
| Microservices / .NET service code | Hicks | DataService, InventoryService, LocationService, NavigationService, MatchmakingService, ProductSearchService, CustomerInformationService, AnalyzePhotoService, ToolReasoningService, shared entities |
| Agents, MAF, Foundry, prompts, tools | Bishop | SingleAgentDemo, MultiAgentDemo, ZavaMAFLocal, ZavaMAFFoundry, ZavaMAFAIFoundry, ZavaAgentsMetadata, working-mode behavior |
| Blazor / Store frontend / settings UI | Vasquez | Store pages, components, working-mode toggle, localStorage |
| Tests / QA / evals / reviewer verdicts | Hudson | xUnit tests, ZavaMAFLocal.Tests, ZavaMAFFoundry.Tests, DataService.Tests, Store.Tests |
| Docs / README / session-delivery / presenter scripts | Lambert | session-delivery-resources/, README.MD, src/readme.md, presenter notes, demo scripts |
| Code review | Ripley (architecture); Hudson (quality/tests) | PR review, regression risk |
| Session logging | Scribe | Automatic — never needs routing |
| Work queue / backlog monitoring | Ralph | Issue triage scans, PR follow-up |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, assign `squad:{member}` label | Ripley (Lead) |
| `squad:ripley` | Architecture, scope, cross-cutting | Ripley |
| `squad:hicks` | Service / backend work | Hicks |
| `squad:bishop` | Agent / MAF / Foundry work | Bishop |
| `squad:vasquez` | Store / Blazor / UI | Vasquez |
| `squad:hudson` | Tests, QA, evaluation | Hudson |
| `squad:lambert` | Docs, README, session delivery | Lambert |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, **Ripley** triages it — analyzing content, assigning the right `squad:{member}` label, and commenting with triage notes.
2. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
3. Members can reassign by removing their label and adding another member's label.
4. The `squad` label is the "inbox" — untriaged issues waiting for Lead review.

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what working modes exist?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** New service from Hicks → Hudson writes tests in parallel; new working mode from Bishop → Vasquez updates the settings UI in parallel; any user-visible change → Lambert updates docs.
7. **Issue-labeled work** — when a `squad:{member}` label is applied, route to that member. Ripley handles all `squad` (base label) triage.
