---
id: MOD-0148
name: Supplier Portal
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
status_note: "Review draft (2026-10-19); no runtime code or service-scaffold authority. Placement CONCURRED (DC-01, 2026-10-18, docs/records/decisions/2026-10/mvp6-supplier-dc01-approved-01.md); domain-config, DCP-009 and the module-id registry record it. SUPPLIER-BASE is frozen (docs/analysis/contracts/supplier.openapi.yaml v1.2.0, 780f8368a). DC-02 DECIDED for D1/D2-b/D4 (caller identity stays on DC-03) 2026-10-20 (docs/records/decisions/2026-10/mvp6-supplier-dc02-owner-decision-01.md; §0.4), seam not built. ready-for-dev stays blocked: DC-02/DC-03 are unbuilt build seams, DC-04/DC-05 deferred (no producer service); target 2026-12-31 is a placeholder bound to those gates; see §0, §18 and docs/roadmap/plans/mvp6-mod0148-spec-split-01/CONCURRENCE-OPEN.md."
owner: supply-chain-execution / control-tower
branch: feature/sce/mod-0148-supplier-portal
started: 2026-09-15
target: 2026-12-31
form_field_count: 0
---

# MOD-0148 — Supplier Portal

> **Identity gate:** `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0148 --name "Supplier Portal"`
> returned `OK` on 2026-09-15 against Blueprint 8.1 and again on 2026-10-19 against Blueprint 8.8 and the module ID
> registry.
>
> **Phase A only:** this draft records the proposed initial Phase B backend/contract slice. It does not authorize
> runtime code, service scaffold, frontend, gateway, permission-registry or integration changes.
>
> **Placement (CONCURRED, DC-01 2026-10-18):** MOD-0148 is a Blueprint Procurement Suite (P2P) module placed in
> `supply-chain-execution` / `Diten.SupplyChainService` for delivery (`docs/records/decisions/2026-10/mvp6-supplier-dc01-approved-01.md`). MOD-0140 remains the Supplier
> master owner (domain `procurement`). Placement grants no runtime, contract, gateway or permission ownership.

## 0. Requirement status and traceability (review 2026-10-19)

Every requirement below carries one of three labels. A label is a claim about authority, not about code (there is none).

| Label | Meaning |
|---|---|
| **ONAYLI** | Fixed today: the frozen wire surface of `SUPPLIER-PERFORMANCE` v1.0.0 FROZEN (`docs/analysis/contracts/supplier-performance.openapi.yaml`, `eb1306590`, sha256 `e311cd102bed0041…`), or a CONCURRED gate. |
| **ÖNERİLEN** | Selected target written in `docs/roadmap/plans/mvp6-mod0148-spec-split-01/SPEC-DELTA.md` (`ce3396656`, sha256 `6dce93fde8c4069a…`). **Not merged into this pack** — that delta may be merged only after its named owner artifacts are exact and hash-bound. Reviewable, not binding. |
| **BLOKE** | Depends on an OPEN gate in `docs/roadmap/plans/mvp6-mod0148-spec-split-01/CONCURRENCE-OPEN.md`; the named owner must close it. A fixture or mock never closes it. |

Where this pack's own text (sections 1–21) states a behaviour as a rule and the label below says ÖNERİLEN or BLOKE, **the
label wins**: the text is the target, not an approved rule.

| Endpoint (operationId) | Wire | Behaviour | Gate / decision owner | Tests (`ACCEPTANCE-TEST-MAP.r3.tsv`) |
|---|---|---|---|---|
| `GET /portal/status` (getSupplierPortalStatus) | ONAYLI | **BLOKE** — actor binding (DC-03); Tenant+LE eligibility ÖNERİLEN (DC-02 decided, §0.4; caller identity on DC-03); status matrix ÖNERİLEN | Platform security owner; SCE / CT (DC-02 build) | AC-03, AC-04, AC-08, AC-09, T-0148-ELG-01, T-0148-ELG-02 |
| `POST /portal/submissions` (createOwnPortalSubmission) | ONAYLI wire; **payload contradiction** (§0.2) | **BLOKE** — DC-03; eligibility ÖNERİLEN (DC-02 decided, §0.4); receipt ÖNERİLEN (§0.1) | security owner; CT Supplier Seam Owner | AC-05 (suspended → 403), AC-12 (scope override → 400), AC-13 (create replay), AC-15 (fingerprint, whitespace key), AC-16 (remap), AC-17 (race) |
| `POST /portal/submissions/{id}/submit` (submitOwnPortalSubmission) | ONAYLI | ÖNERİLEN — only `DRAFT→SUBMITTED`; **BLOKE** — DC-03 | security owner | AC-14, AC-15, AC-17 |
| `GET /portal/submissions`, `GET /portal/submissions/{id}` | ONAYLI | ÖNERİLEN — Tenant+LE+mapped Supplier+non-deleted predicate; **BLOKE** — DC-03 | security owner | AC-05 (suspended → 403), AC-10 (predicate), AC-11 (revocation fence) |
| Review operation | **none in v1** — the statuses `UNDER_REVIEW`, `ACCEPTED`, `REJECTED` **are** in the v1 enum | **BLOKE** — no command reaches them; not in the first slice | MOD-0148 owner; CT contract writer | — |
| Portal-source resolution for MOD-0147 | — | **BLOKE** | MOD-0147/Risk owner | AC-25 |
| Placement / Supplier pointer / frozen contract hashes | — | ONAYLI (DC-01) / pointer re-pinned to SUPPLIER v1.2.0 / hash guard | — | AC-01 (r2: CONCURRED), AC-02, AC-07 (hashes only) |
| Shared seam / fixture | — | out of bounded operations; fixture never live evidence | Seam owner; CT fixture coordinator | AC-06, AC-27 |


**Test map revision r2 (2026-10-19).** The original `ACCEPTANCE-TEST-MAP.tsv` and `SHA256SUMS` stay byte-identical: four records pin their hashes. The controlled revision is additive — `ACCEPTANCE-TEST-MAP.r2.tsv` (AC-01 oracle refreshed for DC-01; the `T-…` rows below added) and `SHA256SUMS.r2`, which records the folder's current bytes. The original `SHA256SUMS` already failed on `CONCURRENCE-OPEN.md` since `12416679d` (DC-01 edit, sums not updated); r2 records that file's current hash. r2 is the map this pack follows.

Test IDs added in r2 (MOD-0148) — the behaviours are proposed (§0.2, §0.3); the IDs are in `ACCEPTANCE-TEST-MAP.r2.tsv`:

| ID | Asserts | Rule |
|---|---|---|
| T-0148-TXN-01 | Transaction abort leaves no state, receipt, audit or outbox record | §0.3 |
| T-0148-TXN-02 | A duplicate delivery of one outbox event is ignored by the consumer (EventId) | §0.3 |
| T-0148-TXN-03 | A crash between commit and publish is recovered by the publisher from the Pending record | §0.3 |
| T-0148-PAY-01 | Per-`submissionType` schema, size and depth limits — BLOKE until the contract successor | §0.2 |

**Test map revision r3 (2026-10-20, DC-02).** Additive again: `ACCEPTANCE-TEST-MAP.r3.tsv` = r2 plus the `T-…-ELG-…` rows below; `SHA256SUMS.r3` records the folder's current bytes (`CONCURRENCE-OPEN.md` changed: the DC-02 gate rows). r2, `SHA256SUMS.r2` and the originals stay byte-identical. **r3 is the map this pack follows.**

Test IDs added in r3 (MOD-0148) — DC-02 (§0.4); the IDs are in `ACCEPTANCE-TEST-MAP.r3.tsv`:

| ID | Asserts | Rule |
|---|---|---|
| T-0148-ELG-01 | Portal status and submission proceed only for an `Active` own supplier; `OnHold`, `Blocked`, `Inactive` → 403 (AC-05), zero writes — BLOKE on DC-03 for the caller identity | §0.4 D2-b |
| T-0148-ELG-02 | The eligibility read uses the server-resolved own `supplierId` and the MDM-validated LE (`X-Legal-Entity-Id`); a client-supplied supplier or LE is never used; Procurement unreachable → 503, zero writes | §0.4 D1, D4 |

### 0.1 Replay receipt — ÖNERİLEN (SPEC-DELTA "Create and submit"), BLOKE on DC-03

- Receipt scope `(TenantId, LegalEntityId, mappedSupplierId, validatedIssuer, subject, operationId, targetId-or-CREATE,
  exactIdempotencyKey)`; key 1..200, surrounding whitespace rejected (not trimmed), exact ordinal comparison.
- Fingerprint: typed parsed request over method, operation, target, body and `If-Match`; key order irrelevant, array order
  and string bytes significant, correlation excluded, duplicate JSON properties rejected.
- Same tuple + fingerprint → 200 with the original result, no growth. Same tuple, different fingerprint → proposed 409
  `IDEMPOTENCY_KEY_REUSED` (**not declared in v1** — contract successor). Concurrent identical requests → one commit, one replay.
- Remap S1→S2 or another actor/LE → separate receipt scope; a later binding revision may read an old receipt only after
  current authorization and eligibility pass. Retention follows the business record; expiry cannot reopen a duplicate create.
- `validatedIssuer` and `subject` come from the DC-03 binding, which does not exist yet — hence BLOKE.

### 0.2 Payload — contradiction with the frozen contract, BLOKE

The frozen v1 `CreatePortalSubmissionRequest.payload` is `{ type: object, additionalProperties: true }`. This pack's
"allowlisted shape and bounded size" (sections 4 and 12) is therefore **not** wire authority: a server that rejects a
payload v1 accepts changes the wire. Per-`submissionType` schemas, size and depth limits and reference validation need a
contract successor through the single CT Supplier Seam Owner, with consumer consents. Until then the pack's payload rule
is a target only.

### 0.3 Transaction, outbox and delivery — ÖNERİLEN (SPEC-DELTA "Persistence and evidence invariants")

- Create/submit commit business state, receipt, audit record and Pending outbox in one MongoDB transaction; denied
  operations write none of them. Publishing is outside this slice.
- "Exactly once" applies to the state and the outbox record; delivery is at-least-once, consumers deduplicate by EventId.
- Acceptance tests to add when authorised: abort leaves zero records; duplicate delivery ignored; crash between commit and
  publish recovered from the Pending record.

### 0.4 Supplier eligibility seam (DC-02) — DECIDED 2026-10-20 in part, seam not built

Decision record: `docs/records/decisions/2026-10/mvp6-supplier-dc02-owner-decision-01.md`. It decides the seam; it does not build it and gives no code authority.

- **D1 — source.** The portal's own supplier is checked with the frozen `POST /api/suppliers/validate` (a one-element
  `supplierIds`) through the gateway. This replaces the earlier `getSupplier` target in `CONCURRENCE-OPEN.md`.
- **D2-b — status.** Portal access and submission: `Active` only. A non-`Active` own supplier is **403** (AC-05: the
  supplier is the caller itself, not a referenced record) — not the 422 MOD-0147 uses.
- **D4 — LE.** The LE forwarded in `X-Legal-Entity-Id` is the MDM-validated one; never client-supplied.
- **Not decided here — caller identity.** D3 (caller's own token) does **not** apply: a supplier user must not hold
  `procurement.suppliers.read` (it reads every supplier of the LE). The call needs a service identity or a bound-actor
  read, both **BLOKE on DC-03** (actor binding).
- Unreachable / timeout → 503, zero writes (D5).
- Tests: T-0148-ELG-01, T-0148-ELG-02 (r3).

## 1. Module Summary

MOD-0148 owns the authenticated supplier actor's portal access status and module-owned submission lifecycle. The
proposed initial Phase B slice is backend-only and contract-first against
`docs/analysis/contracts/supplier-performance.openapi.yaml` (`SUPPLIER-PERFORMANCE` v1). Supplier identity is always
resolved server-side from the authenticated actor and cannot be overridden by a request.

## 2. Ownership and Boundaries

**Owns:** portal access projection, submission records/payload snapshots, submit/review lifecycle, own-record queries,
idempotency, audit/outbox evidence and correlation propagation.

**Consumes:** API Gateway/authenticated actor mapping, Event Bus and
**CONSUMES: SUPPLIER-BASE (merkez üretecek)** through a frozen mock contract.

**Does not own:** MOD-0140 Supplier master/onboarding/credentials, tenant or legal-entity selection, supplier KYC,
MOD-0147 evaluations/scorecards/risks, source document SoRs, gateway/shared permission seams or foreign persistence.

## 3. Owned Objects

| Object | Purpose |
|---|---|
| `SupplierPortalAccessProjection` | Server-resolved supplier access state and submission summary |
| `SupplierPortalSubmission` | Supplier-owned draft/submitted/reviewed payload record |
| Commands | Create own submission; submit own draft; internal review is a later bounded command |
| Queries | Get own portal status; list/get own submissions |
| API | `/portal/status` and `/portal/submissions/**` in `SUPPLIER-PERFORMANCE` v1 |
| Events | Portal submission submitted/reviewed |
| Permissions | `supplychain.supplier-portal.read`, `.submit`, `.review` |

## 4. Entity Fields

### SupplierPortalSubmission

| Field | Rule |
|---|---|
| `Id` / `SubmissionId` | Server-minted stable identifier |
| `TenantId`, `LegalEntityId` | Server-resolved; never request body |
| `SupplierId` | Server-resolved from authenticated actor; never request body/query override |
| `SubmissionType` | Frozen enum: corrective action/evidence/certificate/response |
| `Subject` | Required, trimmed, max 250 |
| `ReferenceType`, `ReferenceId` | Optional typed reference; referenced SoR is not copied |
| `Payload` | Submission snapshot object; allowlisted and size-limited by type validator |
| `Status` | `DRAFT`, `SUBMITTED`, `UNDER_REVIEW`, `ACCEPTED`, `REJECTED` |
| `Version` | Optimistic concurrency token |
| `CreatedAt`, `SubmittedAt` | Server/audited UTC timestamps |
| `CorrelationId` | Required UUID propagated through response/audit/event |
| Soft-delete/audit fields | Repo-standard `EntityBase` fields |

Proposed indexes are tenant-first: `(TenantId, LegalEntityId, SupplierId, Status, CreatedAt)` and unique replay
index `(TenantId, SupplierId, Operation, IdempotencyKey)` — **insufficient** (omits LegalEntity, actor and target); the
selected receipt scope is in §0.1 (ÖNERİLEN, BLOKE on DC-03).

## 5. Repo Scope

After all ready-for-dev gates pass, the proposed Phase B slice may write only:

- `services/Diten.SupplyChainService/src/**/Features/SupplierPortal/**`.
- `services/Diten.SupplyChainService/tests/**/SupplierPortal/**`.
- Module-specific API reference/audit evidence paths explicitly assigned by the implementation WP.
- This module pack's status/evidence fields.

The frozen `supplier-performance.openapi.yaml` is read-only implementation authority in Phase B. This draft grants
no current write permission to runtime paths.

## 6. Protected Paths

- `.antigravity/**`, all frozen contracts and MOD-0140 Supplier contract/schema.
- `gateway/Diten.ApiGateway/**/ocelot.json`, authentication mapping, shared permission registries/registrations.
- All frontend paths in this backend-only slice, Archive paths and the frozen shared layout.
- Other domain services and non-MOD-0148 features, including MOD-0147 runtime paths.
- Supplier master/profile/onboarding/identity persistence and direct foreign database access.

## 7. Dependencies

| Dependency | State | Rule |
|---|---|---|
| `SUPPLIER-PERFORMANCE` v1 | FROZEN | Portal paths are implementation authority |
| `SUPPLIER-BASE` | FROZEN in `docs/analysis/contracts/supplier.openapi.yaml` (x-owner MOD-0140, `780f8368a`, v1.2.0, sha256 `373ebb21bae15f152d1b9ac24e03f7bfd02e595d343cf68cf918da553f5f42f7`; first pinned at `c8badff96` v1.0) — identity surface only; consumption **decided in part** (DC-02, 2026-10-20, §0.4: `validateSuppliers`; caller identity on DC-03), seam unbuilt | **CONSUMES: SUPPLIER-BASE (merkez üretecek)**; no local substitute |
| Authenticated actor mapping | Central/Gateway seam | `supplierId` resolved server-side; client override forbidden |
| Event Bus | Platform dependency | Correlation-preserving outbox publication |
| Domain ownership | **CONCURRED** (DC-01, 2026-10-18) | Placement recorded in domain-config, DCP-009 `placed_non_canonical_modules`, module-id registry |

## 8. Runtime Constraints

- Proposed service is `Diten.SupplyChainService` on port 5066 (AGENTS.md §3; 5061 was CRM's port, corrected 2026-10-19); this draft does not authorize runtime work.
- Tenant, legal entity and supplier identity are resolved from trusted server context.
- Portal mutation schemas reject `supplierId`, `tenantId`, `legalEntityId` and unknown fields.
- Own-record queries never expose another supplier; inaccessible records return 404.
- Mutations require `Idempotency-Key`, UUID `X-Correlation-Id` and version checks where specified.
- State, receipt, audit and outbox commit in one transaction (ÖNERİLEN, §0.3); no silent partial success.
- Portal does not store Supplier credentials, KYC or base-profile replicas.
- SUPPLIER-BASE/auth mapping unavailability fails closed.

## 9. Layout & Shell Contract

`shell: none`. The proposed first slice has no Razor UI, route, menu, DataTable or localization surface.

## 10. Backend File Convention

Proposed feature root is `Features/SupplierPortal/` with separate `Commands`, `Queries`,
`Handlers/CommandHandlers`, `Handlers/QueryHandlers`, `Validators` and one models file. Each public command, query,
handler and validator stays in its own file. Five-layer CQRS, pipeline and response-envelope rules apply after
authorization; this section is not scaffold permission.

## 11. Frontend File Contract

Not applicable. A usable supplier-facing Razor surface requires a separate approved pack revision defining tenant
shell, actor journey, exact fields, DataTable choice, golden reference, denied state and seven-language localization.

## 12. Validation Rules

| Field/Header | Required | Rule | Pre-check |
|---|---|---|---|
| `Idempotency-Key` | Mutations | 1..200; surrounding whitespace **rejected (400), never trimmed** — ÖNERİLEN, §0.1 (AC-15) | Receipt scope of §0.1 (ÖNERİLEN, BLOKE on DC-03) |
| `X-Correlation-Id` | All operations | UUID | Propagate to response/audit/event |
| Supplier identity | Every operation | Authenticated actor mapping only | SUPPLIER-BASE central mock; fail closed |
| `SubmissionType` | Create | Frozen enum | Type-specific payload validator exists |
| `Subject` | Create | Trimmed, 1..250 | — |
| `Payload` | Create | Target: allowlisted shape and bounded size — **not wire authority** (v1 `additionalProperties: true`, §0.2) | No secret/token fields |
| `If-Match` | Submit/review | Current version | Conditional update |
| Status transition | Submit/review | Frozen lifecycle | Terminal record cannot be silently reopened |

## 13. Failure Path to Verify

- Missing/unmapped authenticated supplier returns 403 `PORTAL_SUPPLIER_IDENTITY_UNRESOLVED`; no write.
- Body/query attempts to override supplier/tenant/legal entity return 400; trusted context remains unchanged.
- Cross-supplier or cross-tenant lookup returns 404 without existence leakage.
- Same receipt tuple and fingerprint replays the original result without duplicate submission/event; a different
  fingerprint is 409 (proposed, not in v1) — §0.1.
- Stale `If-Match` returns 409 and never overwrites a concurrent update.
- Invalid payload or lifecycle transition returns the frozen 400/422 error with UUID correlation.
- SUPPLIER-BASE/auth mapping outage fails closed and does not grant portal access.

## 14. Authorization Convention

Actors: authenticated supplier actor for own read/create/submit; tenant reviewer only for a separately approved review
command. Proposed server-side permissions:

- `supplychain.supplier-portal.read`
- `supplychain.supplier-portal.submit`
- `supplychain.supplier-portal.review`

Permission definition/seed and supplier actor mapping are shared seams and are not authorized by this draft.

## 15. Gateway / API Routing Decision

The frozen contract base is `/api/supplier-performance`, with portal paths under `/portal/**`. A future
`integration-agent` WP owns Ocelot and authenticated supplier context propagation. It must verify Authorization,
tenant/legal-entity context, correlation and idempotency headers. This draft grants no gateway write.

## 16. Acceptance Criteria

- [ ] Implementation payloads and failures match `SUPPLIER-PERFORMANCE` v1 portal contract tests.
- [ ] Portal status resolves supplier exclusively from authenticated server context.
- [ ] Create body cannot bind supplier/tenant/legal-entity identifiers or unknown fields.
- [ ] List/get returns only the authenticated supplier's records; cross-scope reads return 404.
- [ ] Submit transitions one draft exactly once and emits one correlation-preserving lifecycle event.
- [ ] Idempotent replay and stale version paths create no duplicate or overwritten side effects.
- [ ] Unresolved identity and SUPPLIER-BASE outage fail closed without access or writes.
- [ ] No Supplier master/KYC/credential replica exists locally.
- [ ] First slice: a `SUBMITTED` portal record is resolvable as a same-scope `PORTAL_SUBMISSION` source for MOD-0147 (AC-25; BLOKE on the resolver).
- [ ] Later slice (not the first): G5 evidence links **accepted** portal feedback to MOD-0147 — requires the review operation (not in v1) or an approved external producer; `ACCEPTED` is never counted done without it.

## 17. Test Expectations

- DCP-002 identity verifier PASS and OpenAPI syntax/reference/mock validation.
- Domain tests for submission lifecycle and terminal-state behavior.
- Handler/API tests for actor-derived identity, forbidden override fields, idempotency and version conflict.
- Tenant/legal-entity/cross-supplier isolation and permission denial tests.
- Contract tests for each portal success/error response and event payload.
- Mongo integration tests follow DB-010 isolated database naming.
- E4 module runtime evidence when authorized; G5 remains open until cross-module E5 integration passes.

## 18. Ready-for-dev Checklist

- [x] Canonical ID/name passed DCP-002 on 2026-09-15.
- [x] Owned OpenAPI portal surface is frozen as `SUPPLIER-PERFORMANCE` v1.
- [x] Backend-only proposed first slice, identity boundary, paths, failures and acceptance are explicit.
- [x] Central CT publishes/freezes SUPPLIER-BASE and mock — done 2026-09-15 in `docs/analysis/contracts/supplier.openapi.yaml` (x-status FROZEN, x-owner MOD-0140, `c8badff96`), the day before this pack was committed.
- [ ] Central owner freezes authenticated supplier actor mapping behavior.
- [x] Placement: DC-01 CONCURRED 2026-10-18; domain-config / DCP-009 / registry aligned 2026-10-19.
- [ ] Supplier base identity/status + Tenant/LE eligibility (DC-02) — **decided in part 2026-10-20** (§0.4: source, status, LE); caller identity waits on DC-03; build seam (SCE / CT), not built.
- [ ] Actor binding + authorization (DC-03) — Platform/Auth security owner: one active Supplier per actor/Tenant/LE,
      binding revision and revocation fencing, trusted LE issuance; build seam, not built. **The portal's core path.**
- [ ] Shared contract, replay receipt, own-record persistence, portal-source resolver (with MOD-0147), fixture — OPEN in
      `docs/roadmap/plans/mvp6-mod0148-spec-split-01/CONCURRENCE-OPEN.md`.
- [ ] SupplyChain service audit trail accepted under AUD-001 (§21) — the service has no audit-ledger file (BL-615).
- [ ] Module owner reviews and explicitly changes `status` to `ready-for-dev`.
- [ ] Runtime implementation WP supplies base HEAD, agent lane and bounded allowed paths.

## 19. Implementation Notes

- **ASSUMPTION:** the initial slice is backend-only and uses `SupplierPortal` as feature root.
- **ASSUMPTION:** structured payload validation is selected by `submissionType`; arbitrary executable content and
  secrets are rejected.
- **Resolved 2026-10-19:** the ownership ASSUMPTION is replaced by DC-01 placement (above); the governance records are
  aligned in the same change.
- **CONSUMES: SUPPLIER-BASE (merkez üretecek)**; supplier identity is server-derived and opaque.

## 20. Follow-up Items

- SUPPLIER-BASE is published and frozen: `docs/analysis/contracts/supplier.openapi.yaml` (x-status FROZEN, x-owner MOD-0140, `c8badff96`, 2026-09-15). Still open and unchanged: **GAP: merkez CT üretmeli** the authenticated supplier actor mapping mock behavior; the consumption authority is decided in part (DC-02, 2026-10-20, §0.4); the portal's caller identity waits on DC-03.
- ~~Reconcile MOD-0148 ownership in domain-config and central records~~ — done 2026-10-19 (DC-01 governance alignment).
- Define and approve the internal review command if required beyond the frozen portal self-service surface.
- Separate permission/gateway integration WP after endpoints exist.
- Future supplier-facing UI pack revision and G5 integration with MOD-0147.

## 21. Audited Events

AUD-001 (`audit-trail-standard.md`). `Diten.SupplyChainService` has **no accepted audit trail** today: there is no
`tests/architecture/audit-ledger/Diten.SupplyChainService.md` (part of BL-615), and its module-local `*_audit`
collections are not an accepted §5.c equivalent trail. Per §10.1 the trail is a **dependency** and blocks
`ready-for-dev`. The actor (supplier user) is server-derived; no command here changes identity or permissions
(binding is DC-03's, in Auth), so none is K2 class.

| Komut | Olay adi | Nesne (tur) | Yol | Onceki/sonraki ya da degisen alanlar | Istisna sinifi + gerekce |
|---|---|---|---|---|---|
| CreateSupplierPortalSubmissionCommand | supplier-portal-submission.created | SupplierPortalSubmission | — (bagimlilik: SupplyChain izi) | AfterState: SubmissionType, Status, payload snapshot hash | — |
| SubmitSupplierPortalSubmissionCommand | supplier-portal-submission.submitted | SupplierPortalSubmission | — (bagimlilik: SupplyChain izi) | Before/After: Status; AfterState: SubmittedAt | — |
| ReviewSupplierPortalSubmissionCommand (later bounded command, not in the first slice) | supplier-portal-submission.reviewed | SupplierPortalSubmission | — (bagimlilik: SupplyChain izi) | Before/After: Status, ReviewOutcome | — |

Not recorded: the submission payload body itself (only its snapshot hash and type), supplier credentials (never held
by this module) and any secret the validator rejects.
