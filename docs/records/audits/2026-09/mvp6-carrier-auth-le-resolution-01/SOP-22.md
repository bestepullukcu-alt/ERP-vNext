# MVP6-CARRIER-AUTH-LE-RESOLUTION-01 — SOP §22 handoff

Date: 2026-09-23  
Role: Control Tower / orchestrator inspection  
Branch: `feature/mvp6-logistics`  
HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`

## Verdict

**BLOCKED for real Auth-issued Carrier E2E; root cause confirmed and a reviewable candidate is ready.**

The missing `legal_entity_id` is a production issuance gap, not a Carrier UI defect, fixture toggle, or incorrect use of an existing Auth flow. Auth has no authoritative Legal Entity input and never emits the claim. Platform already owns a fail-closed actor-to-Legal-Entity resolver, but Auth has no approved endpoint/client seam to use it. The accepted Carrier UI and middleware correctly remain fail-closed.

The exact cross-service candidate builds cleanly and passes static scope/fail-closed checks, but it is not applied. Carrier UI authority does not cover the proposed Auth and Platform production changes. Consequently no new real Auth-issued E2E PASS is claimed.

## Baseline and preservation

- Carrier final UI manifest: `e96578d0e874e8796469ee763aa9b6e723c7f0c9a966b5fe8a46b535279b26cb`; prior independent record reports 21/21 exact.
- All artifacts listed by Carrier VER `ARTIFACTS.sha256` revalidated successfully; see `carrier-ver-artifacts-check.log`, exit `0`.
- The working tree was already dirty with parallel MVP-6 work. This lane did not alter accepted Carrier, Auth, Platform, Program.cs, gateway, permission or Lane B source.
- All ten candidate paths still match their recorded preimages (including the three expected-absent additions); see `source-no-change.log`.
- Source candidate was created and built only in `/private/tmp/mvp6-carrier-auth-le-candidate-V7EbGR/repo`.

## Root cause

The complete path:line table is in `ROOT-CAUSE.tsv`.

1. Auth tenant token issuance emits `sub`, user identity, `actor_type`, `tenant_id`, and permissions, but no `legal_entity_id` (`TokenService.cs:25-43`).
2. Auth's `User` and `TenantUserMembership` carry no Legal Entity authority (`User.cs:25-42`; `TenantUserMembership.cs:14-16`).
3. Login calls token generation after role/permission resolution without any LE selection or authoritative lookup (`LoginCommandHandler.cs:169-181`). MFA, refresh and forced-password issuance follow the same shape.
4. Platform's `OrgDataScopeResolver` resolves active assignments and revalidates candidate Legal Entities against MDM fail-closed (`OrgDataScopeResolver.cs:46-111,228-272`). This authority is not exposed to Auth.
5. Carrier requires the signed claim and matching tenant/LE headers and rejects missing scope with 403 (`CarrierContextMiddleware.cs:55-76`). That behavior is correct and unchanged.

Classification: **missing production behavior and cross-service authority seam**.

## Candidate disposition

Candidate patch SHA-256: `6559c94835814ab65dc50a05cca32db905e31e637f9a15aeeeb033e9850c83e6`  
Source manifest SHA-256: `84ce27833951bfed92edf46c02a0ee7c8934d1c6eff006e665d3ddaf09bf8b23`

The candidate changes ten exact paths:

- adds an Auth client for a Platform internal Legal Entity scope read;
- adds a Platform internal endpoint backed by existing `IDataScopeResolver`;
- accepts only exactly one distinct, active, MDM-revalidated Legal Entity;
- emits one canonical UUID claim on login, MFA, refresh, and forced-password issuance;
- omits the claim for zero/multiple/invalid/unavailable results;
- reuses the existing internal API-key and correlation pattern;
- adds no client-supplied LE authority, Program.cs, gateway, permission, Carrier, contract, or UI change.

The endpoint does not select the first of multiple scopes. This avoids converting storage/order behavior into authorization.

## Security impact

- Authority remains server-side: actor assignments and revalidated Platform scopes control the claim.
- Ambiguous or unavailable authority fails closed by omitting the claim; Carrier continues to return 403.
- Refresh and other token-renewal paths re-resolve scope instead of copying a stale claim.
- No bearer token, JWT key, internal API key or connection string is stored in this package.
- The candidate introduces an Auth-to-Platform dependency at issuance time. Focused runtime verification must cover exact-one, zero, multiple, stale/archived, cross-tenant, timeout, non-2xx and refresh/revocation behavior.

## Validation

| Check | Result | Evidence |
|---|---:|---|
| Carrier VER artifacts | PASS | `carrier-ver-artifacts-check.log`, exit `0` |
| Candidate patch applies to current baseline | PASS | `git apply --check`; `candidate-validation.log` |
| Exact ten-path boundary | PASS | `candidate-validation.log` |
| Exact-one/fail-closed static rules | PASS | `candidate-validation.log` |
| Auth API candidate build | PASS | 0 warnings, 0 errors; `auth-build.log`, exit `0` |
| Platform API candidate build | PASS | 0 warnings, 0 errors; `platform-build.log`, exit `0` |
| Real checkout candidate-path preservation | PASS | 10/10 preimages/absence; `source-no-change.log` |
| Real Auth-issued Carrier E2E | BLOCKED | Production candidate is not authorized or applied |
| Independent runtime VER | NOT RUN | Must follow authorized implementation |

The candidate validation is E1/E2 only. Build and static inspection do not prove JWT issuance, Platform dependency behavior, tenant isolation, or Carrier E2E.

## Assumptions requiring owner disposition

- **ASSUMPTION-LE-01:** Automatic token binding is allowed only when the authoritative resolver yields exactly one Legal Entity.
- **ASSUMPTION-LE-02:** Zero, multiple, malformed or unavailable scope omits `legal_entity_id` rather than failing all tenant login; LE-bound modules remain inaccessible.
- **ASSUMPTION-LE-03:** The existing internal API-key channel may be reused for this read without a new public contract or gateway route.

These assumptions are encoded only in the unapplied candidate and are not approved policy.

## Required decision and next evidence

The single missing decision is in `OWNER-DECISION-TEXT.md`. Approval must bind the exact patch and manifest hashes above and cover both Auth issuance and the Platform internal authority endpoint.

After approval, a single writer may apply the candidate in an isolated checkout. Required evidence is: focused unit/security tests, exact-one/zero/multiple/failure cases, fresh Auth and Platform build, real Auth-issued token inspection without storing the token, Carrier list/create/status through the actual JWT pipeline, cross-tenant/LE rejection, refresh/MFA/forced-password behavior, and separate independent VER.

## Scope exclusions

No Carrier UI relaxation, claim fabrication, client-authoritative LE selection, Program.cs, gateway, permission expansion, public contract, migration, rollout, E5/G5, commit, push, or stash is included.

## Final state

**NOT READY for real Auth E2E.** Exact reason: the only safe production path requires an unapproved ten-path Auth/Platform change. The candidate is reviewable and technically buildable; implementation and acceptance remain separate gates.
