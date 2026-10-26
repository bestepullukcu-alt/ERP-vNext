# BL-652 — Option 2 replaces Option 1 (owner decision, 2026-10-21)

- owner: **Natig** (`ny@gmgroup.ch`, product owner / management) — decided in chat 2026-10-21, recorded by the Control Tower
- status: **ACCEPTED — Option 2** (SupplyChain → MDM service identity) · **timing amended the same day: Option 2 later,
  not urgent; interim = tenant role assignment** (see Addendum)
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

## Addendum — Ali's position, 2026-10-21 (later the same day)

Ali (`GMG-ERP`, product owner), replying to plan v10.0:

- **Option 1: do not apply** — agrees with the decision above.
- **Interim = the project's normal path.** The four SupplyChain modules and MDM `legal-entity` are tenant-assignable, so
  the needed keys, `mdm.legal-entities.read` included, are granted to the roles that use these screens in the tenant's
  Roles → Permissions screen. Tests must run with that **real role assignment**, not with hand-minted tokens.
- **Option 2: later, not urgent** (removes the module coupling; "can be evaluated in the future").
- The 2026-11-03 expiry is moot.

Verified by CT (read-only, `origin/main` `9d44e8a1f`):

- `IsTenantAssignable: true` — `CarrierManagementManifestProvider.cs:19`, `RoutingLoadPlanningManifestProvider.cs:24`,
  `ReverseLogisticsManifestProvider.cs:31`, `ClaimsManagementManifestProvider.cs:28` (SupplyChain) and
  `LegalEntityManifestProvider.cs:29` (MDM, key `mdm.legal-entities.read`).
- Template only at tenant set-up — owner decision 2026-10-10 (FU-0022-SEED-02), proven by
  `services/Diten.AuthService/tests/Diten.AuthService.Application.Tests/Roles/DefaultRoleTemplateOnceTests.cs:68`
  (`A_key_added_to_the_template_later_does_not_reach_an_existing_tenant_through_either_event`). So Option 1 would not
  have changed existing tenants' Admin roles; the 2026-10-18 record's "every tenant Admin then holds…" held for new
  tenants only.

**Agreed 2026-10-21 (Natig, after Ali's reply):** the operating state is the interim above — keys granted to the
roles in the tenant's Roles → Permissions screen, tests with real role assignment. **Option 2 is later and not
urgent**; the Auth / MDM XMC-001 questions (`mvp6-bl652-coupling-decision-01/XMC-001-QUESTIONS-v0.1.md`) stay
**unsent** until Option 2 is scheduled. Option 1 stays rejected.
