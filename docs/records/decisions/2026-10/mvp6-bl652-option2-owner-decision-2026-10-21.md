# BL-652 — Option 2 replaces Option 1 (owner decision, 2026-10-21)

- owner: **Natig** (`ny@gmgroup.ch`, product owner / management) — decided in chat 2026-10-21, recorded by the Control Tower
- status: **ACCEPTED — Option 2** (SupplyChain → MDM service identity)
- supersedes: the Option 1 ruling in `mvp6-bl652-admin-baseline-coupling-owner-decision-01.md` (2026-10-18). Option 1
  (widen the tenant Admin baseline with `carrier-management`, `routing-load-planning`, `reverse-logistics`,
  `claims-management`) was **never applied**; its 2026-11-03 expiry is therefore moot. No widening is to be applied.
- scope: `docs/roadmap/plans/mvp6-bl652-coupling-decision-01/OPTION2-S2S-SCOPE-v0.1.md` (measured on `origin/main` `9d44e8a1f`)
- modules: MOD-0184/0185/0186/0187 · `Diten.SupplyChainService` · consumes MDM `lookup-validation`

## Decision

SupplyChain calls MDM's `GET api/legal-entities/{id}/lookup-validation` with an **Auth-issued, tenant-scoped,
single-audience service token**, using the pattern already on main (Auth `service-tokens/issue`, `ServiceClientIdentity`
+ `ServiceClientTenantGrant`, MDM's `AuthTrustedSourceAuditServiceIdentityProvider` as the caller model, Platform's
`TrustedServiceTokenValidationExtensions` as the receiver model). The tenant Admin baseline is **not** widened.

## Interim (until Option 2 ships)

The current state stays: a SupplyChain create needs `mdm.legal-entities.read` on the user's role; without it the
validator answers 503 (fail-closed). No temporary widening.

## Still open (not decided here)

1. Auth owner and MDM owner consent (XMC-001) and who builds each part.
2. Delegated user (`X-Delegated-Authorization` beside the service token): yes / no.
3. How the SupplyChain client's tenant grants are provisioned for every tenant.
4. The new MDM audience name (proposal: `TRUSTED_LEGAL_ENTITY_REFERENCE_CONSUMER`).

This record grants no permission, applies no patch and authorizes no code; implementation needs its own approved scope.
