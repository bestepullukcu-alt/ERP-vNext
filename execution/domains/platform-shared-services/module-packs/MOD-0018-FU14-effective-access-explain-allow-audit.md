---
id: MOD-0018-FU14
name: Effective Access Explain + Allow Audit
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: GlobalEntity
status: ready-for-dev
owner: platform-team
branch: feature/governance/access-governance-execution
started: 2026-06-10
target: ""
form_field_count: 0
---

# MOD-0018-FU14 — Effective Access Explain + Allow Audit

> **Ready-for-dev note.** Module-pack contract authored, decision-locked, and promoted `draft → ready-for-dev`
> **before** any Explain Access runtime work (Access Governance step **AG-STEP-011**), grounded on read-only repo
> evidence at HEAD `c035bf5`. Authoring + decision-lock + promotion made **no** runtime, seed, migration, test,
> frontend, gateway, `.antigravity`, registry, or roadmap change. **FU14 v1 has no open decision** — §9 locks
> OD-FU14-01…07 + 08A; only **OD-FU14-08B** (a non-bypass role extension) is deferred and is **not a v1 runtime
> blocker**. `ready-for-dev` authorizes implementation **planning / handoff only**; production code still passes the
> orchestrator / add-module gate. Runtime stays fail-closed throughout; **no runtime implementation begins in this
> step.**

> **Identity (DCP-002, proven).** Canonical ID `MOD-0018-FU14`, name **Effective Access Explain + Allow Audit**, slug
> `effective-access-explain-allow-audit`, parent **MOD-0018** (RBAC / ABAC Authorization), owner
> `platform-shared-services`, registry status `planned`. Verified fail-closed with
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0018-FU14 --name "Effective Access Explain + Allow Audit" --parent MOD-0018`
> (exit 0); `--check-all` exit 0, 0 hard violations. Deprecated shorthand `FU14` must not be used standalone. No new
> `MOD-xxxx` is minted.

> **Golden Reference decision.** Backend / shared-runtime diagnostic slice, not a UI/DataTable module. `shell: none`,
> `golden_reference: none`, `form_field_count: 0`. Razor layout, DataTable verifier, RESX and the frontend file set are
> N/A.

> **entity_base rationale.** Frontmatter requires `entity_base`; FU14 adds **no persisted entity** (it observes the live
> authorization chain and may reuse the existing `AuditEvent` model). `GlobalEntity` matches the MOD-0018 / FU12 / FU13 /
> FU15 sibling convention.

---

## 1. Scope split — LOCKED

The registry identity **"Effective Access Explain + Allow Audit"** has **two halves**:

| Half | Step | This pack |
|---|---|---|
| **Effective Access Explain** (decision trace) | **AG-STEP-011** | ✅ designed here (v1) |
| **Allow Audit** (audit policy for *allow* decisions) | **AG-STEP-012** (`EA OD-D`) | ❌ **out of scope** — separate EA decision |

- **AG-STEP-011 designs only "Explain Access — decision trace."** It is a **pack-first, backend-only planning step.**
- The **Allow-Audit policy** (whether/how to audit *allow* decisions; today only **deny** is logged → real sink
  MOD-0021) is **AG-STEP-012**, a separate EA decision; **it is NOT designed, implemented, or marked complete here.**
- **Runtime implementation does not begin in this step.** Explain Access is a later-gate item, **not** a
  business-domain-start blocker (roadmap §8).

---

## 2. Grounded runtime inventory (repo evidence, HEAD `c035bf5`)

Explain Access **observes** these existing chains; it **never makes or changes** an authorization decision.

### Platform (filter-based)
- `HasPermissionAttribute` (`Diten.Platform.API.Security`): unauthenticated → **deny** (`UnauthorizedResult`);
  `platform_admin` / `partner_admin` → **bypass** (with admin-lifecycle checks → `ForbidResult` on missing-email /
  not-found / deleted / inactive); else `HasPermissionClaim` → allow; else **deny** (`ForbidResult`).
- `PermissionAliasResolver.Expand` — **canonical + legacy-alias dual-read** (a canonical requirement is satisfied by the
  canonical key **or** a mapped legacy alias token).
- `JwtTenantAuthorizationContext` (FU12) — **once-per-request memoized** hydration; `OrgDataScopeResolver` →
  `EffectiveScopes` of `EntitlementDataScopeKind`: **`OrgUnit`, `Position`, `ManagerChain`, `LegalEntity`, `Country`**.
  **`Company` is NOT a live scope kind — do not assume it.**
- **Empty scope ⇒ deny.** LegalEntity referenceability via a **live, fail-closed MOD-0220 lookup**.

### AuthService (policy-based)
- `HasPermissionAttribute → PermissionPolicyProvider → PermissionRequirement → PermissionAuthorizationHandler`.
- `TokenService` bakes `permission` / `role` / `actor_type` claims at issuance; **access-token TTL ≤ 15 min**; refresh
  path **re-reads** current grants.
- **FU13 revoke-on-removal:** user-role removal → `RevokeAllByUserAsync`; role-permission removal → tenant-scoped
  `GetUserIdsByRoleAsync` holder lookup + per-holder `RevokeAllByUserAsync`.

### MDM (policy-based)
- Same 4-piece stack; canonical `mdm.legal-entities.*`; tenant context; LegalEntity referenceability lookup.

### EnterpriseStrategy — **boundary note only**
- Separate `[EnterpriseStrategyPermission]` enforcement stack. **NOT automatically in FU14 v1 scope.** **Slice 5B
  blocker is unchanged by this pack.**

### Reason/explain seam — **none exists**
Repo-wide search for `ExplainAccess` / `AccessDecision` / `AuthorizationDecision` / `DecisionReason` / `ReasonCode` /
`DeniedReason` / `WhyAllowed` / `WhyDenied` / `PermissionEvaluation` / `EffectivePermission` → **0 hits**. FU14 must
introduce a **new bounded reason model** (§4).

---

## 3. Explain Access v1 goal (backend-only decision-trace contract)

A read-only contract that **explains** the result the existing authorization chain would produce, for a given
(subject, permission key [, optional resource scope]) — **without** making or altering enforcement.

- **Observes** the live chain (§2); **never decides**.
- **Tenant isolation** mandatory.
- **Fail-closed:** if an explanation cannot be produced, **no access is granted**; a diagnostic failure does **not**
  override the real allow/deny.
- **No sensitive data leak** (§5).
- **Endpoint precedent (directional only):** `TenantModuleEntitlementsController.HttpGet("effective-access/{moduleCode}")`
  shows a Platform-API "effective-access" GET shape. The **exact route / controller is NOT finalized here** — proposed
  at pack level only. No endpoint is implemented in this step.

---

## 4. Reason-code model (LOCKED — bounded controlled vocabulary)

No reusable reason DTO exists; FU14 defines a **new bounded enum** model. **No raw exception text is ever returned as a
reason code.** The model is a read-only observation of the chain; it does **not** duplicate enforcement logic.

### 4.1 Permission decision
`authenticated` · `unauthenticated` · `actor-bypass-platform-admin` · `actor-bypass-partner-admin` ·
`permission-claim-canonical-match` · `permission-claim-legacy-alias-match` · `permission-claim-missing` ·
`permission-key-unknown`

### 4.2 Data-scope decision
`scope-resolved` · `scope-empty-deny` · `scope-org-unit-match` · `scope-position-match` · `scope-manager-chain-match` ·
`scope-legal-entity-referenceable` · `scope-legal-entity-not-referenceable` · `scope-country-match` · `scope-no-match`

### 4.3 Token-freshness note (bounded)
`token-valid-until-expiry` · `refresh-required-after-grant-change` · `token-version-unavailable` ·
`revocation-timestamp-unavailable`

> Rules: bounded enum only; never return raw exception messages or an internal full-rule trace; never produce an
> authorization decision; do not duplicate enforcement logic in a debug path.

---

## 5. Response security boundary (LOCKED guard)

**MAY return:** canonical permission key · allow/deny · bounded reason code (§4) · actor type · tenant id · evaluated
**scope kinds** · scope **counts** · bypass reason · `matchedViaLegacyAlias: true/false` · limited freshness note (JWT
expiry, if available).

**MUST NOT return (default):** raw JWT · raw claims dump · secrets · internal API key · other-tenant data · unnecessary
PII · full grant inventory · **raw organization-chain node id list (default)** · **role id / role-name list (default)** ·
repository exception detail · cache-internal keys.

**Redaction default:** return scope **kind + count**; **raw scope ids are NOT returned by default**; any more-detailed
debug level requires a separate explicit decision (OD-FU14-07). The existing `AuditEvent` model's masked fields
(`ActorEmailMasked`, `ActorDisplayNameMasked`, `IpAddressMasked`, `RedactionStatus`) are the masking precedent.

---

## 6. Fail-closed rules (LOCKED)

1. The Explain endpoint **does not change** the enforcement decision.
2. If an explanation cannot be produced, **access is not opened**.
3. A diagnostic failure does **not** override the real allow/deny.
4. **Tenant isolation is mandatory**; another tenant's subject cannot be explained.
5. An **unauthorized caller** never receives a grant inventory.
6. **No raw JWT / secret** is returned.
7. Alias visibility is **only** `matchedViaLegacyAlias: true/false` — the legacy claim **value** is not returned by
   default.
8. Explain Access is **not** a cache-staleness debug endpoint.
9. The **FU13 RabbitMQ rollout-gate status is not changed** by FU14.

---

## 7. API / UI boundary
- **FU14 v1 = backend-only.** Frontend / gateway **deferred** — there is **no Explain Access UI evidence** in the repo
  (`./frontend` exists but has no explain/effective-access references). No UI screen is assumed or designed.
- **No API endpoint is implemented in this step.** A new explain **permission key** is needed but is **not invented /
  seeded / locked** here (OD-FU14-08).

---

## 8. Audit / logging grounding
**Reusable seam:** `AuditEvent` (TenantScopedEntity) + `AuditEventRepository` + `PlatformAuditController`
(`api/platform/audit`). Existing fields: `CorrelationId`, `RequestType`, `ActorType`, `ActorId`, masked actor fields,
`TargetTenantId`, `Category`, `EntityType`, `EntityId`, `Operation`, **`Outcome`** (allow/deny analogue), `Metadata`,
masked IP, `OccurredAtUtc`, `SourceService`, `SourceModule`, redaction fields.

- Auditing an **explain request** is feasible over this seam (no new model needed).
- Whether explain requests **must** be audited is an **open decision** (OD-FU14-04) for AG-STEP-011.
- **Allow-decision auditing policy belongs to AG-STEP-012.** AG-STEP-011 does **not** design or implement
  "audit all allow decisions."

---

## 9. Locked Decisions (`OD-FU14-*`) — resolved from repo evidence (AG-STEP-011 open-decision audit)

> **FU14 v1 has no open decision.** OD-FU14-01…07 + **08A** are **LOCKED**; only **OD-FU14-08B** (a non-bypass-role
> extension) is **deferred** and is **not a v1 runtime blocker**. Each lock is grounded in read-only repo evidence at
> HEAD `c035bf5`. The pack is now `ready-for-dev`; runtime implementation is a separate, explicitly-authorized step.

### OD-FU14-01 — Caller policy → **LOCKED**
- **Repo evidence.** `platform_admin`/`partner_admin` **bypass** `HasPermissionAttribute` (`isPlatformActor || HasPermissionClaim`); `AuthController.Me` (`HttpGet("me")`, `[Authorize]`, own identity) is the self-read precedent; **no `tenant_admin` actor exists** in Platform.
- **Self-explain (LOCKED).** An authenticated subject may explain **only its own** effective-access result; the self-explain caller **cannot choose another `subjectUserId`**; no cross-user diagnostic key is required; tenant isolation enforced.
- **Cross-user explain (LOCKED).** Only `platform_admin` / `partner_admin` may explain another subject — a read-only diagnostic privilege surface gated by the canonical marker `platform.access.explain` and covered by the Platform actor-bypass model. To query another tenant's subject, an **explicit target tenant context** is required **and** the caller's authorized actor-bypass + tenant isolation are both verified. The Explain endpoint is **not** a grant-inventory endpoint.
- **Deferred (08B):** tenant-admin / auditor / any **non-bypass** role cross-user explain — **not in v1.**

### OD-FU14-02 — Backend-only v1 → **LOCKED**
Backend-only v1; **no** Explain Access UI / gateway evidence in repo; frontend/gateway **deferred**; no UI assumed. The existing `effective-access/{moduleCode}` GET is **only** a diagnostic-route precedent. No new route/controller is implemented in this step.

### OD-FU14-03 — Reason-code granularity → **LOCKED**
Bounded enum / controlled vocabulary (§4); **no** full internal trace in the response; **no** raw exception text as a reason code; the model **observes** the live chain and does **not** duplicate enforcement logic; a diagnostic failure never changes the decision. `Company` is **not** a scope kind and is not added/assumed.

### OD-FU14-04 — Audit every Explain request → **LOCKED**
**All** explain requests are audited: self-explain, cross-user, **denied** explain, and **diagnostic-failure** are all audited; an **audit-write failure does not change the enforcement decision**. Explain audit is **distinct** from the AG-STEP-012 allow-decision audit (which is **not** designed/implemented/completed here). Reuses `AuditEvent` / `AuditEventRepository` + the existing masked/redacted/correlation/actor/tenant/target/outcome/metadata/occurred-at/source fields. **Proposed category** `authorization.explain`; **proposed bounded metadata only:** self/cross-user mode, canonical permission key, allow/deny, bounded reason-code list, actor type, target tenant id, scope-kind list, scope counts, `matchedViaLegacyAlias`, diagnostic-failure flag. **Audit metadata MUST NOT include:** raw JWT · raw claim list · secrets · internal API key · raw role inventory · raw org-chain node ids · unnecessary PII · raw exception text.

### OD-FU14-05 — Alias visibility → **LOCKED**
Response exposes **only** `matchedViaLegacyAlias: true|false`; **no** raw legacy permission key, **no** raw matched claim, **no** alias-resolver internal dump. Slice 1B / Slice 7 alias-retirement state is **not** changed by FU14.

### OD-FU14-06 — Token-freshness visibility → **LOCKED**
MAY return: limited JWT-expiry info (if present) + bounded `refresh-required-after-grant-change` note + the FU13 `≤15-min` bounded-fallback explanation. MUST NOT return/assume: token version · revocation timestamp · deny-list state · cache-internal key · entitlement-cache staleness debug · RabbitMQ topology debug. A **cache-debug endpoint is a v1 non-goal**; not conflated with the FU13 rollout gate.

### OD-FU14-07 — Default scope redaction → **LOCKED**
Default response: scope **kinds + counts**. **Out of the default response:** raw org-unit/position/manager-chain/legal-entity id lists · role id/name inventory · unnecessary PII. Detailed debug view **deferred**; tenant isolation mandatory; empty-scope ⇒ deny preserved; LegalEntity live fail-closed lookup preserved; `Company` scope kind not assumed.

### OD-FU14-08A — Explain permission marker → **LOCKED (v1)**
Canonical cross-user Explain Access marker: **`platform.access.explain`**.
- **Repo rationale.** PKS-001 lowercase-dotted, 3 segments, fits the Platform namespace and the `HasPermissionAttribute` actor-bypass model. v1 cross-user callers are **only** `platform_admin`/`partner_admin`, which **bypass** `[HasPermission]` — so **no seed / migration / alias row is needed in v1**; the marker fixes the authorization intent at the attribute level (consistent with every other `platform.*` key, none of which are seeded/granted in-repo).
- The alternative `platform.authorization.explain` is **rejected**. **v1 marker LOCKED: `platform.access.explain`.**

### OD-FU14-08B — Non-bypass extension → **DEFERRED (external evidence; NOT a v1 blocker)**
Granting cross-user explain to a **non-bypass** tenant-admin / auditor role requires an **AuthService `Permission` / `RolePermission` DB catalog migration**, a **role-grant decision**, and **external runtime evidence** (and possibly a separate canonical key). **Out of v1 scope; not a FU14 v1 runtime blocker.** **Rules:** no seed/migration, no alias row, no non-bypass implementation; this deferred item is **not** marked completed.

---

## 10. Acceptance Criteria (reconciled to the §9 locks)
1. Self-explain works **only** for the authenticated own subject.
2. Cross-user explain works **only** for `platform_admin` / `partner_admin`.
3. Cross-user marker = `platform.access.explain`.
4. **No** seed / migration / alias row for v1.
5. Non-bypass tenant-admin / auditor extension is **deferred** (OD-FU14-08B).
6. Explain **only observes** the current enforcement decision.
7. A diagnostic failure does **not** change the enforcement decision.
8. Tenant isolation is mandatory.
9. Response uses the **bounded reason-code** model (§4).
10. No raw JWT / claims / secrets / internal key returned.
11. Alias visibility is **only** the `matchedViaLegacyAlias` boolean.
12. Scope-detail default = **kinds + counts**.
13. Token-freshness = **only** expiry + bounded `refresh-required` note.
14. **All** explain requests are audited (self, cross-user, denied, diagnostic-failure).
15. Explain audit **≠** AG-STEP-012 Allow Audit.
16. Frontend / gateway **deferred**.
17. EnterpriseStrategy **out of scope**.
18. Cache-debug endpoint **out of scope**.
19. **FU13 rollout-gate status is unchanged.**

---

## 11. Test plan (planned — written only when runtime starts later)
self-explain allow/deny policy · cross-user caller-policy gate · tenant isolation · canonical-match reason · legacy-alias
boolean · bypass reason · missing-permission deny · unauthenticated deny · empty-scope deny · LegalEntity-not-referenceable
deny · scope kinds+counts redaction · raw JWT/secret absent · token-expiry limited visibility · diagnostic failure does
not alter enforcement · audit-event behavior after OD-FU14-04 lock · EnterpriseStrategy out-of-scope regression · full
Platform regression. **No tests are written in this step.**

---

## 12. Non-Goals
Allow-Audit runtime policy (AG-STEP-012) · frontend / gateway · raw JWT viewer · full grant-inventory endpoint ·
unrestricted org-chain viewer · cache debug endpoint · Redis / distributed cache · token deny-list · token versioning ·
EnterpriseStrategy integration · Slice 5B / Slice 7 resolution · FU13 RabbitMQ rollout-gate closure · new seed / migration ·
the non-bypass cross-user extension (OD-FU14-08B).

---

## 13. Runtime implementation groups (planning only — no runtime here)

> Atomic-group plan for the **future** runtime step (a separate, explicitly-authorized step). **No runtime code, DTO,
> endpoint, test, seed, or migration is written by this pack.**

- **Group A — Explain reason model.** Bounded reason-code enum/DTO (§4) + response redaction model (§5/§7) + an
  **enforcement-observer** contract that reads the live decision and **produces no authorization decision**.
- **Group B — Self-explain backend flow.** Authenticated own-subject only; tenant isolation; **no** cross-user marker;
  audit-event write (§8); fail-closed diagnostics.
- **Group C — Cross-user explain backend flow.** Canonical marker `platform.access.explain`; actor bypass
  (`platform_admin`/`partner_admin`) only; explicit target-tenant isolation; audit-event write; **no** non-bypass grant
  extension (OD-FU14-08B stays deferred).
- **Group D — Tests + integration audit.** Caller-policy matrix; redaction; alias boolean; limited token-freshness;
  audit-event behavior; full Platform regression.
- **Group E — Governance reconciliation.** Pack runtime-completion note + roadmap; docs-only; last.

---

## 14. Open-decision reconciliation

**Resolved / locked (FU14 v1):** OD-FU14-01 · 02 · 03 · 04 · 05 · 06 · 07 · **08A**.
**Deferred (NOT a v1 blocker):** **OD-FU14-08B** — non-bypass tenant-admin / auditor extension + external grant-catalog
migration + role-grant decision.

> **FU14 v1 has no open decision.** This pack is promoted to **`ready-for-dev`** (implementation handoff only — **no
> runtime code is written by this pack**); **runtime implementation is a separate, explicitly-authorized step**, and
> **no PR / merge** (not merged to `main`). Cross-user runtime relies on the platform/partner-admin **bypass** (no
> external grant needed for v1); the only external dependency is the **deferred** OD-FU14-08B extension.
