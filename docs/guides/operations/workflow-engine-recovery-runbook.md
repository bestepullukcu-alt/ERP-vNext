# Approval engine recovery (MOD-0023) — runbook

What the approval engine does with work it left half-done, and what an operator does about it. Covers BL-549 (a
generic start that crashed after its instance), BL-539 (a trusted start that never completed) and BL-540 (1) (approvals
and reviews left live by deleted tasks or replaced review rounds). WP: WP-WF-ENGINE-RECOVERY-01.

**No value appears in this document** — only setting names, codes and where they live.

## What the engine does on its own

| Case | What happens | Where |
|---|---|---|
| A generic start crashed after writing its instance (no task, snapshot or start log) | The **same key with the same facts** completes it and answers what the first call would have. Different facts: 409 `WORKFLOW_INSTANCE_START_CONFLICT`. Two replays at once write one task. | `StartWorkflowInstanceHandler` |
| …and nobody replays it | The recovery job closes it once it has not moved for the age below: Cancelled, reason `WORKFLOW_START_ABANDONED`, a transition log line, the completion event, an audit row. An instance with **any** task is never touched. | `WorkflowStartRecoverySweepJob` → `CloseAbandonedWorkflowStartCommand` |
| A trusted start (MDM) stopped short of Completed | Its consumer's replay completes it (checkpoints). If nobody does, the job closes it after the age: Cancelled with `WORKFLOW_START_ABANDONED`, its start task withdrawn if one was written, a log line, an audit row. | same |
| A closed trusted reservation is looked up | `start-result` answers it **exactly as a key the engine never saw**: 404 `NOT_FOUND_NON_LEAKAGE`, same body. A replay of the start gets the same answer and is never resumed. | `GetTrustedWorkflowStartResultHandler`, `WorkflowInstanceStartCoordinator` |

A task approval or review that was waiting on a closed generic start starts a **new round** the next time it is asked
for (the key names the closed round) — nothing to do by hand.

### The race between the job and a replay

Only one of them wins. The close holds only while the reservation is still Pending, at the checkpoint and version it read;
a replay completes only while the reservation is still Pending. The loser gets a named answer: the close 409
`WORKFLOW_TRANSITION_CONFLICT` (nothing written), the replay the not-found answer above. A completed reservation is never
closed; a closed one is never completed. A start task somebody has moved (it is no longer waiting) is never withdrawn — the
close loses and the reservation is left to its completion.

## Settings

| Setting | Meaning |
|---|---|
| `BackgroundJobs:EnabledJobs:Diten.Platform.MOD-0023.WorkflowStartRecoverySweepJob` | **Off unless set to true** (and `BackgroundJobs:Enabled` and `BackgroundJobs:RegisterStandardJobs` are on too). The job runs every 10 minutes where it is on. Where it runs, against which data, is the operator's decision. |
| `Workflow:StartRecovery:AbandonAfterMinutes` | How long a start may stand incomplete before the job closes it. Default 30, **minimum 15** (a smaller value is raised to 15). |
| `Workflow:StartRecovery:MaxClosesPerTenant` | How many the job closes per tenant per cycle; the rest wait for the next. |

**Rule: the age must be longer than every consumer's wait window.** A consumer that started a trusted workflow waits
before it looks the key up again; the reservation may be closed only once it has stopped waiting. MDM's window is
`StartSettleSeconds` (default 120 seconds). Raising a consumer's window above the age breaks this rule — raise the age
first.

## What to watch

The job writes one line per cycle, `workflow.start_recovery.cycle` (tenants, abandoned, closed, stuck, incomplete trusted,
failed tenants). An instance it could not close is named once (`workflow.start_recovery.stuck`, Error) and quietly after
(`still_stuck`). The same counts are gauges on the meter `Diten.Platform.WorkflowStartRecovery`:

- `platform.workflow.start_recovery.abandoned`
- `platform.workflow.start_recovery.stuck`
- `platform.workflow.start_recovery.incomplete_trusted`

## A consumer cancels a trusted start that did not complete

Today's contract has no "cancel by key". A consumer (MDM) that wants to withdraw a start whose `start-result` answers 409
`WORKFLOW_START_NOT_COMPLETED` (no ids yet) does it in three calls:

1. **Replay the start** with the same key and facts — the engine completes it and answers the ids.
2. **`cancel-preflight`** with those ids — answers the versions.
3. **`cancel`** with the versions — the instance is Cancelled; its terminal evidence reads `CLOSED_WITHOUT_DECISION`.

**The window:** between step 1 and step 3 the task is live — listed to its candidates in the Task Center and decidable. A
candidate who decides in that window wins; the cancel then answers a conflict. A cancel by key that never opens the task
is backlog BL-560 (a Platform endpoint and the MDM link, together).

## The orphan recovery (BL-540 (1)) — run by hand, on purpose

`POST api/v1/workflow/operator/orphans`, permission `platform.workflow.orphans.recover`. **That permission cannot be
given to a person's role** (Auth refuses it); only a service client may hold it, and granting it is the owner's step.

- **Dry run by default:** body `{}` or `{"apply": false}` counts and lists the orphans of the caller's tenant (instance id,
  object, age, reason — no personal data) and writes nothing.
- **`{"apply": true}`** cancels each orphan's open approval task through the engine's own cancel (log, completion event,
  audit row), actor `system:workflow-orphan-recovery`, reason `WORKFLOW_ORPHAN_CLOSED`.
- An orphan is an approval or review whose task is gone (`TASK_DELETED`), or a review round a later round replaced
  (`REVIEW_ROUND_REPLACED`). The latest round of a task is never one. Upward requests are not looked at.

Run the dry run first and read the list. Running it against shared or live data is the owner's decision, not a routine.
