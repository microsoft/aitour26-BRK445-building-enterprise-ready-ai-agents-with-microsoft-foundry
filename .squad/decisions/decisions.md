# Decisions

## MAF AgentRunUpdateEvent Does Not Fire Per Executor in Hosted Foundry Sequential Workflows

**Date:** 2026-04-20T18:21:30Z  
**Status:** Empirical Finding  
**Mitigation Applied:** Default-case event-type logging

### Finding
MAF's AgentRunUpdateEvent does not change ExecutorId between hosted-agent steps in sequential workflows. This causes only the first executor transition to be logged during hosted Foundry runs.

### Observation
- Run 1: Only first executor transition logged
- Run 2: Only first executor transition logged (confirmed across multiple runs)
- Expected: Executor transitions logged for each step

### Mitigation
Default case in ProcessWorkflowEvent now logs all event types via `evt.GetType().Name`, ensuring every workflow event surfaces in Aspire logs for live presenter visibility during Demo 2.

### Lightweight Implementation
```csharp
default:
    _logger.LogInformation("Workflow event: {EventType}", evt.GetType().Name);
    break;
```

Applied to:
- MultiAgentControllerMAFFoundry.cs
- MultiAgentControllerMAFLocal.cs

### Follow-up Investigation
Post-demo: Investigate whether ExecutorInvokedEvent / ExecutorCompletedEvent are emitted during hosted sequential workflows.

### References
- Commit 4db8714: "Demo 2: log all workflow event types for live visibility"
