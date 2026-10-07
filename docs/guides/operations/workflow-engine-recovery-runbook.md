# Approval engine recovery (MOD-0023) — runbook

What the approval engine does with work it left half-done, and what an operator does about it. Covers BL-549 (a
generic start that crashed after its instance), BL-539 (a trusted start that never completed) and BL-540 (1) (approvals
and reviews left live by deleted tasks or replaced review rounds). WP: WP-WF-ENGINE-RECOVERY-01.

**No value appears in this document** — only setting names, codes and where they live.

## What the engine does on its own

| Case | What happens | Where |
|---|---|---|
| A generic start crashed after writing its instance (no task, snapshot or start log) | The **same key with the same facts, by the same person** completes it and answers what the first call would have. Different facts or another person: 409 `WORKFLOW_INSTANCE_START_CONFLICT`. Two replays at once write one task; a first call that loses its claim to a replay (a double submit) answers as the replay does. | `StartWorkflowInstanceHandler` |
| The task is written | Only **together with the claim**, in one Platform transaction: a start that stops between them (a paused pod, a crash) leaves the instance exactly as it was — never a Cancelled instance with an open task. Without transactions the start answers 503 (replay the key). | same |
| A start written before BL-549 (no ids, no fingerprint on the instance) | Completed the same way; its first replay fixes the ids. Only its definition and object are compared (the facts it stored). A closed one that never wrote its start log answers 200 as before. | same |
| A task approval or review finds a half start it cannot complete (the manager, reviewer or due date was edited, or somebody else submits) | The half start is **closed** — Cancelled, reason `WORKFLOW_START_SUPERSEDED`, a log line, the completion event, an audit row under the person — and a **new round** starts with the facts as they are now. Once: a second refusal is reported, never looped on. Never an instance with any task. | `TaskApprovalService`, `TaskReviewService` → `SupersedeWorkflowHalfStartCommand` |
| …and nobody replays it | The recovery job closes it once it has not moved for the age below: Cancelled, reason `WORKFLOW_START_ABANDONED`, a transition log line, the completion event, an audit row. An instance with **any** task is never touched. | `WorkflowStartRecoverySweepJob` → `CloseAbandonedWorkflowStartCommand` |
| A trusted start (MDM) stopped short of Completed | Its consumer's replay completes it (checkpoints). If nobody does, the job closes it after the age: Cancelled with `WORKFLOW_START_ABANDONED`, its start task withdrawn if one was written, a log line, an audit row. | same |
| A trusted reservation **the recovery closed** (`WORKFLOW_START_ABANDONED`) is looked up | `start-result` answers it **exactly as a key the engine never saw**: 404 `NOT_FOUND_NON_LEAKAGE`, same body. A replay of the start gets the same answer and is never resumed. Only this close: a reservation the engine closed because nobody but the maker may approve it keeps its 409 `WORKFLOW_START_NOT_COMPLETED`. | `GetTrustedWorkflowStartResultHandler`, `WorkflowInstanceStartCoordinator` |

A task approval or review that was waiting on a closed generic start starts a **new round** the next time it is asked
for (the key names the closed round) — nothing to do by hand.

**How MDM reads the 404** (measured in MDM's code, read only): its Platform client maps HTTP 404 to `NotFound`. A Global
Product correction that finds no start inside its settle window (`StartSettleSeconds`) tries again; past it, it ends the
request and frees the product (`StartNotReceived`). An LSKU identity start moves to waiting for the maker's replay and
releases its lease.

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
| `Workflow:StartRecovery:CountWhileSweepIsOff` | Default **true**: while the job is off, a read-only count still feeds the gauges (it closes nothing). `false` stops it on that replica. |
| `Workflow:StartRecovery:CountIntervalMinutes` | How often that count runs. Default **15**, held between 5 and 1440. The first count comes one interval after start. |
| `Workflow:StartRecovery:ScanLimitPerTenant` | The most unmoved Active instances one tenant's count looks at (every approval waiting longer than the age is one). Default 5 000, held between 100 and 100 000. Past it the counts are "at least" (`count_capped` = 1). |
| `Workflow:StartRecovery:DeferredWarnAfterCycles` | After this many cycles in a row in which the same close could not be written (503 — no transaction), it is named once at Warning. Default 5. |

**Rule: the age must be longer than every consumer's wait window.** A consumer that started a trusted workflow waits
before it looks the key up again; the reservation may be closed only once it has stopped waiting. MDM's window is
`StartSettleSeconds` (default 120 seconds). Raising a consumer's window above the age breaks this rule — raise the age
first.

⚠ MDM's own validation accepts `StartSettleSeconds` up to **3600 seconds (60 minutes)** in every workflow worker section
(for example `GlobalProductCorrectionWorkflowWorker`, `GskuCorrectionWorkflowWorker`). That is longer than the default
age (30 minutes) and far longer than the floor (15 minutes): MDM does not stop an operator from breaking this rule.
Before any MDM window is set above 25 minutes, set `Workflow:StartRecovery:AbandonAfterMinutes` to at least that window
plus 5 minutes. Checked on each change: compare every `*WorkflowWorker:StartSettleSeconds` in MDM's environment with the
age here.

## What to watch

The job writes one line per cycle, `workflow.start_recovery.cycle` (`Mode=close` or `Mode=count-only`; tenants, abandoned,
closed, raced, deferred, stuck, incomplete trusted, failed tenants, capped tenants). An instance it could not close is
named once (`workflow.start_recovery.stuck`, Error) and quietly after (`still_stuck`). A close that lost its race to a
replay (`raced`) or could not be written now (`deferred`) is not stuck; a deferral that repeats for
`DeferredWarnAfterCycles` cycles is named once at Warning (`deferred_persistent`: check that Platform's Mongo runs
transactions). The same counts are gauges on the meter `Diten.Platform.WorkflowStartRecovery`:

- `platform.workflow.start_recovery.abandoned`
- `platform.workflow.start_recovery.stuck`
- `platform.workflow.start_recovery.incomplete_trusted`
- `platform.workflow.start_recovery.count_capped` — 1: the tenant's count stopped at `ScanLimitPerTenant`; the numbers above
  are "at least".

**The gauges fill while the job is off.** The count-only cycle (above) feeds them every `CountIntervalMinutes`. A tenant the
last cycle did not count drops out, and a value older than 25 hours is not published (the counting stopped).

**Cost, per replica.** There is no single-replica coordination: every Platform replica runs its own count (MDM's GP
recovery counter has the same shape). The cost is one bounded count per tenant per interval **times the number of
replicas**. On a fleet, leave `CountWhileSweepIsOff` true on one replica and set it to false on the others.

## A consumer cancels a trusted start that did not complete

Today's contract has no "cancel by key". A consumer (MDM) that wants to withdraw a start whose `start-result` answers 409
`WORKFLOW_START_NOT_COMPLETED` (no ids yet) does it in three calls:

1. **Replay the start** with the same key and facts — the engine completes it and answers the ids.
2. **`cancel-preflight`** with those ids — answers the versions.
3. **`cancel`** with the versions — the instance is Cancelled; its terminal evidence reads `CLOSED_WITHOUT_DECISION`.

**The window:** between step 1 and step 3 the task is live — listed to its candidates in the Task Center and decidable. A
candidate who decides in that window wins; the cancel then answers a conflict. A cancel by key that never opens the task
is backlog BL-560 (a Platform endpoint and the MDM link, together).

## The orphan recovery (BL-540 (1)) — a one-shot command, run by hand, on purpose

There is **no HTTP endpoint and no permission key**: the recovery is a command the operator runs on the Platform server,
against the Platform API host. The right to run it is access to that server and its environment (SAP: an administrator's
background report; Oracle: an ESS job).

The host reads its own configuration — `appsettings.json`, `appsettings.<environment>.json` and the environment
variables, among them `MongoDbSettings__ConnectionString`, `MongoDbSettings__DatabaseName` and `ASPNETCORE_ENVIRONMENT`.
In Production the host's required secrets are read from the **process environment only**: `MongoDbSettings__ConnectionString`,
`JwtSettings__Secret`, `AuthService__InternalApiKey`, `ModuleRegistrationCredentials__Mdm__ActiveSecret`. Its services are
checked when they are built: a missing registration fails before anything is read (exit 1).
Their **values** come from the server's secret store and are never typed on the command line, written here, or printed by
the command.

From the API host's folder, first the **dry run** (one tenant per run; lists and counts, closes nothing):

```bash
dotnet Diten.Platform.API.dll --run-workflow-orphan-recovery --tenant <tenant-id>
```

then, after reading the list, **apply** (N = the dry run's count):

```bash
dotnet Diten.Platform.API.dll --run-workflow-orphan-recovery --tenant <tenant-id> --apply --expect N
```

- An orphan is an approval or review whose task is gone (`TASK_DELETED`), or a review round a later round replaced
  (`REVIEW_ROUND_REPLACED`). The latest round of a task is never one. Upward requests are not looked at. Every open
  instance of the tenant is scanned, page by page.
- `--apply` cancels each orphan's open approval task through the engine's own cancel (log, completion event, audit row),
  actor `system:workflow-orphan-recovery`, reason `WORKFLOW_ORPHAN_CLOSED` — and **only the orphans the dry run inside the
  same run counted** (the ones `--expect` checked); one that appears meanwhile waits for the next run.
- An orphan with **no open task** has nothing to cancel: it is listed apart (`NO OPEN TASK`) and is not a failure. One with
  no task at all is the abandoned-start recovery's to close (its age floor); one whose tasks are all decided but which is
  still live is for a person to look at.
- Each pass writes an audit row (actor System; `Tool = workflow-orphans`, `Host = <machine name>`; no personal data).
- **Exit codes:** 0 done · 1 failure (only the exception's type is printed — never a message, never the connection; an
  `--apply` run may have closed some orphans before it stopped: run the dry run again to see what is left) · 2 usage
  (`--tenant` is required) · 4 some orphans could not be closed (listed) · 5 the count was not the expected one (nothing
  closed).
- Running it against shared or live data is the owner's decision, not a routine.
