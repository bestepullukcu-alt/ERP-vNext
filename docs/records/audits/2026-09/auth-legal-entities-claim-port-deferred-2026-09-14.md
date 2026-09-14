# Auth — `legal_entities` JWT claim foundation — GAP (deferred port)

**Date:** 2026-09-14 · **Status:** OPEN (backlog) · **Severity:** Medium · **Blocks release:** No
**Owner:** Ali · **Source:** `origin/hr-future` (backup branch) · **Target:** NOT ported to `main` (deliberately)

## Context

`hr-future` is a parallel, unrelated-root branch (no common ancestor with `main`). Its valuable
non-auth work is being recovered onto `feat/hr-recovery-from-hrfuture` off current `main`. **The
auth/token portion is intentionally excluded from that recovery** — `main`'s auth/token contract
must stay exactly as it is. This record captures what was built on `hr-future`, why it is not being
ported, and who owns closing the gap.

## (a) What `hr-future` did

`hr-future` added a `legal_entities` JWT claim so tokens carry the user's assigned legal entities,
enabling per-legal-entity data scoping (`X-Legal-Entity-Id`, roll-up reads, fail-closed 403) across
the services. The auth-side foundation spans:

- **New (additive):**
  - `Domain/Entities/UserLegalEntityAssignment.cs` — assignment entity (TenantId, UserId, LegalEntityId).
  - `Application/Common/Interfaces/IUserLegalEntityAssignmentRepository.cs` + `Persistence/Repositories/UserLegalEntityAssignmentRepository.cs` — `GetByUserIdAsync`.
- **Modified auth files (would change `main`'s token):**
  - `Infrastructure/Services/TokenService.cs` — emits the `legal_entities` claim (CSV of GUIDs) + a new `GenerateAccessToken(user, roles, permissions, legalEntities, expiresInMinutes)` overload.
  - `Application/Common/Interfaces/ITokenService.cs` — declares that overload.
  - `.../Features/Auth/Handlers/CommandHandlers/LoginCommandHandler.cs` — fetches the user's assignments and passes them into the token.
  - `.../Features/Auth/Handlers/CommandHandlers/RefreshTokenCommandHandler.cs` — the fix so a **refreshed** token keeps the `legal_entities` claim (login had it, refresh dropped it → scoped writes 403'd after a refresh). See `hr-future` commit `475ff5b9`.

## (b) Why it is deferred (NOT ported)

1. **`main`'s auth/token must not change.** Product decision: leave `main`'s authentication/token
   exactly as-is; do not touch `TokenService`, `ITokenService`, `LoginCommandHandler`,
   `RefreshTokenCommandHandler` on `main`.
2. **`main` has diverged.** On `main`, `RefreshTokenCommandHandler` builds the token from
   `effectivePermissions` (a different permission model than `hr-future`'s `permissions`), and `main`
   has **no** legal-entity assignment repository/entity and **no** `legal_entities` claim at all. A
   clean cherry-pick is impossible; adapting it would rewrite `main`'s token construction — exactly
   what must not happen without owner review.
3. **Contract change.** Adding a claim changes the token contract every downstream service reads;
   that is an owner-level decision, not a mechanical recovery port.

## (c) Source of truth

Full implementation is preserved on the backup branch **`origin/hr-future`** (pushed 2026-09-14).
Nothing is lost — it can be lifted from there when the owner integrates it.

## (d) Ownership & closure

**Ali** owns this gap. Before this capability goes live, Ali will integrate the `legal_entities`
claim foundation onto `main`'s (current, `effectivePermissions`-based) auth service and reconcile the
token change with all consumers — rather than it being force-ported here.

## Second deferred seam — HCM `EmployeeProfileProjection` ← main `HcmService` (owner: Ali)

The recovered `HumanCapitalService` (readiness) has an `EmployeeProfileProjection` and a
`SensitiveAccessDataScopeEvaluator` that, at go-live, are meant to source employee data from
main's **`Diten.HcmService`** (the employee master, 5060). During the shell port (PR-A1) the
`ISensitiveAccessDataScopeEvaluator` + its `DeferredSensitiveAccessDataScopeEvaluator` (already a
deferred stub on `hr-future`) and the `EmployeeProfileProjection` feature were removed so the shell
compiles standalone without touching `HcmService`. **Ali will wire this projection to the HcmService
employee master before go-live** (2nd PENDING seam). Source of truth: `origin/hr-future`.

## Dependent work (linked)

- **TEP legal-entity scoping rollout** (`hr-future` commit `d2ad629d`, TalentEcosystemService) and the
  HCM legal-entity scoping consume this claim at runtime (`X-Legal-Entity-Id` + effective-entity
  roll-up derived from the token). During recovery, any such piece that cannot be adapted **without**
  the token change is marked as a dependent gap and NOT ported until this foundation lands. See the
  recovery branch's per-feature notes.
