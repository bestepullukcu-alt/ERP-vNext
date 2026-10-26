---
id: MOD-0148
name: Supplier Portal
domain: supply-chain-execution
service: Diten.SupplyChainService
shell: none
golden_reference: none
entity_base: EntityBase
status: draft
status_note: "Review draft; v1 scope revision 2026-10-22, D2 eligibility, service identity gate and RCS-001 added 2026-10-26 (§0.0): MOD-0148 started (Natig) — v1 = the supplier's own submissions, internal only. No runtime code authority until this revision has CT-APR-02 and CT-APR-01 and the §0.0 gates (DC-03 binding pack and build, DC-02 / MOD-0140 1.3.0, PR #198, permissions, replay receipt) are met. Placement CONCURRED (DC-01, 2026-10-18). SUPPLIER-BASE frozen (supplier.openapi.yaml v1.2.0, 780f8368a)."
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

## 0.0 v1 scope — the supplier's own submissions for internal use only

Owner decision 2026-10-22 — revision draft.

> `docs/records/decisions/2026-10/mvp6-mod0148-start-owner-decision-2026-10-22.md` (Natig): MOD-0148 starts now, which is
> the D9 trigger for Beste's DC-03 binding. **Draft revision — not approved**; needs CT-APR-02 and a new CT-APR-01 row
> (high risk: a new actor type, authentication path and product code). Where this section and an older line below
> disagree about v1, this section wins.

| Area | v1 | Later |
|---|---|---|
| Operations | `getSupplierPortalStatus`, `createOwnPortalSubmission`, `submitOwnPortalSubmission`, own `GET /portal/submissions[/{id}]` | internal review command (`UNDER_REVIEW`/`ACCEPTED`/`REJECTED`) — contract successor |
| Actor (DC-03, approved 2026-10-21) | `supplier_user` from the DC-03 binding (Beste, separate Auth/Platform pack): separate portal subdomain, mandatory two-step verification, one account per email per tenant, Gateway allow-list `supplier_user` → `/api/supplier-performance/portal/*` + own session endpoints; `supplierId` resolved from the binding, never the request | — |
| Supplier (DC-02) | tenant-wide supplier (MOD-0140 1.3.0); `accessStatus` from the **DC-02 D2** mapping below; the eligibility read uses a service identity (below) | — |
| Exposure | **internal only** — not on the internet until `TrustedProxies`, Gateway rate limit (MOD-0032) and a shared limiter store exist (DC-03) | public portal |
| Payload (§0.2) | frozen v1 wire kept (any object, no schema, no field rejection); **request body limit 1 MiB** for create (Natig, 2026-10-22), measured on the service; over the limit → 400 `INVALID_REQUEST` with `details.limitBytes` (the frozen create declares neither 400 nor 413 — a documented exception, see "Undeclared statuses" below); stored as canonical JSON text + SHA-256, so `$`-prefixed or dotted keys are data, never Mongo operators | per-`submissionType` schemas, secret rejection and size/depth limits via a contract successor |
| MOD-0147 link | none (portal-source resolution belongs to MOD-0147's risk part, after TPRM; AC-25 is a later slice) | with MOD-0147 risk |
| Permissions | `getSupplierPortalStatus`, own list and get → `supplychain.supplier-portal.read`; create and submit → `.submit`; `.review` later. The `supplier_user` actor receives `.read`/`.submit` through the DC-03 binding pack (its role or seed — defined there). The service also checks `actor_type = supplier_user`: a company user calling `getSupplierPortalStatus`, the own list or create → 403 `PORTAL_SUPPLIER_IDENTITY_UNRESOLVED` (declared there); on own get and submit, which declare no 403, → 404 (no record is visible to an unmapped caller); a `supplier_user` outside `/portal/*` is refused by the Gateway allow-list. Whether the portal re-checks the two-step-verification claim or trusts Auth is decided with the DC-03 pack | `.review` |
| Audit (§21) | path b, `supplychain-merkezi-iletim` — **valid from the moment PR #198 is on `main`**; the actor recorded is the `supplier_user` (issuer, subject, mapped supplier id) | review command |
| UI | none in v1 (backend first, §9); the supplier screen is a later revision on the portal subdomain, 7-language scope | — |

**Still gates for v1 code:** the DC-03 binding pack and its build (Beste) — an **external** gate, not a slice of this pack;
DC-02 (MOD-0140 1.3.0 frozen on `main`, Cihan) and the **service identity for the eligibility read**: MOD-0140 1.4.0 DL-7
with BL-652 Option 2, SupplyChain's `service_name` on the accepted list (MOD-0140 S-5, Auth owner) and Procurement's own
MDM service identity (MOD-0140 §7 / S-2); PR #198 on `main`; this revision's CT-APR-02 + CT-APR-01; the
permission/composition single writer (§14); and the replay receipt (§0.1, needs DC-03's issuer/subject).

**Open-draft cap (Natig, 2026-10-22, revised after review round 2):** the cap counts **only `DRAFT` records** — at most
**200 open drafts per supplier + LE**; a create that would make the 201st draft → 422 `BUSINESS_RULE_VIOLATION` with
`details.code = PORTAL_DRAFT_LIMIT` and `details.limit = 200`. Submitting a draft frees its place, so the cap is never a
lifetime block. It is enforced **atomically**: the create transaction increments a per-supplier + LE counter document with a
conditional update (`openDrafts < 200`) and submit decrements it in its own transaction, so two concurrent creates at 199
leave at most 200 drafts. **Counter creation (closes review round 3 F-2):** the counter document has a **deterministic `_id`**
(`{tenantId}:{legalEntityId}:{mappedSupplierId}`), so two documents for one supplier + LE cannot exist even without a
secondary index; it is created by a separate step in the same transaction — an upsert that only sets `openDrafts = 0` on insert
(`$setOnInsert`) and never filters on the count — and only then is the conditional increment run, **without upsert**. No
matched document → 422 `PORTAL_DRAFT_LIMIT`, never an insert attempt.

**Contention (round 4 F-1, corrected after round 5 F-1):** the counter is one hot document per supplier + LE, so **both
create and submit** run inside the MongoDB driver's `WithTransaction` callback API (`MongoDB.Driver` 2.27.0, the service's
pinned version). What that API does, measured in 2.27.0 `TransactionExecutor`: an error labelled `TransientTransactionError`
(e.g. a write conflict) re-runs the whole callback **immediately, without backoff**; `UnknownTransactionCommitResult`
retries **only the commit**, so a committed draft is never created twice; the overall limit is 120 s. Two additions close
the gaps: (1) a **duplicate-key error (E11000) is not labelled transient**, so the counter upsert catches it and the
callback is re-run by a bounded outer loop (at most 3 runs) — the deterministic `_id` means the second run finds the
document; (2) every create and submit runs under a `CancellationToken` that expires **below the Gateway's request
timeout**, and a short random delay (10–50 ms) precedes each re-run of the callback. Ordinary contention (concurrent creates
at the cap, a create racing a submit) therefore ends in a business answer (201 / 422 for create; 200 / 404 / 422 for submit)
— never a misleading 409 (`VersionConflict` is `If-Match` only). A full cap is always 422. A request whose deadline expires
answers 503 with the correlation id (declared only for status — see "Undeclared statuses"); a commit may still have happened
in that case, which the replay receipt (§0.1, BLOKE on DC-03) will make safe to retry; until then the client sees the
draft in its own list. Tests: T-0148-V1-CAP-01
(201st draft → 422; submit frees a place; two concurrent creates at 199 → one 201/one 422; **counter at 200 → create → 422,
not 500**; two concurrent first creates for a new supplier + LE → both 201, one counter document with `openDrafts = 2`;
**a create concurrent with a submit → both succeed, counter consistent**; **five concurrent creates below the cap → all 201,
no 500**; an injected `UnknownTransactionCommitResult` → exactly one draft; **an injected E11000 on the first counter upsert →
201, not 500**; an expired deadline → 503, never a hanging request).

**Undeclared statuses (round 4 F-2, extended to all five operations after round 5 F-3):** the frozen wire declares no
default response, and v1 returns some statuses it does not declare. They are deliberate, documented exceptions, not
contract behaviour; a generated client may not know them. The contract successor declares them, together with the paged own
list.

| Operation | Declared (frozen v1) | Also returned in v1 (undeclared) |
|---|---|---|
| `getSupplierPortalStatus` | 200, 403, 503 | 401, 500 |
| `listOwnPortalSubmissions` | 200, 403 | 400 (invalid `status` / `submissionType` query value), 401, 503 (SUPPLIER-BASE / eligibility read unavailable, deadline), 500 |
| `createOwnPortalSubmission` | 201, 200, 403, 422 | 400 (body over 1 MiB, malformed JSON, scope override AC-12), 401, 503 (eligibility read unavailable, deadline), 500 |
| `getOwnPortalSubmission` | 200, 404 | 401, 503 (eligibility read unavailable), 500 |
| `submitOwnPortalSubmission` | 200, 404, 409, 422 | 400 (malformed JSON), 401, 503 (eligibility read unavailable, deadline), 500 |

**Supplier eligibility — DC-02 D2 (Natig 2026-10-23 / 2026-10-26; Cihan's MOD-0140 Revision 2,
`origin/feature/mvp2/supplier-tenant-01` @ `7f937c812`, §3.1 `SupplierEligibility`).** On every portal operation the service
reads `GET /api/suppliers/{supplierId}/eligibility` (SUPPLIER 1.3.0) for the binding's mapped supplier and **the LE of the
DC-03 binding — never a header the supplier chooses** (F-2): a request whose LE is not in the binding is answered before any
read with 403 `PORTAL_SUPPLIER_IDENTITY_UNRESOLVED` (status / list / create) or 404 (get / submit), so a supplier cannot learn
its relationship with another company. `X-Tenant-Id` is the tenant of the `supplier_user` token. The answer is mapped:

| `SupplierEligibility` answer | `accessStatus` | status | own list | create | own get | submit |
|---|---|---|---|---|---|---|
| Procurement **404 `UnknownSupplier`** (no relationship, unknown supplier, other tenant — MOD-0140 §3.1; on a 200 `relationshipExists` is always `true`, so a 200 with `relationshipExists != true` is handled the same way) | — | 403 `PORTAL_SUPPLIER_IDENTITY_UNRESOLVED` | 403 (same) | 403 (same) | 404 | 404 |
| `status = Active`, `onboardingStatus = Approved`, `purchasingEnabled = true` | `ACTIVE` | 200 | 200 | allowed | 200 | allowed |
| `status = Active`, but onboarding not `Approved` or `purchasingEnabled` not `true` | `PENDING` | 200 | 200 | 422 `BUSINESS_RULE_VIOLATION`, `details.code = PORTAL_SUPPLIER_PENDING` | 200 | 422 (same) |
| `status` = OnHold, Blocked or Inactive | `SUSPENDED` | 200 with `openSubmissionCount = 0` and no `lastSubmissionAt` (AC-05) | 403 (AC-05) | 403 (AC-05) | 404 (no 403 declared) | 404 (no 403 declared) |

**Deviation from AC-03 / AC-05, recorded (F-4):** AC-05 expects 403 on list, get, create and submit for `SUSPENDED`; get
and submit declare no 403, so v1 answers 404 there (the r3 oracle is this table). AC-03 expects "Active → operations
allowed"; v1 adds `PENDING` (owner decision, D2 Q3) where create and submit answer 422. The 422 on submit uses
`BUSINESS_RULE_VIOLATION` with `details.code = PORTAL_SUPPLIER_PENDING` deliberately, although submit's declared 422 example
is `INVALID_STATUS_TRANSITION` (the `Error` body is the same; the contract successor names it) (F-5).

`eligibleForTransactions` is not used (it ignores onboarding and purchasing; MOD-0140 §20 S-1). **Any other answer → 503,
no write (AC-04, F-1):** Procurement unreachable / 5xx / timeout; a 200 that does not match `SupplierEligibility`, has a null
`status`, or names a `supplierId` other than the binding's mapped supplier; a rejection of the service token (401 / 403) or
any other unexpected status. **Service identity (F-3):** the `supplier_user` token cannot make this read — the Gateway
allow-list keeps that actor inside `/portal/*`, and a supplier must never hold a Procurement permission. The read therefore
uses **a trusted service token with no permission claim, accepted by MOD-0140 DL-7** (for the two eligibility endpoints the
scheme itself is the authorization, MOD-0140 §14), with `X-Tenant-Id` = the token's tenant and a mandatory
`X-Legal-Entity-Id`. Its prerequisites are v1 gates (above). Tests: T-0148-V1-ELIG-01 (each table row with the Procurement
mock answering **404** for no relationship; a malformed 200, a mismatched `supplierId` and a service-token 401 → 503 with no
write; an LE outside the binding → no Procurement read at all; sabotage: forwarding the supplier's token instead of the
service identity, or taking the LE from the header, makes the test red).

**Visible code (RCS-001, BL-565):** a portal submission is a new record type, but the shared record-code service (BL-565)
does not exist yet. v1 shows **no visible code** — only the frozen contract's opaque `submissionId` — and never creates its
own counter or format. When BL-565 exists, a later revision takes the visible code (`MG-…` form) from that service.

**Own-list size (closes review round 3 F-1):** the frozen list has no paging and returns every record with its payload, and
`SUBMITTED` records are not capped, so the response can grow without bound (each record up to 1 MiB). v1 accepts this only
while the portal is internal. The service measures, on every own-list call, the record count and the total stored payload
bytes per supplier + LE and emits a warning (structured log + metric, no behaviour change) above **1,000 records or 100 MiB**
(confirmed by Natig, 2026-10-23). **Measurement (closes review round 4 F-3):** the count and bytes cover **all** non-deleted
records of the supplier + LE, whatever `status` / `submissionType` filter the call carries; "above" is strict — more than
1,000 records or more than 104,857,600 bytes (exactly 1,000 records → no warning); the metric carries only service and
operation labels, while tenant, LE and supplier ids are log fields, never metric labels. **Paging is a release-gate
precondition:** the portal may not be exposed outside the internal network until a contract successor with a paged own list
is on `main` (see the release gate below). Tests: T-0148-V1-LIST-01 (1,000 records → no warning, 1,001 → warning;
100 MiB + 1 byte → warning; a filtered call returning 3 records still measures the full total; response unchanged; no
supplier id among the metric labels).

**Release gate (internal only, measurable):** before any deployment the evidence shows that the portal subdomain and the
`/portal/*` routes are not published on any public ingress (Gateway route list + ingress configuration, recorded); opening
them needs `TrustedProxies`, the Gateway rate limit (MOD-0032) and a shared limiter store, each with its owner, and Natig's
approval, and a contract successor with a paged own list on `main` (review round 3 F-1) — owner: the MOD-0148 owner
(MVP-6 CT) writes it, Natig approves; evidence: the frozen successor of `supplier-performance.openapi.yaml` on `main` whose
`listOwnPortalSubmissions` takes `page` / `pageSize` and returns a paged envelope, plus the service passing its contract
tests (recorded with the commit).

v1 test IDs (added with this revision; the r2 test map stays unchanged): T-0148-V1-PAY-01 (any object accepted; 1 MiB + 1 →
400; `$`-keys stored as data), T-0148-V1-PERM-01 (company user: status/list/create → 403 `PORTAL_SUPPLIER_IDENTITY_UNRESOLVED`, get/submit → 404; mapping of §0.0), T-0148-V1-EXP-01
(release gate evidence, including the paged-list precondition), T-0148-V1-CAP-01, T-0148-V1-LIST-01 and T-0148-V1-ELIG-01 (above).

## 0. Requirement status and traceability (review 2026-10-19)

Every requirement below carries one of three labels. A label is a claim about authority, not about code (there is none).

| Label | Meaning |
|---|---|
| **ONAYLI** | Fixed today: the frozen wire surface of `SUPPLIER-PERFORMANCE` v1.0.0 FROZEN (`docs/analysis/contracts/supplier-performance.openapi.yaml`, `eb1306590`, sha256 `e311cd102bed0041…`), or a CONCURRED gate. |
| **ÖNERİLEN** | Selected target written in `docs/roadmap/plans/mvp6-mod0148-spec-split-01/SPEC-DELTA.md` (`ce3396656`, sha256 `6dce93fde8c4069a…`). **Not merged into this pack** — that delta may be merged only after its named owner artifacts are exact and hash-bound. Reviewable, not binding. |
| **BLOKE** | Depends on an OPEN gate in `docs/roadmap/plans/mvp6-mod0148-spec-split-01/CONCURRENCE-OPEN.md`; the named owner must close it. A fixture or mock never closes it. |

Where this pack's own text (sections 1–21) states a behaviour as a rule and the label below says ÖNERİLEN or BLOKE, **the
label wins**: the text is the target, not an approved rule.

| Endpoint (operationId) | Wire | Behaviour | Gate / decision owner | Tests (`ACCEPTANCE-TEST-MAP.r2.tsv`) |
|---|---|---|---|---|
| `GET /portal/status` (getSupplierPortalStatus) | ONAYLI | **BLOKE** — actor binding (DC-03), Tenant+LE eligibility (DC-02); status matrix ÖNERİLEN | Platform security owner; MOD-0140 owner | AC-03, AC-04, AC-08, AC-09 |
| `POST /portal/submissions` (createOwnPortalSubmission) | ONAYLI wire; **payload contradiction** (§0.2) | **BLOKE** — DC-03, DC-02; receipt ÖNERİLEN (§0.1) | security owner; CT Supplier Seam Owner | AC-05 (r3 oracle = §0.0 D2 table: suspended → 403 on list / create, 404 on get / submit), AC-12 (scope override → 400), AC-13 (create replay), AC-15 (fingerprint, whitespace key), AC-16 (remap), AC-17 (race) |
| `POST /portal/submissions/{id}/submit` (submitOwnPortalSubmission) | ONAYLI | ÖNERİLEN — only `DRAFT→SUBMITTED`; **BLOKE** — DC-03 | security owner | AC-14, AC-15, AC-17 |
| `GET /portal/submissions`, `GET /portal/submissions/{id}` | ONAYLI | ÖNERİLEN — Tenant+LE+mapped Supplier+non-deleted predicate; **BLOKE** — DC-03 | security owner | AC-05 (r3 oracle = §0.0 D2 table: suspended → 403 on list / create, 404 on get / submit), AC-10 (predicate), AC-11 (revocation fence) |
| Review operation | **none in v1** — the statuses `UNDER_REVIEW`, `ACCEPTED`, `REJECTED` **are** in the v1 enum | **BLOKE** — no command reaches them; not in the first slice | MOD-0148 owner; CT contract writer | — |
| Portal-source resolution for MOD-0147 | — | **BLOKE** — later slice, with MOD-0147's risk part (§0.0) | MOD-0147/Risk owner | AC-25 |
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

### 0.1 Replay receipt

ÖNERİLEN (SPEC-DELTA "Create and submit"), BLOKE on DC-03.

- Receipt scope `(TenantId, LegalEntityId, mappedSupplierId, validatedIssuer, subject, operationId, targetId-or-CREATE,
  exactIdempotencyKey)`; key 1..200, surrounding whitespace rejected (not trimmed), exact ordinal comparison.
- Fingerprint: typed parsed request over method, operation, target, body and `If-Match`; key order irrelevant, array order
  and string bytes significant, correlation excluded, duplicate JSON properties rejected.
- Same tuple + fingerprint → 200 with the original result, no growth. Same tuple, different fingerprint → proposed 409
  `IDEMPOTENCY_KEY_REUSED` (**not declared in v1** — contract successor). Concurrent identical requests → one commit, one replay.
- Remap S1→S2 or another actor/LE → separate receipt scope; a later binding revision may read an old receipt only after
  current authorization and eligibility pass. Retention follows the business record; expiry cannot reopen a duplicate create.
- `validatedIssuer` and `subject` come from the DC-03 binding, which does not exist yet — hence BLOKE.

### 0.2 Payload — contradiction with the frozen contract (resolved for v1 in §0.0: no validation, 1 MiB limit)

The frozen v1 `CreatePortalSubmissionRequest.payload` is `{ type: object, additionalProperties: true }`. This pack's
"allowlisted shape and bounded size" (sections 4 and 12) is therefore **not** wire authority: a server that rejects a
payload v1 accepts changes the wire. Per-`submissionType` schemas, size and depth limits and reference validation need a
contract successor through the single CT Supplier Seam Owner, with consumer consents. **v1 (Natig, 2026-10-22):** the wire
stays as frozen — no schema, no field rejection — and only the 1 MiB request limit applies (§0.0). T-0148-PAY-01 applies to the
contract successor, not to v1.

### 0.3 Transaction outbox and delivery

ÖNERİLEN (SPEC-DELTA "Persistence and evidence invariants").

- Create/submit commit business state, receipt, audit record and Pending outbox in one MongoDB transaction; denied
  operations write none of them. Publishing is outside this slice.
- "Exactly once" applies to the state and the outbox record; delivery is at-least-once, consumers deduplicate by EventId.
- Acceptance tests to add when authorised: abort leaves zero records; duplicate delivery ignored; crash between commit and
  publish recovered from the Pending record.

## 1. Module Summary

MOD-0148 owns the authenticated supplier actor's portal access status and module-owned submission lifecycle. The
proposed initial Phase B slice is backend-only and contract-first against
`docs/analysis/contracts/supplier-performance.openapi.yaml` (`SUPPLIER-PERFORMANCE` v1). Supplier identity is always
resolved server-side from the authenticated actor and cannot be overridden by a request.

## 2. Ownership and Boundaries

**Owns:** portal access projection, submission records/payload snapshots, submit lifecycle (review: later slice), own-record queries,
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
| Permissions | `supplychain.supplier-portal.read`, `.submit`; `.review` with the later review command |

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
| `Payload` | v1: any object (frozen wire), request ≤ 1 MiB, stored as canonical JSON text + SHA-256; type schemas later (§0.0) |
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
| `SUPPLIER-BASE` | FROZEN in `docs/analysis/contracts/supplier.openapi.yaml` (x-owner MOD-0140, `780f8368a`, v1.2.0, sha256 `373ebb21bae15f152d1b9ac24e03f7bfd02e595d343cf68cf918da553f5f42f7`; first pinned at `c8badff96` v1.0) — identity surface only; consumption authority open (DC-02) | **CONSUMES: SUPPLIER-BASE (merkez üretecek)**; no local substitute |
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
| `SubmissionType` | Create | Frozen enum | v1: no type-specific payload validator (§0.0) |
| `Subject` | Create | Trimmed, 1..250 | — |
| `Payload` | Create | v1: any object; request body ≤ 1 MiB, else 400 `INVALID_REQUEST` (`details.limitBytes`) | Secret/token rejection: contract successor, not v1 |
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

Actors: authenticated `supplier_user` (DC-03) for own read/create/submit; tenant reviewer only for the later review command.
v1 mapping (§0.0): status, own list, own get → `.read`; create, submit → `.submit`; company users are refused on `/portal/*`
(403 `PORTAL_SUPPLIER_IDENTITY_UNRESOLVED` on status, list and create; 404 on own get and submit — §0.0). Server-side
permissions:

- `supplychain.supplier-portal.read`
- `supplychain.supplier-portal.submit`
- `supplychain.supplier-portal.review` (later review command)

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
- [ ] **Later slice (with MOD-0147's risk part):** a `SUBMITTED` portal record is resolvable as a same-scope `PORTAL_SUBMISSION` source for MOD-0147 (AC-25; BLOKE on the resolver).
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
- [ ] Central owner freezes authenticated supplier actor mapping behavior — this is the DC-03 binding pack (Beste), below.
- [x] Placement: DC-01 CONCURRED 2026-10-18; domain-config / DCP-009 / registry aligned 2026-10-19.
- [ ] Supplier base identity/status + Tenant/LE eligibility (DC-02) — MOD-0140 owner; build seam, not built.
- [ ] Actor binding + authorization (DC-03) — Platform/Auth security owner: one active Supplier per actor/Tenant/LE,
      binding revision and revocation fencing, trusted LE issuance; build seam, not built. **The portal's core path.**
      Decisions approved 2026-10-21 (plan v10 C4); builder Beste (D9), triggered by the MOD-0148 start of 2026-10-22 (§0.0).
- [ ] Shared contract, replay receipt, own-record persistence, portal-source resolver (with MOD-0147), fixture — OPEN in
      `docs/roadmap/plans/mvp6-mod0148-spec-split-01/CONCURRENCE-OPEN.md`.
- [ ] SupplyChain service audit trail accepted under AUD-001 (§21) — **met when PR #198 merges** (ledger `Diten.SupplyChainService.md`, trail `supplychain-merkezi-iletim`, path b).
- [ ] Module owner reviews and explicitly changes `status` to `ready-for-dev`.
- [ ] Runtime implementation WP supplies base HEAD, agent lane and bounded allowed paths.

## 19. Implementation Notes

- **ASSUMPTION:** the initial slice is backend-only and uses `SupplierPortal` as feature root.
- **v1 (Natig, 2026-10-22):** no structured payload validation; the payload is stored as data and never executed or
  rendered as HTML. Per-`submissionType` validation and secret rejection come with the contract successor.
- **Resolved 2026-10-19:** the ownership ASSUMPTION is replaced by DC-01 placement (above); the governance records are
  aligned in the same change.
- **CONSUMES: SUPPLIER-BASE (merkez üretecek)**; supplier identity is server-derived and opaque.

## 20. Follow-up Items

- SUPPLIER-BASE is published and frozen: `docs/analysis/contracts/supplier.openapi.yaml` (x-status FROZEN, x-owner MOD-0140, `c8badff96`, 2026-09-15). Still open and unchanged: **GAP: merkez CT üretmeli** the authenticated supplier actor mapping mock behavior; and the authority to consume SUPPLIER-BASE — DC-02, AWAITING-CT per the 2026-10-05 CT ruling.
- ~~Reconcile MOD-0148 ownership in domain-config and central records~~ — done 2026-10-19 (DC-01 governance alignment).
- Define and approve the internal review command if required beyond the frozen portal self-service surface.
- Separate permission/gateway integration WP after endpoints exist.
- Future supplier-facing UI pack revision and G5 integration with MOD-0147.

## 21. Audited Events

AUD-001 (`audit-trail-standard.md`). Create and submit use **path b, trail `supplychain-merkezi-iletim`** (ledger
`tests/architecture/audit-ledger/Diten.SupplyChainService.md`), **valid from the moment PR #198 is on `main`**; until then it
is a v1 gate (§0.0). The recorded actor is the `supplier_user` — issuer, subject and the mapped supplier id from the DC-03
binding, actor type supplier. No command here changes identity or permissions (binding is DC-03's, in Auth), so none is K2
class.

| Komut | Olay adi | Nesne (tur) | Yol | Onceki/sonraki ya da degisen alanlar | Istisna sinifi + gerekce |
|---|---|---|---|---|---|
| CreateSupplierPortalSubmissionCommand | supplier-portal-submission.created | SupplierPortalSubmission | b — supplychain-merkezi-iletim (PR #198 main'de) | AfterState: SubmissionType, Status, payload snapshot hash | — |
| SubmitSupplierPortalSubmissionCommand | supplier-portal-submission.submitted | SupplierPortalSubmission | b — supplychain-merkezi-iletim (PR #198 main'de) | Before/After: Status; AfterState: SubmittedAt | — |
| ReviewSupplierPortalSubmissionCommand (later bounded command, not in the first slice) | supplier-portal-submission.reviewed | SupplierPortalSubmission | — (bagimlilik: SupplyChain izi) | Before/After: Status, ReviewOutcome | — |

Not recorded: the submission payload body itself (only its SHA-256 and type), supplier credentials (never held by this
module). v1 has no secret validator; the payload is data only.

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | portal durumu ve kendi kayıtlarını okuma, oluşturma | v1 scope — the supplier's own submissions for internal use only, Replay receipt, Owned Objects, Entity Fields, Runtime Constraints, Validation Rules, Failure Path to Verify, Authorization Convention |
| DL-2 | gönderim (DRAFT → SUBMITTED) ve değişmezlik | Transaction outbox and delivery, Failure Path to Verify, Audited Events |

DL-1: durum, kendi listesi ve kaydı, oluşturma, D2 uygunluk eşlemesi ve servis kimliği (§0.0), v1 eşlemesi (§14). DL-2:
gönderim (submit) ve değişmezlik, denetim (§21).

DC-03 bağlaması (Beste, ayrı Auth/Platform paketi), iç inceleme komutu (sözleşme halefi) ve tedarikçi ekranı (portal alt
alanı, 7 dil) bu paketin dilimi **değildir**: ilki dış kapıdır, diğer ikisi ayrı bir paket revizyonu ve onayıyla gelir.
CT-APR-01 satırının not alanı: "yalnız DL-1, DL-2".
