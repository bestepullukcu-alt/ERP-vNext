# MVP6-CARRIER-AUTH-LE-SUCCESSOR-01 — SOP §22

Date: 2026-09-23  
Role: orchestrator / single candidate writer  
Branch: `feature/mvp6-logistics`  
HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
Verdict: **CANDIDATE READY — IMPLEMENTATION HELD**

## 1. Purpose and authority

The predecessor artifact was applied and independently returned **REWORK**. This package does not reopen that disposition. It produces one successor candidate for:

- `GAP-CARRIER-AUTH-LE-01`: the authenticated Platform internal endpoint reached tenant-filtered repositories without establishing `ITenantContext`;
- `GAP-CARRIER-AUTH-LE-02`: Auth login and refresh have no caller bearer that Platform can forward to the protected MDM lookup endpoint.

The predecessor ten-path authorization remains evidence for its exact bytes. It does not authorize the additional Platform or MDM/security paths in this successor. The decision text in `OWNER-DECISION-TEXT.md` is therefore still required before implementation.

## 2. Exact candidate

| Artifact | SHA-256 |
|---|---|
| `successor.patch` | `e6b2a3d335ee8f76270fe6ace09793a08d69e57174874be2b2db99941b01f673` |
| `SOURCE-MANIFEST.tsv` | `275f204c29113b2bb80b9aa10c87e08b81bd2ab2c71c59a92a98d18130ccb93d` |
| `source-archive.tar.gz` | `1c497022407788a5b38f5c554c867d446cc08541cf00c0d3390704c466301846` |
| `evidence.tar.gz` | `091f31450bec79797b5faeb5d93ba8ff9bdcbd657c9d3e403339abeb41ddf00a` |

The patch contains 22 exact paths. Nine predecessor targets remain byte-identical. The predecessor internal controller is the only predecessor target intentionally superseded: `6e5687…` → `eb33b6…`.

## 3. Successor behavior

### GAP-CARRIER-AUTH-LE-01

The Platform endpoint keeps constant-time internal-key validation. Only after that check succeeds does it bind the route tenant and actor to scoped contexts, before `IDataScopeResolver` invokes tenant-filtered repositories. See candidate source:

- `InternalTenantLegalEntityScopeController.cs:35-55`
- `IInternalScopeResolutionContext.cs`
- `InternalScopeResolutionContext.cs`

Wrong or missing internal keys never bind tenant context and never call the resolver. A request does not supply or select a Legal Entity.

### GAP-CARRIER-AUTH-LE-02

The candidate adds one endpoint-local MDM read seam:

`GET /api/internal/tenants/{tenantId}/legal-entities/{legalEntityId}/reference-validation`

Platform uses it only while the authenticated Auth internal scope request is bound. The request carries a short-lived HS256 service token with exact issuer, audience, caller, key id, scope, tenant, actor and Legal Entity claims. MDM validates every value before MediatR or a tenant-filtered repository is invoked. `X-Tenant-Id` and `X-Actor-Id` must match the signed claims; neither header is authority by itself.

The scope is exactly `mdm.legal-entities.reference.validate`. The token cannot authorize other MDM reads. Disabling the identity, rotating key id/secret, invalid configuration, wrong caller, wrong key, wrong audience/scope, tenant/actor/LE mismatch, expiry or excessive lifetime fails closed. No secret is stored in this package.

Public human-call validation remains on the existing endpoint and caller bearer. Global `/api/internal` tenant bypass, MDM global authorization, Carrier UI 403 handling and Supplier behavior are unchanged.

## 4. Verification performed

All work ran in `/private/tmp/mvp6-carrier-auth-le-successor-01`; production source was not changed.

| Check | Result |
|---|---|
| Previous ten target chain | 9 unchanged; internal controller intentionally superseded |
| `git apply --check` against current source preimages | PASS |
| Disposable patch apply and target hashes | 22/22 PASS |
| Auth API build | PASS, 0 errors |
| Platform API build | PASS, 0 errors |
| MDM API build | PASS, 0 errors |
| Platform focused tests | 8/8 PASS |
| MDM service-identity tests | 7/7 PASS |

Build warnings are existing nullable/obsolete warnings and `NU1900` vulnerability-feed availability warnings; no zero-warning claim is made. The candidate tests cover key ordering, no-read on wrong key, exact service-token claims, narrow internal route, fail-before-wire configuration behavior, wrong signing key/caller/scope, tenant/actor/LE mismatch, disabled identity and excessive lifetime.

The predecessor runtime RED remains preserved: correct internal key returned HTTP 400 because tenant context was unresolved, while wrong key returned 401. It is not relabeled as successor runtime evidence.

## 5. Acceptance boundary

`ACCEPTANCE.tsv` is controlling. Candidate apply/build and executable E1/E2 checks pass. Full successor runtime remains **NOT RUN** because the new Platform and MDM/security seam is not yet owner-authorized or activated. In particular, zero/multiple/inactive/revoked scope, real dependency failure, login, refresh, MFA and forced-password issuance must be executed by the authorized implementation lane and independently reproduced.

Exactly-one active authorized LE remains the only claim-producing outcome. Zero, multiple, inactive, revoked, unavailable or invalid results omit `legal_entity_id`; no stale claim is copied.

## 6. Required next gate

1. Obtain the exact Platform/Auth and MDM/security owner decision in `OWNER-DECISION-TEXT.md`.
2. Apply this exact patch in an isolated checkout; configure matching ephemeral service identity through secret-safe environment configuration.
3. Execute the full matrix and produce a writer handoff.
4. Use `INDEPENDENT-VER-PROMPT-v1.0.md` with a different verifier.

Candidate PASS is not implementation approval, Carrier E2E acceptance, Supplier concurrence or rollout authority.

## 7. Repository preservation

The existing dirty repository was preserved. This lane added only this audit directory. It did not alter Auth, Platform, MDM, Carrier, gateway, permission, contract, pack, `.antigravity`, or other lanes. No commit, push, stash or rollout occurred.
