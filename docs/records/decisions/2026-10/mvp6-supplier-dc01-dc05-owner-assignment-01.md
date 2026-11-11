# Supplier DC-01…DC-05 — owner assignment (0147/0148 readiness)

> **OWNER DECISION.** Assigns the named owner for each of the five Supplier concurrence items, on CT's
> evidence-based recommendation. Grounded in the repo's ownership structure (service presence, `x-owner`
> tags, domain-config); no person was invented. Argument and the five exact texts:
> `docs/roadmap/plans/mvp6-supplier-concurrence-close-prep-01/OWNER-DECISION-PACK.md`. CT ruling that
> `OD-Q19 A` is only a release-to-solicit: `mvp6-ct-ruling-dc01-dc05-concurrence-01.md`.

- owner: **ny@gmgroup.ch** (project owner) — decision relayed by the Control Tower, 2026-10-18
- scope: MOD-0147 Supplier Performance & Risk, MOD-0148 Supplier Portal (both `draft`, zero runtime code)

## Assignment

| DC | assigned owner | type | note |
|---|---|---|---|
| **DC-01** domain/service placement | **ny@gmgroup.ch, as Enterprise/domain owner** (CT + Supply Chain Execution concur) | signature, **no build** | The project owner is hereby **designated the Enterprise/domain owner** for this item, so their `APPROVE DC-01` is the named-owner concurrence — not the `:5`-barred "another owner's approval". Approve now; it opens the governance-diff lane. |
| **DC-02** MOD-0140 Supplier + LE eligibility | **MOD-0140 Supplier owner** = the `Diten.SupplyChainService` / `Diten.MdmService` supplier lane (held by ny@gmgroup.ch / CT) | **build commitment** | Service exists (`supplier.openapi.yaml x-owner: MOD-0140`). Assigning it commits the lane to BUILD the affirmative Tenant+LE eligibility seam; it is not closed by this signature. Schedule as real work. |
| **DC-03** auth/security binding + revocation | **Platform auth/security owner** = `Diten.AuthService` / `Diten.Platform` (held by ny@gmgroup.ch / CT) | **build commitment** | Service exists. Commits the lane to BUILD binding resolution, trusted LE issuance, membership currency and the mutation/revocation fence. Schedule as real work. |
| **DC-04** Metric Registry policy | **DEFERRED — no owner assigned** | net-new capability | No Metric Registry service/module exists in the tree. Not assignable to an existing team; designate an owner only if/when the capability is built. |
| **DC-05** Risk Register taxonomy/bands | **DEFERRED — no owner assigned** | net-new capability | No Risk Register service/module exists. Same disposition as DC-04. |

## What this makes developable, and what it does not

- **DC-01 (sign now)** promotes the placement toward `ready-for-dev` at no build cost.
- **DC-02 + DC-03** are owned but are **scheduled build seams**, not completed by this record.
- **DC-04 + DC-05 deferred** ⇒ the scoring slice (0147) and the risk slice (0147/0148) that depend on a
  Metric Registry / Risk Register are **out of realistic MVP-6 scope**. 0147/0148 can reach a **bounded**
  developable state (identity + placement + Supplier consumption), not full scope — the same bounded
  pattern as MOD-0190/0192 (`mvp6-0190-0192-fixture-bound-owner-decision-01`).

## Sequence

1. DC-01 solicitation: `docs/roadmap/plans/mvp6-supplier-0147-0148-readiness-01/DC-01-SOLICITATION.md` — ready
   for the owner's `APPROVE DC-01 as written`.
2. On DC-01 approval: open the governance-diff lane (domain-config / DCP-009 / registry alignment), then
   schedule DC-02 and DC-03 as build seams.
3. DC-04/DC-05 revisited only if the owner decides to stand up those capabilities (post-MVP-6).
