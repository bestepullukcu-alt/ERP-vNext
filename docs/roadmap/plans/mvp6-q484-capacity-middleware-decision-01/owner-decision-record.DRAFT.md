# Q484 — Capacity context middleware: production branch vs test-host seam (MVP-6, MOD-0190/0192)

> **DRAFT FOR OWNER RULING — no decision is made in this file.** CT prepared it so the ruling is a
> one-page read-and-sign. To enact: fill `owner`, `status`, and the **Ruling** section, then move this
> file to `docs/records/decisions/2026-10/mvp6-q484-capacity-middleware-owner-decision-01.md`. Evidence
> and the full argument are in `docs/roadmap/plans/mvp6-q484-capacity-middleware-decision-01/DECISION-MEMO.md`.

- owner: __________ (product owner) — decision relayed by the Control Tower, 2026-10-__
- status: __________ (ACCEPTED option N / REJECTED / DEFERRED)
- module: MOD-0192 Capacity Planning + MOD-0190 S&OP · service `Diten.SupplyChainService` (5066) · packs
  `execution/domains/supply-chain-execution/module-packs/MOD-0192-capacity-planning.md` (`:199`, `:273`)
  and `MOD-0190-sop-workflow-signoffs.md` (`:261`)
- code: `feature/mvp6-logistics` — the Capacity branch is **currently live** at `Program.cs:152`
  (sha256 `0d66f096247b644c…`), merged via `681bb44d3` (commit `956d6d097`)
- evidence: `docs/records/audits/2026-10/mvp6-q484-capacity-branch-01/` (REPORT.md, sabotage proof,
  `evidence/composition-root-guard.log` 3/3)

## What is being decided

`CapacityContextMiddleware` must run for Capacity requests (without it, an authenticated Capacity request
answered 500 with a zero correlation id — measured). The fix is one pipeline branch. The only question is
**where it lives**, because `Program.cs` is the integration-owner surface whose composition hash Q292
(owner, 2026-10-03) decided to keep as a **gated target** (`50c48a2b…` / `a2a216be…`), uptake blocked until
MOD-0188 + DEMAND v1.1 + a SUPPLY-CONSTRAINTS contract.

Since the Q484 report was written the branch has **landed in `Program.cs`** (via the bounded-slice merge),
while the pack hashes were left at the Q292 target. So "do nothing" currently equals Option 1.

## Options (detail and trade-offs in DECISION-MEMO.md §2)

- **Option 1 — keep the branch in `Program.cs` as a recorded exception to Q292.** Ratifies today's state;
  a new exception to a gate the owner's own 2026-10-11 reading says binds the production host.
- **Option 2 — move the branch into the test host (`SandopCapacityHttpHost`), out of `Program.cs`.**
  CT's recommendation. The gated production hash stays true by construction; the owner read Q292 on
  2026-10-11 as not binding a test host; the defect, sabotage proof and rewritten guard transfer unchanged.
- **Option 3 — revert the branch.** `:199`/`:200` stay open until the gate opens; now an active revert of
  committed code.

Measured for weighing: the production container is intact with the branch in (`CompositionRootGuardTests`
3/3); the branch is inert in production and only live in the test host; the harmful composition items (1–3
of 5, incl. the never-completing executor) were **not** touched — this is item 4 of 5.

## Ruling

> _Owner: state the option chosen and any condition. If Option 2 or 3, this authorises the
> integration-owner edit to `Program.cs`; if Option 2, also the test-host seam in
> `services/Diten.SupplyChainService/tests/…/SandopCapacityHttpHost.cs`. If Option 1, authorise the
> pack-note exception (hashes stay at the Q292 target). Note whether `MOD-0192:199`'s stale "reverted"
> narrative is corrected under this ruling._

________________________________________________________________

- re-pin of `MOD-0192:273` / `MOD-0190:261` composition hashes: __________ (CT refused this on 2026-10-12;
  only overturned by an explicit owner instruction that supersedes Q292)
