---
module: MOD-0148
work_package: WP-MVP6-SUPPLIER-DC03-01
evidence_level: E1
decision: approved
commit:
modules: [MOD-0148]
build_owner: Auth / Platform (DC-03 owner) — TBD
---

# DC-03 — Supplier actor binding, authorization and revocation (owner decision)

- owner: **project owner (K1)**, acting as the DC-03 owner named in
  `mvp6-supplier-dc01-dc05-owner-assignment-01.md` (Platform auth/security owner = `Diten.AuthService` / `Diten.Platform`,
  held by the project owner / CT). Chosen in chat on 2026-10-20 from
  `docs/roadmap/plans/mvp6-supplier-dc03-decision-prep-01/DECISION-DRAFT.md` (D1–D8 as recommended).
- scope: **decision only.** DC-03 stays a **build commitment** in Auth/Platform. Nothing is built; no pack becomes
  `ready-for-dev`; no product code, scaffold or deployment is authorised. Every Auth/Platform change needs its own pack,
  XMC-001 consent and approval.
- measured base: `origin/main` `673995c15` (code read, no runtime test). Auth has no supplier actor today: account kinds
  `Unknown/Human/Service`, actor types `tenant_user/platform_admin/partner_admin`, no `legal_entity_id` claim, access
  token 120 min.

## Decisions

| # | Decision |
|---|---|
| **D1** | The binding is owned and stored by **Auth**: a new `SupplierActorBinding` record — `UserId`, `TenantId`, `LegalEntityId`, `SupplierId`, `Status`, `Revision`. Not in Procurement, not in SupplyChain. |
| **D2** | A new `actor_type = supplier_user` (the account stays `Human`). A `supplier_user` token is issued **only** while an active binding exists; it carries no tenant roles or tenant menus. |
| **D3** | The token carries `supplier_id`, `legal_entity_id`, `binding_rev`. Reads trust these claims; **every write** re-checks the current `binding_rev` with Auth. Auth unreachable on a write → 503, zero writes. |
| **D4** | Revocation takes effect **at once for writes** and within the token lifetime for reads; the `supplier_user` access token lives **15 minutes** (tenant users' lifetime unchanged). |
| **D5** | At most **one active binding per person per Tenant + LE**. A person bound in several tenants/LEs chooses one at sign-in; a token carries exactly one binding. |
| **D6** | The **tenant Admin** creates and removes bindings in Auth, under a new permission `auth.supplier-bindings.manage`. At bind time the supplier must be `Active` in Procurement (DC-02 D1, read with the Admin's token). Onboarding-driven binding is a later version. |
| **D7** | MOD-0148 calls Procurement's `validate` with the **SupplyChain service identity** (S2S, existing `ServiceClientIdentity`) and the `supplierId`/LE from the binding; the S2S scope is `validate` only. A supplier person never holds `procurement.suppliers.read`. Closes the MOD-0148 caller identity DC-02 left open. |
| **D8** | Error precedence: 401 (no/invalid token) → 403 (not `supplier_user`, no binding, binding revoked, stale `binding_rev`) → 403 (supplier not `Active`, AC-05) → 400 (syntax, or the client tries to pass a `supplierId`/LE) → 503 (Auth or Procurement unreachable). Zero writes on every refusal. |
| D9 | Build owner and timing: **TBD** (Auth lane). |

## Conditions and open risks (owner to settle when D9 is scheduled)

- **Tenant isolation:** a `supplier_user` token must reach no tenant API. That needs an explicit `actor_type` check at
  the gateway and in every service — a rule outside today's `[HasPermission]` model. Required before any `supplier_user`
  token is issued.
- **Personal data:** supplier persons come from outside the tenant; invitation, MFA and account closure for them are an
  Auth-owner design decision.
- **MOD-0140 system of record:** the binding's `SupplierId` points at this repository's Procurement suppliers — the same
  condition as DC-02 D1 (`mod-0140-dual-implementation-01`).

## Effect

- MOD-0148: actor binding, authorization and the replay receipt's `validatedIssuer`/`subject` become **decided, unbuilt**.
  The pack revision follows after the DC-02 pack revision (PR #171) merges, to avoid a stacked change to the same file; it
  will need a new CT-APR-01 approval.
- MOD-0147: unaffected.
