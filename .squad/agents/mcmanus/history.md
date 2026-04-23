# McManus — History

## Role

**McManus** is the DevRel/Writer on this project. Charter: Support speaker enablement by surfacing rough edges in delivery docs, extracting patterns for reuse, and recommending improvements to speaker experience.

---

## Learnings

### T-30min Recovery Checklist Implementation (2026-04-21)

Implemented atomic pre-talk recovery procedures as new subsection in `session-delivery-resources/readme.md` immediately after Preparation section. Table format enables copy-paste recovery for 6 highest-friction failure modes:

1. Aspire stuck/won't start → cleanup script + restart
2. MSBuild stale-lock (CS0009) → build-server shutdown + delete obj/bin + clean rebuild
3. Build permission errors (Linux/macOS) → fix_permissions.sh
4. Agent auth misconfig → `az account show` → re-auth if wrong tenant
5. Demo 2 slow (>10min) → UI fallback to "MAF - Local Agents" (30–60% speedup)
6. Foundry HTTP 400 (invalid_payload) → git pull to ensure sanitizer/adapter in place

Also deprecated `ManualAgentDeployment.md` with banner pointing speakers to canonical automated deployment path (Step 4 in 02.NeededCloudResources.md). This reduces confusion and steering toward unsupported workflow.

**Result:** Speakers now have one-glance recovery table instead of hunting across docs. Deprecation banner surfaces the canonical path and removes ambiguity on which agent deployment method to use.

**Commit:** a4ac376 — docs(speaker): T-30min recovery checklist + deprecate manual agent deployment

---

### Speaker Doc Rough Edges (2026-04-20)

Reviewed all speaker-facing docs (`session-delivery-resources/docs/` + `readme.md`) in context of recent demo prep work (Demo 2 payload fix, MSBuild permissions, timing expectations, Aspire hang recovery, credential probes). Key findings:

**Highest friction areas:**
1. **Recovery procedures deeply nested** — Troubleshooting sections exist but scattered across files. T-30min pre-talk checklists need one-glance recovery for Aspire stuck, MSBuild stale-lock, credential stalls. Currently speakers hunt.
2. **Timing expectations newly documented, but fallback paths not linked** — Demo 2 timing data added (~8min normal) but no explicit "if >10min, switch to MAF Local (30–60% faster)" callout linked from timing section. Speakers unsure if slow = normal or hang.
3. **Cloud resource failures (quota, throttling, region availability) have no guidance** — Setup docs end at successful provisioning; post-setup failures (quotas hit, throttling, region-specific model unavailable) have zero context. Speakers blame demo code instead of infrastructure.
4. **Secret misconfigs fail silently** — No validation step-by-step; speakers copy partial secrets (e.g., URL without auth token, truncated connection string) → demo fails with vague "auth denied" messages. No troubleshooting path.
5. **Health-check criteria undefined** — "Verify health endpoints" referenced but not operationalized. No UI-based smoke test defined (e.g., "Agent Pool list populates" = auth OK). Speakers see UI load, assume ready, hit agent auth failures on stage.

**Positive patterns:**
- Timing expectations (Demo 1 ~3min, Demo 2 ~8min) are now well-documented with empirical data, speaker tips, and explanation of log sparsity (ExecutorId doesn't change between hosted-Foundry sequential steps).
- Recovery scripts exist (cleanup-aspire.sh, fix_permissions.sh) but aren't discoverable.
- Navigation footer links (prev/next) recently added; documentation structure is improving.

**Recommendation:** Next doc update cycle should focus on consolidating recovery procedures at top-level (T-30min checklist in readme.md) and linking explicit fallback paths (MAF Local speedup) from demo timing sections. Proposal document in `.squad/decisions/inbox/mcmanus-speaker-doc-suggestions.md` with 10 specific, actionable improvements (6 🔴 High, 3 🟡 Nice, 1 🟢 Polish).

---

## Completed Work

- [2026-04-21] T-30min recovery checklist + manual deployment deprecation
  - Added 6-row recovery table to readme.md Preparation section
  - Deprecated ManualAgentDeployment.md with banner + link to canonical Step 4
  - Tested link target (02.NeededCloudResources.md Step 4) — verified anchor accuracy
  - Pushed to branch bruno-AddSquad-PrepForAus (commit a4ac376)
  - Updated history.md with learnings

- [2026-04-20] Speaker doc review + recommendations (no implementation)
  - Analyzed 6 docs + decisions.md + recent orchestration logs
  - Identified 10 concrete improvements grouped by priority and pain area
  - Created decision inbox file with detailed change proposals
  - Documented learnings for future doc work
