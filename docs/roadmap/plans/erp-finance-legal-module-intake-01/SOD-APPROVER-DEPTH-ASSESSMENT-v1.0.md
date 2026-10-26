# SoD / approver depth — behavioural assessment

    created : 2026-10-11
    method  : static inspection of the DESTINATION at HEAD 2acce233b. No service was started,
              no request issued, no test executed. Every verdict below is "the code does/does
              not contain this control", never "this behaves correctly at runtime".
    why     : MOD-0126 Payments & Approvals is the only Treasury member whose gates are
              otherwise met, and approvals are its entire purpose. A file count cannot answer
              whether it can safely deliver that.

## First — the 12-file figure was a false signal, and I withdraw it

Earlier amendments reported "SoD / approver — 12 files" in the destination. Measured properly, **all
twelve are in `Diten.TalentEcosystemService`**: ReviewBoard, CandidateProfiles, ExitReferenceRecords,
TrustLevels. That is HR review-board governance that happens to use SoD vocabulary. **None of it is
a payments or workflow approval control.**

This is the third instance of the same measurement error in this intake — keyword matches in an
unrelated domain counted as capability. MOD-0003's "3 files" were HcmService employee validators;
"Billing" resolved by name rather than contract code; and now this. The file count was never
evidence, which is precisely the point of assessing behaviour instead.

**The real control lives in `Diten.Platform`'s Workflow engine — MOD-0023, which is MOD-0126's own
Blueprint gate.** So MOD-0126 would inherit it rather than implement it.

## The six questions

### Q1 · Can the requester approve their own payment? — **NO, refused server-side**

`WorkflowTaskTransitionSupport.cs:118`

    if (action == WorkflowTransitionAction.Approve && IsStarter(instance, actorId))
        return Fail("Submitter cannot approve their own workflow.", 409,
                    WorkflowReasonCodes.SodViolation, correlationId);

Three layers, all present:

1. **Server gate** on the Approve action, HTTP 409, reason code `SodViolation`. Not advisory.
2. **The delegation loophole is closed.** `:387` refuses delegating a task *to* the starter, with the
   reason recorded in the code: *"the approve gate refuses the starter (SOD_VIOLATION), so a task
   delegated TO the starter would sit with the one person who can never approve it"* (BL-491). That
   is a measured defect that was found and fixed, not a hypothetical.
3. **UI mirrors the server rule** — `WorkItemProjectionService.cs:271` disables the approve button
   with `SELF_APPROVAL_NOT_ALLOWED` rather than offering it and failing afterwards.

**One qualification.** `IsStarter` compares **user id** when `StartedByUserId` is present, and falls
back to comparing `instance.StartedBy` — an actor **name** — when it is null, which the code
documents as the legacy path for older instances. A name comparison is a weaker control than an id
comparison. For payments this matters: **MOD-0126 must only ever run against instances that carry
`StartedByUserId`**, and that should be an acceptance criterion rather than an assumption.

### Q2 · Are approvers resolved from an authoritative, tenant-scoped source? — **partly, and the mechanism is a snapshot**

The transition path refuses on `WorkflowAssignmentSnapshotNotFound` (`:92`, `:102`) before any actor
check, then on `WorkflowActorDenied` (`:111`) if the actor is not assigned. So assignment is resolved
from a **stored assignment snapshot**, not re-resolved from an authoritative directory at decision
time.

That is sound for determinism and it is explicitly fail-closed when the snapshot is missing. But a
snapshot is a point-in-time copy — **the same distinction that TP-001 T14 draws for LegalEntity
eligibility, where a stored snapshot is not current eligibility.** Whether the snapshot's source is
authoritative and tenant-scoped was **not established by this inspection**; it requires reading the
snapshot's producer, which I did not do.

### Q3 · Are eligibility and permissions rechecked when approval occurs? — **assignment is; permission recheck NOT ESTABLISHED**

Measured guard order before a transition is accepted:

    :64   NotFoundNonLeakage                  — task not found, no existence leak
    :73   WorkflowTaskInvalidState            — not in a transitionable state
    :83   NotFoundNonLeakage                  — instance not found
    :92   WorkflowAssignmentSnapshotNotFound  — fail-closed on missing snapshot
    :111  WorkflowActorDenied                 — actor not assigned to this task
    :118  SodViolation                        — starter cannot approve
    :223  WorkflowTransitionConflict          — concurrent transition

Assignment and SoD are rechecked at decision time. **A permission or RBAC recheck at the moment of
approval does not appear in this path** — authorization is presumably enforced at the controller via
`[HasPermission]`, which is a different thing from re-evaluating a revoked permission mid-flight.
**Not established**, and for payments it is the question that matters most after Q1.

### Q4 · Revocation, delegation expiry, policy change? — **UNVERIFIED BY THIS INSPECTION**

    DelegationExpiry      0 files
    FourEyes              0 files
    MakerChecker          0 files
    RequesterIsApprover   0 files

`Delegation` appears in 50 files, so delegation exists as a mechanism, and `DelegateWorkflowTaskValidator`
enforces that a delegate differs from the actor.

**Correction applied 2026-10-11:** v1.0 first read this as "NOT FOUND". Zero keyword matches do not
prove absence — the same error this document opens by criticising. Four keyword probes returned zero
and **50 delegation files were not read**, so the correct verdict is **unverified by this
inspection**. The behaviour after a revocation, an expired delegation or a changed policy is
undetermined, and for payments it must be evidenced before release rather than assumed either way.

### Q5 · Duplicate, concurrent and replayed decisions? — **YES, idempotency and conflict detection both present**

- **Replay:** every transition path opens with
  `_logRepository.GetByTaskActionIdempotencyKeyAsync(taskId, action, idempotencyKey)` and returns a
  built idempotent response on a hit — measured at `:55`, `:355`, `:451`, `:525`, i.e. on all four
  action paths, not just approve. The key is scoped **per task per action**, so the same key cannot
  replay across different actions.
- **Concurrency:** `WorkflowTransitionConflict` at `:223`.
- Chained tasks get a derived key — `$"{idempotencyKey}:next:{nextTask.Id}"` — so a follow-on task
  cannot collide with its parent's key.

This is the strongest of the six.

### Q6 · Durably audited, with safe failure when dependencies are unavailable? — **fail-closed is present; the audit write is NOT ESTABLISHED**

Fail-closed is explicit: `WorkflowTerminalWriteOutcome.TransactionUnavailable` → **HTTP 503**
`WorkflowTransactionUnavailable` (`:761`). A transition that cannot be written durably is refused
rather than reported as succeeded.

The transition **log** is written and read for idempotency, so decisions are persisted. But whether a
MOD-0021 **AuditEvent** is emitted for each decision — actor, tenant, object, operation, outcome,
and a `Failed` record for a refused decision — **was not established in this file.** AUD-001 requires
it, and the destination pack's §21 Audited Events cannot be written without it.

## Verdict for MOD-0126

| question | verdict |
|---|---|
| Q1 self-approval | **control present, server-side, layered** — with the legacy name-comparison caveat |
| Q2 approver source | **snapshot-based, fail-closed** — authority and tenant scoping of the snapshot's source not established |
| Q3 recheck at approval | **assignment yes, permission recheck not established** |
| Q4 revocation / expiry / policy change | **unverified by this inspection** (4 keyword probes zero; 50 delegation files unread) |
| Q5 duplicate / concurrent / replay | **present and thorough** |
| Q6 durable audit + safe failure | **fail-closed present; AuditEvent emission not established** |

**MOD-0126 does not need to build an approval engine — it inherits a real one from MOD-0023.** Two of
the six are solid, one is solid with a caveat, and three are unevidenced rather than absent.

**The three unevidenced ones are the acceptance criteria MOD-0126's pack should carry**, not reasons
to stop:

    AC  approval refuses the starter, proven against an instance carrying StartedByUserId
    AC  permission revoked between assignment and approval -> approval refused
    AC  expired or revoked delegation -> approval refused
    AC  each decision, including a refused one, emits an AuditEvent carrying actor, tenant,
        object, operation and outcome (AUD-001)

Those are exactly the behaviours this inspection could not confirm, which is what makes them worth
asserting as tests rather than assumptions.

## Limits of this assessment

Static inspection only, of one file family in one service. No service started, no request issued, no
test run, so **every "present" means the control exists in code, never that it holds at runtime**.
Not inspected: the assignment snapshot's producer; the controller layer's `[HasPermission]`
enforcement; MOD-0021's emission path; the 50 delegation files beyond the validator; and the source
project's own approval behaviour, which is its packet's §G and §H to disclose.
