---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# MOD-0147 / MOD-0148 (Supplier) readiness — draft, zero code, DEV lane blocked on DC-01…DC-05

    item      : Supplier Performance & Risk (0147) + Supplier Portal (0148) · owner: named DC owners + CT
    author    : CT (gmg-10)
    measured  : 2026-10-18 from the packs, the DC ruling, and the tree
    verdict   : the LEAST-ready MVP-6 modules — not "undeveloped" but "development not yet authorised." Both
                are `draft` with no runtime code and a blocked DEV lane; the blocker is owner/governance
                (DC-01…DC-05), not CT code.

## Measured state

| | MOD-0147 Supplier Performance & Risk | MOD-0148 Supplier Portal |
|---|---|---|
| pack status | **draft** | **draft** |
| backend | **none** | **none** |
| UI | **none** | **none** |
| effort | **unestimated** | **unestimated** |

Both pack front-matters: *"Phase A specification draft only; no runtime code or service-scaffold
authority."* A tree search for 0147/0148 backend or UI code returns nothing.

## Why the DEV lane is blocked — DC-01…DC-05

`docs/records/decisions/2026-10/mvp6-ct-ruling-dc01-dc05-concurrence-01.md` (CT ruling, 2026-10-05):

- The owner's `OD-Q19 A` is **not concurrence** for any of DC-01…DC-05 — it is *"a release to solicit, not
  a concurrence."* For all five, the project owner is "another owner"; each must go to its **named** owner.
- Each DC needs **a new producer seam, not a signature**: DC-01 durable domain ownership, DC-02 authority
  to consume the contract (AWAITING-CT), DC-03 binding resolution + trusted LE issuance, DC-04 a live
  Metric Registry with immutable revisions, DC-05 an immutable taxonomy revision + scoped portal resolver.
  None of these upstream capabilities exists.
- Explicit: *"Governance amendment, pack promotion to `ready-for-dev`, and any DEV lane remain blocked."*
- CT recommendation: pursue **DC-01 first and alone** (the cheapest, releases one governance-diff lane);
  the owner may overrule.

### Named owner per DC (from `mvp6-supplier-concurrence-close-prep-01/OWNER-DECISION-PACK.md`)

Each DC goes to its **named** owner, not the project owner, and the pack carries the exact proposed text +
a `APPROVE … as written` / `REJECT …` response line for each.

| DC | named owner | new producer seam |
|---|---|---|
| **DC-01** domain/service ownership | **Enterprise/domain owner** (CT + Supply Chain Execution concurrence) | **none** — only a later governance-diff lane aligning domain-config/DCP-009/registry |
| DC-02 Supplier consumption + LE eligibility | MOD-0140 Supplier owner + its eligibility owner | affirmative Tenant+LE eligibility, failure/correlation semantics, live-producer evidence |
| DC-03 identity/binding | Platform auth/security identity owner (0148/0140 concur) | binding resolution, trusted LE issuance, membership currency, mutation/revocation fencing |
| DC-04 metric registry | Metric Registry owner (0147 consumes) | live registry pointer, immutable revision, validity/withdrawal query |
| DC-05 risk taxonomy/bands | Risk Register owner (0147 lifecycle, 0148 portal-source, Metric owner on bands) | immutable taxonomy revision + validity source + scoped portal-source resolver |

**Cheapest first step to make 0147/0148 developable:** take **DC-01 to the Enterprise/domain owner** for
`APPROVE DC-01 as written` — it needs no producer seam from any team and opens the governance-diff lane that
leads to `ready-for-dev`. The ready approval text is in the pack above.

### Owners assigned 2026-10-18 (owner ny@gmgroup.ch)

`docs/records/decisions/2026-10/mvp6-supplier-dc01-dc05-owner-assignment-01.md`: DC-01 → the **project owner
as Enterprise/domain owner** (sign now, no build); DC-02 → the **MOD-0140 / SupplyChain-Mdm lane** and DC-03
→ the **Auth/Platform lane** (both owned but **build commitments**, scheduled not closed); **DC-04 and DC-05
DEFERRED** — no Metric Registry or Risk Register service exists in the tree, so the scoring (0147) and risk
(0147/0148) slices are out of realistic MVP-6 scope. Net: 0147/0148 become **bounded-developable**, the same
pattern as MOD-0190/0192. The DC-01 solicitation is ready at `DC-01-SOLICITATION.md` for the owner's
`APPROVE DC-01 as written`.

### DC-01 APPROVED 2026-10-18 (ny@gmgroup.ch)

`docs/records/decisions/2026-10/mvp6-supplier-dc01-approved-01.md`. The **Ownership placement** gate row is
now **CONCURRED** in both `mvp6-mod0147-spec-split-01/CONCURRENCE-OPEN.md` and the `mvp6-mod0148` matrix. The
governance-diff is **prepared, not applied** (`DC-01-GOVERNANCE-DIFF-PREP.md`): exact proposed
domain-config / DCP-009 / module-id-registry edits placing 0147/0148 in SCE / `Diten.SupplyChainService`
(MOD-0140 stays Supplier master), for the domain-config/DCP/registry writers to apply. Still blocked for
`ready-for-dev`: DC-02 + DC-03 (build seams) and DC-04/DC-05 (deferred, no producer capability).

The SUPPLIER-BASE contract (`supplier.openapi.yaml`, FROZEN) exists and answers *who a supplier is*; the
five DCs decide *who owns the policy, authority and boundary* — and the pack records "SUPPLIER v1 coverage:
none" for DC-01/DC-04. So a frozen contract does not make these modules buildable.

## Side finding (folded into the pin reconciliation)

The packs pin SUPPLIER-BASE at `c8badff96…` / 2026-09-15, but the file is now **v1.2.0 /
`373ebb21bae15f152d1b9ac24e03f7bfd02e595d343cf68cf918da553f5f42f7`** — a third instance of the pack-pin
drift. Recorded in `mvp6-pack-pin-reconciliation-01/RECONCILIATION.md` §C as **low priority** (the packs
are draft, nothing consumes it yet; re-pin when they promote to `ready-for-dev`).

## CT action: none

No code, no CT-ownable decision. The path to readiness is entirely owner/governance: close DC-01…DC-05 with
their named owners (CT recommends DC-01 first), which promotes the packs to `ready-for-dev` and unblocks a
DEV lane. Only then does "develop MOD-0147/0148" become actionable work.

## Files

- `STATUS.md` — this verification.
