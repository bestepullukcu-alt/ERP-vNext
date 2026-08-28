---
id: DCP-007
slug: qms-baseline-import-completion-visibility-consumer-guardrails
name: QMS Baseline Import Completion Visibility and Consumer Guardrails
type: Delivery Capability Pack
standard: CAP-001
status: under-review
owner_domain: platform-shared-services
owner: platform-shared-services
created: 2026-08-27
runtime_code_allowed: false
authoring_branch: pending-clean-governance-branch
---

# DCP-007 — QMS Baseline Import Completion Visibility and Consumer Guardrails

> **Artifact and status guard.** This is a CAP-001 Delivery Capability Pack: a governance and orchestration
> contract, not a runtime entity, Module Pack, follow-up identity, production module, permission seed, Gateway task,
> or implementation authorization. It is `status: under-review` and remains `runtime_code_allowed: false`.
>
> **Identity evidence.** On 2026-08-27 the user explicitly assigned `DCP-007` and this exact canonical name. A
> repository-wide exact search immediately before authoring found no other `DCP-007` use, reservation, or artifact.
> This pack does not resolve the existing DCP-004 collisions, create a global DCP registry, or modify DCP-001 through
> DCP-006.
>
> **Exceptional authoring context.** The user explicitly authorized creation of this one new untracked governance
> file on the existing dirty `feature/crm-integration` branch. That exception authorizes neither runtime work nor a
> branch change, stage, commit, stash, reset, cleanup, push, approval, or execution. A clean governance branch remains
> the expected continuation point, hence `authoring_branch: pending-clean-governance-branch`.

## 1. Identity and status

| Field | Value |
|---|---|
| ID | `DCP-007` |
| Canonical name | QMS Baseline Import Completion Visibility and Consumer Guardrails |
| Owner domain | `platform-shared-services` |
| Standard | CAP-001 |
| Status | `under-review` |
| Runtime code allowed | `false` |
| Current authority | Planning and review only |
| Production implementation | Prohibited until this DCP and every affected member pack pass their own approval gates |

The user's 2026-08-27 approval authorizes technical review, governance reconciliation, and the four bounded member
amendments recorded below. It does not change DCP-007 to `approved`/`ready-for-execution` and is not runtime
implementation authority. G2 member governance, G4 governance ownership, and the G6 MVP self-polling policy decisions
are resolved below; their runtime implementation/evidence gates and all other execution gates remain open. FU07
remains `draft` with runtime disabled.

The capability is intentionally distinct from the future **Tenant-Scoped Group Baseline Sharing, Company Overlays
and Template Propagation** capability. The two must not be merged merely because both consume a QMS baseline.

## 2. Business outcome

No normal downstream consumer can observe or use an incomplete, non-completed, cross-tenant, partially persisted, or
integrity-mismatched QMS structure-baseline import. A new FU07 baseline becomes reviewable only after its import is
proven complete by exact immutable evidence. Controlled pre-FU07/manual evidence follows the separate legacy
disposition contract. Activation and provisioning remain separately fail-closed against source authority and
governance blockers.

This outcome preserves three independent truths:

1. import persistence completed and is internally consistent;
2. a tenant-authorized governance user may safely review the completed DRAFT output;
3. the reviewed output is eligible for a requested operational action.

Completion never implies approval, Effective status, activation, provisioning, publication, source authority, or
production readiness.

## 3. Problem statement

The current runtime creates a `BaselineRelease` before its `CollectionDefinition` set and returns a synchronous
success result. It has no `QmsRegisterImportOperation`, immutable import manifest, manifest-last finalization, or
central completion guard. Existing list/detail/definition queries read the baseline directly. Lifecycle handlers,
Company instantiation, Corporate provisioning, and reconciliation enforce tenant and lifecycle status but do not
prove completion of the import that produced the baseline.

Consequently a crash, retry race, partial definition write, missing manifest, count/hash mismatch, or direct-ID bypass
can expose intermediate state or allow it to become a downstream input. Consumer-specific filters would duplicate
policy and leave future entry points vulnerable to omission.

## 4. Capability boundary

### In boundary

- FU07 resumable/chunked Commit operation, worker ownership, lease/fencing, recovery, and manifest-last finalization.
- Exact completion evidence for the baseline, definitions, immutable detection findings, counts, hashes, and immutable
  import manifest.
- One authoritative `ImportCompletionGuard` service/policy and tenant-first exact/batched lookup primitives.
- Baseline and definition normal-list and direct-ID visibility.
- Controlled operation-status polling independent from baseline consumer visibility.
- FU03 TenantShell 202/poll/terminal-state behavior.
- Publish, approve, mark-effective, Company instantiation, Company provisioning, Corporate provisioning, and governed
  reconciliation enforcement seams.
- Legacy/manual null-operation inventory and explicit disposition.
- Fail-closed error behavior, audit evidence, crash recovery, idempotency, concurrency, and tenant-security proof.

### Boundary constraints

- This DCP coordinates owners; it does not replace or automatically approve any Module Pack.
- Existing packless `MOD-0028-FU08` and `MOD-0028-FU09` annotations are AS-IS drift evidence only. They are not
  canonical identities, member packs, systems of record, approved dependencies, or authority to create/reserve a new
  FU identity.
- Baseline lifecycle completion-guard integration is owned by an FU02 amendment. Company reconciliation/readiness is
  owned by an FU05 amendment and Corporate reconciliation/readiness by an FU06 amendment.
- The Gateway is transport only and never owns the completion policy.

## 5. Member modules and follow-ups

| Member / seam | Current governance state | DCP-007 impact | Required next governance action |
|---|---|---|---|
| `MOD-0028-FU07` | `draft`, `runtime_code_allowed: false` | Own import operation, resumable worker, immutable import manifest, finalization, and authoritative `ImportCompletionVerified` evidence only | Final reconciliation, review, and explicit approval |
| `MOD-0028-FU02` | `approved` | Add lineage/disposition and combined consumer-guard contract; enforce list/detail/definition plus publish/approve/effective seams | Approved amendment |
| `MOD-0028-FU03` | `approved` | Handle 202 Accepted, operation polling, stale version, controlled terminal UX, and Completed redirect | Approved amendment |
| `MOD-0028-FU05` | `approved` | Require guard before prerequisites, planning, execution, operation creation, and instance creation | Approved amendment |
| `MOD-0028-FU06` | `review` | Require guard before Corporate provisioning operation creation and definition consumption | Amendment plus normal pack review/approval |
| Baseline lifecycle runtime seam | FU02 amendment | Enforce the completion guard for list/detail/definition and publish/approve/mark-effective | Approved FU02 amendment; FU08 annotation remains AS-IS drift evidence only |
| Reconciliation runtime seam | FU05 Company + FU06 Corporate amendments | Direct baseline/definition/instance consumption requires completion and explicit scope-owner guards | Approved FU05/FU06 amendments; FU09 annotation remains AS-IS drift evidence only |

No member status is promoted by this DCP. Production code requires this DCP to be `approved` or
`ready-for-execution`, the active member pack to be `approved` or `ready-for-dev`, and explicit implementation
authorization for the named delivery step.

## 6. Ownership map

| Concern | Authoritative owner | Consumed by | Ownership rule |
|---|---|---|---|
| Import operation identity/state | FU07 | Polling UI, guard, audit | Server-generated, tenant-scoped, Commit-only |
| Operation status and safe findings-summary endpoints | FU07 | FU03 polling UI | Self-operation only in MVP; tenant/requester non-leakage is enforced server-side |
| Lease, epoch, cursor, chunks, retry | FU07 worker | Finalization | Stale owner/epoch/version cannot mutate state |
| Immutable import manifest | FU07 | Guard, audit, qualification | Written last; identical replay allowed, different content conflicts |
| Import completion evidence | FU07 | All guarded consumers | Immutable definition and detection-finding projections are recomputed/read back before `Completed` CAS |
| Import-completion decision | FU07 semantic contract | Combined consumer guard | Produces `ImportCompletionVerified` only; no legacy/manual bypass |
| Combined evidence/review/action guard | FU02 consumer boundary coordinated by DCP-007 | FU03/FU05/FU06 and governed seams | Applies legacy/manual disposition and downstream policy; never delegated to client or scattered filters |
| Baseline/definition lineage fields | FU02 amendment | Guard and query/mutation handlers | `ImportOperationId` required for new FU07 imports |
| Normal list/detail/lifecycle enforcement | FU02 amendment | FU03 and API consumers | List uses batch evaluation; direct ID always re-evaluates |
| TenantShell polling and review UX | FU03 amendment | Tenant governance users | UI displays controlled operation state, not raw import rows |
| Company instantiation/provisioning | FU05 amendment | Company instances | Guard before plan/operation/instance side effects |
| Corporate provisioning | FU06 amendment | Corporate instances | Guard before operation creation or definition reads |
| Baseline lifecycle completion-guard integration | FU02 amendment | List/detail/definition and publish/approve/mark-effective entry points | Broader Controlled Document lifecycle ownership is unchanged; MOD-0029 is not this guard's owner |
| Company reconciliation/readiness | FU05 amendment | Company-scoped reconciliation and readiness entry points | Requires explicit `CollectionScopeType + ScopeOwnerId`; guard and scope validation precede reads/side effects |
| Corporate reconciliation/readiness | FU06 amendment | Corporate-scoped reconciliation and readiness entry points | Requires explicit `CollectionScopeType + ScopeOwnerId`; guard and scope validation precede reads/side effects |
| Generic reconciliation engine | Owner-neutral technical component | FU05/FU06 governed entry points | Not a business SoR or canonical owner; scope-less or owner-mismatched calls fail closed |
| Legacy disposition authority | TBD governance owner + authorized reviewer | Guard | Evidence-backed, audited, and segregated from importer self-approval |
| Audit evidence transport/store | MOD-0021 boundary, exact retention owner TBD | Operations and guard failures | This DCP defines required evidence, not Records retention policy |

### Permission and segregation-of-duties boundary

- FU07 owns operation status, safe findings-summary, self-operation authorization, and tenant/requester non-leakage.
- FU02 remains the source of the existing `platform.document-management.qms-baselines.import` literal and owns the
  combined evidence/review/action guard plus baseline consumer enforcement.
- Import permission may start an import and poll only the current actor's same-tenant operation; it never grants
  publish, approve, Effective, provisioning, `ReviewVisible`, or baseline list/detail authority.
- Another actor's operation/findings access is deny-by-default with 404 non-leakage. Cross-user governance review is
  outside MVP and requires a future permission/security amendment; no current permission alone grants it.
- View or provisioning permission never bypasses the guard.
- Operation polling is same-tenant and permission-controlled but does not make its baseline review-visible.
- Legacy disposition must require controlled evidence, audit, and an owner-approved authorization seam; an importer
  cannot silently grandfather the import they initiated.
- This DCP creates no permission key or seed. Any new permission requires a separately approved member amendment.

## 7. Dependency graph

```text
DCP-007 review and approval
        |
        +--> FU02/FU03/FU05/FU06 amendments
        +--> resolved lifecycle/reconciliation ownership decisions
        +--> FU07 final reconciliation and approval
                    |
                    v
          FU02 lineage/disposition contract
                    |
                    v
FU07 operation + worker + manifest-last finalization + completion evidence
        |                         |
        |                         +--> 202/status/findings API
        v
FU02 read/lifecycle seams --------+--> FU03 polling/review UX
        |
        +--> FU05 Company guard
        +--> FU06 Corporate guard
        +--> governed reconciliation guard
                    |
                    v
          legacy inventory/disposition
                    |
                    v
 failure/concurrency/tenant-security/qualification evidence
```

The future Company-sharing/overlay/template-propagation capability may consume only stable guarded outputs after this
delivery is complete; it is neither a dependency of DCP-007 nor authorized by it.

## 8. Ordered delivery sequence

1. DCP-007 review and approval.
2. FU02, FU03, FU05, and FU06 amendments are prepared, reviewed, and approved through their own gates.
3. Apply the resolved governance ownership: FU02 for baseline lifecycle guard integration, FU05 for Company
   reconciliation/readiness, and FU06 for Corporate reconciliation/readiness.
4. FU07 is reconciled against DCP-007, reviewed, and approved; `runtime_code_allowed` changes only through an explicit
   authorized pack decision.
5. FU02 establishes baseline/definition lineage, origin, and legacy-disposition contracts.
6. FU07 implements the operation, worker, lease/fencing, manifest-last finalization, and
   `ImportCompletionVerified` evidence seam.
7. The async 202/status/findings API is delivered under approved permissions and transport tasks.
8. FU02 integrates the combined evidence guard, batch list, direct detail/definition, and lifecycle enforcement.
9. FU03 delivers polling, timeout/replay handling, stale-version behavior, terminal-state UX, and Completed redirect.
10. FU05 integrates the guard before prerequisites, planning, operation, and instance creation.
11. FU06 integrates the guard before Corporate provisioning operation creation and definition reads.
12. Reconciliation is integrated under FU05/FU06 amendments with explicit `CollectionScopeType + ScopeOwnerId`.
13. Legacy inventory, verified migration/backfill, grandfather evidence, and quarantine/retirement are executed.
14. Failure-injection, concurrency, tenant-security, load, runtime-smoke, and qualification evidence close.

Sequence order is a governance dependency. Parallel implementation is allowed only where the approved member packs
and interface contracts make it safe; no consumer may be enabled before its guard integration and negative tests pass.

## 9. Prerequisites

- This DCP is approved or ready-for-execution.
- The active member pack is approved or ready-for-dev for the named delivery step.
- FU02/FU03/FU05/FU06 amendment scopes and acceptance criteria are approved.
- FU07 final persistence, state-machine, manifest, endpoint, and test contracts are approved.
- The resolved lifecycle/reconciliation ownership decision is retained in each approved amendment; existing FU08/FU09
  annotations remain AS-IS drift evidence only.
- Tenant-first operation/manifest repositories and batch-query shapes are designed without client-provided tenant or
  proof values.
- Legacy baselines are inventoried before any compatibility rule is enabled.
- The FU07-owned MVP self-polling policy is retained; reason-code, audit-evidence, legacy-disposition, and any future
  cross-user reviewer authorities are selected through their remaining gates.
- Load-test row counts, chunk sizes, concurrency, lease duration, retry budget, and pass thresholds are approved.
- LOG-0007 v0.36 remains DRAFT / NOT APPROVED / NOT LIVE and cannot be operationally activated.
- Any Gateway work is handled as a separately authorized integration task; no Gateway change is implicit here.

## 10. Architecture decisions

### AD-1 — One authoritative completion policy

A single semantic `ImportCompletionGuard` service/policy is authoritative. Repository implementations provide only
tenant-first exact/batched lookup and persistence primitives. Completion logic must not be copied into repositories,
query filters, controllers, UI code, or individual downstream consumers.

Minimum semantic operations:

- `EvaluateBaselineAsync`
- `EvaluateBaselinesBatchAsync`
- `RequireCompletedAsync`

Minimum controlled result:

- `ImportCompletionVerified`
- `LegacyDispositionAllowed`
- `EvidenceOrigin`
- `ReviewVisible`
- `ActivationEligible`
- `ProvisioningEligible`
- `ReasonCode`
- `OperationId`
- `ManifestId`
- `VerificationFingerprint`
- `GovernanceBlockers`

If a compatibility field named `CompletionVerified` is retained, it is an alias for
`ImportCompletionVerified` only. It must never represent the combined import-or-legacy evidence decision.

List surfaces use batch evaluation and must not issue one operation/manifest/definition-count query per baseline.
Every direct-ID and mutation entry point re-evaluates authoritative state. A denormalized completion projection may be
used only as a performance hint; it is never authority. Client-provided proof is forbidden. A future proof token, if
used, must be server-signed and server-resolved, and mutations still revalidate current policy.

### AD-2 — Canonical operation state and ImportCompletionVerified

```text
ImportCompletionVerified =
    baseline.TenantId == currentTenant
    AND baseline.IsDeleted == false
    AND baseline.ImportOperationId != null
    AND same-tenant operation exists
    AND operation.Status == Completed
    AND exact same-tenant immutable import manifest exists
    AND operation.ManifestId == manifest.Id
    AND manifest.ImportOperationId == operation.Id
    AND manifest.BaselineReleaseId == baseline.Id
    AND persisted definition count/hash == finalized definition count/hash
    AND persisted finding count/hash == finalized finding count/hash
```

FU07's exact canonical persisted operation statuses are `Received`, `Validating`, `ValidationFailed`,
`ReadyToPersist`, `PersistingDefinitions`, `PersistingFindings`, `Verifying`, `Finalizing`, `Completed`,
`FailedRetryable`, and `FailedTerminal`. DCP-007 creates no additional operation state.

`ImportCompletionVerified` can be true only for `Completed`. `Received`, `Validating`, `ReadyToPersist`,
`PersistingDefinitions`, `PersistingFindings`, `Verifying`, `Finalizing`, `FailedRetryable`, `ValidationFailed`, and
`FailedTerminal` are never import-completion-verified. `ValidationFailed`, `Completed`, and `FailedTerminal` are
terminal. `FailedRetryable` may return only to the continuation state permitted by `LastFailurePhase`. Missing or
mismatched operation/manifest evidence, partial trees, tampered counts or hashes, deleted baselines, and cross-tenant
relationships fail closed.

A UI may map canonical statuses to a controlled display phase, but a display phase is neither a canonical persisted
status nor authority for the guard. Cancellation and manual retry remain future amendment scope; DCP-007 defines no
canonical cancellation or manual-retry state.

### AD-3 — EvidenceVerified and ReviewVisible

```text
EvidenceVerified =
    ImportCompletionVerified
    OR LegacyDispositionAllowed

ReviewVisible =
    EvidenceVerified
    AND tenant-authorized governance review
```

An import-completion-verified DRAFT baseline, candidate nodes, and safe aggregate finding summaries may be reviewed.
Governance findings alone do not hide a complete DRAFT baseline from authorized review. A legacy/manual baseline may
reach the same controlled review surface only through the explicit disposition contract in AD-8.

`Quarantined`, `Retired`, and `Unclassified` legacy inventory remains absent from normal operational and standard
baseline-review lists. As the sole surface-scoped exception, such a record may be `ReviewVisible` only on the dedicated
governance-inventory surface for an authorized inventory reviewer. That exception does not make `EvidenceVerified`
true and never grants activation or provisioning eligibility.

Review responses never expose raw workbook bytes, raw rows, `Implementation Note`, sensitive Notes, secrets, or
uncontrolled source payloads. Normal operational consumers do not receive review-only records.

### AD-4 — Action eligibility

`ActivationEligible` and `ProvisioningEligible` first require `EvidenceVerified`, then add the applicable current
source-authority, approval, lifecycle, and governance-blocker policies. A blocker can prohibit activation/provisioning
without hiding an evidence-verified DRAFT baseline from authorized review.

LOG-0007 v0.36 is DRAFT / NOT APPROVED / NOT LIVE; therefore its activation and provisioning eligibility remain false
regardless of technical completion or profile review approval.

### AD-5 — Operation-status exception

Normal baseline visibility and import-operation polling are different policies. FU07 owns the operation aggregate,
status endpoint, safe findings-summary endpoint, self-operation authorization enforcement, and tenant/requester
non-leakage. The MVP policy is:

```text
CanPollOwnImportOperation =
    same tenant
    AND HasPermission("platform.document-management.qms-baselines.import")
    AND operation.RequestedBy == current actor
```

The safe findings-summary endpoint applies the identical policy. Cross-tenant and another-actor access return 404
without disclosing operation existence, source identity, checksum, finding metadata, or actor metadata. Cross-user
governance reviewer polling is outside MVP, deny-by-default, and deferred to a future permission/security amendment.
Existing `view`, `publish`, or `import` permission alone never grants cross-user polling. This deferral is not a
DCP-007 ready-for-execution blocker for the self-polling MVP.

Polling access does not make the baseline `ImportCompletionVerified`, `EvidenceVerified`, `ReviewVisible`,
consumer-visible, activation-eligible, or provisioning-eligible. Completed baseline review is independently governed:

```text
CanReviewCompletedBaseline =
    same tenant
    AND HasPermission("platform.document-management.qms-baselines.view")
    AND ReviewVisible == true
```

`POST import/commit` returns `202 Accepted` with controlled `OperationId`, `Status`, `IsReplay`, and `StatusUrl`.
Status/findings responses may contain only `OperationId`, canonical `Status`, controlled display phase, `Version`,
`IsReplay`, retryability, bounded progress counts, controlled `ReasonCode`, `StatusUrl`, safe aggregate finding
counts/categories, and `BaselineReleaseId` only after `Completed`. They never return raw workbook, raw row,
`Implementation Note`, sensitive Notes, secrets, uncontrolled source payload, or another user's actor metadata. Normal
resume is worker-owned; cancel and mandatory manual retry are not implied by this DCP.

### AD-6 — Visibility and failure behavior

| Condition / surface | Required behavior |
|---|---|
| Incomplete baseline in normal list | Omit |
| Incomplete direct baseline/definition access | `404 NOT_FOUND_NON_LEAKAGE` |
| Cross-tenant baseline/operation/manifest | `404 NOT_FOUND_NON_LEAKAGE` |
| Incomplete publish/approve/effective/provisioning | `409 IMPORT_NOT_COMPLETED` |
| Manifest/count/hash mismatch | `409 IMPORT_INTEGRITY_MISMATCH` plus integrity audit evidence |
| Missing legacy disposition | `409 LEGACY_DISPOSITION_REQUIRED` |
| Source or governance blocker | `409 SOURCE_AUTHORITY_BLOCKED` |
| Guard storage/dependency unavailable | fail-closed `503 IMPORT_COMPLETION_GUARD_UNAVAILABLE` |

No failed guard permits a mutation or downstream side effect. Company planning produces no plan/operation/instance;
Company provisioning produces no instance; Corporate provisioning produces no operation; reconciliation reads no
incomplete definitions. Direct reads use 404 rather than conflict so existence is not leaked.

### AD-7 — Manifest-last recovery and concurrency

FU07 correctness does not depend on one long multi-document transaction. The worker persists staged state under a
lease/epoch and optimistic version, reads back definitions/findings, verifies counts/hashes, enters Finalizing, inserts
the exact immutable manifest, then changes the operation to Completed by CAS. A crash after manifest insert leaves the
operation Finalizing; retry verifies the identical manifest and reapplies the Completed CAS.

An identical duplicate manifest is an idempotent replay. Different immutable content under the same manifest key is a
terminal integrity conflict. A stale lease owner, stale epoch, or stale expected version cannot advance state.

#### Immutable finding count/hash projection

The finding count/hash verified during import finalization covers only immutable detection evidence. Its deterministic
projection includes:

- `FindingScopeKey`;
- controlled `FindingCode` and `FindingCategory`;
- immutable field/scope identity;
- immutable observed-evidence hash;
- immutable blocker flags as detected at import time; and
- any other detection field that the approved FU07 contract explicitly classifies as immutable.

The finalization projection excludes mutable workflow `CurrentStatus`, assignee/assignment projections, resolution
state, `QmsRegisterFindingResolution` entries, later governance decisions, re-evaluated eligibility projections,
`UpdatedAt`, `Version`, and audit-projection state. Later assignment, resolution, or parent-resolution re-evaluation
therefore does not invalidate a completed import manifest or its finding hash.

Resolution integrity is independently proven by the append-only `QmsRegisterFindingResolution` ledger, tenant-scoped
CAS/version rules, and audit contract. Because the current FU07 draft declares expected/persisted finding hashes but
does not explicitly freeze this projection, Gate G12 must reconcile this exact semantic into FU07 before persistence
implementation begins; DCP-007 does not amend FU07 by implication.

### AD-8 — Legacy baseline disposition

Blanket `ImportOperationId == null -> visible` is prohibited. Legacy/manual evidence is available only to a
controlled inventory of pre-FU07 or manual baselines and requires one of these exact dispositions:

1. `LegacyVerifiedMigration` — source, tree, counts, hashes, tenant, and lineage are reproduced and independently
   verified through controlled migration/backfill.
2. `LegacyGrandfathered` — an inventoried pre-FU07 baseline has approved qualification and immutable evidence that
   justifies controlled continued use when exact reconstruction is impossible.
3. `ManualVerified` — a controlled manual-origin baseline has independently verified immutable evidence.

```text
LegacyDispositionAllowed =
    baseline is in the controlled pre-FU07/manual inventory
    AND disposition IN (LegacyVerifiedMigration, LegacyGrandfathered, ManualVerified)
    AND tenant-scoped immutable evidence exists
    AND authorized approver and segregation of duties are proven
    AND disposition audit evidence exists
    AND VerificationFingerprint matches the immutable evidence
```

`EvidenceOrigin` identifies `FU07Import`, `LegacyVerifiedMigration`, `LegacyGrandfathered`, or `ManualVerified`; it is
evidence classification, not an operation status. A new FU07 import must always use `ImportCompletionVerified` and can
never bypass its operation/manifest gate through a legacy or manual disposition. Null operation is never implicit
manual origin or implicit completion. Records with insufficient evidence remain `Quarantined`, `Retired`, or
`Unclassified` inventory and are never activation- or provisioning-eligible. Disposition changes require
authorization, immutable evidence, audit, verification fingerprint, and segregation of duties.

## 11. Scope

### Governance and contract scope

- Cross-pack architecture, ownership, dependencies, sequencing, gates, and acceptance criteria.
- FU07 operation/manifest/guard requirements and FU02/FU03/FU05/FU06 amendment boundaries.
- Lifecycle/reconciliation owner-selection gates without legitimizing current annotations.
- Baseline/definition/operation visibility semantics and controlled HTTP/reason-code behavior.
- Legacy inventory and disposition decision hierarchy.
- Required security, failure, concurrency, load, audit, and qualification evidence.

### Future implementation repo impact, only after all gates

- `services/Diten.Platform/src/**` and corresponding tests for approved member slices.
- `frontend/Diten.Web/**` for the approved FU03 polling/review slice.
- Gateway only through a separate authorized integration task if endpoint exposure requires it.

This section predicts impact; it grants no permission to modify those paths.

## 12. Explicit exclusions

- LOG-0007 source-column, path, classification, or mapping redesign.
- Tenant/Company sharing policy, Company overlays, local additions, group-node propagation/removal, or template
  sharing/rebase/propagation.
- General Controlled Document lifecycle redesign.
- Records Management retention, legal hold, records disposition, or business retention policy.
- Permission key or seed changes.
- Gateway route or configuration changes.
- Creation, reservation, renaming, or legitimization of FU08, FU09, FU10, or any other MOD/FU identity.
- Treating current FU08/FU09 annotations as a member pack, SoR, or approved dependency.
- Production runtime implementation, activation, migration execution, data repair, or deployment.
- Production activation of DRAFT LOG-0007.

## 13. Governance drift risks

| Risk | Consequence | Required control |
|---|---|---|
| Completion copied into consumer filters | New entry point bypasses policy | Central guard plus entry-point coverage test |
| Denormalized flag treated as authority | Stale false-positive visibility | Always resolve authoritative operation/manifest evidence |
| Review visibility conflated with action eligibility | Findings incorrectly hide review or DRAFT becomes operational | Preserve three separate decisions |
| Polling conflated with baseline visibility | Incomplete baseline exposed through status UX | Operation-only controlled DTO and permission |
| Lifecycle/reconciliation annotations treated as canonical | Packless runtime becomes accidental SoR | Canonical-owner gate; no inferred identity |
| Existing lifecycle manifest confused with FU07 import manifest | Wrong evidence proves completion | Separate semantic contracts and exact cross-links |
| Null operation treated as legacy success | Unverified baseline becomes operational | Explicit disposition inventory and fail-closed default |
| Importer self-grandfathers their own import | SoD and audit failure | Independent authorization/evidence decision |
| Mutable finding workflow fields enter finalization hash | Later resolution falsely invalidates a completed import | Hash only the immutable detection-evidence projection; verify resolution through its ledger |
| Guard invoked after operation/instance creation | Forbidden side effect survives rejection | Guard before planning and every mutation side effect |
| N+1 guard evaluation | Normal list becomes unusable | Batch API and query-count regression |
| DRAFT source status ignored | Technical completion becomes activation | Source-authority rule evaluated for each action |
| DCP status confused with implementation readiness | Premature coding/release | Preserve DCP and member-pack approval gates |

## 14. Review questions

Resolved on 2026-08-27: lifecycle guard integration belongs to FU02; reconciliation/readiness is split between FU05
Company and FU06 Corporate scopes; FU07 owns self-operation polling/findings under the exact MVP policy in AD-5; and
cross-user polling is deny-by-default/deferred.

1. Which owner approves legacy verified migration, grandfather evidence, and quarantine/retirement?
2. Which authorization seam enforces segregation between importer and legacy disposition approver?
3. Which component owns the controlled reason-code catalog and client localization mapping?
4. Which audit owner stores integrity failures, disposition evidence, and guard-denial events, and what technical
   retention applies without redefining Records policy?
5. What load fixture, chunk size, worker concurrency, lease duration, retry budget, and latency/query thresholds are
   required for qualification?
6. Is the existing lifecycle `BaselineSnapshotManifest` retained as a separate lifecycle artifact, and what exact
    naming prevents it from being mistaken for FU07's import-completion manifest?
7. Which FU07 contract revision and test fixture freezes the immutable detection-finding count/hash projection while
    keeping assignment, resolution, and re-evaluated eligibility outside it?

## 15. Gate criteria

| Gate | Pass condition | Blocks |
|---|---|---|
| G1 — DCP review | User approves DCP-007 scope, decisions, members, exclusions, and sequence | All execution |
| G2 — Member governance | **Resolved/approved at governance level on 2026-08-27:** FU02/FU03/FU05/FU06 amendments separately approved by the user | Runtime still requires DCP/member execution gates and acceptance evidence |
| G3 — FU07 pack | FU07 reconciled, approved, and explicitly authorized for the named step | Operation/guard runtime |
| G4 — Canonical seam ownership | **Governance decision resolved:** FU02 lifecycle guard; FU05 Company reconciliation/readiness; FU06 Corporate reconciliation/readiness; no inferred FU identity | Runtime still requires G2/G3 and acceptance evidence |
| G5 — Completion contract | Operation, state machine, manifest, count/hash, CAS, and tenant contracts approved | Persistence/runtime start |
| G6 — Polling access | **MVP policy resolved:** FU07-owned same-tenant importer self-polling/findings; cross-user deny/deferred. DTO, endpoint, enforcement, and tests remain acceptance evidence | Async API/UI runtime |
| G7 — Legacy authority | Inventory, evidence, disposition, permission, SoD, and audit owners approved | Legacy visibility/use |
| G8 — Reason/audit ownership | Reason codes, audit events, evidence store, and technical retention approved | External failure contract |
| G9 — Load qualification | Fixture and quantitative pass thresholds approved | Readiness claim |
| G10 — Source authority | DRAFT/NOT APPROVED/NOT LIVE activation prohibition proven | Activation/provisioning |
| G11 — Negative evidence | Side-effect absence, cross-tenant 404, crash/retry, fencing, and tamper tests pass | Release/readiness |
| G12 — FU07 immutable finding-hash projection reconciliation | FU07 explicitly fixes the immutable detection fields, exclusions, ordering/canonicalization, and fixtures used by finalization count/hash | Finding persistence/runtime start |

G2 member governance, G4 governance ownership, and the G6 MVP policy decisions are closed by explicit user approval on
2026-08-27. This is not execution permission: G1, G3, G5, G7, G8, G9, G10, G11, G12, runtime
DTO/endpoint/enforcement/tests, and all applicable member execution gates remain open. FU06's parent pack remains
`review`, so its runtime step is separately blocked. No gate is waived by existing runtime code or by changing this
DCP's status alone. In particular, G12 remains open until FU07's exact finding-hash canonical
serialization/normalization and fixture values receive explicit user/owner approval.

## 16. Acceptance criteria

- [ ] A partial or nonterminal import baseline appears in no normal consumer list.
- [ ] Incomplete direct baseline and definition access returns `404 NOT_FOUND_NON_LEAKAGE` without existence or
  source/checksum leakage.
- [ ] Cross-tenant baseline, operation, manifest, definition, polling, and findings-summary access returns 404.
- [ ] An import-completion-verified or explicitly allowed legacy/manual DRAFT baseline is visible to an authorized
  governance reviewer under the applicable surface policy.
- [ ] Governance findings do not hide an evidence-verified DRAFT baseline from authorized review.
- [ ] Governance findings and source blockers still prevent activation/provisioning where policy requires.
- [ ] Review surfaces expose safe summaries only; raw workbook, raw rows, `Implementation Note`, sensitive Notes, and
  uncontrolled payloads are absent.
- [ ] FU07 uses only `Received`, `Validating`, `ValidationFailed`, `ReadyToPersist`, `PersistingDefinitions`,
  `PersistingFindings`, `Verifying`, `Finalizing`, `Completed`, `FailedRetryable`, and `FailedTerminal` as canonical
  persisted operation statuses; only `Completed` may satisfy `ImportCompletionVerified`.
- [ ] `ValidationFailed`, `Completed`, and `FailedTerminal` are terminal; `FailedRetryable` resumes only at the
  continuation state permitted by `LastFailurePhase`.
- [ ] Missing/mismatched manifest, relationship, count, or hash fails closed and records integrity audit evidence.
- [ ] Publish/approve/effective and provisioning return controlled 409 when import completion is absent.
- [ ] Source/governance blocker returns controlled `409 SOURCE_AUTHORITY_BLOCKED` without erasing review visibility.
- [ ] Guard dependency failure returns controlled 503 and creates no mutation/downstream side effect.
- [ ] Company guard failure creates no prerequisite candidate, plan, instantiation operation, outcome, or instance.
- [ ] Corporate guard failure creates no provisioning operation or instance.
- [ ] Reconciliation reads no incomplete definitions and persists no finding derived from them.
- [ ] Every reconciliation/readiness call supplies and validates `CollectionScopeType + ScopeOwnerId`; scope-less or
  owner-mismatched calls fail closed, and providers/readiness queries never aggregate every same-baseline instance
  without the approved Company/Corporate scope filter.
- [ ] Reconciliation side effects begin only after completion-guard and scope-owner validation succeeds.
- [ ] Commit responds 202 and exposes only controlled operation status until Completed.
- [ ] Manifest-last crash recovery completes the same operation without duplicate baseline, definition, finding, or
  manifest records.
- [ ] Same-operation retry is idempotent; different immutable content conflicts deterministically.
- [ ] Lease epoch/fencing and optimistic CAS reject stale workers.
- [ ] Identical duplicate manifest is replay-safe; different-content duplicate is terminal conflict.
- [ ] Count/hash tampering closes all visibility and eligibility paths.
- [ ] Finalization finding count/hash contains only the immutable detection-evidence projection; later assignment,
  resolution, parent-resolution re-evaluation, mutable status, version, timestamp, or audit projection does not alter
  a completed import's manifest/hash integrity.
- [ ] Resolution integrity is independently reproducible from the append-only `QmsRegisterFindingResolution` ledger,
  CAS/version, and audit evidence.
- [ ] Batch list evaluation produces no per-baseline N+1 completion query pattern.
- [ ] Legacy null-operation baselines follow the approved `LegacyVerifiedMigration`, `LegacyGrandfathered`, or
  `ManualVerified` disposition matrix; no blanket visibility exists.
- [ ] Every allowed legacy/manual disposition has tenant-scoped immutable evidence, authorized approver, SoD, audit,
  and a matching verification fingerprint.
- [ ] A new FU07 import cannot use a legacy/manual disposition to bypass operation/manifest verification.
- [ ] Quarantined/Retired/Unclassified legacy records are absent from normal operational surfaces, visible only as
  controlled inventory evidence, and never activation- or provisioning-eligible.
- [ ] FU03 handles timeout, deterministic replay, stale polling version, Completed redirect, and controlled terminal
  failure without treating polling access as baseline visibility.
- [ ] DRAFT / NOT APPROVED / NOT LIVE LOG-0007 produces no operational activation or provisioning under any path.
- [ ] Gateway remains transport-only and owns no completion decision.

## 17. Downstream business-module impacts

| Consumer | Impact |
|---|---|
| FU03 TenantShell baseline review | Distinguishes operation polling, evidence-verified review, and action eligibility |
| FU05 Company instantiation/provisioning | Consumes authoritative guard before prerequisites, planning, operations, and instances |
| FU06 Corporate provisioning | Consumes authoritative guard before operation creation and definition reads |
| Lifecycle consumer | FU02 amendment prevents list/detail/definition and publish/approve/effective use of an incomplete baseline |
| Reconciliation consumer | FU05 Company and FU06 Corporate amendments prevent scope-less or incomplete definition/instance use |
| Template/Controlled Document consumers | May consume only guarded provisioning outputs; no direct incomplete baseline access |
| Future Company-sharing capability | Receives stable completed inputs later; no sharing behavior is authorized here |

Existing completed historical outputs are not automatically deleted or rolled back by this DCP. Current action
eligibility is re-evaluated under source/governance policy, while destructive rollback or withdrawal requires separate
governance.

## 18. Open decisions

Resolved on 2026-08-27 and therefore removed from the open-decision table: lifecycle guard ownership, the
Company/Corporate reconciliation ownership split, FU07 polling/findings endpoint ownership, the importer self-polling
policy, and cross-user polling deferral/deny-by-default.

| Decision | Owner | Required before |
|---|---|---|
| Import versus lifecycle manifest naming/relationship | FU02/FU07 owners + architecture | Persistence contract approval |
| Legacy evidence and disposition authority | QA/QMS + governance owner | Legacy inventory execution |
| Legacy disposition SoD/authorization seam | Security + QA/QMS | Legacy compatibility enablement |
| Reason-code ownership/localization | API/UI owners | Public contract implementation |
| Audit evidence and technical retention ownership | Audit owner + compliance | Runtime evidence design |
| Load fixture and pass thresholds | QA/performance/operations | Qualification run |
| Manual retry permission/gate, if required | Product/security owner | Any manual retry endpoint |
| FU07 immutable finding-hash projection reconciliation | FU07 owner + architecture + QA/QMS | Finding persistence/runtime start |

Open decisions are fail-closed. The resolved ownership/access decisions likewise authorize no new FU identity,
permission seed, Gateway route, or runtime step.

## 19. Future follow-ups

- A separate Delivery Capability Pack for **Tenant-Scoped Group Baseline Sharing, Company Overlays and Template
  Propagation**, only after FU07 import and DCP-007 guard evidence are complete.
- A separately governed source revision when LOG-0007 becomes approved/live; DRAFT v0.36 is never promoted by this
  pack.
- Optional cancellation or manual-retry lifecycle only through an explicit future amendment with permission, audit,
  idempotency, and worker-ownership decisions.
- Optional server-signed proof-token optimization only after the central guard is proven and remains authoritative.
- Any destructive withdrawal of already Effective Company content after source suspension requires separate
  governance; it is not an automatic DCP-007 effect.
- Permanent DCP registry/verifier work remains separate from this pack and requires its own authorization.

## 20. Audit and reconciliation notes

### AS-IS evidence at draft authoring, 2026-08-27

- Repository-wide exact `DCP-007` search returned no existing use before this file was authored.
- Current `BaselineRelease` and `CollectionDefinition` runtime types have no import-operation lineage field.
- Current Commit creates a DRAFT baseline and definitions synchronously and returns 201; no FU07 operation,
  manifest-last completion, or polling contract exists.
- Current baseline list/detail/definition handlers do not evaluate import completion; list also performs per-baseline
  definition lookup.
- Current publish/approve/effective handlers enforce lifecycle/definition conditions but not FU07 completion evidence.
- Current FU05 and FU06 paths enforce lifecycle status but do not prove exact completed import evidence before planning
  or operation creation.
- Current reconciliation explicitly supports multiple baseline statuses and reads definitions directly.
- Current FU03 UI treats Commit success as immediate baseline success and redirects by baseline ID; no 202 polling UX
  exists.
- Repository-wide runtime search found no `ImportCompletionGuard`, `QmsRegisterImportOperation`,
  `QmsRegisterImportManifest`, or the controlled DCP-007 reason codes.
- Existing FU08/FU09 annotations were treated only as AS-IS drift evidence. No identity was created, reserved, or
  legitimized.

### Authoring integrity

- Initial authoring was limited to this governance Markdown file on the dirty branch.
- The 2026-08-27 governance-reconciliation authorization is limited to this DCP and the existing FU07 draft Module
  Pack. No runtime, frontend, Gateway, registry, tracker, test, `.antigravity`, branch, stage, commit, stash, reset,
  cleanup, or push change is authorized.
- Build/test execution is not applicable to this documentation-only authoring step.

### Governance review record — 2026-08-27

- DCP-007 technical draft review: **PASS**.
- Canonical operation-state, legacy/manual evidence, and immutable finding-hash review findings are closed at the DCP
  contract level.
- The pack moved from `draft` to `under-review`.
- FU07 governance reconciliation was started against DCP-007.
- Runtime implementation did not start.
- The user has not changed DCP-007 to `approved` or `ready-for-execution`.
- Member Module Pack approval gates and runtime acceptance evidence remain independent, and FU07 remains `draft` with
  `runtime_code_allowed: false`.

### Ownership and access reconciliation record — 2026-08-27

- The user explicitly approved FU02 amendment ownership for QMS `BaselineRelease` list/detail/definition and
  publish/approve/mark-effective completion-guard integration. This changes no broader Controlled Document lifecycle
  ownership; MOD-0029 is not the owner of this baseline guard, and FU08 annotations remain AS-IS drift evidence.
- The user explicitly approved FU05 amendment ownership for Company reconciliation/readiness and FU06 amendment
  ownership for Corporate reconciliation/readiness. The generic reconciliation engine is an owner-neutral technical
  component, requires explicit `CollectionScopeType + ScopeOwnerId`, and is neither a business SoR nor authority.
- The user explicitly approved FU07 ownership of the import operation, status endpoint, safe findings-summary endpoint,
  self-operation authorization, and tenant/requester non-leakage. FU02 retains the import permission literal and
  combined consumer guard; FU03 remains the polling UI consumer.
- G4 governance ownership is resolved. G6's MVP self-polling policy is resolved; cross-user polling is explicitly
  deferred and deny-by-default. Runtime DTO/endpoint/enforcement/tests remain open acceptance evidence.
- FU02/FU03/FU05/FU06 amendments were separately approved by the user on 2026-08-27, resolving G2 at governance level.
  DCP/FU07 execution approval gates, FU06 parent runtime readiness, G12, load/lease/heartbeat, retention/audit, and all
  other named runtime evidence gates remain open.
- No runtime, frontend, Gateway, registry, tracker, test, permission seed, `.antigravity`, or Git-state change was made
  by this governance reconciliation.

### Member amendment approval record — 2026-08-27

- The user explicitly approved the bounded DCP-007 amendments in FU02, FU03, FU05, and FU06 as scope, acceptance
  criteria, and test governance contracts.
- G2 is therefore resolved/approved at governance level.
- The approval is not runtime implementation, deployment, activation, or permission-seed authority.
- DCP-007 remains `under-review` with `runtime_code_allowed: false`; FU07 remains `draft` with runtime disabled.
- G2 closure does not close G1, G3, G5, G7, G8, G9, G10, G11, or G12. FU06's parent `review` status separately blocks
  its runtime step.

### Reconciliation template for later approved delivery

| Delivery step | Member pack/status evidence | Changed paths | Tests/smoke | Gate result | Remaining debt |
|---|---|---|---|---|---|
| Governance reconciliation only | DCP `under-review`; FU07 remains `draft`/runtime-disabled | DCP-007 and FU07 governance Markdown only | Documentation checks only | G4 ownership and G6 MVP policy decisions resolved; no execution permission | Member amendments, runtime evidence, G12, and remaining gates stay open |

This section may be updated only with truthful evidence after the relevant DCP/member-pack gates and explicit user
authorization. Planning text or existing code presence must never be reported as completion, verification, or
production readiness.
