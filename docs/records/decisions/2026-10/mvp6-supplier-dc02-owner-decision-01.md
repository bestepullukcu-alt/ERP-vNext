---
module: MOD-0147
work_package: WP-MVP6-SUPPLIER-DC02-01
evidence_level: E1
decision: approved
commit:
modules: [MOD-0147, MOD-0148]
consumes: MOD-0140
---

# DC-02 — Supplier consumption + Tenant/LE eligibility (owner decision)

- owner: **project owner (K1)**, acting as the DC-02 owner named in
  `mvp6-supplier-dc01-dc05-owner-assignment-01.md` (MOD-0140 Supplier lane = SupplyChain/MDM supplier lane, held by the
  project owner / CT). Chosen in chat on 2026-10-20 from the options in
  `docs/roadmap/plans/mvp6-supplier-dc02-decision-prep-01/DECISION-DRAFT.md`.
- scope: **decision only.** It closes the DC-02 *decision*; DC-02 stays a **build commitment** — nothing is built, no
  pack becomes `ready-for-dev`, and no product code, scaffold or deployment is authorised by this record.
- measured base: `origin/main` `365f260a9` (code read, no runtime test).

## Decisions

| # | Decision | Applies to |
|---|---|---|
| **D1** | Eligibility is read from the existing frozen `POST /api/suppliers/validate` (`validateSuppliers`, SUPPLIER v1.2.0) through the gateway, called with the request's MDM-validated Legal Entity. Procurement filters every supplier read by Tenant + LE (`SupplierRepository.cs:141-148`), so a supplier of another LE is `known:false`. **No contract successor.** | MOD-0147, MOD-0148 |
| **D2** | Status → permission matrix: **evaluation and risk registration** — `Active`, `OnHold` proceed; `Blocked`, `Inactive` are refused. **Portal access and submission** — `Active` only. | D2-a MOD-0147, D2-b MOD-0148 |
| **D3** | SupplyChain calls Procurement **with the caller's own token**; the caller needs `procurement.suppliers.read`. No service identity in the first slice. | MOD-0147 |
| **D4** | SupplyChain forwards the Legal Entity it validated with MDM (R-2, `MdmLegalEntityScopeValidator`) in `X-Legal-Entity-Id`. Procurement's own header-trust gap (`TenantResolutionMiddleware.cs:77-100`: header LE not validated against the tenant) is **MOD-0140's** to fix; it is notified separately (XMC-001), not changed here. | MOD-0147, MOD-0148 |
| **D5** | Fail-closed: Procurement unreachable / timeout / non-success → **503**; supplier `known:false` or status refused by D2 → **422**; **zero writes** in both. | MOD-0147; MOD-0148 for 503 only (an ineligible own supplier is 403, AC-05) |
| **D6** | Build owner: the SCE / CT lane. Next step now: revise both packs (docs only) and seek a new CT-APR-01 approval before any code. | MOD-0147, MOD-0148 |

## What this does NOT decide

- **MOD-0148 caller identity.** A portal actor is a supplier user; giving supplier users `procurement.suppliers.read`
  (read of every supplier in the LE) is not acceptable, so **D3 does not apply to MOD-0148**. MOD-0148's eligibility
  call needs a service identity or a bound-actor read — both depend on **DC-03** (actor binding, Auth/Platform). MOD-0148
  therefore takes D1, D2-b and D4 now; its caller identity and error mapping stay **BLOKE on DC-03**.
- **MOD-0148 error code.** The pack's existing AC-05 says an ineligible (suspended) portal actor gets **403**, not D5's
  422: the supplier there is the caller itself, not a referenced record. Kept as 403 (ÖNERİLEN); D5 applies to MOD-0147.
- Derived, not decided: when Procurement answers 401/403 to a MOD-0147 caller (missing `procurement.suppliers.read`),
  SupplyChain answers **403**, zero writes. Marked ÖNERİLEN in the pack for the owner to confirm.
- DC-03, DC-04 (Metric Registry), DC-05 (Risk Register) — unchanged; MOD-0147's writes stay BLOKE on DC-04/DC-05.
- **Which repository is MOD-0140's system of record.** A 2026-10-11 lane record found MOD-0140 built twice, in two
  unrelated repositories (`docs/records/audits/2026-10/mod-0140-dual-implementation-01/REPORT.md` — local, not on main
  at the time of writing): `ERP-vNext` (Platform, 5057, no frozen OpenAPI) and this repository (Procurement, 5065, frozen
  `supplier.openapi.yaml`, passing DCP-002). D1 is bound to **this repository's** Procurement implementation and frozen
  contract. If the owner rules the other repository canonical, D1 must be re-decided before the seam is built.

## Effect on the packs

Both packs get a "Supplier eligibility seam (DC-02)" section and test IDs (map revision r3). Changing the packs voids
their CT-APR-01 approvals of 2026-10-20 (`7c5848aa4`): a **new approval row** is needed before dispatch.
