---
module: MOD-0192
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# Q484 — the Capacity middleware decision, with a measured change the earlier framing did not know

    item      : Q484 (v9.6 §5 item 3) · owner: Ali (product owner) · CT role: measurement + recommendation
    author    : CT (gmg-10)
    measured  : 2026-10-17 on feature/mvp6-logistics at HEAD
    status    : OWNER DECISION — OPEN. No decision is invented here (standing instruction on Q484/Q496).
                This package carries the measurement, CT's standing recommendation, and a ready-to-file
                ruling record; the ruling itself is the owner's.

## 0. What changed since the Q484 report — read this first

The Q484 report (`docs/records/audits/2026-10/mvp6-q484-capacity-branch-01/REPORT.md`, 2026-10-12) and the
pack line `MOD-0192:199` both describe the Capacity context-middleware branch as **measured and then
reverted** — *"it is restored to `c846af73…`"*. That is **no longer the state of the tree.**

Measured today, committed and clean on `feature/mvp6-logistics`:

| | value |
|---|---|
| `Program.cs` sha256 now | `0d66f096247b644c…` — the report's **"after" (branch-in)** hash, not the reverted `c846af73…` |
| the branch | **live** at `Program.cs:152` (`app.UseWhen(CapacityContextMiddleware.IsCapacityPath, …)`) plus the catch-all exclusion at `:155` and the `using` at `:8` |
| how it got there | commit `956d6d097` ("0190/0192 bounded slice"), merged into this branch **this session** via `681bb44d3` |
| pack hash pins | **unchanged** — `MOD-0192:273` still `50c48a2b…` and `MOD-0190:261` still `a2a216be…` (the Q292 gated target); CT's re-pin refusal held |

So there is now a **three-way mismatch** the owner is deciding against, not the two-way one the report
framed:

- the pack's Q292 **target** hash: `50c48a2b…` (MOD-0192) / `a2a216be…` (MOD-0190)
- the pack `:199` **narrative**: "reverted, restored to `c846af73…`"
- the **actual tree**: `0d66f096…`, branch live

The code half of the re-pin the owner released on 2026-10-12 effectively landed through the bounded-slice
merge; only the pack-hash re-pin (the half CT refused) did not. "Do nothing" is therefore **Option 1 by
default today**, not the neutral state. This is a critical change to the decision's inputs, so it is
reported rather than assumed away.

## 1. The decision, unchanged in substance

The owner released "the Capacity branch + re-pinning the two hashes" (2026-10-12). CT **refused the
re-pin** and returned it, because:

- Q292 (owner-approved, 2026-10-03, decision 5 of 7) keeps `MOD-0190 §22` / `MOD-0192 §22` accepted
  composition as a **correct but GATED target** — uptake blocked until MOD-0188 exists, DEMAND v1.1 carries
  version+checksum, and a SUPPLY-CONSTRAINTS contract for 0192 lands;
- Q298 executed that gate *"without withdrawing the accepted composition or altering its hash."*

Re-pinning now would overwrite an owner decision with a newer one taken without that decision on the table.
That refusal still stands and nothing here reverses it.

## 2. The three options (re-framed against the live branch)

| | option | effect now that the branch is live | touches |
|---|---|---|---|
| 1 | Keep the branch in `Program.cs` as a recorded **exception** to Q292; hash left unchanged | ratifies today's de facto state; a new exception to a gate the owner's reading says **does** bind the production host | pack note only (record the exception); `:199/:200` close |
| **2 ✅ (CT recommends)** | Move the branch out of `Program.cs` into the **test host** (`SandopCapacityHttpHost`, e.g. an `IStartupFilter`) | gated production composition untouched **by construction**; the owner already read Q292 on 2026-10-11 as **not** binding a test host; defect, sabotage proof and rewritten guard all transfer unchanged | `Program.cs` (−branch), test host (+seam); `:199/:200` close on a test-only seam |
| 3 | Revert the branch; `:199/:200` stay open until the gate opens | now an **active revert** of committed code, not "stay as-is" | `Program.cs` (−branch) |

**CT's recommendation: Option 2**, exactly as in the report — and it is stronger now. With the branch live
in `Program.cs`, Option 1 is not a light continuation; it is a standing exception to a shut gate in the
very host the owner's reading says the gate binds. Option 2 puts the fix where the owner has **already**
read the gate as not reaching, and leaves `50c48a2b…`/`a2a216be…` true by construction. The recommendation
is not the decision.

## 3. Measured in the module's favour (so the owner can weigh it)

- `CompositionRootGuardTests` builds the **production** container with the branch in place: **3/3 pass**
  (`mvp6-q484-capacity-branch-01/evidence/composition-root-guard.log`). The branch does not change what the
  production host composes — the substance of the Q292 concern — however the wording is read.
- In production the branch is **inert**: Capacity persistence is unregistered, so the Q381 filter drops
  `CapacityPlansController`; `CapacityContextMiddleware.InvokeAsync` returns `next()` for an endpoint with
  no `CapacityPermissionAttribute`. Its only live effect is in the test host, which composes the module.
- The harmful composition items (Q283: `CapacityEvaluationExecutor` referenced nowhere → evaluations would
  202 and never complete) are **items 1–3 of the five**, none of which this branch touched. The branch is
  **item 4 of 5**; item 5 (model-error adapter) is still absent.

## 4. Why this package carries no code patch (unlike BL-650 / Q498)

Two reasons, both deliberate:

1. **Scope.** The handoff pins Q484 as *"permitted scope: none until decided — this is a decision, not
   work"* with exit criterion *an owner decision record*. Shipping an Option-2 implementation now would
   pre-empt that.
2. **Unverifiable here.** Option 2 moves code into `SandopCapacityHttpHost`
   (`services/Diten.SupplyChainService/tests/…`, a protected path) and out of `Program.cs` (integration-owner
   surface, protected). An `IStartupFilter` seam cannot be proven correct without building that test
   project, which the protected-path rules and the earlier build-safety both counsel against doing
   speculatively. A patch I cannot verify is not one I will label ready.

So the deliverable is the **ruling record, pre-drafted** (`owner-decision-record.DRAFT.md`), ready to move
into `docs/records/decisions/2026-10/` once the owner rules — the exact exit artifact the process names.
When the owner picks Option 2 (or 1, or 3), the implementation is a short, well-scoped integration-owner
job the report already measured.

## 5. Also for the owner's awareness

`MOD-0192:199`'s narrative is stale (it says reverted; the tree has the branch). Per K4 a pack line is not
silently corrected — the owner's Q484 ruling is what should drive that pack update, and it is the same
surface as owner-queue item 4 (MOD-0192 `:199`/`:200`). Flag, not fix.

## Files

- `owner-decision-record.DRAFT.md` — the ruling record in house format, options pre-filled, ruling blank.
- `DECISION-MEMO.md` — this memo.
