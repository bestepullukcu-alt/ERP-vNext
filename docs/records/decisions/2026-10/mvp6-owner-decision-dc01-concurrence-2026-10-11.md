# Owner decision — DC-01 concurrence, and the standing of CT ruling Q437

- date: 2026-10-11
- decided by: project owner, via the CT question tool
- recorded by: Control Tower
- supersedes in part: `docs/records/decisions/2026-10/mvp6-ct-ruling-dc01-dc05-concurrence-01.md` (Q437, 2026-10-05)
- ledger: Q437 (partially superseded), DC-01 (closed)

## The conflict this resolves

On 2026-09-27 the owner recorded, verbatim:

> **OD-DC** Q19 DC-01…DC-05: the owner holds all five decision-owner roles for MVP6 (Enterprise/domain;
> MOD-0140 Supplier + eligibility; Platform auth/security identity; Metric Registry; Risk Register).
> DC-01…05 are APPROVED; the AWAITING-CT note is closed.
> — `docs/records/audits/2026-09/mvp6-ct-verdicts-q150-q143-q152-uc01-2026-09-27.md:48`

On 2026-10-05, CT ruled that this did not constitute concurrence for any of the five, on three grounds.
That ruling is why MOD-0147 and MOD-0148 have not moved since. It ends: *"The owner may overrule this ruling."*

## Decision

**Option A3 — the ruling is overruled in part, and DC-01 is approved as written.**

### A3: which of CT's three grounds survive

| CT ground | standing |
|---|---|
| 1. "another owner's approval is not concurrence" (`OWNER-DECISION-PACK.md:5`) | **Overruled.** The pack names *roles*, not people. Who holds a role is an organizational fact the owner states; CT set the owner's own 2026-09-27 statement aside in favour of a planning table's earlier assumption. That was a CT error. The five roles are the owner's, and `:5` cannot be read against the owner. |
| 2. "each item requires a new producer seam, not a signature" | **Upheld, and it is the real constraint.** It is also expressly **not** true of DC-01, whose pack entry reads `New producer seam required: none`. For DC-02…DC-05 a concurrence authorizes a build; it does not perform one. Each keeps its own work package. |
| 3. "the frozen contract does not cover these five" | **Upheld as scope, irrelevant to authority.** `supplier.openapi.yaml` (`c8badff96`, FROZEN 2026-09-15) closed the identity gate only. |

CT's recommendation — *"pursue DC-01 first, and alone"* — stands and is adopted.

### DC-01: `APPROVE DC-01 as written`

The approved text, from `OWNER-DECISION-PACK.md:15`, unchanged:

> MOD-0147 Supplier Performance & Risk and MOD-0148 Supplier Portal are durably placed in
> `supply-chain-execution` and implemented, when separately authorized, by `Diten.SupplyChainService`.
> MOD-0140 remains the Supplier master owner. This concurrence assigns no runtime, contract, gateway or
> permission ownership to the two modules.

Acceptance binding: AC-01. Artifact binding and split-package seal as recorded in the pack.

## What this authorizes, exactly

One governance-diff **preparation** lane may now write an exact *proposed* alignment of `domain-config`,
DCP-009 and the module registry. Per the pack's own "After this decision" clause, three things remain
separate authorities and are **not** granted here:

- applying that diff,
- changing either pack's status from `draft`,
- any runtime or DEV work.

## What is still open

DC-02…DC-05 are unaffected by this record and remain open, each carrying a producer-seam obligation.
`DEPENDENCY-SEQUENCE.md` keeps them in pairs: DC-02 with DC-03, DC-04 with DC-05.

MOD-0147 and MOD-0148 remain `draft` and are four waves from code. This decision closes Wave 1's
independent item; it does not shorten Waves 2 through 5.

## Correction CT owes the record

CT (this role, in an earlier session) wrote Q437 and is the party that held these two modules for six days
on a ground now overruled. The ruling's other two grounds were sound and are kept. Q437 is marked
**partially superseded** rather than withdrawn.
