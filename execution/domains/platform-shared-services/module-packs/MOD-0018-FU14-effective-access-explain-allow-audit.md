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

> **Ready-for-dev note (scope: SELF-EXPLAIN ONLY v1).** Module-pack contract authored, decision-locked, promoted
> `draft → ready-for-dev`, and **scope-reconciled to self-explain-only** by the AG-STEP-011 runtime-inventory audit (HEAD
> `8c1d8fb`) — all **before** any Explain Access runtime work. Every revision so far made **no** runtime, seed, migration,
> test, frontend, gateway, `.antigravity`, registry, or roadmap change. **FU14 self-explain v1 has no open decision** —
> §9 locks OD-FU14-01 (self-explain) · 02 · 03 · 04 (handler-level) · 05 · 06 · 07 · 08A (marker **reserved**, not active
> in self-v1). **Cross-user explain is deferred** (OD-FU14-09 target effective-grants contract + OD-FU14-08B non-bypass
> extension) — Platform has **no seam to read another user's effective grants**, and a permission-less cross-user trace
> is rejected. `ready-for-dev` authorizes implementation **planning / handoff only**; production code still passes the
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
  `EffectiveScopes` of `EntitlementDataScopeKind` (the resolver emits **`OrgUnit`, `Position`, `ManagerChain`,
  `LegalEntity`**; `Country` arrives via the context path). **`Company` exists in the `EntitlementDataScopeKind` enum,
  but the current `OrgDataScopeResolver` does NOT emit it; the observer projects only resolver output and never
  fabricates Company.**
- **Empty scope ⇒ deny *only for an opted-in scoped resource* (BME-001) — NOT a universal rule;** platform/partner
  admins are not row-scoped. LegalEntity referenceability via a **live, fail-closed MOD-0220 lookup**.

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

## 3. Explain Access v1 goal — **SELF-EXPLAIN ONLY** (backend-only decision-trace contract)

> **Scope narrowed by the AG-STEP-011 runtime-inventory audit (HEAD `8c1d8fb`).** FU14 **v1 is self-explain only**.
> Cross-user explain is **deferred** (see §3.1 + OD-FU14-09): Platform has **no seam to read another user's effective
> permission grants**, so a cross-user *permission* trace cannot be produced today, and a permission-less cross-user
> trace would be misleading. The `ready-for-dev` status is kept; runtime is a separate step.

A read-only contract that **explains** the result the existing authorization chain would produce for the
**authenticated caller's own** (permission key [, optional resource scope]) — **without** making or altering enforcement.

- **Self-subject only.** The subject is resolved from the caller's JWT (`sub`/NameIdentifier) + the current tenant
  context; the caller **cannot** choose another `subjectUserId` (no request/query subject id).
- **Observes** the live chain (§2); **never decides**. **Tenant isolation** mandatory.
- **Fail-closed:** if an explanation cannot be produced, **no access is granted**; a diagnostic failure does **not**
  override the real allow/deny.
- **No sensitive data leak** (§5).
- **Auth:** the self route is `[Authorize]` (own identity); **no cross-user diagnostic marker is used on the self route.**
- **Proposed self route (proposal only — no controller/endpoint created here):** `GET api/platform/access/explain/me`.

### 3.1 Runtime-inventory finding — LOCKED (observer seam, side-effect boundary)

- **Side-effect boundary.** The Explain observer **must NOT call the real `HasPermissionAttribute` filter.** Its
  platform-actor branch **mutates admin lifecycle state** (`HasPermissionAttribute.cs:56-64` — invitation acceptance,
  last-login update, `repository.UpdateAsync`). An explain endpoint is a **read-only observer** and must never trigger
  that write. The **pure claim-matching** part (`HasPermissionClaim` / `IsPermissionClaim` + `PermissionAliasResolver.Expand`)
  is side-effect-free and is **extracted into a shared pure evaluator** (canonical-match / legacy-alias-match /
  missing-claim / unknown-key). Extraction must **not** change enforcement behavior; `HasPermissionAttribute` then
  **delegates** claim-matching to the same evaluator; the observer uses **only** the pure evaluator. Enforced behavior
  is held by regression tests (Group A).
- **Data-scope observer.** `OrgDataScopeResolver.ResolveAsync(tenantId, userId, moduleCode, featureCode, ct)` is
  **read-only** and accepts an **explicit userId**, so it is safely reused for self-explain. The response carries **only
  scope kinds + counts**; raw scope id lists stay out. The resolver produces no second authorization decision — it is
  used **only** for the explain projection. **Empty scope ⇒ deny** preserved; LegalEntity **live fail-closed** lookup
  preserved.
- **Token-freshness observer.** JWT `exp`, `actor_type`, `sub`, `tenant_id` are readable; there is **no token-version
  seam and no revocation-timestamp seam**. The response carries **only** expiry + a bounded `refresh-required` note; no
  freshness state that cannot actually be proven at runtime is produced.

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

### 4.4 Producibility for self-explain v1 (AG-STEP-011 runtime audit)
- **Producible from self-explain v1 runtime** — Permission: `authenticated` · `permission-claim-canonical-match` ·
  `permission-claim-legacy-alias-match` · `permission-claim-missing` · `permission-key-unknown`. Data-scope: all §4.2
  codes (`scope-resolved` … `scope-no-match`). Token-freshness: all §4.3 codes (`token-valid-until-expiry` ·
  `refresh-required-after-grant-change` · `token-version-unavailable` · `revocation-timestamp-unavailable`).
- **Observer / regression vocabulary (may not reach the self-v1 response on every flow)** — `unauthenticated` (the self
  route may be rejected by `[Authorize]` *before* the handler) · `actor-bypass-platform-admin` ·
  `actor-bypass-partner-admin` (the bypass reasons belong to the deferred cross-user flow, not the self-v1 main path).
- **Out of the v1 response** — raw exception text · raw JWT · raw claims · raw permission-token list · raw alias claim
  value · raw role inventory · raw scope ids · raw org-chain ids · cache-internal keys. `Company` scope kind is not added
  or assumed.

---

## 5. Response security boundary (LOCKED guard) — self-explain v1 shape

**MAY return (TWO SEPARATE observations — see §16; NO combined `Allowed` verdict):** `mode: self` · canonical permission
key · **`PermissionSatisfied`** (permission-gate observation only) · bounded `PermissionMatch` (§4) · actor type ·
tenant id · descriptive **scope kinds** · scope **counts** · **`ScopeNotes`** · `matchedViaLegacyAlias: true/false` ·
limited freshness note (JWT expiry, if available) + bounded `refresh-required` note · `diagnosticFailure: true/false`.

**MUST NOT return:** any **other-subject id selection** · raw JWT · raw claims dump · secrets · internal API key ·
other-tenant data · unnecessary PII · full grant inventory · **raw organization-chain node id list** · **role id /
role-name list** · raw scope id lists · repository exception detail · cache-internal keys.

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

### OD-FU14-01 — Caller policy → **LOCKED (self-explain only in v1)**
- **Repo evidence.** `platform_admin`/`partner_admin` **bypass** `HasPermissionAttribute` (`isPlatformActor || HasPermissionClaim`); `AuthController.Me` (`HttpGet("me")`, `[Authorize]`, own identity) is the self-read precedent; **no `tenant_admin` actor exists** in Platform.
- **Self-explain (LOCKED — v1).** An authenticated subject may explain **only its own** effective-access result; the caller **cannot choose another `subjectUserId`** (subject from JWT `sub` + tenant context; no request/query subject id); no cross-user diagnostic marker is used on the self route; tenant isolation enforced; the endpoint is **not** a grant-inventory endpoint. Self route is `[Authorize]`.
- **Cross-user explain → DEFERRED (NOT in v1).** The actor-bypass model authorizes *who may call*, but Platform has **no seam to read another user's effective permission grants** (those live in the AuthService `RolePermission` DB / only in that user's JWT; the Platform→AuthService client carries only provisioning/invitation surfaces). A cross-user *data-scope-only* explain is **rejected** — without permission provenance it is incomplete and misleading. Cross-user explain therefore moves to a **deferred follow-up** gated by **OD-FU14-09** (target effective-grants contract) and remains subject to **OD-FU14-08B** for any non-bypass caller. **No new AuthService cross-service contract is invented in v1.**
- **Deferred (08B):** tenant-admin / auditor / any **non-bypass** role cross-user explain — **not in v1.**

### OD-FU14-02 — Backend-only v1 → **LOCKED**
Backend-only v1; **no** Explain Access UI / gateway evidence in repo; frontend/gateway **deferred**; no UI assumed. The existing `effective-access/{moduleCode}` GET is **only** a diagnostic-route precedent. No new route/controller is implemented in this step.

### OD-FU14-03 — Reason-code granularity → **LOCKED**
Bounded enum / controlled vocabulary (§4); **no** full internal trace in the response; **no** raw exception text as a reason code; the model **observes** the live chain and does **not** duplicate enforcement logic; a diagnostic failure never changes the observation. `Company` **exists** in the `EntitlementDataScopeKind` enum but the current `OrgDataScopeResolver` does **not** emit it (the observer projects only resolver output and never fabricates Company).

### OD-FU14-04 — Audit every Explain request → **LOCKED (handler-level, honestly bounded)**
**Every self-explain execution that reaches the application handler is audited** — successful, application-level **denied**, and **diagnostic-failure** — and an **audit-write failure does not change the enforcement decision** (it is isolated at the handler). **Honesty caveat:** an **unauthenticated** request rejected by `[Authorize]` **before** the handler is **not guaranteed** to emit a FU14-specific audit event via the handler seam; FU14 v1 does **not** invent a new middleware/filter audit seam — existing API authentication/logging behavior is kept, and pre-handler-401 FU14 audit hardening is a **separate follow-up**. **Do not claim "every unauthenticated request emits a FU14 AuditEvent."** Reuses **`IAuditService.AppendAsync(AuditAppendRequest, ct)`** (the service performs masking/redaction). **Proposed category** `authorization.explain`; **bounded metadata only:** mode `self`, canonical permission key, **`permissionSatisfied`** (not a combined `allowed`), bounded `permissionMatch`, actor type, tenant id, scope-kind list, scope counts, **`scopeNotes`**, `matchedViaLegacyAlias`, diagnostic-failure flag (+ `validationCode` on a validation/invalid-context deny). **MUST NOT include:** raw JWT · raw claim list · raw permission-token list · raw alias claim value · secrets · internal API key · raw role inventory · raw scope ids · raw org-chain node ids · unnecessary PII · raw exception text. Explain audit is **distinct** from the AG-STEP-012 allow-decision audit (not designed/implemented here). **Cross-user audit is deferred with the cross-user flow** (still mandatory when that flow lands).

### OD-FU14-05 — Alias visibility → **LOCKED**
Response exposes **only** `matchedViaLegacyAlias: true|false`; **no** raw legacy permission key, **no** raw matched claim, **no** alias-resolver internal dump. Slice 1B / Slice 7 alias-retirement state is **not** changed by FU14.

### OD-FU14-06 — Token-freshness visibility → **LOCKED**
MAY return: limited JWT-expiry info (if present) + bounded `refresh-required-after-grant-change` note + the FU13 `≤15-min` bounded-fallback explanation. MUST NOT return/assume: token version · revocation timestamp · deny-list state · cache-internal key · entitlement-cache staleness debug · RabbitMQ topology debug. A **cache-debug endpoint is a v1 non-goal**; not conflated with the FU13 rollout gate.

### OD-FU14-07 — Default scope redaction → **LOCKED**
Default response: scope **kinds + counts** + bounded `ScopeNotes`. **Out of the default response:** raw org-unit/position/manager-chain/legal-entity id lists · role id/name inventory · unnecessary PII. Detailed debug view **deferred**; tenant isolation mandatory; **empty scope is NOT a universal deny** — data-scope is opt-in per resource (BME-001) and the scope observation is **descriptive**, never a verdict; LegalEntity live fail-closed lookup preserved; `Company` exists in the enum but the resolver does not emit it.

### OD-FU14-08A — Explain permission marker → **RESERVED (not active in self-explain v1)**
Canonical marker name **`platform.access.explain`** (PKS-001 lowercase-dotted, 3 segments, fits the Platform namespace + the `HasPermissionAttribute` actor-bypass model). Because **cross-user explain is deferred** (OD-FU14-01 / OD-FU14-09), the marker is **RESERVED for the future cross-user diagnostic route** and is **not used on the self-explain v1 route** (the self route is `[Authorize]` only). For self-explain v1: **no seed, no migration, no alias row**; the marker is **not written to runtime** in this revision step; the cross-user route is **not** treated as complete. The alternative `platform.authorization.explain` remains **rejected**.

### OD-FU14-08B — Non-bypass caller extension → **DEFERRED (external evidence; NOT a v1 blocker)**
Granting cross-user explain to a **non-bypass** tenant-admin / auditor role requires an **AuthService `Permission` / `RolePermission` DB catalog migration**, a **role-grant decision**, and **external runtime evidence** (and possibly a separate canonical key). **Out of v1 scope; not a FU14 self-explain v1 blocker.** **Rules:** no seed/migration, no alias row, no non-bypass implementation; **not** marked completed.

### OD-FU14-09 — Cross-user target effective-grants contract → **DEFERRED (NOT a self-v1 blocker)**
Cross-user permission explain needs a **safe target effective-grants lookup seam** that does not exist today. Decisions required before any cross-user runtime:
- Is a new **AuthService internal contract** required (Platform has no seam for another user's grants)?
- What **bounded DTO** is returned (no raw grant inventory)?
- How is **tenant isolation** of the target subject verified?
- How is **permission-grant-inventory leakage** prevented?
- How are **audit / redaction** applied to the cross-user path?
- Does the **Platform→AuthService client** need to be extended?

**Rules:** **no** new endpoint / contract / DTO is written in this step; cross-user runtime **does not start** until this decision is resolved; this is **not** a FU14 self-explain v1 blocker; it is **distinct** from AG-STEP-012 Allow Audit. **FU14 self-explain v1 has no open decision; the cross-user extension is deferred and not completed.**

---

## 10. Acceptance Criteria (FU14 self-explain v1 — reconciled to the AG-STEP-011 runtime audit)
1. Self-explain works **only** for the authenticated own subject.
2. The caller **cannot** choose another subject.
3. Tenant isolation is preserved.
4. The explain observer **does not** call the real `HasPermissionAttribute` filter.
5. A **shared pure evaluator** is used jointly with the enforcement claim-matching logic.
6. Existing authorization behavior is preserved by regression (incl. the admin-lifecycle side-effect).
7. Data-scope returns **only kinds + counts**.
8. **Empty scope ⇒ deny** preserved.
9. LegalEntity **live fail-closed** lookup preserved.
10. Alias visibility is **only** the `matchedViaLegacyAlias` boolean.
11. Token-freshness = **only** expiry + bounded `refresh-required` note.
12. No raw JWT / claims / secrets / raw scope ids / role inventory returned.
13. Every self-explain execution **reaching the handler** is audited.
14. An audit-write failure does **not** change the enforcement decision.
15. Pre-handler unauthenticated FU14 audit hardening is **out of v1**.
16. Cross-user explain is **out of v1**.
17. The cross-user **data-scope-only** alternative is **rejected**.
18. `platform.access.explain` is a **reserved** marker; **not actively used** in self v1.
19. Cross-user runtime does **not** start before **OD-FU14-09** is resolved.
20. **OD-FU14-08B** stays deferred.
21. AG-STEP-012 Allow Audit stays separate.
22. Frontend / gateway deferred.
23. EnterpriseStrategy out of scope.
24. **FU13 rollout-gate status is unchanged.**

---

## 11. Test plan (planned — written only when runtime starts later)
self-explain allow/deny policy · cross-user caller-policy gate · tenant isolation · canonical-match reason · legacy-alias
boolean · bypass reason · missing-permission deny · unauthenticated deny · empty-scope deny · LegalEntity-not-referenceable
deny · scope kinds+counts redaction · raw JWT/secret absent · token-expiry limited visibility · diagnostic failure does
not alter enforcement · audit-event behavior after OD-FU14-04 lock · EnterpriseStrategy out-of-scope regression · full
Platform regression. **No tests are written in this step.**

---

## 12. Non-Goals
**Cross-user explain runtime** · **cross-user data-scope-only partial explain (rejected)** · **AuthService target
effective-grants endpoint / contract (OD-FU14-09)** · the non-bypass tenant-admin / auditor extension (OD-FU14-08B) ·
Allow-Audit runtime policy (AG-STEP-012) · pre-handler-401 FU14 audit middleware/filter · frontend / gateway · raw JWT
viewer · full grant-inventory endpoint · unrestricted org-chain viewer · cache debug endpoint · Redis / distributed
cache · token deny-list · token versioning · EnterpriseStrategy integration · Slice 5B / Slice 7 resolution · FU13
RabbitMQ rollout-gate closure · new seed / migration / alias row.

---

## 13. Runtime implementation groups (planning only — no runtime here)

> Atomic-group plan for the **future** runtime step (a separate, explicitly-authorized step). **No runtime code, DTO,
> endpoint, test, seed, or migration is written by this pack.**

- **Group A — Pure permission evaluator + reason model.** Extract a **shared pure `PermissionClaimEvaluator`** (canonical /
  legacy-alias / missing / unknown-key); **`HasPermissionAttribute` delegates** claim-matching to it — **enforcement
  behavior unchanged**; bounded reason-code model (§4) + redacted self-explain projection (§5/§7).
  **Regression gate:** canonical claim · legacy alias · missing claim · unknown key · unauthenticated · platform_admin
  bypass · partner_admin bypass · inactive-actor/lifecycle · **admin-lifecycle side-effect** · alias dual-read · full
  Platform regression.
- **Group B — Self-explain backend flow.** `[Authorize]`, **own-subject only**; tenant isolation; reuse
  `OrgDataScopeResolver` (kinds + counts); limited token-freshness; bounded response; **`IAuditService.AppendAsync`**;
  diagnostic failure fail-closed. **Proposed route (proposal only):** `GET api/platform/access/explain/me`.
- **Group C — Self-explain tests + integration audit.** Policy matrix; redaction; alias boolean; limited token-freshness;
  audit-write isolation; no raw claim / raw scope leakage; full Platform regression; FU13 tests green.
- **Group D — Governance reconciliation.** After a runtime audit PASS: pack runtime-completion note + roadmap; docs-only; last.

> **Deferred cross-user follow-up (separate, explicitly-authorized).** Reserved marker `platform.access.explain` +
> **OD-FU14-09** (target effective-grants contract) + **OD-FU14-08B** (non-bypass role extension). **Does not start
> without the new AuthService contract decision.**

---

## 14. Open-decision reconciliation

**FU14 v1 scope = SELF-EXPLAIN ONLY.**
**Resolved / locked (self-explain v1):** OD-FU14-01 (self-explain) · 02 · 03 · 04 (handler-level) · 05 · 06 · 07 · **08A**
(marker reserved, not active in self-v1).
**Deferred (NOT a self-v1 blocker):** **OD-FU14-08B** (non-bypass caller extension) · **OD-FU14-09** (cross-user target
effective-grants contract — Platform has no seam for another user's grants; no AuthService contract is invented here).

> **FU14 self-explain v1 has no open decision.** This pack stays **`ready-for-dev`** (implementation handoff only — **no
> runtime code is written by this pack**); **runtime is a separate, explicitly-authorized step**, and **no PR / merge**
> (not merged to `main`). **Self-explain v1 needs no external grant** (own `[Authorize]` route, own JWT claims, reused
> `OrgDataScopeResolver`). **Cross-user explain is deferred** until OD-FU14-09 is resolved. AG-STEP-012 Allow Audit stays
> separate; Slice 5B / Slice 7 + the FU13 rollout gate are unchanged.

---

## 16. Runtime implementation — completed in integration branch

> **Pack status note.** Frontmatter stays **`ready-for-dev`** (repo convention: `done` is reserved for runtime **merged
> to `main`**, which is blocked by the merge-freeze). Runtime completion is recorded here as a section, not a status flip.

**Self-explain v1 runtime is implemented on the integration branch; post-fix integration audit: PASS. No PR / no merge;
not merged to `main`** (`main` still `d3ab4a4`).

**Runtime commits:**
- `a2445b8` — **Group A:** shared pure `PermissionClaimEvaluator` extraction + `HasPermissionAttribute` delegation (enforcement byte-for-byte unchanged).
- `9ddb36f` — **Group B:** backend-only self-explain route (`GET api/platform/access/explain/me`, plain `[Authorize]`) + API-layer `SelfAccessExplainService` + bounded DTO + audit flow.
- `bba038a` — **Group C:** **remove the combined `Allowed` verdict**; return **separate permission + descriptive scope observations**.

**Build & test (final):** `Diten.Platform.API` **0 errors**; `Diten.Platform.Application.Tests` **603 passed, 0 failed**;
`Diten.Platform.Eventing.Tests` **56 passed, 0 failed, 3 pre-existing skipped**. Self-explain v1 is integrated,
test-green, and **semantically aligned with live enforcement (no false-negative universal empty-scope rule)**.

### 16.1 Response model — RECONCILED (separate observations, no combined verdict)
The earlier "allow/deny" / combined-`Allowed` wording in §3.1/§5/§8 is **superseded.** The Group C integration audit
proved a combined verdict is **unsound**: row-level data-scope is **opt-in per resource** and platform/partner admins are
**not row-scoped** (BME-001), so an empty scope is **not** a universal deny; and there is **no permission↔module binding
catalog**, so the two inputs cannot be soundly combined. The response therefore carries **two separate observations** and
**no `Allowed` verdict**:
- **Permission observation — `PermissionSatisfied`** (+ `PermissionMatch`, `MatchedViaLegacyAlias`): the permission-gate
  observation **only** (platform/partner-admin bypass, or the pure `PermissionClaimEvaluator` result). It is **not**
  affected by the data-scope result, an empty scope, a resolver exception, or a module mismatch. It is **not** an
  authorization grant, **not** an access decision for any other endpoint, and **not** an enforcement verdict — it is a
  diagnostic observation.
- **Scope observation — `ScopeKinds` + `ScopeCounts` + `ScopeNotes`**: a **descriptive** observation, **not** an
  applicability verdict, **not** a scope-deny verdict, and **not** a permission↔module binding claim; raw scope ids are
  never returned. Base notes: `data-scope-opt-in-per-resource`, `scope-applicability-not-determined`; empty scope adds
  `empty-scope-does-not-imply-universal-deny`; a resolver failure adds `scope-observation-failed`.

### 16.2 DTO contract (`SelfAccessExplainResponse`)
**Fields:** `Mode`, `PermissionSatisfied`, `RequiredPermission`, `PermissionMatch`, `MatchedViaLegacyAlias`, `ActorType`,
`TenantId`, `ScopeKinds`, `ScopeCounts`, `ScopeNotes`, `TokenExpiresAtUtc`, `FreshnessNotes`, `DiagnosticFailure`.
**Never:** `Allowed` · `ScopeApplicable` · `ScopeDenied` · raw JWT · raw claims · raw alias · permission/role inventory ·
raw scope ids / `ScopeCode` / `Value` · org-chain ids · secrets · internal API key · raw exception · cache debug ·
subject/tenant selector.

### 16.3 Empty-scope parity (verified)
Canonical / legacy-alias / `platform_admin` / `partner_admin` with an **empty** scope → `PermissionSatisfied = true`
(no false negative; `empty-scope-does-not-imply-universal-deny` note present). Missing permission with a **non-empty**
scope → `PermissionSatisfied = false`.

### 16.4 permissionKey ↔ moduleCode independence (verified)
The request is one `permissionKey` + one `moduleCode` (+ optional `featureCode`). The permission observation comes
**only** from the claim; the scope observation **only** from the resolver's module input. There is **no permission↔module
binding catalog** in the API layer (the alias map is **not** such a catalog); the endpoint does **not** assert the pair
is semantically matched, produces **no** combined verdict, and **no** new catalog / mapping / validator was written.

### 16.5 Permission observer + Company (corrected)
The observer never calls the real `HasPermissionAttribute` filter (side-effect boundary preserved); it reuses the shared
pure `PermissionClaimEvaluator` (canonical / legacy-alias / missing) + the bounded actor bypass
(`bypass-platform-admin` / `bypass-partner-admin`); no raw claim/alias is returned. **`permission-key-unknown` is NOT
produced** at runtime (no known-canonical-key catalog exists in the API layer; none was invented) — it remains a
deferred-observation limit. **Company correction:** `EntitlementDataScopeKind.Company` **exists** in the enum, but the
current `OrgDataScopeResolver` does **not** emit it; the observer projects only resolver output and never fabricates it.

### 16.6 Audit (reconciled)
Audited handler-level flows: success · application-level deny · validation deny · invalid context · resolver diagnostic
failure. Metadata **removed `allowed`**, **added `permissionSatisfied` + `scopeNotes`**; kept mode, required permission,
permission match, `matchedViaLegacyAlias`, actor type, tenant id, scope kinds, scope counts, diagnostic-failure flag
(+ `validationCode` on a validation/invalid-context deny). **Outcome** = `diagnostic ? Failed : permissionSatisfied ?
Succeeded : Denied`. Failure isolation: `OperationCanceledException` propagates; other audit exceptions are logged +
swallowed; a Rejected / EnqueueFailed result never changes the response; a pre-handler `[Authorize]` 401 is **not**
guaranteed to emit a FU14-specific AuditEvent (no new middleware/filter seam was written).

### 16.7 Deferred (unchanged)
**OD-FU14-08B** (non-bypass tenant-admin / auditor extension + external AuthService catalog migration + role-grant +
external evidence) and **OD-FU14-09** (cross-user target effective-grants contract; the cross-user data-scope-only
alternative is **rejected**) remain **deferred** — neither is a self-v1 blocker, neither is complete, and both are
distinct from **AG-STEP-012** (Allow Audit, `OD-D`, separate, not designed/implemented here). Explain-request audit is
**not** the AG-STEP-012 allow-decision audit.
