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

## Test coverage follow-up — TEP (owner: Ali / QA)

**Status:** OPEN (backlog) · **Blocks release:** No

The recovered `TalentEcosystemService` (TEP, `feat/hr-tep-recovery`, port 5064) currently ships
**without a test project** — the service `.sln` intentionally excludes it (the shell removed the
tests project so the service compiles standalone, mirroring the shell discipline). All 31 modules
were ported and verified only by: `dotnet build` 0-error, `GET /health` 200, and static DI-graph
completeness (every injected repository registered). **Follow-up:** restore/author a TEP test
project (unit + handler/repository integration) before go-live, or as a fast-follow after merge.
Source of the original tests: `origin/hr-future`.

> **Note on branch names:** this recovery was later split from the single
> `feat/hr-recovery-from-hrfuture` branch (now deleted) into two clean, current-`main`-based
> branches: **`feat/hr-tep-recovery`** (TEP, PR #107) and **`feat/hr-hcm-recovery`** (HCM, PR #108).
> References above to `feat/hr-recovery-from-hrfuture` should be read as those two branches.

## Frontend recovery follow-ups (owner: Ali, before go-live)

The `hr-future` frontend (`Diten.Web`) module UI for the recovered TEP/HCM services is being
ported additively onto `main` (branches `feat/hr-hcm-webui` PR #109, `feat/hr-tep-webui`). The
pages are reachable by URL and functional (they proxy to the recovered gateway routes), but two
seams remain open:

- **(a) Module Catalog nav + entitlement (module-nav-visibility-chain) — 54 modules.** `main`'s
  sidebar is data-driven from the Platform Module Catalog (`GET /api/platform/navigation/menu`),
  not a static frontend file. The ported pages will NOT appear in the sidebar until a navigation
  entry + entitlement exist in the Module Catalog for each module, and the user holds the required
  permission. This is a backend/ops wiring task, intentionally NOT part of the additive frontend
  port. Until then the pages are URL-reachable only.
- **(b) L10n completeness — 5 missing languages.** The recovered module UI ships with `en` + `tr`
  resx only (2/7). Tenant-module parity requires `fr`, `es`, `zh`, `ar`, `ru`. Missing cultures
  fall back to `en` (no crash). Author the 5 missing resx sets before go-live.
