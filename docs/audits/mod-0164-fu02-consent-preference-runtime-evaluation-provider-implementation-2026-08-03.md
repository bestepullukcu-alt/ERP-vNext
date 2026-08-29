# MOD-0164-FU02 — Consent & Preference Runtime / Evaluation Provider Implementation

**Date:** 2026-08-03
**Module:** MOD-0164 (Consent & Preference Management)
**Service:** Diten.CrmService (port 5061, Gateway 5000)
**Target tenant (smoke):** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
**Branch:** `feature/crm-integration`
**Verdict:** **PASS** (API runtime + evaluation provider complete and tested; authenticated Gateway live smoke **65/65 PASS** on the target tenant; admin UI deferred as an explicitly authorized API-only delivery — see §24)

---

## 1. Preflight

| Check | Result |
|---|---|
| Task type | Runtime implementation (backend + gateway + tests) |
| FU01 boundary pack read | `docs/audits/mod-0164-consent-preference-management-boundary-pack-authorization-2026-08-02.md` (**PASS**) + `execution/domains/commercial-suite/module-packs/MOD-0164-FU01-consent-preference-management-boundary.md` |
| Golden implementation reference | MOD-0165-FU03 `VisitFrequencyPolicy` (aggregate + read-only resolve provider) — the closest structural analogue |
| Clean-architecture layout confirmed | Domain / Persistence / Application / Infrastructure / Api |
| Existing MOD-0150 consent seam inspected | `Features/ConsentPreference/{ContactConsentPreferenceModels,IContactConsentPreferenceReader}.cs` + `NullContactConsentPreferenceReader` — left **untouched** (§14) |
| CRM conventions re-confirmed | string-Guid class maps (binary-subtype trap), DateTimeOffset-as-BSON-array (no parallel-array index/sort), `$ne` forbidden in partial-index filters |
| MediatR auto-registration | Handlers/validators are discovered from the Application assembly — no manual wiring needed |
| Build isolation | Isolated `OutputPath` used for test builds because the running fleet holds the normal `bin` DLLs |
| Registry write | **Not performed** (out of scope) |
| MOD-0048 publish | **Not performed** (out of scope; vocabulary is in-domain/structural) |
| RBAC seed/grant | **Not performed** (out of scope; documented fallback used — §9) |
| Mongo hand-edit | **None** (the only Mongo access was a read-only index/count listing — §22) |

## 2. Dependency Confirmation

All listed prerequisites are treated as PASS and were **not modified** by this task:

| Dependency | Status | Interaction in FU02 |
|---|---|---|
| MOD-0164-FU01 Consent & Preference Boundary | PASS | This is the runtime of the FU01-authorized model |
| MOD-0165-FU02 Campaign / Targeting Boundary | PASS | No campaign target created/read/written; provider is consumable later (§11) |
| MOD-0165-FU03 Visit Frequency Policy Implementation | PASS | Untouched; used only as the structural template |
| MOD-0151-FU09B Frequency Provider Integration | PASS | Untouched; no readiness/route code changed |
| MOD-0150 Contact Availability | PASS | Untouched; availability vs. preference separation enforced (§14) |
| MOD-0162-FU01/FU01A/FU01B/FU01C Knowledge/Content/Path/Journey/Concept | PASS | Untouched; `knowledge-content` exists only as a consent **scope type** |
| MOD-0290-FU01 Brand/Product Boundary | PASS | Untouched; `brand`/`product` exist only as consent **scope types** (ids supplied by the caller) |
| MOD-0028 / MOD-0029 Files & Controlled Documents | Live | `EvidenceRef` points at them; **no file read/copy/render** |
| MOD-0155 Visit/Route Planning | **Not started** | Provider prepared for it; nothing planning-related implemented (§13) |
| MOD-0167 Segmentation | Boundary only | Consent filter provider exists; **no segment engine** (§12) |

## 3. Scope Confirmation

**Implemented (all 15 requested items):** ConsentRecord aggregate · PreferenceRecord aggregate · consent create/read/list/update/archive · preference create/read/list/update/archive · read-only consent evaluation provider · purpose/channel/legal-basis/status validation · preference restriction handling · effective-window validation · `EvidenceRef` boundary (format-level) · `ExternalReferences` boundary · contract flags · Gateway routes · 35 tests · Gateway smoke (unauthenticated executed; authenticated scripted for the operator) · this evidence report.

**NOT implemented (boundary preserved):** campaign target runtime · campaign engine · segmentation engine · frequency runtime · visit planning · route planning · due/overdue · last-visit history · digital detailing · content recommendation · KnowledgeContent implementation · Account/Contact mutation · ContactAvailability mutation · Territory mutation · workflow/approval · file upload/render · import/export · patient data · hard delete · Mongo hand-edit · RBAC seed/grant · MOD-0048 publish · registry write.

## 4. Implementation Summary

Two new aggregates in **Diten.CrmService** with their own collections (`consent_records`, `preference_records`), plus a single read-only evaluation seam:

- Full **CRUD-minus-delete** (create / update / archive / get / list) over MediatR handlers for both aggregates.
- A pure, deterministic, **fail-closed evaluation engine** exposed at `GET /api/crm/consents/evaluate` and available in-process as `IConsentPreferenceEvaluator`.
- A contract endpoint advertising exactly the **six** allowed consent flags — and none of the forbidden ones.
- **In-domain (structural) vocabulary validation**, so the runtime is ready without a MOD-0048 publish and never fails open on an unpublished set.
- Dedicated Gateway routes exposing **GET/POST/PUT/OPTIONS only** (no DELETE anywhere in the chain).
- 35 tests; the whole CRM suite is green (581 passed / 0 failed / 5 pre-existing skips).

Design decision recorded up front: the **question dimensions are immutable**. `SubjectType`/`SubjectId`, `Channel`, `Purpose`, `ScopeType`/`ScopeId` (and `PreferenceType` for preferences) cannot be changed by `PUT` — a different question is a different record. This is what structurally prevents a permission being silently repurposed, which was the FU01 §12 "no silent overwrite" requirement. A **status transition** (e.g. `granted → withdrawn`) *is* allowed on `PUT`, is audit stamped, and never deletes or blanks the record.

## 5. ConsentRecord Model

`ConsentRecord : EntityBase` — `services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/ConsentRecord.cs`

`ConsentId` (=`Id`) · `TenantId` (**claim-only**) · `SubjectType` · `SubjectId` · `ScopeType?` · `ScopeId?` · `Channel` · `Purpose` · `LegalBasis` · `ConsentStatus` · `EffectiveFrom` · `EffectiveTo?` · `Source` · `EvidenceRef?` · `WithdrawalReason?` · `Notes?` · `ExternalReferences[]` · `CreatedAt/By` · `UpdatedAt/By` · `ArchivedAt?/By?`

Vocabulary (all in-domain, validated structurally):

| Dimension | Values |
|---|---|
| `SubjectType` | `contact` · `account-contact-link` · `account` · `audience-profile` · `campaign-target` |
| `Channel` | `visit` · `email` · `sms` · `phone` · `whatsapp` · `portal` · `digital-detailing` · `training` · `other` |
| `Purpose` | `campaign` · `medical-visit` · `product-information` · `training` · `marketing` · `service` · `compliance` · `research` · `other` |
| `LegalBasis` | `explicit-consent` · `contract` · `legal-obligation` · `legitimate-interest` · `public-interest` · `vital-interest` · `other` |
| `ConsentStatus` | `granted` · `denied` · `withdrawn` · `restricted` · `unknown` · `expired` |
| `ScopeType` | `brand` · `product` · `campaign` · `segment` · `therapeutic-area` · `knowledge-content` · `other` |
| `Source` | `subject-declared` · `field-capture` · `portal` · `consent-center` · `legacy-import` · `contract-document` · `manual` · `other` |

Rules enforced (each returns 400 unless stated):

- `TenantId` is never accepted from a payload — no write contract exposes it (test T02), and a missing tenant claim refuses the write.
- **No general consent flag.** Evaluation is always `subject × channel × purpose × scope × time`; a channel/purpose permission is never transferable (tests T09/T10, smoke step 5).
- `SubjectType` / `SubjectId` / `Channel` / `Purpose` / `LegalBasis` / `ConsentStatus` / `Source` / `EffectiveFrom` are all **required** and never defaulted.
- `EffectiveTo < EffectiveFrom` → 400.
- Unknown vocabulary value → 400 (a typo is rejected rather than stored and later read as a harmless "unknown").
- `ScopeId` without `ScopeType` → 400.
- `ConsentStatus = withdrawn` without `WithdrawalReason` → 400; the reason is preserved forever afterwards.
- `unknown` is an explicit authored state and is **never** evaluated as allowed.
- Withdrawal does not delete the old record: it is a new status on the same record (or a new record), always audit stamped.
- **No hard delete** anywhere; archive is the soft lifecycle (`ArchivedAt/By` stamped), archived rows stay readable but are excluded from evaluation.
- `EvidenceRef`, when supplied, must be a well-formed MOD-0028/MOD-0029 pointer; no file is copied.

## 6. PreferenceRecord Model

`PreferenceRecord : EntityBase` — same file.

`PreferenceId` (=`Id`) · `TenantId` (claim-only) · `SubjectType` · `SubjectId` · `Channel` · `PreferenceType` · `PreferenceValue` · `Priority` · `EffectiveFrom` · `EffectiveTo?` · `Source` · `Notes?` · `ExternalReferences[]` · `CreatedAt/By` · `UpdatedAt/By` · `ArchivedAt?/By?`

`PreferenceType`: `preferred-channel` · `do-not-contact` · `do-not-visit` · `preferred-visit-window` · `language-preference` · `content-preference` · `frequency-cap` · `topic-interest`.

Rules enforced:

- **A preference never substitutes for consent** and can never grant: the engine only ever uses it to turn `allowed`/`unknown` into `blocked`. There is deliberately **no preference-evaluate endpoint**, so a caller cannot read a preference as permission.
- A restrictive preference blocks even a **granted** consent (tests T19/T20, smoke steps 6–7).
- **An absent preference invents no default** (test T21).
- `EffectiveTo < EffectiveFrom` → 400; `Priority` is required and ≥ 1 (deterministic, never auto-defaulted).
- Restrictive boolean types (`do-not-contact`, `do-not-visit`) require a boolean literal value — only `true` restricts, and `false` never reads as an opt-in. An ambiguous value (`"sometimes"`) is rejected at write time so the engine never has to guess at evaluation time.
- `frequency-cap` requires a positive integer and is **advisory only** — surfaced as `preference_frequency_cap`, never blocking. The frequency policy SoR stays MOD-0165 and no frequency runtime is opened here.
- `preferred-channel` values must be a known channel.
- No hard delete; archive is the soft lifecycle.

**Documented model decision — the `all` channel sentinel.** `PreferenceRecord.Channel` accepts the nine consent channels **plus** `all`, meaning "every channel". Without it, a blanket do-not-contact would require nine records and a missed one would fail *open*. The sentinel exists for preferences only; consent never uses it, because a channel permission is not transferable. `do-not-visit` restricts the `visit` channel only, whatever channel it was authored on.

**Availability vs. preference** (MOD-0150 separation, §14): availability = *when* is the subject available; preference = *which channel / restriction / preference*. `preferred-visit-window` is a preference signal and does **not** replace a MOD-0150 availability row.

## 7. Evaluation Provider Contract

Interface: `IConsentPreferenceEvaluator` (`Features/ConsentPreference/Evaluation/IConsentPreferenceEvaluator.cs`) — implemented by `ConsentPreferenceEvaluator`, registered scoped in `Application/DependencyInjection.cs`. It is the **single source of truth**: the HTTP endpoint and every future in-process consumer (MOD-0155, MOD-0165 FU04, MOD-0167) call *this*; no consumer copies the engine, and no consumer needs raw consent read access.

The deterministic decision logic lives in the pure static `ConsentEvaluationEngine.Evaluate(request, consents, preferences, now)` — no I/O, no writes, unit-testable in isolation.

**Endpoint:** `GET /api/crm/consents/evaluate`

Query: `subjectType` · `subjectId` · `channel` · `purpose` · `effectiveAt?` · `scopeType?` · `scopeId?` · `includeDiagnostics?` (default `true`).

**Response** (`ConsentEvaluationResult`): `eligibilityStatus` · `decision` · `subjectType` · `subjectId` · `channel` · `purpose` · `scopeType` · `scopeId` · `effectiveAt` · `matchedConsentId` · `matchedPreferenceIds[]` · `reasonCodes[]` · `selectionReason` · `candidateConsents[]` · `candidatePreferences[]` · `evaluatorVersion` · `evaluatedAt`.

| Enum | Values |
|---|---|
| `EligibilityStatus` | `allowed` · `blocked` · `unknown` · `not_applicable` (reserved; never emitted by FU02) |
| `Decision` | `consent_granted` · `consent_blocked` · `consent_unknown` · `preference_restricted` · `not_applicable` (reserved) |

Reason codes: `consent_granted` · `consent_denied` · `consent_withdrawn` · `consent_restricted` · `consent_unknown` · `consent_expired` · `consent_not_effective` · `no_matching_consent` · `preference_do_not_contact` · `preference_do_not_visit` · `preference_channel_blocked` · `preference_frequency_cap` · `preference_restricted` · `consent_selected_by_specificity` · `consent_selected_by_latest_effective_from` · `consent_selected_by_restrictive_status` — plus these FU02 additions (the FU01 list was a suggestion set): `consent_selected_by_stable_id` · `consent_scope_mismatch` · `consent_archived` · `preference_not_effective` · `preference_advisory_only` · `consent_ambiguous_conflict` · `consent_evaluation_error`.

Provider guarantees:

- **GET/read-only, write-free.** Verified structurally (test T25 fails on any repository write during evaluation) and behaviourally (smoke step 14 compares record counts before/after).
- No consent ⇒ `unknown`; **`unknown` is not `allowed`**.
- Denied / withdrawn / restricted ⇒ `blocked`.
- A restrictive preference ⇒ `blocked` even over a granted consent.
- An absent preference preserves the consent outcome.
- **The provider never 500s.** An internal failure returns a controlled `unknown` carrying `consent_evaluation_error` (test T35), because a 500 tempts a caller into falling back to "allowed".
- Nothing is chosen silently: every outcome carries reason codes, a human-readable `selectionReason`, and (unless suppressed) full candidate diagnostics with a per-row reason.
- `evaluatorVersion` (`mod-0164-fu02.v1`) + `evaluatedAt` let a consumer store provenance without copying any consent data.

One deliberate contract detail: an unrecognized `channel`/`purpose`/`subjectType` on evaluate returns **400**, not `unknown`. A malformed *question* must not come back as a benign-looking answer.

## 8. Fail-Closed Resolution Rules

Order implemented in `ConsentEvaluationEngine`:

1. **Tenant match** — applied by the repository; never widened in the engine.
2. **Subject match** — exact `SubjectType` + `SubjectId`; consent is never inherited from a broader subject.
3. **Channel match** — exact.
4. **Purpose match** — exact.
5. **Scope specificity** — scope instance (rank 1) > scope kind (2) > general (3).
6. **Effective window** — not-yet-effective (`consent_not_effective`) and expired (`consent_expired`) records are eliminated, visibly.
7. **Restrictive status wins** — `denied`/`withdrawn`/`restricted` (1) > `granted` (2) > `unknown` (3).
8. **Latest `EffectiveFrom`.**
9. **Stable `ConsentId`** tie-breaker.

Consequences, all pinned by tests:

- At equal specificity a restrictive record beats a granted one (T12/T13/T14).
- A record whose window has closed, or whose status is authored `expired`, is **never allowed** but stays visible as a reason code (T15).
- A scope-specific record outranks the general record **inside its scope** (T16). Conversely a scope-bound record never answers the general question — it is eliminated with `consent_scope_mismatch` (T16). This follows the FU01 ordering literally (specificity before status): a brand-scoped grant governs that brand, and a brand-scoped denial does not silently blanket-block every other brand.
- No matching consent ⇒ `unknown` (T10/T11).
- A full same-band tie is still resolved deterministically by stable id **and** flagged `consent_ambiguous_conflict` so the ambiguity is visible rather than hidden (T18).
- Archived records are excluded at both the repository filter and the engine (defence in depth) — `consent_archived`.

## 9. CRUD / Authoring Endpoints

| Method | Path | Permission (fallback in use) |
|---|---|---|
| GET | `/api/crm/consents/contract` | `crm.consent.read` (`crm.territory.read`) |
| GET | `/api/crm/consents` | `crm.consent.read` (`crm.territory.read`) |
| GET | `/api/crm/consents/evaluate` | `crm.consent.evaluate` (`crm.territory.read`) |
| GET | `/api/crm/consents/{consentId:guid}` | `crm.consent.read` (`crm.territory.read`) |
| POST | `/api/crm/consents` | `crm.consent.manage` (`crm.territory.model.manage`) |
| PUT | `/api/crm/consents/{consentId:guid}` | `crm.consent.manage` (`crm.territory.model.manage`) |
| POST | `/api/crm/consents/{consentId:guid}/archive` | `crm.consent.manage` (`crm.territory.model.manage`) |
| GET | `/api/crm/preferences` | `crm.preference.read` (`crm.territory.read`) |
| GET | `/api/crm/preferences/{preferenceId:guid}` | `crm.preference.read` (`crm.territory.read`) |
| POST | `/api/crm/preferences` | `crm.preference.manage` (`crm.territory.model.manage`) |
| PUT | `/api/crm/preferences/{preferenceId:guid}` | `crm.preference.manage` (`crm.territory.model.manage`) |
| POST | `/api/crm/preferences/{preferenceId:guid}/archive` | `crm.preference.manage` (`crm.territory.model.manage`) |

- **No DELETE** — not on a controller, not in a command namespace, not on a repository interface, not in the Gateway route methods (test T24 asserts all four).
- Archived record: readable, excluded from evaluation, `PUT` → **409**.
- `TenantId` never in a payload.
- Gateway routes added to `gateway/Diten.ApiGateway/ocelot.json` → `localhost:5061`, methods `GET/POST/PUT/OPTIONS` only. **No direct-5061 business smoke was performed**; the only direct call is `/health`.
- **Permission note:** the canonical keys `crm.consent.read|manage|evaluate` and `crm.preference.read|manage` are *defined* in `ConsentPreferencePermissions` but **not seeded** (RBAC seed/grant is out of scope). The endpoints therefore run on the documented territory fallback, exactly as MOD-0165 FU03 does. The fallback widens nothing — every FU02 guard still runs behind it. Follow-up: **MOD-0164-FU-RBAC**.

## 10. Contract Flags

`GET /api/crm/consents/contract` emits exactly:

```json
{
  "supportsConsentManagement": true,
  "supportsPreferenceManagement": true,
  "supportsConsentEvaluation": true,
  "supportsConsentPurposeChannelScope": true,
  "supportsConsentEvidenceReference": true,
  "supportsConsentFilterProvider": true
}
```

Forbidden flags — `supportsCampaignEngine`, `supportsVisitPlanning`, `supportsRoutePlanning`, `supportsDigitalDetailing`, `supportsRecommendationEngine`, `supportsWorkflowApproval` — are **absent**, and are not emitted as `false` either: advertising a capability even as false would misrepresent the boundary. Test T31 asserts both the absence and that the flag object has exactly six members.

The contract also surfaces the full authoring vocabulary, the evaluation vocabulary (`eligibilityStatuses`, `decisions`, `evaluatorVersion`) so consumers can be written against the contract instead of observed strings, the permission keys, and 19 explicit limitations.

## 11. Campaign / Targeting Boundary

- **No** `CampaignTarget` is created, read, snapshotted or written. No campaign engine, no segment→target resolution.
- `campaign` exists only as a consent **`ScopeType`** (the caller supplies the id) — that is scoping, not campaign runtime.
- MOD-0165 FU04 will be able to consume `IConsentPreferenceEvaluator` and store **only** provenance on its own target: `decision`, `reasonCodes`, `evaluatedAt`, `matchedConsentId`, `matchedPreferenceIds`, `evaluatorVersion`. FU02 provides those fields and writes none of them anywhere.
- No consent data is copyable into a target: the result carries ids and reason codes, not consent payloads.
- Response-shape guard (test T32 + smoke step 13) forbids any `campaignTargetId`-shaped field in the FU02 surface.

## 12. Segmentation Boundary

- No segment engine, no CDP runtime, no membership calculation, no dynamic resolution.
- The **consent filter provider** is supplied (MOD-0167's dependency is satisfiable) but is not wired into any segment code by this task.
- `segment` exists only as a consent `ScopeType`; a segment id is never expanded to members.

## 13. MOD-0155 Boundary

MOD-0155 has not started, and FU02 opened nothing on its behalf: **no** visit plan, route plan, due/overdue, last-visit read, schedule, execution or route optimizer.

What MOD-0155 will consume: `allowed` ⇒ keep evaluating the candidate; `blocked` ⇒ candidate blocked/not-ready with the visible reason code; `unknown` ⇒ unknown/not-ready (**never** allowed). The final blocking decision stays in MOD-0155 — MOD-0164 reports, it does not enforce.

## 14. MOD-0150 Contact / Availability Boundary

- **No flat consent field was added anywhere.** `Contact.ConsentStatus`, `Contact.MarketingConsent`, `Contact.VisitConsent`, `AccountContactLink.ConsentStatus` do not exist and are not introduced. Consent/preference live only in `ConsentRecord` / `PreferenceRecord` + the evaluation provider.
- No Contact, AccountContactLink, Account, ContactAvailability or ContactAvailabilityException record is read or mutated by FU02. Subject ids are supplied by the caller; there is no cross-aggregate FK by design.
- The availability/consent/preference separation is preserved: availability = time; consent = permission per channel/purpose; preference = channel/restriction.
- **Left deliberately untouched:** the MOD-0150 Contact 360 seam (`IContactConsentPreferenceReader` / `NullContactConsentPreferenceReader`) still returns the controlled `not-available` no-op. Re-pointing it at the now-live MOD-0164 store would change Contact 360 behaviour, which is outside this task's scope. Follow-up: **MOD-0164-FU04 — Contact 360 consent/preference seam activation**.

## 15. EvidenceRef / Document Boundary

`ConsentEvidenceRef` = `RefType` (`document` | `file`) · `RefId` · `SourceModule` (`MOD-0028` | `MOD-0029`) · `RefCode?`.

- **Format-level validation only**: shape, non-empty id, and a document-module attribution. FU02 performs **no** document-master lookup, no file upload, no download, no render, no copy and no evidence pack. The file SoR stays MOD-0028/MOD-0029.
- The evidence DTO deliberately has no `Content`/`Url`/`Uri`/`Bytes`/`File` member — test T28 asserts this by reflection, so a future edit cannot quietly add a content field.
- A malformed pointer (bad type, empty id, foreign module) → 400.
- Recorded as PARTIAL: cross-module existence validation of the referenced document is a follow-up (**MOD-0164-FU05**), because it needs a MOD-0029 read seam that FU02 is not allowed to open.

## 16. External References / Legacy Migration

`ConsentExternalReference` = `SourceSystem` · `ExternalId` · `ExternalCode?` · `ExternalName?` · `ImportedAt?` · `IsPrimary` — the same contract as MOD-0290-FU01 / MOD-0165-FU02, carried by **both** aggregates.

- **No silent merge.** A duplicate `(SourceSystem, ExternalId)` pair inside one payload → **409**; a pair already owned by another non-archived record → **409** with the owning record id (fusing two legacy opt-in/opt-out histories into one record is exactly the failure this prevents).
- More than one `IsPrimary` → 400. Missing `SourceSystem`/`ExternalId` → 400.
- `ImportedAt` supplied by the caller is preserved verbatim (legacy history is never rewritten); it is stamped with "now" only when omitted.
- Legacy withdrawal/opt-out history is preserved by design: no hard delete, withdrawal is a status with a mandatory reason, and archive keeps the row readable.
- **Import/export is not implemented** in FU02 (out of scope). Follow-up: **MOD-0164-FU06**.
- Indexed for the guard: `ix_consent_records_tenant_external_ref` / `ix_preference_records_tenant_external_ref`.

## 17. UI Boundary

**No UI was built** — FU02 is API-first, as the task permits. No `.cshtml`, no controller in `Diten.Web`, no `.resx` change, no menu/nav change.

Follow-up opened: **MOD-0164-FU03 — Consent & Preference Admin UI** (consent list/detail/create/edit/archive · preference list/detail/create/edit/archive · an evaluate test panel · 7-language RESX parity).

## 18. Tests

`services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/ConsentPreferenceRuntimeTests.cs` — 35 tests, one per required scenario:

| # | Test | Result |
|---|---|---|
| 1 | `T01` create granted consent valid | PASS |
| 2 | `T02` TenantId never accepted from payload (reflection over all 8 write contracts + no-claim refusal) | PASS |
| 3 | `T03` missing SubjectType → 400 | PASS |
| 4 | `T04` missing SubjectId → 400 | PASS |
| 5 | `T05` missing Channel → 400 | PASS |
| 6 | `T06` missing Purpose / LegalBasis / ConsentStatus → 400 | PASS |
| 7 | `T07` EffectiveTo < EffectiveFrom → 400 (both aggregates) | PASS |
| 8 | `T08` unknown channel/purpose/status/legal-basis/preference-type/ambiguous value → 400 | PASS |
| 9 | `T09` evaluate matching granted → allowed | PASS |
| 10 | `T10` evaluate no matching consent → unknown (channel and purpose both non-transferable) | PASS |
| 11 | `T11` unknown not treated as allowed (absent **and** authored `unknown`) | PASS |
| 12 | `T12` denied beats granted at same specificity | PASS |
| 13 | `T13` withdrawn blocks (history preserved) | PASS |
| 14 | `T14` restricted blocks | PASS |
| 15 | `T15` expired / out-of-window / not-yet-effective not allowed | PASS |
| 16 | `T16` scope-specific beats general; general does not consume the scoped record | PASS |
| 17 | `T17` latest EffectiveFrom tie-break | PASS |
| 18 | `T18` stable ConsentId tie-break, deterministic + flagged as ambiguous | PASS |
| 19 | `T19` do-not-visit blocks granted visit consent | PASS |
| 20 | `T20` do-not-contact blocks communication consent; no cross-channel leak; `all` sentinel covers every channel | PASS |
| 21 | `T21` absent / false / advisory / not-yet-effective preference invents no default | PASS |
| 22 | `T22` archived excluded from evaluate but readable (consent + preference) | PASS |
| 23 | `T23` archived update → 409 (both aggregates) | PASS |
| 24 | `T24` DELETE structurally unsupported (controllers, repositories, command namespace) | PASS |
| 25 | `T25` evaluate write-free in the allowed / unknown / blocked branches | PASS |
| 26 | `T26` CandidateConsents diagnostics visible (winner + loser + eliminated, each with a reason) | PASS |
| 27 | `T27` CandidatePreferences diagnostics visible with the restrictive flag | PASS |
| 28 | `T28` EvidenceRef stored as reference; no content/url/file member; malformed → 400 | PASS |
| 29 | `T29` ExternalReferences stored; duplicates → 409 in-payload and cross-record; two primaries → 400 | PASS |
| 30 | `T30` contract flags true + vocabulary surfaced | PASS |
| 31 | `T31` forbidden flags absent (exactly six members) | PASS |
| 32 | `T32` no campaign/visit/route/due/last-visit/frequency/content/workflow/availability field in any response type | PASS |
| 33 | `T33` every endpoint is `[Authorize]` + permission-guarded, none `[AllowAnonymous]` (so unauthenticated can only be 401) | PASS |
| 34 | `T34` tenant isolation on read, list, evaluate, update, archive | PASS |
| 35 | `T35` provider never 500s (controlled unknown) + question dimensions immutable on update | PASS |

**Full CRM suite:** `Başarılı! - Başarısız: 0, Başarılı: 581, Atlanan: 5, Toplam: 586` — the 5 skips are pre-existing. **Build PASS** (0 errors, 0 new warnings).

## 19. Authenticated Gateway Live Smoke

**Result: 65 checks, 0 fail — OVERALL PASS.** Executed by the operator on tenant `97c59330-dbc4-4665-b29c-0c26dbb5cc93` via `scripts/smoke-mod0164-fu02-consent-preference-authenticated.ps1` (agent-authored, operator-run: entering a password is outside what the assistant may do). All business calls went through the Gateway (5000); the only direct-5061 call was `/health`.

### 19.1 Authenticated positive + negative sequence (operator run)

| # | Step | Expected | Actual | Result |
|---|---|---|---|---|
| 1 | Gateway login | 200 + token | 200 (token masked) | PASS |
| 2 | `tenant_id` claim == target tenant | `97c59330-…cc93` | `97c59330-…cc93` | PASS |
| 3 | Contract flags all true | true ×6 | true | PASS |
| 4 | Forbidden flags absent | none | none | PASS |
| 5 | Contract moduleId + evaluator version | MOD-0164 + version | `MOD-0164 / mod-0164-fu02.v1` | PASS |
| 6 | Create granted consent | 201 + ConsentId | `201 / a50a0e4b-…8676` | PASS |
| 7 | **Injected `tenantId` ignored** (readable in claim tenant) | readable/granted | `status=granted` | PASS |
| 8 | EvidenceRef stored as MOD-0029 reference (no file copy) | document/MOD-0029 | document/MOD-0029 | PASS |
| 9 | ExternalReferences stored | `OldCRM/CONSENT-2026…` | `OldCRM/CONSENT-2026…` | PASS |
| 10 | **Evaluate granted ⇒ allowed** | allowed/`consent_granted` | allowed/`consent_granted` | PASS |
| 11 | Reason code `consent_granted` present | `consent_granted` | `consent_granted, consent_selected_by_stable_id` | PASS |
| 12 | CandidateConsents diagnostics visible | ≥1 with reason | 1 candidate | PASS |
| 13 | SelectionReason present | non-empty | *"Selected consent a50a0e4b-… ('granted') by the stable consent id (same-band tie) → eligibility 'allowed'."* | PASS |
| 14 | **Visit consent does not leak to email/marketing** | unknown | unknown | PASS |
| 15 | **No consent ⇒ unknown + `no_matching_consent`** | unknown | unknown/`consent_unknown` | PASS |
| 16 | Create `do-not-visit` preference | 201 + PreferenceId | `201 / a47011d5-…198e` | PASS |
| 17 | **Restrictive preference blocks granted consent** | blocked/`preference_restricted` | blocked/`preference_restricted` | PASS |
| 18 | Preference reason codes | `preference_do_not_visit` | `preference_do_not_visit, preference_restricted, preference_channel_blocked` | PASS |
| 19 | MatchedPreferenceIds carries the preference | `a47011d5-…198e` | `a47011d5-…198e` | PASS |
| 20 | CandidatePreferences diagnostics + restrictive flag | restrictive ≥ 1 | 1 candidate, 1 restrictive | PASS |
| 21 | Blocked result still shows the matched consent (explained, not hidden) | `a50a0e4b-…8676` | `a50a0e4b-…8676` | PASS |
| 22 | Archive preference | 200/204 | 200 | PASS |
| 23 | Archived preference readable + `ArchivedAt` stamped | archived + stamp | `isArchived=True / 2026-08-03T04:30:12.479+00:00` | PASS |
| 24 | **Archived preference no longer restricts ⇒ allowed** | allowed/`consent_granted` | allowed/`consent_granted` | PASS |
| 25 | Create scope-specific denied consent | 201 | 201 | PASS |
| 26 | **Scope-specific consent beats general (blocked in scope)** | blocked + scoped id | `blocked / 74b114a9-…6486` | PASS |
| 27 | Specificity reason visible | `consent_selected_by_specificity` | `consent_denied, consent_selected_by_specificity` | PASS |
| 28 | **General question does not consume the scoped record** | allowed | allowed | PASS |
| 29 | Withdraw consent via PUT (history kept) | 200 | 200 | PASS |
| 30 | **Withdrawn consent blocks** | blocked/`consent_blocked` | blocked/`consent_blocked` | PASS |
| 31 | Withdrawal reason preserved on the record | "FU02 smoke withdrawal" | "FU02 smoke withdrawal" | PASS |
| 32 | **Archived consent ⇒ unknown (no default allowed)** | unknown/`consent_unknown` | unknown/`consent_unknown` | PASS |
| 33 | **Unknown is NOT allowed** | not allowed | unknown | PASS |
| 34 | Archived consent still readable (history preserved) | archived + stamp | `isArchived=True` | PASS |
| 35 | `DELETE` consent unsupported | 404/405 | 404 | PASS |
| 36 | `DELETE` preference unsupported | 404/405 | 404 | PASS |
| 37 | Update archived consent → 409 | 409 | 409 | PASS |
| 38 | Update archived preference → 409 | 409 | 409 | PASS |
| 39–48 | Ten validation negatives → 400: invalid `consentStatus` · invalid `channel` · invalid `purpose` · invalid `legalBasis` · missing `subjectType` · empty `subjectId` · `EffectiveTo < EffectiveFrom` · withdrawn without reason · malformed `EvidenceRef` · `ScopeId` without `ScopeType` | 400 ×10 | 400 ×10 | PASS |
| 49 | **Duplicate external mapping → 409 (no silent merge)** | 409 | 409 | PASS |
| 50 | Ambiguous restrictive `preferenceValue` → 400 | 400 | 400 | PASS |
| 51 | **Evaluate with invalid channel → 400** (malformed question, not "unknown") | 400 | 400 | PASS |
| 52 | Evaluate response shape clean (raw JSON scan) | none leaked | none | PASS |
| 53 | Consent record shape clean (raw JSON scan) | none leaked | none | PASS |
| 54 | **Evaluate write-free — consent count unchanged** after 4 evaluations | 4 | 4 | PASS |
| 55 | **Evaluate write-free — preference count unchanged** | 2 | 2 | PASS |
| 56 | Evaluating an unknown subject persists nothing | 0 | 0 | PASS |

Plus the 9 preflight/unauthenticated rows in §19.2 = **65 checks, 0 fail**.

Notes on two rows worth reading carefully:

- **Row 11/13** — the discriminator is `consent_selected_by_stable_id` because a single eligible general-scope record has no competitor to be discriminated against; the engine reports the honest basis rather than claiming a specificity or recency win it did not make. Row 27 shows the real specificity discriminator once a competing scoped record exists.
- **Rows 54–55** — the counts are 4 consents and 2 preferences because the smoke had by then authored two consents plus the previous run's archived rows in the same tenant; the assertion is that the number is *identical* before and after four evaluations, which it was.

A first operator run reported 64/65 with one FAIL on row 20. Root cause was a defect in the **smoke script's assertion**, not the runtime: in Windows PowerShell 5.1 `($array | Where-Object {…}).Count` yields `$null` when the filter matches exactly one object, so a correct `restrictive: true` evaluated as False. Reproduced locally, fixed with the `@(...)` array-subexpression wrapper in both places it occurred (the same idiom was also silently blanking the `OVERALL … fail` count and suppressing the failures-only block). The runtime was never changed between the two runs — and the adjacent rows 17/18/19 already proved the flag was correct.

### 19.2 Unauthenticated preflight (executed by the agent)

| Check | Expected | Actual |
|---|---|---|

| Check | Expected | Actual |
|---|---|---|
| Ports 5000 / 5001 / 5056 / 5057 / 5061 answering | reachable | reachable |
| CRM `/health` (only permitted direct-5061 call) | 200 | **200** |
| `GET /api/crm/consents/contract` (no token) | 401 | **401** |
| `GET /api/crm/consents` (no token) | 401 | **401** |
| `GET /api/crm/consents/evaluate` (no token) | 401 | **401** |
| `GET /api/crm/consents/{id}` (no token) | 401 | **401** |
| `GET /api/crm/preferences` (no token) | 401 | **401** |
| `GET /api/crm/preferences/{id}` (no token) | 401 | **401** |
| `POST /api/crm/consents` (no token) | 401 | **401** |
| `POST /api/crm/preferences` (no token) | 401 | **401** |
| `POST /api/crm/consents/{id}/archive` (no token) | 401 | **401** |
| Garbage token (`Bearer x.y.z`) | 401 | **401** |
| `DELETE /api/crm/consents/{id}` | 404/405 | **404** |
| `DELETE /api/crm/preferences/{id}` | 404/405 | **404** |
| Bogus subpath `/api/crm/consents/nope-not-a-route-xyz` | 404 | **404** |

The last row is the control that makes the 401s meaningful: a non-existent path returns 404 through the same Gateway route, so a 401 proves the action really exists in the restarted CRM service (the fleet runs `dotnet watch`, which rebuilt and restarted it).

**Smoke script:** `scripts/smoke-mod0164-fu02-consent-preference-authenticated.ps1` (PowerShell 5.1-compatible; the credential stays in the operator's process memory, is never written to a file, and the Authorization header is never printed). Every record it creates is closed with **archive**, never deleted, and per-run unique ids mean re-runs never collide — it is safely repeatable.

**Runtime evidence that the persistence layer is live and healthy** (read-only Mongo listing, `DitenERP_Dev` — no hand-edit, no write):

```
=== consent_records === exists=True, documents: 0
    ix_consent_records_tenant_subject_channel        { TenantId, SubjectType, SubjectId, Channel }
    ix_consent_records_tenant_channel_purpose_status { TenantId, Channel, Purpose, ConsentStatus }
    ix_consent_records_tenant_external_ref           { TenantId, ExternalReferences.SourceSystem, ExternalReferences.ExternalId }
=== preference_records === exists=True, documents: 0
    ix_preference_records_tenant_subject      { TenantId, SubjectType, SubjectId }
    ix_preference_records_tenant_channel_type { TenantId, Channel, PreferenceType }
    ix_preference_records_tenant_external_ref { TenantId, ExternalReferences.SourceSystem, ExternalReferences.ExternalId }
```

All six indexes were created at startup — proving no parallel-array error and no `$ne` partial-filter crash — and `documents: 0` proves the runtime has fabricated no record of its own.

## 20. Response Shape Guard

Asserted by test T32 across `ConsentEvaluationResult`, `CandidateConsent`, `CandidatePreference`, `ConsentRecordDto`, `PreferenceRecordDto`, `ConsentEvidenceRefDto`, `ConsentExternalReferenceDto`, and re-checked against raw JSON in smoke step 13:

| Must be absent | Status |
|---|---|
| `visitPlanId` | absent |
| `routeId` / route order / distance / travel time | absent |
| `dueStatus` / `overdue` | absent |
| `lastVisitDate` | absent |
| `campaignTargetId` (and no target creation) | absent |
| `requiredVisitCount` / `frequencyPolicyId` / `periodType` (no frequency write) | absent |
| segment membership | absent |
| content / recommendation | absent |
| workflow / approval | absent |
| availability | absent |

## 21. Data Mutation Guard

| Guard | Result |
|---|---|
| Evaluate GET is write-free | **Yes** — pure engine; T25 flags any repository write during evaluation; smoke step 14 compares record counts before/after |
| Evaluating an unknown subject persists nothing | **Yes** — no implicit `unknown` record is ever created (smoke) |
| Account mutation | **None** |
| Contact / AccountContactLink mutation | **None** |
| ContactAvailability mutation | **None** |
| Territory mutation | **None** |
| VisitFrequencyPolicy mutation | **None** |
| CampaignTarget write | **None** (no such aggregate is touched) |
| Hard delete | **None** — no DELETE endpoint, command or repository method exists |
| Mongo hand-edit | **None** — the only Mongo access was a read-only index/count listing |
| RBAC seed / grant | **None** |
| MOD-0048 publish | **None** |
| Registry write | **None** |

## 22. Guard Checks

| Check | Result |
|---|---|
| ConsentRecord aggregate implemented | **Yes** |
| PreferenceRecord aggregate implemented | **Yes** |
| Consent create/read/list/update/archive | **Yes** |
| Preference create/read/list/update/archive | **Yes** |
| Read-only evaluation provider | **Yes** |
| Evaluate endpoint writes data? | **No** |
| Unknown treated as allowed? | **No** |
| Denied/withdrawn/restricted blocks? | **Yes** |
| Restrictive preference blocks granted consent? | **Yes** |
| Default preference invented when absent? | **No** |
| Effective window + scope specificity work? | **Yes** |
| Diagnostics visible (candidates + reason codes + selection reason)? | **Yes** |
| Deterministic, stable tie-break? | **Yes** |
| Provider can 500? | **No** (controlled unknown + `consent_evaluation_error`) |
| Consent embedded into Contact/Account? | **No** |
| DELETE endpoint added? | **No** |
| Hard delete possible? | **No** |
| Campaign target runtime opened? | **No** |
| Campaign engine opened? | **No** |
| Segmentation / CDP engine opened? | **No** |
| Frequency runtime opened? | **No** |
| Visit planning opened? | **No** |
| Route planning opened? | **No** |
| Due/overdue or last-visit opened? | **No** |
| Digital detailing / content recommendation opened? | **No** |
| KnowledgeContent implementation opened? | **No** |
| Workflow/approval opened? | **No** |
| File upload/render/evidence pack opened? | **No** |
| Import/export implemented? | **No** |
| Patient data touched? | **No** |
| Account/Contact/ContactAvailability/Territory mutated? | **No** |
| TenantId accepted from payload? | **No** |
| Gateway routes added (no DELETE)? | **Yes** |
| Direct-5061 business smoke performed? | **No** (only `/health`) |
| RBAC seed/grant, MOD-0048 publish, registry write, Mongo hand-edit? | **No** (all four) |
| Contract forbidden flags present? | **No** |
| Build / tests PASS? | **Yes** (581 passed, 0 failed) |
| UI built? | **No** (deferred — MOD-0164-FU03) |

## 23. Created / Updated Files

**Created**

| File | Purpose |
|---|---|
| `services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/ConsentRecord.cs` | `ConsentRecord` + `PreferenceRecord` aggregates, `ConsentEvidenceRef`, `ConsentExternalReference`, and all in-domain vocabularies |
| `services/Diten.CrmService/src/Diten.CrmService.Domain/Repositories/IConsentRecordRepository.cs` | Both repository interfaces (no delete method) |
| `.../Application/Features/ConsentPreference/ConsentPreferenceDtos.cs` | Read models + inbound value inputs |
| `.../Application/Features/ConsentPreference/ConsentPreferenceMapper.cs` | Aggregate ↔ DTO projection |
| `.../Application/Features/ConsentPreference/ConsentPreferenceValidation.cs` | All structural validation rules |
| `.../Application/Features/ConsentPreference/ConsentPreferencePermissions.cs` | Permission key definitions (definition only — seeds nothing) |
| `.../Application/Features/ConsentPreference/Commands/ConsentPreferenceCommands.cs` | Create/update/archive commands (no delete) |
| `.../Application/Features/ConsentPreference/Queries/ConsentPreferenceQueries.cs` | List/get/evaluate queries |
| `.../Application/Features/ConsentPreference/Handlers/ConsentRecordCommandHandlers.cs` | Consent write handlers + external-mapping conflict guard |
| `.../Application/Features/ConsentPreference/Handlers/PreferenceRecordCommandHandlers.cs` | Preference write handlers |
| `.../Application/Features/ConsentPreference/Handlers/ConsentPreferenceQueryHandlers.cs` | Read + evaluate handlers |
| `.../Application/Features/ConsentPreference/Evaluation/ConsentEvaluationContracts.cs` | Eligibility/decision/reason-code vocabulary + result & request records |
| `.../Application/Features/ConsentPreference/Evaluation/ConsentEvaluationEngine.cs` | Pure deterministic fail-closed engine |
| `.../Application/Features/ConsentPreference/Evaluation/IConsentPreferenceEvaluator.cs` | The single read-only provider seam + controlled-degradation implementation |
| `.../Application/Features/ConsentPreference/Contract/ConsentPreferenceContract.cs` | Contract endpoint (flags, vocabulary, limitations) |
| `.../Persistence/Repositories/ConsentRecordRepository.cs` | Both Mongo repositories (no delete) |
| `.../Api/Models/CRM/ConsentPreferenceRequests.cs` | Request bodies (no TenantId, no immutable dimensions on update) |
| `.../Api/Controllers/CRM/ConsentsController.cs` | Consent endpoints incl. `evaluate` (GET) |
| `.../Api/Controllers/CRM/PreferencesController.cs` | Preference endpoints |
| `services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/ConsentPreferenceRuntimeTests.cs` | 35 tests |
| `scripts/smoke-mod0164-fu02-consent-preference-authenticated.ps1` | Authenticated Gateway smoke (operator-run) |
| `docs/audits/mod-0164-fu02-consent-preference-runtime-evaluation-provider-implementation-2026-08-03.md` | This report |

**Updated**

| File | Change |
|---|---|
| `.../Application/DependencyInjection.cs` | Registered `IConsentPreferenceEvaluator` → `ConsentPreferenceEvaluator` (scoped) |
| `.../Persistence/DependencyInjection.cs` | Registered both repositories; added the four class maps (string-Guid for `SubjectId`/`ScopeId`/`EvidenceRef.RefId`); added the six indexes |
| `gateway/Diten.ApiGateway/ocelot.json` | Four routes: `/api/crm/consents`, `/api/crm/consents/{everything}`, `/api/crm/preferences`, `/api/crm/preferences/{everything}` — `GET/POST/PUT/OPTIONS` only |

Not touched: any Contact/Account/Availability/Territory/Frequency file, the MOD-0150 consent seam, `Diten.Web` (no UI), any `.resx`, RBAC/permission seeds, reference-data catalogs, module registries, module packs.

## 24. Final Verdict

### **PASS**

Every PASS criterion is implemented and verified:

- `ConsentRecord` runtime implemented as its **own aggregate**; no flat consent field exists on Contact / AccountContactLink / Account.
- `PreferenceRecord` runtime implemented as its own aggregate, and a preference can only restrict — never grant.
- CRUD/read/archive implemented **without DELETE**; hard delete is structurally impossible (no endpoint, command or repository method).
- Read-only evaluation provider implemented, GET-only and **write-free**, deterministic and fail-closed.
- Matching granted consent ⇒ `allowed`; no matching consent ⇒ `unknown`; **`unknown` is never `allowed`**; denied/withdrawn/restricted ⇒ `blocked`; a restrictive preference blocks even a granted consent; an absent preference invents nothing.
- Effective window, scope specificity, restrictive-status precedence, latest-effective-from and the stable id tie-break all work, with visible diagnostics, reason codes and a selection reason on every outcome.
- Contract flags correct; all six forbidden campaign/visit/route/detailing/recommendation/workflow flags absent.
- Tests and build PASS (581 passed / 0 failed); the runtime is live behind the Gateway with all six Mongo indexes created cleanly.
- **Authenticated Gateway live smoke PASS — 65 checks, 0 fail** on tenant `97c59330-dbc4-4665-b29c-0c26dbb5cc93`, including the injected-`tenantId`-ignored guard, the allowed → blocked → allowed → unknown lifecycle sequence, scope specificity, withdrawal, all negative guards, the response-shape guard on raw JSON and the write-free guard.
- No campaign, visit, route, frequency, segmentation, detailing or content engine was opened.

**Why PASS despite the deferred UI:** the task's §N explicitly authorizes an API-first delivery ("UI yapılmazsa: API-only PASS olabilir") provided the UI follow-up is opened — it is (**MOD-0164-FU03**). Its §L likewise accepts format-level `EvidenceRef` validation when no document-master fetch exists in the runtime ("format-level validation yeterli olabilir"), which is the case here and is documented in §15. The live smoke exercised `account-contact-link`, which is exactly what §P step 4 asked for.

No FAIL criterion is triggered: unknown is not allowed, consent is not embedded into Contact/Account, no campaign target runtime, no visit/route planning, evaluate writes nothing, a restrictive preference is never ignored, no hard delete, no DELETE endpoint, no consent copied into a CampaignTarget, tests and build pass.

**Explicitly out of scope and therefore still open (none of these blocks PASS, each is a named follow-up):** the admin UI · seeding `crm.consent.*` / `crm.preference.*` (the endpoints run on the documented territory fallback) · activating the MOD-0150 Contact 360 seam against the now-live store · document-existence validation for `EvidenceRef` · import/export.

**Follow-ups opened by this task:** MOD-0164-FU03 (Admin UI) · MOD-0164-FU-RBAC (seed `crm.consent.*` / `crm.preference.*` and drop the territory fallback) · MOD-0164-FU04 (activate the MOD-0150 Contact 360 consent/preference seam against the live store) · MOD-0164-FU05 (EvidenceRef document-existence validation) · MOD-0164-FU06 (consent/preference import/export + legacy migration).

**Carried forward from FU01 (unchanged, still open):** the KVKK/GDPR right-to-erasure ↔ hard-delete-prohibition question is a legal decision. FU02 keeps the fail-closed posture (no hard delete, archive only), so nothing is lost — but the erasure path must be decided before consent goes to production.

## 25. Next Recommended Prompt

Per the task's PASS branch:

`MOD-0165-FU04 — Campaign / Targeting Runtime + Static Target Snapshot Implementation`

FU04 is now unblocked on its consent dependency: it can consume `IConsentPreferenceEvaluator` in-process and store **only** `decision` / `reasonCodes` / `evaluatedAt` / `matchedConsentId` / `matchedPreferenceIds` / `evaluatorVersion` as provenance on its own target — never consent data — and must surface `consent_filter_not_applied` when a snapshot is produced without the filter (FU01 §7).
