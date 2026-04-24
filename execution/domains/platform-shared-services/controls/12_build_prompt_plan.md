# 12_build_prompt_plan.md — Platform Build Prompt Plan

**Purpose:** Provide the execution-sequencing view for Platform & Shared Services batches.

| Batch | Prompt file | Primary module(s) | Wave | Objective | Depends on | Suggested status |
|---|---|---|---|---|---|---|
| 1 | `batch-01-module-skeleton-boundary.md` | MOD-0018, MOD-0021, MOD-0023, MOD-0024, MOD-0028, MOD-0031, MOD-0012, MOD-0032 | W1 | Create module skeletons and enforce repo boundaries | Control-1 outputs completed | Ready |
| 2 | `batch-02-shared-service-integration-seams.md` | MOD-0032, MOD-0035, MOD-0041, MOD-0042 (+ AuthN consumption) | W1/W2 | Wire provider-mode seams and deployment mode decisions | Batch 01 | Blocked until mode decisions closed |
| 3 | `batch-03-rbac-abac-mod-0018.md` | MOD-0018 | W1 | Implement RBAC/ABAC MVP | Batch 01, Batch 02 | Ready |
| 4 | `batch-04-audit-trail-mod-0021.md` | MOD-0021 | W1 | Implement append-only audit trail MVP | Batch 01, Batch 03 | Ready |
| 5 | `batch-05-docs-mod-0028.md` | MOD-0028 | W1 | Implement document management MVP | Batch 01, Batch 03, Batch 04 | Ready |
| 6 | `batch-06-evidence-linking-mod-0031.md` | MOD-0031 | W1 | Implement evidence linking MVP | Batch 05, Batch 04 | Ready |
| 7 | `batch-07-workflow-mod-0023.md` | MOD-0023 | W1 | Implement approvals-focused workflow MVP | Batch 03, Batch 04, Batch 06 | Ready |
| 8 | `batch-08-tasks-checklists-mod-0024.md` | MOD-0024 | W1/W2 | Implement tasks and checklist engine MVP | Batch 03, Batch 04, Batch 06, Batch 07 | Ready |
| 9 | `batch-09-vault-mod-0012.md` | MOD-0012 | W1 | Implement thin secrets/config vault MVP | Batch 03, Batch 04, Batch 01 | Ready |
| 10 | `batch-10-api-gateway-mod-0032.md` | MOD-0032 | W1 | Document deferred gateway posture or add future-ready thin placeholder only | Batch 02, Batch 03, Batch 04, Batch 09 | Deferred |
| 11 | `batch-11-event-bus-mod-0035.md` | MOD-0035 | W2 (W1 if event-first) | Stabilize lightweight internal event seam only | Batch 02 | Ready when event-first seam is needed |
| 12 | `batch-12-integration-monitoring-mod-0037.md` | MOD-0037 | W2/W3 | Document deferred monitoring/reconciliation posture only | Batch 02 | Deferred |
| 13 | `batch-13-logging-monitoring-mod-0041.md` | MOD-0041 | W2/W3 | Stabilize lightweight telemetry seam only | Batch 02 | Ready when telemetry normalization is needed |
| 14 | `batch-14-alerting-runbooks-mod-0042.md` | MOD-0042 | W2/W3 | Document deferred alerting/runbook posture only | Batch 13 | Deferred |

## Execution policy
- Execute one batch at a time.
- Respect the dependency chain before activating the next batch.
- Reconcile changed files, targeted verification, and contract impact at the end of every batch.
- Treat batches marked blocked or deferred as inactive until the decision log changes.
