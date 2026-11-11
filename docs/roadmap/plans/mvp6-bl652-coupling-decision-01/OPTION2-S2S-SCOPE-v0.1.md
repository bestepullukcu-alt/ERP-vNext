# BL-652 Option 2 — SupplyChain → MDM service identity: scope (v0.1)

    created  : 2026-10-21
    status   : PROPOSAL — scope for an owner decision; no code, no pack
    measured : origin/main 9d44e8a1f, read-only (no build, no run)
    decision : mvp6-bl652-admin-baseline-coupling-owner-decision-01.md — owner ny@gmgroup.ch, Option 1 ACCEPTED
               2026-10-18, **never applied**, expiry **2026-11-03**; Option 2 = "the agreed standing model"

## 1 · The problem, as measured

`MdmLegalEntityScopeValidator` (`services/Diten.SupplyChainService/src/Diten.SupplyChainService.Infrastructure/Common/MdmLegalEntityScopeValidator.cs:44`)
forwards the **caller's** token to MDM `GET api/legal-entities/{id}/lookup-validation`
(`services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/LegalEntitiesController.cs:48`,
`[HasPermission("mdm.legal-entities.read")]`). A SupplyChain user without that MDM key gets 403 → `Unavailable` → 503
on every create (Carrier/Load/Return/Claim). Option 1 would have widened every tenant Admin; it was never applied.

What MDM checks on that call: the entity exists **in the token's tenant** and is Active (TenantFilter + handler). It
does **not** check the user's own LE rights (that is MOD-0018-FU21, unmerged). So moving to a service identity does
not drop a user-level check that exists today.

## 2 · The pattern already on main (reuse, do not invent)

| piece | where | what it does |
|---|---|---|
| Auth service-token issue | `POST /api/internal/v1/auth/service-tokens/issue` (used by MDM, `AuthTrustedSourceAuditServiceIdentityProvider.cs:18`) | a registered client (ClientId + secret) gets a **tenant-scoped**, **single-audience** JWT (`actor_type`, `service_name`, `tenant_id`, `aud`, short expiry) |
| Client registry | `ServiceClientIdentity` (`ClientCode`, `ServiceName`, **one `AllowedAudience`**, credential + rotation, revocation) and `ServiceClientTenantGrant` | which service may get a token, for which tenant, for which audience |
| Caller side | `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Audit/AuthTrustedSourceAuditServiceIdentityProvider.cs` | acquires + caches the token per tenant/audience, validates its facts, rotates secrets |
| Receiver side | `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedServiceTokenValidationExtensions.cs` | one authentication scheme per audience (`TRUSTED_AUDIT_SOURCE_INGEST`, `TRUSTED_WORKFLOW_CONSUMER`, `TRUSTED_REFERENCE_DATA_CONSUMER`), required claims, required `service_name`; a **delegated-user** variant (`X-Delegated-Authorization`) for workflow |

R-2b (PR #134) removed the earlier Platform→MDM internal LE path because it had no production caller; it did not
remove this Auth-issued service-token pattern.

## 3 · What Option 2 needs (files by service; each other service is XMC-001)

| service (owner) | change |
|---|---|
| **AuthService** (Auth owner) | register a `Diten.SupplyChain` service client with `AllowedAudience` = a new MDM audience (proposal: `TRUSTED_LEGAL_ENTITY_REFERENCE_CONSUMER`); tenant grants for the tenants that use SupplyChain — **how tenant grants are provisioned for every tenant is an open ops question** |
| **MdmService** (MDM owner) | a receiver scheme for that audience (same shape as Platform's), `service_name` must be `Diten.SupplyChain`, and `lookup-validation` accepts **either** the user permission **or** that service token; nothing else in MDM opens to it |
| **SupplyChainService** (SCE, ours) | a token provider modelled on MDM's provider; `MdmLegalEntityScopeValidator` sends the service token + `X-Tenant-Id`; the 403/404/503 mapping stays as it is |
| **Gateway** (integration-agent only) | none if the call stays internal service-to-service; if it keeps going through the gateway, the route must pass the service token unchanged |
| **Config / ops** | ClientId + secret per environment (never in appsettings), rotation, the Auth base URL |

Option to decide with it — **delegated user**: send the user's token in `X-Delegated-Authorization` beside the service
token (the workflow pattern). It keeps the user in the audit trail; MDM still answers only tenant + Active.

## 4 · Tests that would prove it

- MDM: lookup-validation → 200 with a valid SupplyChain service token for tenant A; 404 for an LE of tenant B (no
  existence leak); 401 for a wrong audience, wrong `service_name`, expired or revoked token; the user path unchanged.
- SupplyChain: a create by a user **without** `mdm.legal-entities.read` → 201 (the BL-652 case); MDM down → 503,
  zero writes; foreign LE → the existing refusal.
- Auth: the client can issue only its own audience and only for granted tenants.

## 5 · Decisions needed

1. **Option 2 now, instead of Option 1?** (recommended: yes — Option 1 was never applied, and every temporary widening
   so far left clean-up debt). If yes, record that the 2026-11-03 Option 1 expiry is moot.
2. **Owners:** Auth owner and MDM owner consent (XMC-001); who builds the Auth and MDM parts.
3. **Delegated user:** yes / no.
4. **Tenant-grant provisioning** for the SupplyChain client: per tenant on activation, or another mechanism.
5. **Interim until Option 2 ships:** keep the current state (SupplyChain creates need `mdm.legal-entities.read`
   on the user's role) — no widening.

## 6 · Not measured

How existing clients receive tenant grants; whether SupplyChain calls MDM through the gateway or directly in each
environment; the exact MDM authorization pipeline for mixing a user policy and a service scheme on one endpoint.
