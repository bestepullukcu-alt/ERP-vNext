---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# Pack ↔ contract pin reconciliation — the SCE packs trail the canonical contracts

    item      : follow-up from Q498 (version bump) and Q484 · owner: the module pack owners
    author    : CT (gmg-10)
    measured  : 2026-10-18 on feature/mvp6-logistics at HEAD (239cc2b60)
    status    : NOT applied — a pack's binding version is a pack-owner **acceptance** decision (§31/Q380
                pattern; XMC-001). This is the inventory, the provenance, and the exact target values so
                each re-pin is a confirm-and-edit, not research.

## Canonical contracts now

| contract | info.version | YAML SHA-256 |
|---|---|---|
| `docs/analysis/contracts/shipment-bundle.openapi.yaml` | **3.3.0** | `c59bb4803a2d7e55eff0ce1e5aeb126df7b4eb3fbffb251e999bc0c069d43ef2` |
| `docs/analysis/contracts/sandop-capacity.openapi.yaml` | **3.1.0** | `bbe466ad18d37ed99169be42d52e1939251a7b29f6cacf46398e132473f4b927` |

Wire `contractVersion: v1` is unchanged for both — the re-pin is of the **document** version/hash only, not
the envelope.

## Provenance (why the packs trail, and by how much)

**sandop-capacity — one clean hop, caused by Q498.** The pre-Q498 file hash is
`5213b5353267…`, which is exactly what MOD-0190/0192 pin as "canonical 3.0.0". Q498 took it to
`3.1.0 / bbe466ad…`. So the sandop pins were accurate up to Q498 and need a single bump.

**shipment-bundle — two hops; the first predates Q498.** The packs (MOD-0186/0187 "today (Q380)") pin
`3.1.0 / 6dc1dd48…`. But:
- commit `1ccbc49b7` ("contract(shipment-bundle): 3.2.0 — LegalEntityId arrives with the request, and every
  operation declares 503") bumped **3.1.0 → 3.2.0 / `fb84a72a…`** and did **not** update the pack pins —
  pre-existing drift, not from this lane's work;
- Q498 (`7bdf0110f`) then bumped **3.2.0 → 3.3.0 / `c59bb480…`**.

So a shipment re-pin to 3.3.0 also absorbs the un-tracked 3.2.0 change. **Owners should confirm they accept
the 3.1.0→3.2.0 content (`1ccbc49b7`: LegalEntityId-with-request + 503 on every op) as well as Q498's
additive responses**, since re-pinning "today" to 3.3.0 binds both.

## What to change — and what to LEAVE

Each pack carries two kinds of reference. **Update only the live/"today" pins; leave frozen history.**

- **LEAVE (frozen design history / "at acceptance"):** `info.version 1.0.0 / 1.1.0 / 2.0.0` lines
  (MOD-0184:153, MOD-0185:167/478, MOD-0186:165/480, MOD-0187:166), the "§28 historical draft … superseded"
  notes, and the "Published contracts **at acceptance**" rows (SHIPMENT-BUNDLE `3.0.0 / 5dfe7c1b…`;
  SANDOP-CAPACITY `2.0.0 / 9543e3f2…`, the §22 acceptance pin). These record what was bound *when accepted*
  and must not be rewritten (K4).
- **UPDATE (live "today"/"canonical"/"pinned to" rows):** below.

### shipment-bundle → 3.3.0 / `c59bb480…`  (consumers: MOD-0186, MOD-0187; refs in MOD-0183, MOD-0185)

| file:line (approx) | cited now | set to | note |
|---|---|---|---|
| MOD-0186:585 | `today (Q380) … 3.1.0 YAML 6dc1dd48…` | `3.3.0 / c59bb480…` | live pin |
| MOD-0186:620 | `Published SHIPMENT-BUNDLE 3.1.0 / wire v1 (6dc1dd48…)` | `3.3.0 / c59bb480…` | live pin |
| MOD-0187:534 | `today (Q380) … 3.1.0 YAML 6dc1dd48…` | `3.3.0 / c59bb480…` | live pin |
| MOD-0187:570 | `Published SHIPMENT-BUNDLE 3.1.0 / wire v1 (6dc1dd48…)` | `3.3.0 / c59bb480…` | live pin |
| MOD-0183:89, MOD-0185:259 | prose "(R-2, SHIPMENT-BUNDLE 3.2.0)" | `3.3.0` | prose version ref; update for accuracy |

### sandop-capacity → 3.1.0 / `bbe466ad…`  (consumers: MOD-0190, MOD-0192)

| file:line (approx) | cited now | set to | note |
|---|---|---|---|
| MOD-0190:118 | `3.0.0 … YAML SHA-256 5213b535…` | `3.1.0 / bbe466ad…` | live pin |
| MOD-0190:274 | `canonical … now 3.0.0 (YAML 5213b535…)` | `3.1.0 / bbe466ad…` | live pin |
| MOD-0190:308-309 | `canonical 3.0.0 5213b535…; pinned to 3.0.0` | `3.1.0 / bbe466ad…` | live pin |
| MOD-0192:201 | `3.0.0 YAML and annex` | `3.1.0` | live pin (acceptance checkbox) |
| MOD-0192:317 | `3.0.0 / wire v1 (YAML 5213b535…)` | `3.1.0 / bbe466ad…` | live pin |
| MOD-0192:55, :116, :221, :253 | still `2.0.0 / 9543e3f2…` | `3.1.0 / bbe466ad…` **or confirm frozen** | **MOD-0192 is internally inconsistent** — :201 already says 3.0.0 while these say 2.0.0. The owner must decide per line whether it is a live pin (update to 3.1.0) or a frozen "published 2.0.0 binding" (leave). |

Trivial, not a pin: `CapacityRequestShapeTests.cs:7` doc-comment says "Published SANDOP-CAPACITY 3.0.0"
(now 3.1.0); asserts nothing — fold into whichever pack change touches MOD-0192.

## How to apply (per pack owner)

For each live row above, replace the version and the 64-hex YAML SHA-256 with the canonical values in the
table at the top. The annex filenames (`…-semantics-v3.0.0.md`, etc.) are a **separate** question — Q498
added responses only and did **not** add or rename an annex, so annex pins do not change here. After editing,
confirm the pack still reads coherently (the "at acceptance" row stays; only "today" moves).

This is the first reconciliation that also records the **method**: a contract re-pin must update every pack's
"today" row in the same change as the contract bump, or the pins drift — which is exactly what `1ccbc49b7`
(3.1.0→3.2.0) did not do.

## C. SUPPLIER-BASE (MOD-0147 / MOD-0148) — same drift, lower priority (packs are `draft`)

Added 2026-10-18 while checking 0147/0148 readiness. `docs/analysis/contracts/supplier.openapi.yaml` is now
**v1.2.0**, SHA-256 `373ebb21bae15f152d1b9ac24e03f7bfd02e595d343cf68cf918da553f5f42f7`, but MOD-0147 and
MOD-0148 pin it as `c8badff96…` / 2026-09-15 (pre-1.2.0 — the `1.1.0 → 1.2.0` additive delete/bulk-delete
note is on the file). So the supplier contract is a third instance of the same pin drift.

| contract | pack pin (0147/0148) | canonical now |
|---|---|---|
| `supplier.openapi.yaml` | `c8badff96…` (2026-09-15) | **v1.2.0 / `373ebb21bae15f152d1b9ac24e03f7bfd02e595d343cf68cf918da553f5f42f7`** |

**Priority: low.** Both packs are `draft` with no code and a blocked DEV lane (DC-01…DC-05; see
`mvp6-supplier-0147-0148-readiness-01`), so nothing consumes this pin yet. Reconcile it **when the packs
promote to `ready-for-dev`** (the pin should be current at the moment consumption is authorised), or fold it
into this pass if the pack owners are already editing those files. The additive 1.1.0→1.2.0 change means the
re-pin, like A and B, binds the pack to content it should re-confirm.

## Files

- `RECONCILIATION.md` — this inventory. No pack edits are applied; re-binding is the pack owners' acceptance.
