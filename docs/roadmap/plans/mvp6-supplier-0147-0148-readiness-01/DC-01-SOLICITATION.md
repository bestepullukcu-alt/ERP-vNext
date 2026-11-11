# DC-01 solicitation — durable domain/service ownership (MOD-0147 / MOD-0148)

    to        : the Enterprise/domain owner (assigned 2026-10-18: **ny@gmgroup.ch**), with CT and
                Supply Chain Execution concurrence
    from      : Control Tower (gmg-10)
    date      : 2026-10-18
    basis     : OWNER-DECISION-PACK.md DC-01 · assignment record
                docs/records/decisions/2026-10/mvp6-supplier-dc01-dc05-owner-assignment-01.md
    why now   : DC-01 is the only one of the five with **"New producer seam required: none"** — it costs no
                team a build and it is the single step that opens the governance-diff lane toward
                `ready-for-dev`. CT recommended it be pursued first and alone.

## Exact proposed concurrence (verbatim from the pack)

> MOD-0147 Supplier Performance & Risk and MOD-0148 Supplier Portal are durably placed in
> `supply-chain-execution` and implemented, when separately authorized, by `Diten.SupplyChainService`.
> MOD-0140 remains the Supplier master owner. This concurrence assigns no runtime, contract, gateway or
> permission ownership to the two modules.

## Artifact binding (seals, for provenance)

- common SG-01: `mvp6-supplier-spec-split-01/COMMON-SEAM-GAPS.md`
- MOD-0147 placement gate: `mvp6-mod0147-spec-split-01/CONCURRENCE-OPEN.md`
- MOD-0148 placement gate: `mvp6-mod0148-spec-split-01/CONCURRENCE-OPEN.md`
- bound split-package seal: `26ffe843110b2c9596908c8b7a1599fa5f20e61c608c4bf0472c07f815979c0b`
- acceptance binding: AC-01

## Scope guards (what APPROVE does NOT do)

- **No build.** A later, separately authorized governance writer aligns `domain-config`, DCP-009 and the
  registry **without changing module identity**. MOD-0140 stays the Supplier master owner.
- No runtime, contract, gateway or permission ownership is granted to 0147/0148.
- Pack promotion to `ready-for-dev`, Program.cs composition and any DEV lane remain **separate** later
  authorities (`DEPENDENCY-SEQUENCE.md`).
- `SUPPLIER v1 coverage: none` for DC-01 — contract metadata cannot decide organizational placement; this
  is why a frozen contract alone does not settle it.

## Governance note (why the project owner may approve this one)

The CT concurrence ruling (`mvp6-ct-ruling-dc01-dc05-concurrence-01.md`) held that `OD-Q19 A` is only a
release-to-solicit because, for each item, the project owner was "another owner". The 2026-10-18 assignment
**designates the project owner as the Enterprise/domain owner for DC-01**, so an `APPROVE DC-01` here is the
**named owner's** concurrence — not the `:5`-barred approval by another owner. Recorded as an explicit role
assignment, so `:5` stays enforceable for the other items.

## Owner response (choose one)

```
APPROVE DC-01 as written
```
or
```
REJECT DC-01; exact alternative domain/service owner: …
```

## On APPROVE

1. CT records the approval (a decision record) and promotes the two placement gates.
2. CT opens **one** governance-diff preparation lane: an exact proposed `domain-config` / DCP-009 / registry
   alignment for `supply-chain-execution` — prepared, not applied (applying it is a separate authority, and
   the registry/domain-config writers are their proper owners).
3. DC-02 and DC-03 are then scheduled as build seams; DC-04/DC-05 stay deferred (no producer capability
   exists). 0147/0148 reach a **bounded** developable state, not full scope.
