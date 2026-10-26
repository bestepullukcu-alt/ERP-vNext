---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: accepted
commit:
---

# Q498 — APPLIED, with a minor version bump (owner decision 2026-10-18)

The additive response declarations proposed in `README.md` / `VERIFIED.md` were **applied** to the frozen
contracts, together with the owner's version decision.

## Owner decision

- owner: gmg-10 (ny@gmgroup.ch), in session 2026-10-18
- **Minor bump** (SemVer: additive, backward-compatible response declarations):
  - `shipment-bundle.openapi.yaml`: `info.version` **3.2.0 → 3.3.0**
  - `sandop-capacity.openapi.yaml`: `info.version` **3.0.0 → 3.1.0**
- The **wire `contractVersion: v1` is unchanged** — the envelope version is separate from the document
  version, and no envelope changed.

## What landed, verified (2026-10-18)

| contract | info.version | responses added | removed | parses |
|---|---|---|---|---|
| shipment-bundle | 3.3.0 | `GET /shipments`: 401,403,404,500 · `GET /claims`: 404 | **0** | yes |
| sandop-capacity | 3.1.0 | `500` on all **12** operations (+ `InternalError` component) | **0** | yes |

- PyYAML parse + per-operation comparison vs HEAD: every other operation unchanged; strictly additive.
- `ContractErrorCodeParityTests` **3/3 pass** against the modified `shipment-bundle.openapi.yaml` — the
  additions reuse codes already in the contract (UNAUTHENTICATED, FORBIDDEN, INTERNAL_ERROR,
  CLAIM_NOT_FOUND), so the contract↔`ContractErrorCodes` parity is intact (no new/missing code).

## Follow-ups this bump creates (NOT done here — pack-owner / reconciliation work)

A version bump drags the pack pins behind it, and several were **already** stale before Q498:

- `MOD-0192:253` pins the **superseded 2.0.0** sandop-capacity hash (`9543e3f2…`) and the 2.0.0 annex,
  while the file was already 3.0.0 and is now **3.1.0**. `:55`/`:116` also still cite 2.0.0; `:201` was
  corrected to 3.0.0 on 2026-10-11.
- `MOD-0183` cites SHIPMENT-BUNDLE 3.2.0; `MOD-0186` "re-pinned to 3.1.0"; `MOD-0185` cites 2.0.0/1.1.0.
  These now trail the 3.3.0 file.
- Trivial: `CapacityRequestShapeTests.cs:7` has a doc-comment "Published SANDOP-CAPACITY 3.0.0" (now 3.1.0);
  a comment, asserts nothing — fix with the pack reconciliation.

Reconciling the pack pins to 3.3.0 / 3.1.0 is a pack-owner pass (XMC-001: not this change's to make). It
is tracked as the same surface as owner-queue item for the packs; Q498 itself (the contract under-declaring
what the code returns) is now **closed**.
