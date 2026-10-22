# MOD-0148 Supplier Portal — starts now (owner decision, 2026-10-22)

- owner: **Natig** (`ny@gmgroup.ch`, management) — decided in the MVP-6 Control Tower chat 2026-10-22, recorded by the CT
- status: **ACCEPTED**
- module: MOD-0148 Supplier Portal (`execution/domains/supply-chain-execution/module-packs/MOD-0148-supplier-portal.md`, `status: draft`)
- related: plan v10.0 C4 — DC-03 approved 2026-10-21 (separate portal subdomain, mandatory two-step verification for
  suppliers, no same-email company + supplier account in one tenant, Gateway allow-list for `supplier_user`, no internet
  exposure until `TrustedProxies`, Gateway rate limit and a shared limiter store exist); D9 answered 2026-10-21: **Beste
  (central team) builds the DC-03 binding when MOD-0148 starts (MVP-6)**.

## Decision

1. **MOD-0148 starts now (2026-10-22).** This is the D9 trigger: Beste's DC-03 binding work (a separate Auth/Platform pack
   for the `supplier_user` actor, the portal door and the Gateway rule) may begin.
2. **v1 = the supplier's own submissions, internal only.** The `SUPPLIER-PERFORMANCE` v1 portal operations
   (`getSupplierPortalStatus`, `createOwnPortalSubmission`, `submitOwnPortalSubmission`, own list/get). The portal is not
   exposed to the internet until the three platform conditions of DC-03 are met — unchanged.
3. The decision authorizes the **MOD-0148 pack revision** (draft → ready-for-dev candidate). It authorizes no code: code
   waits for the revised pack's CT-APR-02 review and CT-APR-01 approval, the DC-03 binding (Beste) and DC-02 (MOD-0140
   1.3.0, Cihan).

## Consequences

- Load on Beste: the DC-03 binding pack comes in addition to her reviews (#196–#198) and the MOD-0140 CT-APR-01 approval.
- Out of v1: the internal review command (`UNDER_REVIEW` / `ACCEPTED` / `REJECTED` have no v1 operation), portal-source
  resolution for MOD-0147's risk part (risk waits for TPRM), and a supplier-facing screen (a later revision with
  seven-language scope).
- **Confirmed by Natig the same day:** the frozen v1 `payload` accepts any object (§0.2);
  v1 keeps that wire and limits only the total request size the service already enforces, and per-type payload schemas go
  to a contract successor.

## Follow-up decision after the independent review (Natig, 2026-10-22)

The CT-APR-02 review (`docs/records/reviews/MOD-0148/2026-10-22-7ba14d85.md`, KOŞULLU) found that the service has no
explicit request-size limit (only Kestrel's 30 MB default, above MongoDB's 16 MiB document limit). Natig set the v1 limit:
**1 MiB per create request** (over the limit → 400 `INVALID_REQUEST`; the frozen contract declares no 413).
Own-list cap: the frozen list has no paging, so v1 caps **open drafts** — at most **200 `DRAFT` records per supplier + legal
entity** (the 201st draft → 422); submitting frees a place, so it is never a lifetime block (revised by Natig after review
round 2, which showed a cap on all records would block a supplier for good). Paging comes with a contract successor.

This record authorizes no code and no pack change by itself.
