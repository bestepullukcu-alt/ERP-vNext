---
module: MOD-0147
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# DC-01 governance-diff — exact proposed alignment (PREPARED, NOT APPLIED)

    opened    : 2026-10-18 on DC-01 approval
                (docs/records/decisions/2026-10/mvp6-supplier-dc01-approved-01.md) · author: CT (gmg-10)
    status    : PREPARED ONLY. These are the exact proposed edits to place MOD-0147/0148 in
                supply-chain-execution / Diten.SupplyChainService per DC-01. **CT does not apply them** —
                domain-config, DCP-009 and the module-id registry have their own authorized writers
                (DC-01 grants no runtime/contract/gateway/permission ownership, only placement). Each change
                is anchored to current text so the writer can apply it mechanically.
    guardrails : MOD-0140 remains the Supplier master owner (domain `procurement`). 0147/0148 are
                P2P-origin (Blueprint Procurement Suite) placed in SCE for delivery; they are NOT inventory
                SoR. Their own ready-for-dev gates stay closed (DC-02/DC-03 are build commitments; DC-04/DC-05
                deferred). So each edit must carry a `draft / not ready-for-dev` qualifier.

## Change 1 — `execution/domains/supply-chain-execution/domain-config.md`

**Anchor:** the "In-Scope Reserved Modules" block (currently ends with the `MOD-0183-0187
Transport/Logistics` line, ~line 32), which does not list 0147/0148.

**Proposed addition** (a new qualified sub-bullet under that block — not folded into the inventory-SoR
lists):

```
**Supplier (P2P-origin, placed by DC-01 2026-10-18 — delivery placement only)**
- `MOD-0147 Supplier Performance & Risk` and `MOD-0148 Supplier Portal` — Blueprint Procurement Suite (P2P)
  modules, durably PLACED in `supply-chain-execution` / `Diten.SupplyChainService` for delivery per DC-01.
  They are NOT inventory/SoR of this domain. `MOD-0140 Supplier` remains the Supplier master owner
  (domain `procurement`). Placement grants no runtime/contract/gateway/permission ownership; each module's
  own module pack stays `draft` until its ready-for-dev gates close (DC-02/DC-03 build seams; DC-04/DC-05
  deferred). Ref: docs/records/decisions/2026-10/mvp6-supplier-dc01-approved-01.md.
```

## Change 2 — `execution/portfolio/delivery-capability-packs/DCP-009-supply-chain-inventory.md`

**Anchor:** `canonical_modules:` (line 16) lists `[MOD-0173 … MOD-0193]` and omits 0147/0148. The
`status_note` (line 8) already names them as "two draft (MOD-0147, MOD-0148)" member packs.

**Proposed change:** do **not** add 0147/0148 to `canonical_modules` (that list is the inventory/SoR set).
Instead add a sibling field making the placement explicit:

```
placed_non_canonical_modules: [MOD-0147, MOD-0148]   # P2P-origin, placed in SCE by DC-01 2026-10-18 for
                                                     # delivery; MOD-0140 remains Supplier master
                                                     # (procurement); both packs draft, not ready-for-dev.
```

(If the DCP writer prefers, the same fact can instead be a one-line note in `status_note`; the binding
requirement is only that 0147/0148's SCE placement is recorded and that they are marked non-canonical,
draft, and master-owned by MOD-0140.)

## Change 3 — `execution/registries/module-id-registry.md`

**Anchor:** line ~312 already states 0147/0148 are "assigned to the MVP-6 lane per DCP-009 §18 OD-4 … each
reserves its own ID at pack-authoring time; not minted here." There are **no explicit 0147/0148 rows** yet.

**Proposed addition** (two rows in the same table that carries MOD-0140…, matching its column shape;
`status = draft`, domain `supply-chain-execution`, service `Diten.SupplyChainService`):

```
| MOD-0147 | Supplier Performance & Risk | supplier-performance-risk | Module | draft | | | supply-chain-execution | P2P-origin (Blueprint Procurement Suite); placed in SCE / Diten.SupplyChainService by DC-01 2026-10-18 (mvp6-supplier-dc01-approved-01). MOD-0140 remains Supplier master (procurement). NOT ready-for-dev: DC-02/DC-03 are build seams, DC-04 (Metric Registry)/DC-05 (Risk Register) deferred — no producer capability exists. Consumes supplier.openapi.yaml (x-owner MOD-0140) + supplier-performance.openapi.yaml. |
| MOD-0148 | Supplier Portal | supplier-portal | Module | draft | | | supply-chain-execution | P2P-origin (Blueprint Procurement Suite); placed in SCE / Diten.SupplyChainService by DC-01 2026-10-18 (mvp6-supplier-dc01-approved-01). MOD-0140 remains Supplier master (procurement). NOT ready-for-dev: DC-02/DC-03 build seams pending, DC-04/DC-05 deferred. Consumes supplier.openapi.yaml + supplier-performance.openapi.yaml. |
```

Update the line-312 note to reference this record (the IDs are now placed, still not promoted).

## Who applies, and what still gates after

- **Writers:** domain-config owner (Change 1), the DCP-009 owner (Change 2), the registry owner (Change 3).
  CT prepared; CT does not apply (DC-01 is placement, not a writer grant).
- **After application:** the two placement gate rows are already CONCURRED (done in the gate files). Pack
  promotion to `ready-for-dev` still requires DC-02 + DC-03 to be built and their gate rows closed; DC-04/
  DC-05 stay deferred. 0147/0148 remain **bounded** (identity + placement + Supplier consumption), not full
  scope.

## Files

- `DC-01-GOVERNANCE-DIFF-PREP.md` — this. No governance file is edited by CT here.
