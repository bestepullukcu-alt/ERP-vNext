# MOD-0165-FU04 — Campaign / Targeting Runtime + Static Target Snapshot Implementation

**Date:** 2026-08-03
**Module:** MOD-0165 (Campaign / Targeting)
**Service:** Diten.CrmService (port 5061, Gateway 5000)
**Target tenant (smoke):** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
**Branch:** `feature/crm-integration`
**Verdict:** **PARTIAL** (API runtime + snapshot + consent integration complete and tested; UI deferred and the authenticated positive smoke is scripted for the operator — see §24)

---

## 1. Preflight

| # | Check | Result |
|---|---|---|
| 1 | MOD-0165-FU02 boundary report present? | **Yes** — `docs/audits/campaign-targeting-boundary-pack-authorization-2026-08-02.md` (PASS) + pack `MOD-0165-FU02-campaign-targeting-boundary.md` |
| 2 | MOD-0164-FU02 runtime provider present? | **Yes** — `docs/audits/mod-0164-fu02-…-2026-08-03.md` (PASS, 65/65 authed smoke) |
| 3 | `IConsentPreferenceEvaluator` seam present? | **Yes** — `Features/ConsentPreference/Evaluation/IConsentPreferenceEvaluator.cs`, registered scoped in `Application/DependencyInjection.cs` |
| 4 | MOD-0165-FU03 referenceable without touching it? | **Yes** — only `FrequencyTargetType.All` is *read* in a test to prove the two vocabularies stay separate; **no FU03 file changed** |
| 5 | Campaign feature folder exists? | **No** — created `Features/Campaign/**` |
| 6 | Existing Campaign/CampaignTarget runtime? | **None** — greenfield (`find -iname "*Campaign*"` returned nothing) |
| 7 | Gateway route pattern | Per-resource ocelot pairs (`/x` + `/x/{everything}`) → `localhost:5061`; followed exactly |
| 8 | string-Guid class map convention kept? | **Yes** — 4 class maps added (see §23); a miss would silently return "no targets" on every read |
| 9 | DateTimeOffset BSON-array index/sort trap avoided? | **Yes** — no DateTimeOffset in any index key; all ordering in memory |
| 10 | `$ne` in a partial index avoided? | **Yes** — no partial filters at all; code uniqueness enforced in the handler |
| 11 | Direct 5061 business smoke avoided? | **Yes** — only `/health` is called directly |
| 12 | Registry write / MOD-0048 publish / RBAC seed / Mongo hand-edit? | **None of the four** (the only Mongo access was a read-only index/count listing) |

**Two findings surfaced during preflight** (neither blocks this task):

- **MOD-0048 reconciliation F6 confirms this task's target-type rule.** `campaign-target-type` stays at exactly **7 canonical values** with `campaign-target` **removed** (self-referential loop), and `visit-frequency-target-type` remains a **separate** set that *does* contain `campaign-target`. FU04 implements this literally, and test T13 asserts both halves.
- **A vocabulary divergence in the already-shipped MOD-0164 runtime.** Reconciliation F1 fixed the canonical `consent-legal-basis` at `consent · legitimate-interest · contract · legal-obligation · vital-interest · public-task`, while the shipped FU02 `ConsentLegalBasis` constants read `explicit-consent · … · public-interest · other`. FU04 **did not touch it** (consent runtime is explicitly out of scope) and is unaffected — FU04 only uses `ConsentChannel`/`ConsentPurpose`, which **do** match canonical. Already captured in memory; flagged again here as the follow-up owner's input.

## 2. Dependency Confirmation

All prerequisites treated as PASS and **not modified**:

| Dependency | Status | Interaction in FU04 |
|---|---|---|
| MOD-0164-FU01 Consent Boundary | PASS | Consent filter contract (§9/§12 of the pack) implemented |
| MOD-0164-FU02 Consent Runtime | PASS | **Consumed read-only through `IConsentPreferenceEvaluator`**; no consent file changed |
| MOD-0165-FU02 Campaign Boundary | PASS | This is the runtime of the FU02-authorized model |
| MOD-0165-FU03 Visit Frequency Runtime | PASS | Untouched; only its target-type vocabulary is read in a separation test |
| MOD-0151-FU09B Frequency Provider Integration | PASS | Untouched |
| MOD-0150 Contact Availability | PASS | Untouched; no availability read or write |
| MOD-0162-FU01/A/B/C Knowledge/Content/Path/Journey/Concept | PASS | Optional **reference** ids only; no runtime opened |
| MOD-0290-FU01 Brand/Product Boundary | PASS | Optional **reference** ids only; no master read |
| MOD-0048 CRM reference set governance reconciliation | PASS | F6 honoured (§1); no publish performed |
| MOD-0155 Visit/Route Planning | **Not started** | Nothing planning-related implemented (§13) |
| MOD-0167 Segmentation | Boundary only | Segment stored as provenance; **no membership engine** (§11) |

## 3. Scope Confirmation

**Implemented (all 15 requested items):** Campaign aggregate · CampaignTarget aggregate · campaign create/read/list/update/archive · target create/read/list/update/archive · static target snapshot · manual target authoring · segment-sourced snapshot provenance · consent evaluation provider consumption · consent provenance storage · visible `consent_filter_not_applied` · contract endpoint · Gateway routes · 38 tests covering the 40 required scenarios · authenticated smoke script (+ unauthenticated preflight executed) · this evidence report.

**NOT implemented (boundary preserved):** dynamic segmentation engine · segment membership calculation · campaign automation engine · campaign rule evaluator · visit planning · route planning · due/overdue · last-visit history · frequency policy creation · consent/preference create or update · consent data copy · knowledge content creation · recommendation engine · digital detailing · workflow/approval · Account/Contact mutation · Territory mutation · ContactAvailability mutation · patient data · import/export · file upload/render · hard delete · Mongo hand-edit · RBAC seed/grant · MOD-0048 publish · registry write.

## 4. Implementation Summary

Two new aggregates in **Diten.CrmService** with their own collections (`campaigns`, `campaign_targets`), plus one snapshot handler that consumes MOD-0164:

- **CRUD-minus-delete** (create / update / archive / get / list) for both aggregates over MediatR handlers.
- A **static snapshot** endpoint that normalizes caller-supplied items into targets, asks MOD-0164 per person-shaped target, and records the verdict as provenance.
- A contract endpoint advertising exactly the **six** allowed flags plus a machine-readable `consentIntegration` block that states the blocked/unknown/missing-context/not-applicable behaviour.
- **In-domain (structural) vocabulary validation** — ready without a MOD-0048 publish, never failing open on an unpublished set.
- Dedicated Gateway routes exposing **GET/POST/PUT/OPTIONS only**.
- 38 tests; the whole CRM suite is green (**619 passed / 0 failed / 5 pre-existing skips**).

**The three structural guarantees of the snapshot**, each enforced by construction rather than by convention:

1. **Additive** — there is no delete path anywhere; a snapshot can insert or replace, never remove. A target absent from a later batch keeps its status *and* its original batch id (test T20).
2. **Idempotent per source** — a re-run reconciles in place. A row whose target is owned by a *different* source aborts the **whole batch with 409 before any write**, so the campaign is never left half-snapshotted (test T21).
3. **Never silently unfiltered** — see §9.

## 5. Campaign Model

`Campaign : EntityBase` — `services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/Campaign.cs`

`CampaignId` (=`Id`) · `TenantId` (**claim-only**) · `CampaignCode` (stable) · `CampaignName` · `CampaignType` · `CampaignStatus` · `ObjectiveType?` · `BusinessUnitId?` · `BrandId?` · `ProductId?` · `SubjectId?` · `TopicId?` · `ConceptChainTemplateId?` · `EngagementJourneyId?` · `DefaultKnowledgePathId?` · `DefaultKnowledgeContentId?` · **`DefaultConsentChannel?`** · **`DefaultConsentPurpose?`** · `StartDate` · `EndDate?` · `Description?` · `OwnerUserId?` · `ExternalReferences[]` · `CreatedAt/By` · `UpdatedAt/By` · `ArchivedAt?/By?`

| Dimension | Values (in-domain, structural) |
|---|---|
| `CampaignType` | `product-campaign` · `education-campaign` · `awareness-campaign` · `service-campaign` · `compliance-campaign` · `training-campaign` · `other` |
| `CampaignStatus` | `draft` · `active` · `paused` · `completed` · `cancelled` · `archived` |
| `ObjectiveType` | `awareness` · `education` · `conversion` · `reinforcement` · `objection-handling` · `retention` · `compliance` · `training` · `other` |

Rules enforced: `TenantId` never from a payload (no write contract exposes it — T02) · **campaign is not a master**: every Brand/Product/Subject/Topic/Concept/Journey/Path/Content member is a reference and no master field is copied · Brand/Product **optional** so non-pharma campaigns are fully valid · `StartDate > EndDate` → 400 · `CampaignCode` unique per tenant among non-archived campaigns → 409 (an archived code becomes reusable, T03) · unknown status/type/objective/consent-default → 400 · `CampaignCode` immutable on update (rename via `CampaignName`) · update cannot set `status=archived` (that is the archive endpoint) · **no hard delete**; archive is soft and stays readable · an archived campaign accepts **no** target mutation (T08) · **no consent status is embedded** on a campaign · no route/visit/frequency engine field exists on it.

**Two additive fields beyond the requested minimum**, both needed to keep the consent integration honest: `DefaultConsentChannel` / `DefaultConsentPurpose`. They let an operator author the campaign's consent question once instead of repeating it per snapshot — and they are *optional and never defaulted*, so their absence produces a rejection rather than a guess (§9).

## 6. CampaignTarget Model

`CampaignTarget : EntityBase` — same file.

`CampaignTargetId` (=`Id`) · `TenantId` · `CampaignId` · `TargetType` · `TargetId` · `TargetDisplayName?` · `TargetStatus` · `TargetSource` · `SourceReferenceType?` · `SourceReferenceId?` · `SnapshotBatchId?` · **`SelectionReason` (mandatory)** · **`ReasonCodes[]` (mandatory)** · `Priority?` · `ConsentEvaluation?` · `EffectiveFrom` · `EffectiveTo?` · `ExclusionReason?` · `Notes?` · `ExternalReferences[]` · audit quad · `ArchivedAt?/By?`

| Dimension | Values |
|---|---|
| `TargetType` | `account` · `contact` · `account-contact-link` · `segment` · `territory-node` · `concept-node` · `audience-profile` — **exactly 7; `campaign-target` is absent** |
| `TargetSource` | `manual` · `segment` · `import` · `legacy-import` · `business-rule` · `manager-selection` · `campaign-rule` · `other` |
| `TargetStatus` | `draft` · `active` · `inactive` · `completed` · `excluded` · `archived` |

Rules enforced: `TenantId` claim-only · a **duplicate active** `(TenantId, CampaignId, TargetType, TargetId)` → **409 on the manual path**, **idempotent reconcile on the snapshot path** (see §7 for why they differ) · archived target readable but excluded from the active membership and from snapshot reconcile · **no hard delete** · **not master data**: `TargetId` is a resolution key and no account/contact/segment/territory field is copied · `TargetDisplayName` is a snapshot **label** only, explicitly not a source of truth · **not a consent record**: no consent content is stored, only decision provenance · segment membership never computed · a segment source writes provenance only · `EffectiveTo < EffectiveFrom` → 400 · `SelectionReason` blank → 400 (a silent selection is not authorable) · `TargetStatus=excluded` without `ExclusionReason` → 400 · `CampaignId`/`TargetType`/`TargetId` immutable on update · **`ConsentEvaluation` is not settable by any caller** — no create/update contract carries a consent member (T17), so a verdict can only ever come from a live evaluation.

## 7. Static Target Snapshot

`POST /api/crm/campaigns/{campaignId}/targets/snapshot` → `CreateCampaignTargetSnapshotHandler`

Payload: `SourceType` · `TargetItems[]` (`TargetType`, `TargetId`, `TargetDisplayName?`, `Priority?`, `SourceReferenceType?`, `SourceReferenceId?`) · `SelectionReason` · `ApplyConsentFilter` (**defaults to true**) · `SourceReferenceType?` · `SourceReferenceId?` · `ConsentChannel?` · `ConsentPurpose?` · `EffectiveAt?` · `EffectiveTo?` · `ReasonCodes[]?`

Pipeline, in order:

1. Request-level validation (source type, selection reason, non-empty items, source reference).
2. Campaign load — missing → 404, **archived → 409** `campaign_archived_no_target_mutation`.
3. **Consent context resolution** — fail-closed (§9).
4. **Row pre-validation** — every row is validated *before any write*; one bad or duplicated row rejects the **whole request with 400** listing the offending index. No partial snapshot can exist (T18).
5. **Source-conflict pre-scan** — any row whose target is already owned by a different source aborts with **409** and one error line per conflict; **zero writes** (T21).
6. **Evaluate + write** — per row: ask MOD-0164 (or skip with a visible reason), map the verdict to a status, then insert or reconcile.

Result DTO: `SnapshotBatchId` · `CampaignId` · `SourceType` · `SourceReferenceType/Id` · `EffectiveAt` · `ConsentFilterApplied` · `ConsentChannel/Purpose` · `RequestedCount` · `CreatedCount` · `ReconciledCount` · `ActiveCount` · `ExcludedCount` · `ConflictCount` · `Rows[]` (per-row outcome, status, exclusion reason, reason codes, consent provenance, message) · `ReasonCodes[]` · `SelectionReason`. Row outcomes: `created` · `reconciled` · `source_conflict` · `rejected`.

**Decisions taken and why:**

| Question the prompt left open | Decision | Rationale |
|---|---|---|
| Duplicate on re-run: 409 or idempotent? | **Manual create = strict 409; snapshot = idempotent reconcile for the same source, 409 for a different source** | A human adding the same target twice by hand is a mistake worth surfacing; a *machine* re-running a snapshot is a retry, and 409-ing it would make snapshots non-repeatable. The source check is what keeps the idempotency honest — a segment-A snapshot must not silently take over a manually-authored (or segment-B) target. |
| Conflicts per row or per batch? | **Per batch — pre-scanned, 409, nothing written** | "No silent partial apply" is the FU08/FU02 house rule; a half-applied targeting batch is worse than a rejected one, and the response lists every conflicting row so the operator can fix them all at once. |
| Structurally bad row? | **Whole request 400 before any write** | Same reason: a typo in row 7 must not leave rows 1–6 persisted. |
| `ConflictCount` in a success payload | Always `0` | A conflict aborts, so a 200/201 body can never contain one. Kept in the DTO for contract stability. |

Also: `SnapshotBatchId` is stamped on every row of the batch and is queryable (`GET …/targets?snapshotBatchId=…`), so a batch stays auditable as a unit. **No third aggregate was introduced** for batch history — the data-mutation guard limits writes to `Campaign` and `CampaignTarget`, and the batch is fully reconstructable from the stamped rows.

## 8. Consent Evaluation Integration

FU04 consumes **`IConsentPreferenceEvaluator`** (MOD-0164 FU02) and nothing else:

- It holds **no consent logic** — no status interpretation, no precedence rules, no window arithmetic.
- It **never reads** `IConsentRecordRepository` / `IPreferenceRecordRepository`. Test T30 asserts this structurally over *every* type in the `Features.Campaign` namespace, and T29 injects throwing consent/preference repositories to prove nothing reaches them.
- It **never writes** to MOD-0164. The evaluator seam exposes exactly one member, `EvaluateAsync` (asserted in T29).

Evaluation request per evaluable target: `subjectType` (mapped from the target type) · `subjectId` = `TargetId` · `channel` · `purpose` · `effectiveAt` · **`scopeType = campaign`** · **`scopeId = CampaignId`** · `includeDiagnostics = false` (a campaign stores a verdict, not a diagnostic dump).

Evaluable target types: **`contact`, `account-contact-link`, `account`**. A group-shaped target (`segment`, `territory-node`, `concept-node`, `audience-profile`) is **not** a consent subject; evaluating it would require resolving members, which is the MOD-0167/MOD-0155 boundary. FU04 therefore reports `consent_evaluation_not_applicable` and leaves the target active — visible, not silently "evaluated" (T39).

Stored provenance (`CampaignTargetConsentEvaluation`): `Decision` · `EligibilityStatus` · `ReasonCodes[]` · `EvaluatedAt` · `MatchedConsentId` · `MatchedPreferenceIds[]` · `EvaluatorVersion` · `SelectionReason` · **`Channel`** · **`Purpose`** · **`FilterApplied`**.

The last three are additions beyond the requested minimum, each closing a real hole: `Channel`/`Purpose` record *which question* the verdict answers (a visit verdict must never be read as an e-mail verdict), and `FilterApplied` makes an unfiltered row structurally distinguishable from an evaluated one.

**Forbidden and verified absent** (T28, by reflection on both entity and DTO): `ConsentStatus` · `PreferenceStatus` · `ConsentRecord`/`PreferenceRecord` payload · `LegalBasis` · `WithdrawalReason` · `EvidenceRef` · `PreferenceValue`/`PreferenceType`. The target's only consent-named member is the single `ConsentEvaluation` object.

## 9. Consent Filter Behavior

| Situation | Behaviour | Reason codes |
|---|---|---|
| **allowed** | target `active`, provenance stored | `consent_allowed` · `consent_provenance_stored` · `campaign_target_active` |
| **blocked** | target **created** with `TargetStatus=excluded`, `ExclusionReason=consent_blocked` | `consent_blocked` · `consent_provenance_stored` · `campaign_target_excluded` |
| **unknown** | target **created** with `TargetStatus=excluded`, `ExclusionReason=consent_unknown` | `consent_unknown` · `consent_provenance_stored` · `campaign_target_excluded` |
| **evaluator error** | treated exactly as unknown (never allowed) | `consent_unknown` · `consent_evaluation_error` · … |
| **filter disabled** (`ApplyConsentFilter=false`) | targets produced, `FilterApplied=false` | `consent_filter_not_applied` on every row **and** on the batch |
| **not applicable** (group target) | target `active`, no evaluation performed | `consent_evaluation_not_applicable` |
| **missing channel/purpose with filter on** | **400 `campaign_consent_context_required`**, zero writes, evaluator never called | — |

**The two behaviours the prompt asked to be chosen and justified:**

1. **Blocked/unknown ⇒ the target is created but excluded** (the prompt's recommended option b). Creating the row is what makes the exclusion auditable: "why was this person left out of the campaign?" is answerable from the target itself, with the matched consent id and the evaluator's own reason codes attached. Dropping the row would leave the same person looking simply *unselected*, indistinguishable from never having been considered. The blocked row also keeps its `MatchedConsentId`, so the exclusion can be traced back to the governing consent record without copying it (T24/T25).

2. **Missing consent context ⇒ 400, not an unfiltered snapshot.** A snapshot that silently skipped consent is precisely the audit hole FU01 §7 forbids ("sessiz *hepsi uygun*" assumption), and an omitted query field is the easiest way to trigger it by accident. The caller can still snapshot without the filter — but only by saying so explicitly (`ApplyConsentFilter=false`), and then every row is permanently stamped `consent_filter_not_applied` with `FilterApplied=false` so no consumer can mistake it for an evaluated row. `ApplyConsentFilter` also **defaults to true** so an omitted flag fails closed. Both halves are tested in T26.

**Audit risk recorded, per the prompt's §G4 requirement:** with `ApplyConsentFilter=false` the produced targets are `active` while carrying `consent_filter_not_applied`. A downstream consumer that reads `TargetStatus` **without** reading `ConsentEvaluation.FilterApplied` would treat them as consent-cleared. FU04 makes the flag unmissable (row-level reason code, batch-level reason code, and a boolean on the provenance object) but cannot enforce the consumer's discipline — MOD-0155 must check `FilterApplied` before treating an active target as contactable. Recorded as follow-up **MOD-0165-FU-CONSUMER-GUARD**.

## 10. Manual Target Management

| Method | Path | Permission (fallback in use) |
|---|---|---|
| GET | `/api/crm/campaigns/contract` | `crm.campaign.read` (`crm.territory.read`) |
| GET | `/api/crm/campaigns` | `crm.campaign.read` (`crm.territory.read`) |
| GET | `/api/crm/campaigns/{campaignId:guid}` | `crm.campaign.read` (`crm.territory.read`) |
| POST | `/api/crm/campaigns` | `crm.campaign.manage` (`crm.territory.model.manage`) |
| PUT | `/api/crm/campaigns/{campaignId:guid}` | `crm.campaign.manage` (`crm.territory.model.manage`) |
| POST | `/api/crm/campaigns/{campaignId:guid}/archive` | `crm.campaign.manage` (`crm.territory.model.manage`) |
| GET | `/api/crm/campaigns/{campaignId:guid}/targets` | `crm.campaign.target.read` (`crm.territory.read`) |
| GET | `/api/crm/campaigns/{campaignId:guid}/targets/{campaignTargetId:guid}` | `crm.campaign.target.read` (`crm.territory.read`) |
| POST | `/api/crm/campaigns/{campaignId:guid}/targets` | `crm.campaign.target.manage` (`crm.territory.model.manage`) |
| PUT | `/api/crm/campaigns/{campaignId:guid}/targets/{campaignTargetId:guid}` | `crm.campaign.target.manage` (`crm.territory.model.manage`) |
| POST | `/api/crm/campaigns/{campaignId:guid}/targets/{campaignTargetId:guid}/archive` | `crm.campaign.target.manage` (`crm.territory.model.manage`) |
| POST | `/api/crm/campaigns/{campaignId:guid}/targets/snapshot` | `crm.campaign.target.manage` (`crm.territory.model.manage`) |

- **No DELETE** — not on the controller, not in the command namespace, not on either repository interface, not in the Gateway route methods (T09 asserts the first three; the live probe confirms the fourth).
- Archived target: readable, `PUT` → 409. Archived campaign: readable, `PUT` → 409, and **all** target mutation (create/update/snapshot) → 409.
- Archiving a target **is** allowed on an archived campaign — closing history is not mutating targeting.
- Archiving a campaign **does not cascade** to its targets: a silent cascade would rewrite targeting history. The campaign status is visible to consumers instead (pack §16).
- `paused`/`cancelled`/`completed` campaigns still accept target authoring by design (history correction); only `archived` freezes it, per the prompt. The status is always visible on read so a consumer can apply its own gate.
- Gateway-only; no direct-5061 business surface. Permission keys **defined but not seeded** → documented territory fallback, same as FU02/FU03. Follow-up **MOD-0165-FU-RBAC**.
- Tenant isolation enforced on every read and write (T10, T38).

## 11. Segment Boundary

- **No** segment engine, CDP runtime, membership calculation, dynamic resolution or target auto-refresh.
- A segment-sourced snapshot takes the items **exactly as supplied** and writes `TargetSource=segment`, `SourceReferenceType=segment`, `SourceReferenceId=<segmentId>` plus the `segment_source_snapshot` and `target_source_provenance_stored` reason codes (T22).
- No target is created *for* the segment id itself unless the caller explicitly sends a `segment`-typed item; if they do, it is a provenance row reported `consent_evaluation_not_applicable`.
- The MOD-0167 provider seam is **not** opened — no interface, no registration, no call. A future integration replaces the caller-supplied item list; nothing else changes.

## 12. Frequency Boundary

FU04 creates/updates **no** `VisitFrequencyPolicy`, resolves **no** frequency, computes **no** due/overdue, and stores **no** frequency result on a target. `requiredVisitCount`, `periodType`, `dueStatus`, `lastVisitDate`, `frequencyPolicyId` are absent from every response type (T31 + smoke §20). `CampaignId` remains available as a future frequency *source* key; no placeholder field was added, because an unused field invites a wrong write.

The only contact with MOD-0165-FU03 is a **read of its target-type vocabulary in a test**, to prove the two sets stay separate (T13). No FU03 file was modified.

## 13. MOD-0155 Boundary

MOD-0155 has not started and FU04 opened nothing on its behalf: no visit plan, route plan, schedule, execution, due/overdue or optimizer. What MOD-0155 will consume is an `active`+effective target plus its `ConsentEvaluation` — and it must check `FilterApplied` before treating an active target as contactable (§9 risk note).

## 14. Knowledge Boundary

No `KnowledgeContent`, `KnowledgePath`, `EngagementJourney` or concept-graph runtime, and no recommendation engine. `SubjectId`, `TopicId`, `ConceptChainTemplateId`, `EngagementJourneyId`, `DefaultKnowledgePathId`, `DefaultKnowledgeContentId` are **optional references**, validated at **format level only** (an explicitly supplied empty GUID is rejected; anything else is stored as-is). MOD-0162 has no runtime to resolve them against, so nothing is fetched, copied or silently dropped — recorded as a documented limitation on the contract and as PARTIAL grounds in §24.

## 15. Brand/Product Boundary

`BrandId` / `ProductId` are **optional references**, format-level validated, never copied. A campaign with neither is fully valid (non-pharma). No ATC or therapeutic-area concept is opened. MOD-0290 has no runtime yet, so no master is read.

## 16. Contract Flags

`GET /api/crm/campaigns/contract` emits exactly:

```json
{
  "supportsCampaignManagement": true,
  "supportsCampaignTargetManagement": true,
  "supportsStaticTargetSnapshot": true,
  "supportsConsentEvaluationIntegration": true,
  "supportsTargetExclusionReason": true,
  "supportsTargetSourceProvenance": true
}
```

All ten forbidden flags — `supportsSegmentationEngine`, `supportsDynamicCampaignRules`, `supportsVisitPlanning`, `supportsRoutePlanning`, `supportsDueOverdue`, `supportsLastVisitHistory`, `supportsFrequencyRuntime`, `supportsDigitalDetailing`, `supportsRecommendationEngine`, `supportsWorkflowApproval` — are **absent**, and are not emitted as `false` either. T35 asserts both the absence and that the flag object has exactly six members.

The contract additionally surfaces the full authoring vocabulary, the 23 reason codes, the permission keys, 21 explicit limitations, and a **`consentIntegration`** block naming the provider module, the seam, the evaluator version, the evaluable target types, the scope type, and the exact missing-context / blocked / unknown / filter-disabled / not-applicable behaviours — so a consumer can be written against the contract instead of against observed behaviour.

## 17. Reason Codes

All 21 requested codes are implemented in `CampaignReasonCodes`, plus two FU04 extensions:

`campaign_created` · `campaign_updated` · `campaign_archived` · `campaign_archived_no_target_mutation` · `campaign_target_created` · `campaign_target_updated` · `campaign_target_archived` · `campaign_target_duplicate` · `campaign_target_active` · `campaign_target_excluded` · `campaign_target_snapshot_created` · `campaign_target_snapshot_reconciled` · `segment_source_snapshot` · `manual_target_selected` · `target_source_provenance_stored` · `consent_allowed` · `consent_blocked` · `consent_unknown` · `consent_filter_not_applied` · `consent_evaluation_error` · `consent_provenance_stored`

**Extensions:** `consent_evaluation_not_applicable` (group-shaped target — visible instead of looking evaluated) and `campaign_target_source_conflict` (a snapshot row owned by a different source). Both are surfaced on the contract.

Every target carries at least one reason code and a non-empty `SelectionReason`; codes are de-duplicated case-insensitively so a target is never explained twice or not at all.

## 18. Tests

`services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/CampaignTargetingRuntimeTests.cs` — **38 tests covering the 40 required scenarios** (two pairs are asserted together where they are one structural property: 32+33 "no foreign aggregate writable", 36+37 "authorize + permission on every action").

| # | Scenario | Test | Result |
|---|---|---|---|
| 1 | Campaign create valid | `T01` | PASS |
| 2 | TenantId payload ignored/rejected | `T02` (reflection over all 10 write contracts + no-claim refusal) | PASS |
| 3 | Duplicate active CampaignCode → 409 | `T03` (+ archived code reusable) | PASS |
| 4 | StartDate > EndDate → 400 | `T04` | PASS |
| 5 | Unknown status/type → 400 | `T05` (+ objective, consent defaults) | PASS |
| 6 | Campaign archive soft lifecycle | `T06` (+ idempotent) | PASS |
| 7 | Archived campaign update → 409 | `T07` | PASS |
| 8 | Archived campaign target create → 409 | `T08` (+ update + snapshot 409, targets still readable) | PASS |
| 9 | Campaign DELETE unsupported | `T09` (controller + repositories + command namespace) | PASS |
| 10 | Campaign list tenant isolated | `T10` | PASS |
| 11 | CampaignTarget create valid manual | `T11` (+ reason codes, no batch id, no consent provenance, blank reason → 400) | PASS |
| 12 | Duplicate active target behaviour | `T12` (manual 409 **and** snapshot reconcile — both halves) | PASS |
| 13 | Unknown TargetType → 400 | `T13` (+ `campaign-target` rejected, 7 members, frequency set separate) | PASS |
| 14 | Unknown TargetSource → 400 | `T14` (+ status, priority, window, excluded-without-reason) | PASS |
| 15 | Target archive soft lifecycle | `T15` | PASS |
| 16 | Archived target update → 409 | `T16` | PASS |
| 17 | Target DELETE unsupported | `T09`/`T17` (+ consent provenance not caller-settable, immutable identity) | PASS |
| 18 | Snapshot empty TargetItems → 400 | `T18` (+ invalid row and duplicate row reject the whole request, 0 writes) | PASS |
| 19 | Snapshot creates SnapshotBatchId | `T19` (+ stamped on every row, queryable as a unit) | PASS |
| 20 | Snapshot does not delete previous targets | `T20` (survivor keeps status **and** original batch id) | PASS |
| 21 | Snapshot re-run does not duplicate | `T21` (+ different-source conflict aborts batch, 0 writes) | PASS |
| 22 | Segment source stores reference only | `T22` (no expansion, no target for the segment id itself) | PASS |
| 23 | Consent allowed → target active | `T23` (+ the exact question asked, scoped to the campaign) | PASS |
| 24 | Consent blocked → excluded with reason | `T24` | PASS |
| 25 | Consent unknown → excluded with reason | `T25` (+ evaluator error degrades to unknown, never allowed) | PASS |
| 26 | Consent-filter-not-applied behaviour | `T26` (400 on missing context, opt-out visible, evaluator never called, campaign defaults honoured) | PASS |
| 27 | Provenance completeness | `T27` (all 11 members) | PASS |
| 28 | Consent data not copied | `T28` (reflection: 11 forbidden members absent on entity + DTO) | PASS |
| 29 | Consent/Preference aggregates not mutated | `T29` (throwing repos + single-member read-only seam) | PASS |
| 30 | Provider seam used, not the repository | `T30` (asserted over every type in the namespace) | PASS |
| 31 | No visit/route/due/last-visit/frequency field | `T31` (16 fragments × 8 response types) | PASS |
| 32–33 | No KnowledgeContent / Brand / Product (or any foreign) mutation | `T32_T33` (9 forbidden repositories, every constructor) | PASS |
| 34 | Contract flags true | `T34` (+ vocabulary, consent-integration block, reason codes) | PASS |
| 35 | Forbidden flags absent | `T35` (10 forbidden + exactly 6 members) | PASS |
| 36–37 | Unauthenticated / garbage token → 401 | `T36_T37` (`[Authorize]` + permission guard on all 12 actions, no `[AllowAnonymous]`) | PASS |
| 38 | Tenant isolation | `T38` (read, list, update, archive, snapshot) | PASS |
| 39 | Direct service business smoke not used | `T39` covers the group-target rule; the *no-direct-5061* guard is enforced in the smoke script (§19) and by the gateway-only routing | PASS |
| 40 | Build PASS | see below | PASS |

**Full CRM suite:** `Başarılı! - Başarısız: 0, Başarılı: 619, Atlanan: 5, Toplam: 624` — the 5 skips are pre-existing. **Build PASS** (0 errors, 0 new warnings; built to an isolated output path because the running fleet holds the normal `bin`).

## 19. Authenticated Gateway Live Smoke

**Executed by the agent (no credential needed) — all PASS:**

| Check | Expected | Actual |
|---|---|---|
| CRM `/health` (only permitted direct-5061 call) | 200 | **200** |
| `GET /api/crm/campaigns/contract` (no token) | 401 | **401** |
| `GET /api/crm/campaigns` (no token) | 401 | **401** |
| `GET /api/crm/campaigns/{id}` (no token) | 401 | **401** |
| `POST /api/crm/campaigns` (no token) | 401 | **401** |
| `GET …/{id}/targets` (no token) | 401 | **401** |
| `GET …/{id}/targets/{id}` (no token) | 401 | **401** |
| `POST …/{id}/targets` (no token) | 401 | **401** |
| `POST …/{id}/targets/snapshot` (no token) | 401 | **401** |
| `POST …/{id}/targets/{id}/archive` (no token) | 401 | **401** |
| Garbage token (`Bearer x.y.z`) | 401 | **401** |
| `DELETE /api/crm/campaigns/{id}` | 404/405 | **404** |
| `DELETE …/targets/{id}` | 404/405 | **404** |
| Bogus subpath `…/campaigns/nope-not-a-route-xyz` | 404 | **404** |

The last row is the control that makes the 401s meaningful: a non-existent path returns 404 through the same Gateway route, so a 401 proves the action exists in the restarted CRM service (`dotnet watch` rebuilt and restarted it).

**Runtime evidence that the persistence layer is live and healthy** (read-only Mongo listing, `DitenERP_Dev` — no hand-edit, no write):

```
=== campaigns === exists=True, documents: 0
    ix_campaigns_tenant_code              { TenantId, CampaignCode }
    ix_campaigns_tenant_status_type       { TenantId, CampaignStatus, CampaignType }
    ix_campaigns_tenant_external_ref      { TenantId, ExternalReferences.SourceSystem, ExternalReferences.ExternalId }
=== campaign_targets === exists=True, documents: 0
    ix_campaign_targets_tenant_campaign_target { TenantId, CampaignId, TargetType, TargetId }
    ix_campaign_targets_tenant_campaign_status  { TenantId, CampaignId, TargetStatus }
    ix_campaign_targets_tenant_batch            { TenantId, SnapshotBatchId }
```

All six indexes were created at startup — proving no parallel-array error and no `$ne` partial-filter crash — and `documents: 0` proves the runtime has fabricated no record of its own.

**Deferred to the operator** (entering a password is outside what the assistant may do): `scripts/smoke-mod0165-fu04-campaign-targeting-authenticated.ps1` — PowerShell 5.1-compatible, credential stays in the operator's process memory, never written to a file, Authorization header never printed, and every pipeline count uses the `@(...)` guard.

It runs, in order: fleet health → unauthenticated 401s → login + `tenant_id` claim assertion → contract flags + forbidden-flag absence + `consentIntegration` block + `campaign-target` absence → create campaign with an **injected `tenantId` that must be ignored** → duplicate code 409 → manual target (+ its reason codes, no batch id, no consent provenance) → manual duplicate 409 → **create the MOD-0164 fixtures through the live consent API** (granted consent for the allowed subject; granted consent + `do-not-visit` preference for the blocked subject; nothing for the unknown subject) → consent-filtered snapshot of all three → **allowed active with full provenance whose `matchedConsentId` equals the created consent** → raw-JSON scan proving no consent data was copied → **blocked excluded with `consent_blocked`** → **unknown excluded with `consent_unknown`, asserted not active** → counts 1 active / 2 excluded → segment snapshot ⇒ `consent_evaluation_not_applicable` + provenance-only, one row, no expansion → missing context 400 → explicit opt-out visible → **re-run reconciles 3 / creates 0 with the target count unchanged** → the earlier manual target still not archived → different-source row 409 with the count unchanged → archive target (readable + stamped, update 409) → archive campaign (readable + stamped, **targets not cascaded**, update/target-create/snapshot all 409) → DELETE 404 ×2 → 9 validation negatives (including `campaign-target` → 400, missing selection reason, excluded without reason, empty items) → response-shape guard on raw campaign and target JSON → **data-mutation guard: consent and preference counts unchanged and both records still exactly as authored** → cleanup by archive only.

## 20. Response Shape Guard

Asserted by T31 across `CampaignDto`, `CampaignTargetDto`, `CampaignTargetConsentEvaluationDto`, `CampaignExternalReferenceDto`, `CampaignTargetSnapshotResultDto`, `CampaignSnapshotRowResultDto`, `CampaignListDto`, `CampaignTargetListDto` — and re-checked against raw JSON in the smoke:

| Must be absent | Status |
|---|---|
| `visitPlanId` · `routePlanId` · `routeId` | absent |
| `dueStatus` · `overdue` | absent |
| `lastVisitDate` | absent |
| `requiredVisitCount` · `periodType` · `frequencyPolicyId` | absent |
| `segmentMembership` | absent |
| `recommendationId` · `nextBestAction` | absent |
| `workflowApprovalId` | absent |
| `contentRenderUrl` | absent |
| `consentRecordPayload` · `preferenceRecordPayload` | absent |

## 21. Data Mutation Guard

| Guard | Result |
|---|---|
| Writes performed | **Only `Campaign` and `CampaignTarget`** |
| `ConsentRecord` / `PreferenceRecord` | **No write, no read** — T29 injects throwing repositories; T30 proves no FU04 constructor can even take one; the smoke compares store counts and re-reads both records |
| Account / Contact / AccountContactLink | **None** — T32_T33 (no repository reachable) |
| ContactAvailability | **None** |
| Territory (model/node/assignment) | **None** |
| `VisitFrequencyPolicy` | **None** |
| KnowledgeContent / KnowledgePath / EngagementJourney | **None** (no runtime exists) |
| Brand / Product | **None** (no runtime exists) |
| Workflow / Patient | **None** |
| Evaluate provider call | **Read-only** — the seam has exactly one member, `EvaluateAsync`, and MOD-0164's own engine is write-free (verified in FU02) |
| Hard delete | **None** — no DELETE endpoint, command or repository method exists |
| Mongo hand-edit | **None** — the only Mongo access was a read-only index/count listing |
| RBAC seed / grant · MOD-0048 publish · registry write | **None of the three** |

## 22. Guard Checks

| Check | Result |
|---|---|
| Campaign runtime implemented | **Yes** |
| CampaignTarget runtime implemented | **Yes** |
| CRUD-minus-delete + archive for both | **Yes** |
| Static target snapshot implemented | **Yes** |
| Snapshot additive (never deletes old targets)? | **Yes** |
| Snapshot idempotent (re-run does not duplicate)? | **Yes** |
| Snapshot ever half-applied? | **No** (row pre-validation + conflict pre-scan) |
| Consent provider seam used? | **Yes** (`IConsentPreferenceEvaluator`) |
| Consent logic reimplemented? | **No** |
| Consent repository read directly? | **No** |
| Consent data copied? | **No** |
| Consent provenance stored? | **Yes** |
| Allowed consent ⇒ target active? | **Yes** |
| Blocked consent silently active? | **No** (excluded + reason) |
| Unknown treated as allowed? | **No** (excluded + reason) |
| Consent filter not applied visible? | **Yes** (row + batch reason code + `FilterApplied=false`); missing context is rejected 400 |
| Segment membership computed? | **No** (provenance only) |
| Segmentation / CDP engine opened? | **No** |
| Dynamic campaign rule engine opened? | **No** |
| Visit planning / route planning opened? | **No** |
| Due/overdue or last-visit opened? | **No** |
| Frequency runtime mutated? | **No** |
| Knowledge / Brand / Product runtime opened? | **No** |
| Recommendation / detailing / workflow opened? | **No** |
| `campaign-target` present in CampaignTargetTypes? | **No** (7 values; frequency set kept separate) |
| Hard delete possible? | **No** |
| DELETE endpoint added? | **No** |
| Gateway routes added (no DELETE)? | **Yes** |
| Direct-5061 business smoke performed? | **No** (only `/health`) |
| TenantId accepted from payload? | **No** |
| Tenant isolation enforced? | **Yes** |
| Contract forbidden flags present? | **No** |
| Response leaks visit/route/frequency/recommendation/workflow fields? | **No** |
| Data mutation outside Campaign/CampaignTarget? | **None** |
| Mongo hand-edit / RBAC seed / MOD-0048 publish / registry write? | **None of the four** |
| Build / tests PASS? | **Yes** (619 passed, 0 failed) |
| UI built? | **No** (deferred — MOD-0165-FU05) |

## 23. Created / Updated Files

**Created**

| File | Purpose |
|---|---|
| `services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/Campaign.cs` | `Campaign` + `CampaignTarget` aggregates, `CampaignTargetConsentEvaluation`, `CampaignExternalReference`, all in-domain vocabularies, `CampaignReasonCodes` |
| `.../Domain/Repositories/ICampaignRepository.cs` | Both repository interfaces (no delete method) |
| `.../Application/Features/Campaign/CampaignDtos.cs` | Read models, snapshot item/result DTOs, row-outcome vocabulary |
| `.../Application/Features/Campaign/CampaignMapper.cs` | Aggregate ↔ DTO projection |
| `.../Application/Features/Campaign/CampaignValidation.cs` | All structural validation rules |
| `.../Application/Features/Campaign/CampaignPermissions.cs` | Permission key definitions (definition only — seeds nothing) |
| `.../Application/Features/Campaign/Commands/CampaignCommands.cs` | Create/update/archive + snapshot commands (no delete) |
| `.../Application/Features/Campaign/Queries/CampaignQueries.cs` | List/get queries for both aggregates |
| `.../Application/Features/Campaign/Handlers/CampaignCommandHandlers.cs` | Campaign write handlers + external-mapping conflict guard |
| `.../Application/Features/Campaign/Handlers/CampaignTargetCommandHandlers.cs` | Target write handlers + archived-campaign freeze + duplicate guard |
| `.../Application/Features/Campaign/Handlers/CampaignQueryHandlers.cs` | Read handlers |
| `.../Application/Features/Campaign/Snapshot/CampaignTargetSnapshotHandler.cs` | The static snapshot engine + MOD-0164 consumption |
| `.../Application/Features/Campaign/Contract/CampaignContract.cs` | Contract endpoint (flags, vocabulary, consent-integration block, reason codes, limitations) |
| `.../Persistence/Repositories/CampaignRepository.cs` | Both Mongo repositories (no delete) |
| `.../Api/Models/CRM/CampaignRequests.cs` | Request bodies (no TenantId, no consent member, immutable identity on update) |
| `.../Api/Controllers/CRM/CampaignsController.cs` | 12 endpoints incl. the snapshot |
| `services/Diten.CrmService/tests/…/CampaignTargetingRuntimeTests.cs` | 38 tests |
| `scripts/smoke-mod0165-fu04-campaign-targeting-authenticated.ps1` | Authenticated Gateway smoke (operator-run) |
| `docs/audits/mod-0165-fu04-…-2026-08-03.md` | This report |

**Updated**

| File | Change |
|---|---|
| `.../Persistence/DependencyInjection.cs` | Registered both repositories; added 4 class maps (string-Guid for every Guid FK incl. `MatchedPreferenceIds` list); added 6 indexes |
| `gateway/Diten.ApiGateway/ocelot.json` | Two routes: `/api/crm/campaigns` and `/api/crm/campaigns/{everything}` — `GET/POST/PUT/OPTIONS` only (109 routes total) |

**Deliberately not touched:** any MOD-0164 consent/preference file · any MOD-0165-FU03 frequency file · any Contact/Account/Availability/Territory file · `Application/DependencyInjection.cs` (MediatR auto-discovers the new handlers; the evaluator was already registered by FU02) · `Diten.Web` (no UI) · any `.resx` · RBAC/permission seeds · reference-data catalogs · module registries · module packs.

## 24. Final Verdict

### **PARTIAL**

Every PASS criterion is met and verified:

- Campaign and CampaignTarget runtime implemented as their own aggregates holding **only references** — no Brand/Product/Knowledge/Account/Contact/Segment master field is copied.
- CRUD-minus-delete + archive for both; **no hard delete is possible** (no endpoint, command or repository method).
- Static target snapshot implemented: **additive** (never deletes an earlier target), **idempotent per source** (re-run reconciles), and **never half-applied** (row pre-validation and conflict pre-scan both abort before any write).
- Consent consumed **only** through the MOD-0164 provider seam: no consent logic reimplemented, no consent store read or written, **no consent data copied** — only decision provenance.
- Allowed ⇒ active. **Blocked and unknown ⇒ excluded with a reason and the matched consent id**, kept rather than dropped so the exclusion is auditable. **Unknown is never allowed**, and an evaluator error degrades to unknown.
- Consent-filter-not-applied is impossible to miss: missing context is **rejected 400**, an explicit opt-out stamps `consent_filter_not_applied` on every row and on the batch plus `FilterApplied=false`.
- Segment source stored as provenance only; **no membership engine, no segmentation/CDP runtime**.
- No visit/route/frequency/due/last-visit/recommendation/detailing/workflow engine opened; `campaign-target` is not a campaign target type.
- Contract flags correct; all ten forbidden flags absent (not even as `false`).
- Tests and build PASS (**619 passed / 0 failed**); the runtime is live behind the Gateway with all six Mongo indexes created cleanly and zero fabricated documents.
- Response shape clean; data-mutation guard clean; no Mongo hand-edit, RBAC seed, MOD-0048 publish or registry write.

It is **PARTIAL** on exactly the reasons the task lists as PARTIAL:

1. **UI deferred** — FU04 is API-first; follow-up **MOD-0165-FU05 — Campaign / Targeting Admin UI**.
2. **The authenticated positive smoke is left to the operator** for credential handling. The unauthenticated half (fleet health, route existence, 401s on all ten endpoints, garbage-token 401, DELETE 404 ×2, plus the 404 control) was executed and **passed**, and the full positive script is committed and ready.
3. **Brand/Product/Knowledge references are format-level validated only** — MOD-0290 and MOD-0162 have no runtime to resolve them against.
4. **The segment snapshot accepts caller-provided items with no MOD-0167 provider integration yet** (by design — the segmentation engine is out of scope).
5. **`consent_filter_not_applied` is explicit but not hard-blocking**: with an explicit opt-out the produced targets are `active` while carrying the flag, so a consumer that ignores `FilterApplied` could misread them (§9 risk note, follow-up **MOD-0165-FU-CONSUMER-GUARD**).

No FAIL criterion is triggered: no consent data copied, no consent logic reimplemented, unknown never allowed, blocked never silently active, snapshot never deletes previous targets, no segmentation engine, no visit/route planning, no frequency mutation, `campaign-target` not a target type, no hard delete, no DELETE endpoint, tests and build pass, Gateway routes present, no forbidden field leaked, and no write outside Campaign/CampaignTarget.

**Follow-ups opened:** MOD-0165-FU05 (Admin UI) · MOD-0165-FU-RBAC (seed `crm.campaign.*` and drop the territory fallback) · MOD-0165-FU-CONSUMER-GUARD (require consumers to check `FilterApplied`) · MOD-0165-FU-KPI (campaign results / KPI measurement, still out of scope per pack F6) · MOD-0167-FU-PROVIDER (replace caller-supplied snapshot items with a segment provider) · a shared external-reference value object to unify the three duplicated declarations (MOD-0290-FU01 / MOD-0164-FU02 / MOD-0165-FU04) · **MOD-0164 legal-basis vocabulary reconciliation** (§1 finding: shipped constants diverge from reconciliation F1 canonical).

## 25. Next Recommended Prompt

Recommended immediate step (closes this report to PASS with no new code):

> Run `scripts/smoke-mod0165-fu04-campaign-targeting-authenticated.ps1` for tenant `97c59330-dbc4-4665-b29c-0c26dbb5cc93` and paste the result table back.

Then, per the task's PASS branch — and per its closing note that visit/route planning stays on hold:

`MOD-0165-FU05 — Campaign / Targeting Admin UI`

or, if the master-data lane is preferred first:

`MOD-0290-FU02 — Brand/Product Runtime + UI`

MOD-0155 (Visit Planning / Route Planning) remains **on hold**.
