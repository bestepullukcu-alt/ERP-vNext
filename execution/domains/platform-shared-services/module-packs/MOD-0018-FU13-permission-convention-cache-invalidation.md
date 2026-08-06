---
id: MOD-0018-FU13
name: Permission Convention + Cache Invalidation Events
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: GlobalEntity
status: review
owner: platform-team
branch: feature/governance/access-governance-execution
started: 2026-06-10
target: ""
form_field_count: 0
---

# MOD-0018-FU13 — Permission Convention + Cache Invalidation Events

> **Historical ready-for-dev note.** This pack was authored + reviewed **before** any cache-invalidation
> runtime work begins (Access Governance step **AG-STEP-010**). It is grounded on read-only repo evidence (HEAD
> `050dba4`). Authoring, decision-lock revision, and `draft → ready-for-dev` promotion made **no** runtime, seed,
> migration, test, frontend, gateway, `.antigravity`, registry, or roadmap change. **All three design decisions are
> LOCKED (§5, OD-FU13-01/02/03) from repo evidence — no open decision remains.**

> **Implementation reconciliation (2026-08-06, local):** Current branch contains FU13 Groups A-C implementation
> evidence: Platform per-instance temporary endpoint registration for `EntitlementCacheInvalidationConsumer`; AuthService
> user-role removal refresh-token revoke; AuthService role-permission removal holder lookup + per-holder refresh-token
> revoke. Existing validation evidence is recorded in §16. Frontmatter is now `review`, not `done`, because the live
> 2-instance RabbitMQ fan-out proof remains open.

> **Identity (DCP-002, proven).** Canonical ID `MOD-0018-FU13`, canonical name **Permission Convention + Cache
> Invalidation Events**, slug `permission-convention-cache-invalidation`, parent **MOD-0018** (RBAC / ABAC
> Authorization), owner `platform-shared-services`, registry status `review / pending-smoke`. Verified fail-closed with
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0018-FU13 --name "Permission Convention + Cache Invalidation Events" --parent MOD-0018`
> → **exit 0** (`OK MOD-0018-FU13: proven against Blueprint/registry`); `--check-all` → exit 0, 0 hard violations. The
> deprecated shorthand alias **`FU13`** must **not** be used as a standalone ID — use the parent-prefixed
> `MOD-0018-FU13`. No new `MOD-xxxx` is minted by this pack.

> **Scope of the two halves.** The canonical name pairs **"Permission Convention"** (already delivered: PKS-001 via
> AG-STEP-004 + the migration via AG-STEP-004B) with **"Cache Invalidation Events"** (this pack's subject, AG-STEP-010).
> FU13 here covers only the **cache-invalidation** half; the permission-convention half is referenced, not re-opened.

> **Golden Reference decision.** Backend / shared-runtime authorization-cache slice, not a UI/DataTable module.
> `shell: none`, `golden_reference: none`, `form_field_count: 0`. Razor layout, DataTable verifier, RESX and the
> frontend file set are N/A.

> **entity_base rationale.** Frontmatter requires `entity_base`; FU13 adds **no persisted entity**. `GlobalEntity` is
> recorded to match the MOD-0018 / FU12 / FU15 sibling convention. FU13 owns no aggregate; it wires event-driven
> eviction over existing caches and (pending §5) a new authorization-change event contract.

---

## 1. Module Summary

FU13 defines the **cache-invalidation contract** for the authorization runtime: which caches exist, what stale data
they can hold, which domain changes must evict them, and the fail-closed behavior when invalidation is delayed or
fails. It does **not** introduce a new cache; it standardizes eviction over the **already-proven** event-driven
invalidation seam and identifies the one genuine gap (authorization-claim staleness across the JWT lifetime).

The goal is that a change to a user's effective access (role-permission, user-role, org/position assignment, hierarchy,
or legal-entity referenceability) becomes effective within a **bounded, documented** window, and that no stale cache
ever **widens** access (fail-closed).

---

## 2. Ownership and Boundaries

- **Owner service:** `Diten.Platform` (consumes events, owns the authorization/entitlement/scope caches).
- **Producer service:** `Diten.AuthService` (owns Users/Roles/RolePermission — the role-permission & user-role change
  surface) and `Diten.Platform` MOD-0288 (owns OrganizationUnit / Position / PositionAssignment — the data-scope
  change surface).
- **Out of scope:** EnterpriseStrategy (separate stack), MDM (only LegalEntity referenceability, consumed read-only
  via MOD-0220), frontend (UX-only visibility; never an enforcement cache).

---

## 3. Cache Types and Owners (repo-proven)

| # | Cache | Type / owner (file) | What it holds | Lifetime | Existing invalidation |
|---|---|---|---|---|---|
| C1 | **Entitlement cache** | `EntitlementCacheService` (`IMemoryCache`, `Diten.Platform.Application/Services/EntitlementCacheService.cs`) — `GetOrCreateModule/FeatureAsync`, `EvictModule/Feature/Tenant`, keys `BuildModuleKey`/`BuildFeatureKey` | per-tenant module/feature entitlement results | TTL via `EntitlementCacheOptions` | **✅ event-driven** — `EntitlementCacheInvalidationConsumer` (MassTransit `IConsumer<EventTransportMessage>`) evicts `EvictTenant` on 6 events: `TenantEntitlementAdded/Enabled/Disabled/ExpiryUpdated/OverrideRemoved/SubscriptionChanged V1`; idempotent via `ConsumedEventStore.ExecuteOnceAsync`; fail-safe deserialize |
| C2 | **Lookup memory cache** | `PlatformLookupMemoryCache` (`Diten.Platform.Infrastructure/Services/`) | lookup option lists | in-memory (TTL confirmed at impl time) | none authorization-specific — lookups are reference data, **not** an enforcement grant; fan-out (OD-FU13-02) applies if ever invalidated |
| C3 | **Data-scope** (`EffectiveScopes`) | `OrgDataScopeResolver` (`Diten.Platform.Application/Authorization/OrgDataScopeResolver.cs`) | OrgUnit/Position/ManagerChain/LegalEntity scope set | **request-only** — no cross-request cache; re-queries repos each resolve | **N/A by construction** — recomputed per request |
| C4 | **Request authorization context** | `JwtTenantAuthorizationContext` (FU12, `Diten.Platform.Infrastructure/Authorization/`) | hydrated org fields + scopes for the current request | **once-per-request, memoized, dies at request end** | **N/A** — no cross-request staleness |
| C5 | **JWT access token claims** | issued by `Diten.AuthService` `TokenService` (`AccessTokenExpirationMinutes = 15`) | `permission`, `role`, `actor_type`, org claims baked at issuance | **immutable until refresh / expiry (≤15 min)** | Historical gap at authoring; current branch has Groups B-C refresh-token revoke evidence — see §16 |

**Key consequence.** C3/C4 are request-scoped → org/position/hierarchy/legal-entity changes are picked up on the
**next request** automatically (no cross-request cache to evict). C1 is already event-invalidated. **C5 is the only
real staleness surface**: a role-permission or user-role change does **not** affect an already-issued JWT until it
expires (≤15 min) or is refreshed — the `permission`/`role` claims are baked in at issuance.

---

## 4. Invalidation Triggers → Cache Mapping

| Trigger (domain change) | Owning surface | Stale cache | Required action |
|---|---|---|---|
| **role-permission change** (`Roles.AssignPermission`/`RevokePermission`) | AuthService `RolePermission` | **C5** (JWT `permission` claim) | propagate within a bounded window — §5 OD-FU13-01 (token TTL vs. revocation vs. change-event) |
| **user-role change** (`Users.AssignRole`/`RevokeRole`) | AuthService `UserRole` | **C5** (JWT `role`/`permission`) | as above (same lifecycle surface) |
| **position-assignment change** (create/expire/delete) | Platform MOD-0288 | **C3/C4** (data-scope) | none beyond next-request recompute — **confirm no longer-lived scope cache is added** |
| **org-unit / position hierarchy change** (reparent/archive) | Platform MOD-0288 | **C3/C4** (subtree/manager-chain) | next-request recompute; if a scope cache is later introduced, evict on these events |
| **legal-entity referenceability change** (activate/archive/delete) | MDM (MOD-0220) | **C3** (LegalEntity scope) | resolver already does a **live, fail-closed** MOD-0220 lookup per request → auto-fresh; no cache to evict |
| **entitlement change** (module/feature/subscription) | Platform entitlement | **C1** | **already handled** by `EntitlementCacheInvalidationConsumer` (extend the event list only if a new entitlement trigger appears) |

---

## 5. Locked Decisions (`OD-FU13-*`) — resolved from repo evidence (AG-STEP-010 open-decision audit)

> **All FU13 design decisions are LOCKED. No open decision remains.** Each lock below is grounded in read-only repo
> evidence at HEAD `050dba4`.

### OD-FU13-01 — Authorization-claim (C5) staleness → **LOCKED: bounded token TTL + revoke-on-privilege-removal (B-Option 1)**
- **Repo evidence.** Access-token TTL = **15 min** (`JwtSettings.AccessTokenExpirationMinutes = 15`). The **refresh
  path re-reads the current role-permissions** (`RefreshTokenCommandHandler` → `GetRolesByUserAsync` →
  `GetPermissionsByRolesAsync` → `GenerateAccessToken`). **Single-user refresh-token revocation already exists**
  (`RefreshToken.Revoke`, `IRefreshTokenRepository.RevokeAllByUserAsync(userId, tenantId, ct)`, reused in 9 sites). The
  **users-by-role resolution seam was missing at authoring** — `IUserRoleRepository` had only `GetRolesByUserAsync` /
  `AssignAsync` / `RevokeAsync` / `ExistsAsync`, **no by-role query**. Current branch has the tenant-scoped
  `GetUserIdsByRoleAsync` seam recorded in §16. There is **no** enforcement-side deny-list/blacklist.
- **Lock.** Maximum authorization-staleness window = **≤15-min access-token TTL**; grants re-evaluate at the next refresh.
  **v1 closes the refresh path on a *privilege removal* via existing/new repository methods (no events, no deny-list):**
  - **User-role removal** (`RevokeRoleCommandHandler`, single user): after `_userRoleRepository.RevokeAsync(...)`, call
    the **existing** `IRefreshTokenRepository.RevokeAllByUserAsync(request.UserId, _tenantContext.TenantId, ct)`.
  - **Role-permission removal** (`RevokePermissionCommandHandler`, all role holders): add the **new tenant-scoped seam
    `IUserRoleRepository.GetUserIdsByRoleAsync(roleId, tenantId, ct)`** (+ Mongo impl); after
    `_rolePermissionRepository.RevokeAsync(...)`, loop the affected distinct userIds and call `RevokeAllByUserAsync` per
    holder. **(B-Option 1.)**
- **Out of revoke scope.** User-role **assign** and role-permission **assign** *grant/widen* access and are **not** FU13
  revoke targets — a new grant is naturally seen via the next token/refresh.
- **Explicitly deferred / forbidden (NOT in v1):** cross-service `RolePermissionChangedV1` / `UserRoleChangedV1`
  change-events, enforcement hot-path **deny-list / blacklist**, and any **per-request / per-subject revocation cache**.
  (B-Option 3 — TTL-only with *no* immediate revoke — is **rejected** as the normal plan; the ≤15-min TTL is only the
  **bounded fallback** if a revoke fan-out call fails.)
- **Security rationale.** The window is bounded (≤15 min, never indefinite); a revoked refresh token cannot mint a new
  access token → fail-closed. Resolving holders via a tenant-scoped repository method (not a cross-service event or
  hot-path deny-list) keeps the change in-AuthService, transactional, and low-latency.

### OD-FU13-02 — In-memory cache horizon → **LOCKED: A-Option 1 — per-instance temporary endpoint for the entitlement invalidation consumer only**
- **Repo evidence.** Eventing uses `cfg.ConfigureEndpoints(context)` = MassTransit **default one-queue-per-consumer =
  competing-consumer**. Caches `EntitlementCacheService` and `PlatformLookupMemoryCache` are **`IMemoryCache`** (per-
  instance); **no `IDistributedCache`/Redis** anywhere; entitlement TTL = **300s** (`EntitlementCacheOptions.CacheTtlSeconds = 300`).
  4 consumers are registered (`TenantActivatedV1`, `TenantLifecycleAudit`, `TenantLifecycleNotification`,
  `EntitlementCacheInvalidation`); the dev path uses a custom per-process `InMemoryEventBus` (single-instance). Multi-
  instance RabbitMQ ⇒ only the one instance that dequeues an event evicts its local cache; others stay stale ≤300s.
- **Lock (A-Option 1).** `EntitlementCacheService` and `PlatformLookupMemoryCache` **remain `IMemoryCache`**. A **per-
  instance unique receive endpoint is created for `EntitlementCacheInvalidationConsumer` ONLY**, with these properties:
  - used on the **RabbitMQ** path only;
  - named by a **unique instance identity = a single process-lifetime `Guid` generated once at startup**;
  - **non-durable**, **auto-delete**, **temporary / instance-lifetime scoped** (cleaned on disconnect; no queue
    accumulation; TTL covers any restart gap);
  so **every** running instance receives the fanout and evicts its local cache.
- **Other consumers stay competing-consumer.** `TenantActivatedV1`, tenant-lifecycle **audit**, and tenant-lifecycle
  **notification** **keep the default `ConfigureEndpoints` shared-queue topology** — they must run **once cluster-wide**
  (a per-instance endpoint would duplicate audits/notifications). **A global `SetEndpointNameFormatter` or applying an
  instance suffix to all consumers is forbidden.**
- **Dev transport unchanged.** The custom in-memory dev transport (`InMemoryEventBus`) is **not modified** — it stays the
  single-process dev path.
- **Explicitly deferred (NOT in v1):** Redis / `IDistributedCache`. The cached data is recomputable, so only the
  *eviction signal* must reach all nodes — not the cache contents.
- **Rollout gate.** **Per-instance fan-out is a mandatory gate before horizontal scaling** of `Diten.Platform`. Single-
  instance today ⇒ latent (no current exposure), but fan-out must land before any multi-replica deployment.
- **`PlatformLookupMemoryCache` — v1 scope clarification.** It is a **reference-data** cache, **not** an authorization
  grant cache; it has **no eviction consumer and no `Remove`/`Evict` seam** today. **FU13 v1 adds no lookup invalidation
  consumer.** If lookup invalidation is ever added, it falls under the **same per-instance fan-out rule** — and **no new
  event contract is invented** for it.
- **Security/correctness rationale.** Competing-consumer + per-instance `IMemoryCache` can leave stale grants on N-1
  instances up to TTL; per-consumer fan-out closes this deterministically with no external dependency and without
  duplicating once-only side-effects.

### OD-FU13-03 — Cross-request data-scope cache → **LOCKED: none added in v1; request-fresh only + future-guard**
- **Repo evidence.** `OrgDataScopeResolver` (0 cache fields) and `JwtTenantAuthorizationContext` (0 cross-request cache)
  are **request-only**; org/position/assignment/hierarchy changes are auto-fresh on the next request; legal-entity
  referenceability is resolved via the live, fail-closed MOD-0220 lookup per request.
- **Lock.** **No cross-request data-scope cache in FU13 v1.** `OrgDataScopeResolver` continues to run **request-fresh**;
  FU12 `JwtTenantAuthorizationContext` keeps **only once-per-request memoization** (no cross-request cache).
- **Future-guard (locked rule).** If any later step introduces a cross-request scope cache, it **MUST**: (a) **evict on
  org-unit / position / position-assignment / hierarchy change** events; (b) **preserve the live fail-closed MOD-0220
  lookup** for legal-entity referenceability (never cache a *positive* referenceability across its change); (c) **never
  auto-open on a cache miss** (a miss ⇒ re-evaluate, never allow); (d) **preserve empty-scope ⇒ deny** (BME-001 / FU15).
- **Security rationale.** The simplest fail-closed posture is no scope cache (no staleness surface). Avoid premature
  optimization — request-memoization already bounds per-request cost.

---

## 6. Fail-Closed Behavior (mandatory)

1. **No stale cache may widen access.** A delayed/missed invalidation must never grant access that a fresh evaluation
   would deny. C1 entitlement misses are bounded by TTL and never auto-grant beyond the cached *positive*; the
   enforcement path must treat an unknown/expired cache entry as **re-evaluate**, not **allow**.
2. **Invalidation failure is safe.** The existing consumer is **idempotent** (`ConsumedEventStore.ExecuteOnceAsync`)
   and **fail-safe** on bad payloads (logs `*_payload_invalid`, returns without crashing). A dropped event leaves the
   cache stale only until TTL — it must not extend grants. New FU13 consumers follow the same idempotent + fail-safe
   shape.
3. **C5 window is documented and bounded.** Max authorization-staleness window = **≤15-min access-token TTL**; never
   "indefinite until logout."
4. **Data-scope stays request-fresh.** C3/C4 must remain request-recomputed (or evicted on §4 triggers if cached);
   empty/invalid scope ⇒ **no access** (FU15 / BME-001 C-rules), never auto-open.
5. **Revoke-after-persist failure (privilege removal).** If a refresh-token **revoke** call fails **after** the
   role-permission / user-role removal has been persisted: the **grant is NOT re-opened** (the removal stands); the
   failure is **logged visibly**; the command returns **failed** *or* a clearly-marked **safe partial-failure**; the
   remaining risk is bounded by the **≤15-min access-token TTL** (the stale token still expires). Never roll the grant
   back to "open" to make the revoke succeed.
6. **Tenant isolation in the affected-user lookup.** `GetUserIdsByRoleAsync` is **always** scoped by `roleId + tenantId`;
   a cross-tenant user id can never be returned (server-side tenant scope, BME-001 C4).
7. **Distinct / empty / cancellation.** Duplicate user ids from the lookup are processed **distinctly** (each user
   revoked once); an **empty** affected-user set is a **valid no-op**; the `CancellationToken` is propagated through the
   lookup and every `RevokeAllByUserAsync` call.

---

## 7. Service Boundaries and Cross-Service Contracts (per locked decisions)

- **AuthService (C5, OD-FU13-01 = B-Option 1):** **no** new cross-service change-event in v1. AuthService closes the
  refresh path on a privilege removal **in-process**: user-role removal calls the **existing** `RevokeAllByUserAsync`;
  role-permission removal adds the **new tenant-scoped `IUserRoleRepository.GetUserIdsByRoleAsync(roleId, tenantId, ct)`**
  seam (+ Mongo impl) and loops `RevokeAllByUserAsync` per affected holder. `RolePermissionChangedV1` /
  `UserRoleChangedV1` cross-service events and any deny-list are **explicitly forbidden in v1**.
- **Platform MOD-0288 (OD-FU13-03 = no scope cache):** **no** OrganizationUnit / Position / PositionAssignment change
  events in v1 (data-scope is request-fresh). These become required **only if** a future cross-request scope cache is
  introduced (future-guard, §5 OD-FU13-03).
- **Platform invalidation consumer (OD-FU13-02 = A-Option 1 fan-out):** `EntitlementCacheInvalidationConsumer` keeps the
  proven `IConsumer<EventTransportMessage>` + `ConsumedEventStore` shape but binds a **per-instance unique, non-durable,
  auto-delete receive endpoint** (process-lifetime `Guid` identity) so eviction reaches every instance; the other 3
  consumers stay competing-consumer.
- **Contract invariants:** events carry `TenantId` + correlation/causation metadata (`EventMetadata`); consumers are
  idempotent and fail-safe; no new event contract is invented; no synchronous cross-service call on the enforcement hot path.

---

## 8. Acceptance Criteria (decisions locked — §5)

1. Every cache in §3 has a documented owner, lifetime, and invalidation rule; no enforcement cache lacks an
   invalidation story.
2. **(OD-FU13-01)** The C5 authorization-staleness window is **≤15 min** (access-token TTL), documented; a privilege
   **removal** closes the refresh path immediately — user-role removal via the existing `RevokeAllByUserAsync`,
   role-permission removal via the new `IUserRoleRepository.GetUserIdsByRoleAsync` seam looping `RevokeAllByUserAsync`
   per holder (B-Option 1). Assign/grant ops are out of revoke scope. No deny-list / cross-service event is introduced.
3. New invalidation consumers are idempotent (`ConsumedEventStore`) and fail-safe (bad-payload tolerant), matching the
   existing entitlement consumer.
4. No stale cache path can widen access (fail-closed test): a revoked grant + a stale cache entry ⇒ **deny**.
5. **(OD-FU13-03)** Data-scope remains **request-fresh** (no cross-request scope cache); empty scope ⇒ deny; the
   future-guard rule (§5 OD-FU13-03) is recorded.
6. **(OD-FU13-02)** Invalidation reaches **all running instances** via a per-instance fan-out endpoint (verified in a
   2-instance test); caches stay `IMemoryCache`; per-instance fan-out is a documented prerequisite before horizontal
   scaling. No `IDistributedCache` is introduced in v1.

## 9. Test Expectations (locked)

- Consumer unit tests: each subscribed event → correct eviction; bad payload → logged, no throw; duplicate event →
  single execution (idempotency).
- Fail-closed integration test (**mandatory**): revoked role-permission + stale cache ⇒ access denied at enforcement.
- Data-scope freshness test: assignment/hierarchy change reflected on the next request (no cross-request leakage).
- No regression in the existing 554 Platform / entitlement-consumer tests.

## 10. Failure Paths to Verify

- Event bus down / event dropped → cache stale only to TTL; **no** grant widening; recovery on next event/TTL.
- Duplicate / out-of-order events → idempotent, last-writer-safe (eviction is monotonic-safe).
- Partial multi-instance eviction (OD-FU13-02) → either fan-out or documented bounded inconsistency, never fail-open.
- Malformed payload → logged `*_payload_invalid`, consumer continues.

## 11. Rollout Order (decisions locked)

**Implementation ordering: Group A → Group B → Group C → integration audit → Group D.** Per-instance fan-out (Group A)
is a **mandatory gate before any horizontal scaling** of `Diten.Platform`. See §15 for the exact per-group surface.

---

## 12. Link to AG-STEP-011 (Explain Access, MOD-0018-FU14) — reference only

Invalidation events are a natural input to the **Explain Access** decision trace (FU14): an allow/deny explanation
should be able to state "evaluated against fresh data as of <invalidation/issuance timestamp>." **No FU14
implementation is performed here** — FU13 only ensures the events/timestamps exist to be surfaced later.

---

## 13. Out of Scope / Non-Goals

- No runtime, seed, migration, registry, roadmap, test, or `.antigravity` change by this pack (`ready-for-dev` = handoff only).
- No new `MOD-xxxx`; no EnterpriseStrategy / Slice-5B / Slice-7 interaction.
- No frontend cache (visibility is UX-only, never an enforcement surface).
- No claim-revocation deny-list / blacklist / per-request revocation cache and no cross-service role-change event are built (OD-FU13-01 = B-Option 1: existing `RevokeAllByUserAsync` for user-role removal + a new in-AuthService `IUserRoleRepository.GetUserIdsByRoleAsync` seam for role-permission removal).
- No `IDistributedCache`/Redis (OD-FU13-02 keeps `IMemoryCache` + fan-out).
- No cross-request data-scope cache (OD-FU13-03 keeps request-fresh).

---

## 14. Runtime Constraints (locked)

- **Fail-closed always.** A delayed/missed invalidation, a cache miss, an empty scope, or an event-bus outage must never
  widen access; the enforcement path re-evaluates on miss and denies on empty/invalid scope.
- **Idempotent + fail-safe consumers.** Every invalidation consumer uses `ConsumedEventStore.ExecuteOnceAsync` and
  tolerates malformed payloads (log + continue), matching `EntitlementCacheInvalidationConsumer`.
- **Bounded staleness.** C5 ≤ 15 min (token TTL); C1 ≤ 300s (entitlement TTL) — both documented, neither indefinite.
- **No synchronous cross-service call on the enforcement hot path.** Propagation is event-driven (Outbox + consumer).
- **Per-instance fan-out** for invalidation consumers (no shared competing-consumer queue for cache eviction).

---

## 15. Implementation Scope & Checklist (v1) — exact atomic groups

**Scope summary.** Four atomic groups: (A) Platform per-instance fan-out for the entitlement invalidation consumer;
(B) AuthService single-user revoke-on-role-removal; (C) AuthService role-permission revoke via a new users-by-role seam;
(D) governance reconciliation. No new event contracts, no deny-list, no distributed cache, no scope cache.

### Group A — Platform fan-out *(OD-FU13-02)*
- **Goal:** per-instance temporary receive endpoint for the **entitlement invalidation consumer only**.
- **Surface:** `Diten.Platform.Infrastructure/DependencyInjection.cs` (+ a small, testable instance-identity helper /
  options if needed); Platform consumer-topology tests.
- **Gate:** existing eviction; malformed payload fail-safe; idempotency; **2-instance fan-out — both local caches evict**;
  the other 3 consumers (`TenantActivatedV1`, lifecycle audit, lifecycle notification) **do not run per-instance**
  (no duplicate side-effects); Platform full suite **≥ 554 passed, 0 failed**.
- **Rollback:** revert the infra config + tests.

### Group B — AuthService user-role revoke *(OD-FU13-01)*
- **Goal:** revoke a single user's refresh tokens after their role is removed.
- **Surface:** `RevokeRoleCommandHandler.cs` (inject `IRefreshTokenRepository`; call
  `RevokeAllByUserAsync(request.UserId, _tenantContext.TenantId, ct)` after `RevokeAsync`) + its AuthService test.
- **Gate:** `RevokeAllByUserAsync` called for the revoked user; unrelated user unaffected; tenant id propagated;
  AuthService suite green.
- **Rollback:** revert the handler + test.

### Group C — AuthService role-permission revoke *(OD-FU13-01, B-Option 1)*
- **Goal:** revoke all role holders' refresh tokens after a role-permission is removed.
- **Surface:** `IUserRoleRepository.cs` (new `GetUserIdsByRoleAsync(roleId, tenantId, ct)`) + its Mongo repository impl +
  `RevokePermissionCommandHandler.cs` (inject `IUserRoleRepository` + `IRefreshTokenRepository`; after `RevokeAsync`,
  resolve affected distinct userIds and loop `RevokeAllByUserAsync`) + AuthService tests.
- **Gate:** tenant-scoped affected-user lookup (`roleId + tenantId`); **all distinct holders revoked**; unrelated users
  unaffected; **empty role-holder set = valid no-op**; repository + handler tests green; AuthService suite green.
- **Rollback:** revert the seam + impl + handler + tests.

### Group D — governance reconciliation *(last)*
- **After** Groups A–C land and an integration audit PASSes: a **separate docs-only commit** updating the pack
  implementation note + the roadmap AG-STEP-010 row.

**Ordering:** Group A → Group B → Group C → integration audit → Group D.

> **No open decision remains.** OD-FU13-01/02/03 are all locked (§5) from repo evidence; implementation evidence is now
> recorded locally. AG-STEP-011 Explain Access
> (MOD-0018-FU14) remains a **downstream reference only** (§12) — not implemented here. Slice 5B / Slice 7 blockers and
> the AG-STEP-008 roadmap row are untouched.

---

## 16. Runtime implementation evidence — recorded locally

> **Pack status note.** Frontmatter is **`review`**. Groups A-C implementation evidence exists, but `done` is blocked
> until the live 2-instance RabbitMQ fan-out proof is completed.

**Integration audit: PASS** (read-only audit @ HEAD `34e38cc`). Current local branch contains the implementation
evidence below; this reconciliation does not modify runtime code.

**Runtime commits (Groups A → B → C → integration audit → this Group D):**
- `3a8f9dd` — **Group A:** Platform per-instance entitlement-cache invalidation fan-out.
- `a9ad416` — **Group B:** AuthService user-role removal → refresh-token revoke.
- `34e38cc` — **Group C:** AuthService role-permission removal → tenant-scoped holder lookup + refresh-token revoke.

**Group A result.** Only `EntitlementCacheInvalidationConsumer` gets a process-lifetime-GUID temporary
(non-durable, auto-delete) receive endpoint; the other 3 consumers stay shared competing-consumer; **no
`SetEndpointNameFormatter`, no Redis / `IDistributedCache`**; the custom in-memory dev transport is unchanged.

**Group B result.** After the user-role `RevokeAsync`, the existing `RevokeAllByUserAsync(userId, tenantId, ct)` is
called; tenant comes only from `ITenantContext`; a refresh-token revoke failure is **not swallowed**.

**Group C result.** New seam `GetUserIdsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct)`; the Mongo query
is **server-side** (`RoleId == roleId && TenantId == tenantId && IsDeleted == false`, `UserId` projection, `Distinct()`);
after the role-permission `RevokeAsync`, every distinct holder is revoked via `RevokeAllByUserAsync`; an empty holder
list is a **valid no-op**; **sequential fail-fast** preserved.

**No-op / future guard.** `OrgDataScopeResolver` added no cross-request cache; `JwtTenantAuthorizationContext` stays
once-per-request memoization; LegalEntity referenceability keeps the live fail-closed lookup. FU13 v1 made **no** change
on these surfaces (OD-FU13-03).

**Build & test results:** `Diten.Platform.API` **0 errors**; `Diten.Platform.Application.Tests` **557 passed, 0
failed**; `Diten.Platform.Eventing.Tests` **56 passed, 0 failed, 3 pre-existing skipped**; `Diten.AuthService`
Application + Persistence + API **0 errors**; `Diten.AuthService.Application.Tests` **30 passed, 0 failed**.

### Open rollout gate — NOT completed
A mandatory **manual/integration verification before horizontal scaling** of `Diten.Platform`: a real RabbitMQ broker +
**2 Platform instances** → one published invalidation event evicts **both** instances' local `IMemoryCache`; the
entitlement consumer is on its instance-specific temporary endpoint while the other 3 share their queue; **no duplicate
binding**. This is **not** an integration-branch runtime-completion blocker, but it **is** a horizontal-scaling rollout
blocker, and it is **not yet verified**.

### Bounded fallback note
A removed permission grant is **not re-opened**; earlier successful token revokes are **not** rolled back. Because of
the **sequential fail-fast** model, if a per-user revoke fails, some holders after it may not be revoked in that call —
their access tokens can remain valid for **up to the ≤15-min access-token TTL**. This is a **bounded fallback**, not an
absolute immediate close for every holder.

### Performance note
`GetUserIdsByRoleAsync` is **server-side filtered + projected** (no full-collection scan). A composite Mongo index on
`{ RoleId, TenantId, IsDeleted }` is an optional future optimization — **not required for correctness**; **no
index/migration was written** in this implementation.
