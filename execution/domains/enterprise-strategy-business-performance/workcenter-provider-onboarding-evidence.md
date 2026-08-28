# Enterprise Strategy WorkCenter Provider Onboarding Evidence

Status: **BLOCKED / NON-EXECUTABLE / DEFAULT-OFF**

Measured: 2026-08-28

Authority: DCP-004 provider onboarding; MOD-0352 owns Strategy alignment only.

This ledger is the code-before-code handoff required by
`execution/portfolio/delivery-capability-packs/DCP-004-provider-onboarding-note.md`. It records the current
repository truth and does not turn the existing browser fixtures into runtime authority.

## Eligibility decision

The only bounded candidate is an Objective periodic-review projection. It is **not currently eligible** for
publication because `ObjectiveDto`, the aggregate base and its repository records have no authoritative
`TenantId`. `EsbpSecurityPerimeterMiddleware` therefore returns `TENANT_SAFETY_NOT_READY` (503) after exact JWT,
tenant-header and permission validation. Producing a WorkCenter row would bypass that fail-closed boundary and
could disclose another tenant's Strategy record.

The other five fixture ideas are not eligible:

| Fixture idea | Disposition | Reason |
|---|---|---|
| Objective approval | Blocked | Approval lifecycle is MOD-0023-owned; no approved ES action endpoint exists. |
| Objective periodic review | Blocked | Real fields exist, but authoritative tenant ownership is absent. |
| Demand review | Out of scope | Demand lifecycle SoR is MOD-0117; legacy free-text identity is not proof. |
| Decomposition task | Blocked | Task-like fields require the separate Gate-2 ownership decision. |
| Blocked decomposition task | Blocked | Same Gate-2 ownership issue; fixture state is not authoritative data. |

## Exact current projection mapping

The current `WorkItemProjectionDto` has 50 fields. Candidate source below means the real `ObjectiveDto`; no row
may be emitted until tenant ownership is migrated and accepted. Omitted means the JSON member is absent, not
`null`, zero, `false` or an invented default.

| WorkCenter field | Candidate module value / projection | When source is absent |
|---|---|---|
| `FixtureKind` | constant `workItem` | Reject projection if contract constant cannot be emitted. |
| `Id` | `ObjectiveDto.Id` plus a stable review-occurrence identity still requiring an approved rule | Reject; never use title or list position as identity. |
| `WorkIntent` | `review` | Reject if this is not a periodic-review occurrence. |
| `AssignmentMode` | No authoritative assignment source | Reject; never infer `direct`. |
| `OwnershipState` | No authoritative assignment source | Reject. |
| `AdmissionState` | No authoritative admission source | Reject. |
| `NormalizedStatus` | Deterministic mapping from authoritative review occurrence; mapping not yet approved | Reject; do not reuse objective lifecycle as review lifecycle. |
| `TaskLifecycle` | `notApplicable` for a review | Emit exact `notApplicable`. |
| `ExecutionState` | `notApplicable` for a review | Emit exact `notApplicable`. |
| `TimerState` | `notApplicable` | Emit exact `notApplicable`. |
| `SystemState` | `fresh` only after tenant-safe authoritative read | Reject while tenant safety is unavailable. |
| `ActionDepth` | `inline` only if an approved action exists | Use `notApplicable` with no actions. |
| `Title` | display label from `ObjectiveDto.Name` | Reject blank title. |
| `NativeStatus` | exact review occurrence status; not `ObjectiveDto.Status` | Reject until occurrence model exists. |
| `Source` | provider `enterprise-strategy`, object type `StrategicObjective`, exact objective identity | Reject missing/mismatched identity. |
| `LifecycleOwner` | `enterprise-strategy` only for a Strategy-owned review occurrence | Reject until ownership decision is executable. |
| `WorkItemCapabilities` | empty initially | Emit empty; never declare a container without data. |
| `Actions` | empty until real endpoints and permissions exist | Emit empty; never copy fixture-only actions. |
| `Concurrency` | `ObjectiveDto.Version` is insufficient for a separate review occurrence | Reject actionable projection; read-only row requires an approved occurrence fence. |
| `WaitingContext` | no source | Omit. |
| `Escalation` | no source | Omit. |
| `DueAt` | `ObjectiveDto.NextReviewDate` after tenant-safe ownership | Omit when no review date; never substitute end date. |
| `PrimaryActionCode` | no approved action | Omit. |
| `OverflowActionCodes` | no approved action | Omit. |
| `Assignee` | typed `ReviewOwner` reference is not yet resolved | Omit; never render free text as verified person. |
| `Requester` | no source | Omit. |
| `Checklist` | no source | Omit and do not declare capability. |
| `Subtasks` | no source | Omit and do not declare capability. |
| `ParentTaskItemId` | not a subtask | Omit. |
| `Gates` | MOD-0023 decision gate is not runtime-ready | Omit; approval-required rows remain absent. |
| `Priority` | `ObjectiveDto.Priority` only after allowed-value reconciliation | Omit when unrecognized; never coerce. |
| `Dependencies` | `ObjectiveDto.DependencyLinks` needs a separate typed WorkCenter mapping | Omit and do not declare capability. |
| `BlockedState` | no authoritative review blocker source | Omit. |
| `Activity` | no source | Omit and do not declare capability. |
| `PlannedDate` | no source | Omit. |
| `TaskType` | review is not a task | Omit. |
| `Pool` | no group-queue assignment | Omit. |
| `SlaState` | no working-calendar decision source | Omit; never calculate in the browser or from calendar days. |
| `BusinessContext` | objective name, review cadence and next-review date after tenant safety | Omit until the capability/schema contract is approved. |
| `ClosedAt` | no review-occurrence close instant | Omit. |
| `Summary` | display label from `ObjectiveDto.Statement` | Omit blank/whitespace. |
| `StartAt` | no source | Omit. |
| `EstimateHours` | no source | Omit. |
| `SpentHours` | no source | Omit. |
| `Tags` | no approved objective tag source | Omit. |
| `Personal` | Platform viewer-owned overlay, not ES business data | Omit from remote provider response. |
| `Watchers` | no source | Omit. |
| `DelegationAllowed` | no policy source | Omit; null means unexpressed, not false. |
| `Notifications` | no source | Omit. |
| `ReminderLeadDays` | no source | Omit. |

## Capability, action and permission ledgers

Capabilities: **none** until their exact containers have authoritative data.

Actions: **none**. `approve`, `reject`, `signoff`, `requestInfo`, `return`, `start` and `reassign` in the legacy
fixture are not executable ES provider authority.

Required action permissions: **empty**. `strategy.objective.view` protects the future projection endpoint read;
it does not authorize a business transition.

## Hand projection and browser gate

No successful real hand projection can be produced without violating tenant isolation. The executable hand
result today is the existing fail-closed response:

```json
{"status":503,"code":"TENANT_SAFETY_NOT_READY","correlationId":"<request-trace>"}
```

Consequently provider configuration, frontend mock replacement, action dispatch and browser acceptance remain
blocked. After an explicitly authorized tenant schema/data migration, this table must be revalidated against one
real tenant-owned Objective review occurrence. Only then may the two DCP-004 endpoints be implemented, the
operator-owned provider row be prepared default-off, and old/new browser evidence be recorded. No legacy route
may be retired before that acceptance.
