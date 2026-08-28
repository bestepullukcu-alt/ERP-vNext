---
id: MOD-0021-FU02
name: Audit Outbox Temporal Storage Hardening
domain: platform-shared-services
service: Diten.Platform
shell: none
golden_reference: none
entity_base: BaseEntity
status: review
owner: audit-owner
branch: feature/pss/mod-0021-fu02-audit-outbox-temporal-storage-hardening
started: 2026-08-28
target: 2026-09-15
form_field_count: 0
parent: MOD-0021
---

# MOD-0021-FU02 — Audit Outbox Temporal Storage Hardening

## 1. Module Summary

This backend-only follow-up repairs the measured temporal-storage defect in MOD-0021 `audit_outbox` without changing the BSON representation of every `DateTimeOffset` member in Platform. The current MongoDB .NET driver representation persists `AuditOutboxMessage.NextAttemptAtUtc` and `CreatedAtUtc` as `[ticks, offsetMinutes]` arrays. Real-Mongo evidence proved that this representation makes the existing ascending claim order and `Status + NextAttemptAtUtc` range predicate semantically incorrect for mixed offsets, even when Mongo uses the existing multikey index.

The safe transition is Audit-Outbox-specific and versioned:

1. add lossless scalar UTC-instant tick shadow fields while retaining legacy fields;
2. dual-write and compatibility-read both representations;
3. run an explicit, default-disabled, durable, checkpointed and fenced migration;
4. measure real-Mongo index candidates before the Audit owner selects one;
5. cut claim eligibility/order to scalar fields with a deterministic ID tie-breaker only after the activation fence proves complete migration and index readiness;
6. retain legacy fields and indexes through a rollback-compatible stabilization release.

This pack does not silently migrate `audit_events`, `BaseEntity` timestamps, eventing outboxes or any other `DateTimeOffset` surface.

## 2. Ownership and Boundaries

### In scope

- MOD-0021 `audit_outbox` temporal persistence and claim correctness only.
- `AuditOutboxMessage.NextAttemptAtUtc` and `CreatedAtUtc` compatibility shadows.
- New writes, retry updates, cancellation release and claim updates dual-writing the exact UTC instant.
- Durable migration preflight, lease, checkpoint, CAS batching, replay and activation fence.
- Candidate scalar claim indexes measured with real Mongo `explain`; owner selection is a code-start gate.
- Scalar claim cutover and rollback compatibility.
- Regression inventory for `audit_events` and other Platform `DateTimeOffset` fields without migrating them.

### Out of scope

- Global/member `DateTimeOffsetSerializer` registration or convention changes.
- Re-serializing all Platform documents.
- Changing `AuditEvent` schema, query/retention semantics or audit payload timestamps.
- Eventing, notifications, BRD, WorkCenter, workflow, MDM, Auth, Gateway or frontend changes.
- Production/Staging migration execution, data cleanup or legacy-field/index removal.
- Reusing or overwriting backlog identity `BL-030`.

### Ownership

- MOD-0021 Audit owner owns the `audit_outbox` schema transition and claim semantics.
- Platform Persistence owner approves the schema-manifest delta and migration checkpoint collection/lease semantics.
- Operations owner separately authorizes each environment migration/cutover/rollback run.
- Backlog owner assigns a collision-free canonical debt identity; this pack does not mint one.

## 3. Owned Objects

| Object | Kind | Ownership / purpose |
|---|---|---|
| `AuditOutboxMessage.NextAttemptAtUtcTicksV1` | additive scalar shadow | UTC instant ticks for eligibility/range |
| `AuditOutboxMessage.CreatedAtUtcTicksV1` | additive scalar shadow | UTC instant ticks for deterministic ordering |
| `AuditOutboxMessage.TemporalStorageVersion` | additive version marker | identifies complete v1 shadow materialization |
| `AuditOutboxTemporalMigrationState` | durable operational state | one named migration, lease, checkpoint, counts, phase and activation evidence |
| `AuditOutboxTemporalStorageMigrationRunner` | explicit runner | preflight, lease, CAS batch migration, replay/read-back; never hosted automatically |
| compatibility codec/helpers | internal persistence logic | exact conversion between legacy `DateTimeOffset` and scalar UTC ticks |
| scalar claim implementation | repository behavior | post-fence eligibility/order using scalar values and `Id` tie-breaker |
| candidate index evidence | test/report evidence | real-Mongo plans and result correctness before one candidate is selected |

No public API endpoint, permission, navigation item, frontend route or business entity is introduced.

## 4. Entity Fields

### Existing `audit_outbox` additive fields

| Field | BSON type | Required by phase | Rule |
|---|---|---|---|
| `NextAttemptAtUtc` | legacy array | retained | Existing compatibility field; never rewritten to a new BSON type in place |
| `CreatedAtUtc` | legacy array | retained | Existing compatibility field; retained through rollback stabilization |
| `NextAttemptAtUtcTicksV1` | `Int64` | required for v1-complete row | `NextAttemptAtUtc.UtcTicks`; exact instant, checked conversion |
| `CreatedAtUtcTicksV1` | `Int64` | required for v1-complete row | `CreatedAtUtc.UtcTicks`; exact instant, checked conversion |
| `TemporalStorageVersion` | `Int32` | required for v1-complete row | exact value `1`; unknown/newer values fail closed |

UTC ticks preserve the instant losslessly. The legacy array remains the rollback/source-evidence representation, including its original offset. No claim comparison uses offset minutes after scalar cutover.

### Migration state

| Field | Type | Rule |
|---|---|---|
| `Id` | string | fixed migration identity; no caller-selected alias |
| `TargetVersion` | int | exact `1` |
| `Phase` | enum/string | `Preflight`, `Ready`, `Migrating`, `RecoveryRequired`, `Completed`, `CompletionVerified`, `CutoverActive`, `Failed` |
| `LeaseOwner` | string | opaque bounded process identity; not a credential |
| `LeaseGeneration` | long | monotonically increasing fencing token |
| `LeaseExpiresAtUtc` | BSON scalar date or long UTC ticks | must itself be range-safe; array `DateTimeOffset` is forbidden |
| `LastProcessedId` | Guid/string | deterministic `_id` checkpoint |
| `ScannedCount`, `MigratedCount`, `AlreadyCurrentCount` | long | monotonic checkpoint counters |
| `SourceFingerprint` | string | SHA-256 over immutable preflight facts/counts, not document payloads |
| `StartedAtUtcTicks`, `UpdatedAtUtcTicks`, `CompletedAtUtcTicks` | long | scalar operational evidence |
| `FailureCode` | string? | bounded non-secret classification; raw document content forbidden |

The migration performs field-level `$set` updates only. Whole-document `ReplaceOne` is forbidden.

### Index policy

- Existing legacy indexes remain during migration, cutover and rollback stabilization.
- The Access Governance schema manifest is the single index source of truth for production and tests.
- At least two plausible scalar index shapes must be measured against representative ready, future, stale, completed and max-attempt distributions.
- `explain` must report winning plan, multikey state, keys/documents examined, returned rows, blocking sort and semantic result order.
- No candidate is selected merely because it uses `IXSCAN`; semantic correctness comes first.
- A new migration-state collection/index requires schema-profile budget and owner approval. It is not hidden in test setup.

## 5. Repo Scope

This is the **provisional exhaustive runtime/test allow-list**. Phase 1.5 owner review must freeze it before code-start; implementation may use fewer files but may not add paths silently.

### Existing runtime files

- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Models/AuditOutboxMessage.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/AuditOutboxRepository.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Audit/AuditOutboxProcessingItem.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Audit/AuditOutboxProcessor.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Audit/AuditOutboxWorker.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Services/Audit/AuditOutboxWorkerOptions.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/DependencyInjection.cs`
- `services/Diten.Platform/src/Diten.Platform.API/Program.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/AuditCollectionNames.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/PlatformSchemaManifest.AccessGovernance.cs` (after the verified schema-profile base is present)
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Schema/SchemaProfileBudget.cs`

### Planned runtime files

- `services/Diten.Platform/src/Diten.Platform.API/Configuration/AuditOutboxTemporalStorageMigrationOptions.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Models/AuditOutboxTemporalMigrationState.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Repositories/AuditOutboxTemporalMigrationRepository.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Migrations/AuditOutboxTemporalStorageMigrationRunner.cs`
- `services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/Migrations/AuditOutboxTemporalStorageCompatibility.cs`

### Exact test files

- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/AuditOutboxClaimEligibilityTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/AuditOutboxWorkerTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/AuditPersistenceFoundationTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/DependencyInjectionSmokeTests.cs`
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Schema/PlatformSchemaManifestTests.cs` (after the verified schema-profile base is present)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/AuditOutboxTemporalStorageCompatibilityTests.cs` (new)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/AuditOutboxTemporalStorageMigrationMongoTests.cs` (new)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/AuditOutboxTemporalStorageCutoverMongoTests.cs` (new)
- `services/Diten.Platform/tests/Diten.Platform.Application.Tests/Audit/AuditDateTimeOffsetRegressionInventoryTests.cs` (new, read-only inventory guard)

### Governance evidence

- `execution/domains/platform-shared-services/module-packs/MOD-0021-FU02-audit-outbox-temporal-storage-hardening.md`
- `execution/registries/module-id-registry.md`
- `execution/registries/module-implementation-status.md` only after runtime status actually changes.

## 6. Protected Paths

- `.antigravity/**`
- `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Audit/AuditEvent.cs` and all `audit_events` query/retention runtime files.
- All non-audit Platform entities/repositories containing `DateTimeOffset`.
- Global BSON serializer/convention registration in `Diten.Platform.Infrastructure/DependencyInjection.cs`, except ordinary DI registration for this exact runner/repository; `BsonSerializer.RegisterSerializer<DateTimeOffset>` is explicitly forbidden.
- `services/Diten.AuthService/**`, `services/Diten.MdmService/**`, `services/Diten.EnterpriseStrategyService/**`, `services/Diten.DevEnablementService/**`.
- `gateway/**`, `frontend/**`, WorkCenter/workflow runtime paths.
- `appsettings*.json`, committed credentials, Production/Staging config and live data.
- Existing legacy temporal fields/index removal or in-place BSON type conversion.
- Direct Mongo mutation outside the exact real-Mongo fixtures and separately approved operational runner.
- Git branch/stage/commit/push operations.

## 7. Dependencies

- MOD-0021 existing outbox writer, processor, worker, unique idempotency index and Access Governance schema profile.
- Merged `PlatformSchemaManifest` / `SchemaProfile.AccessGovernance` infrastructure and its budget tests; no copied test-only index list.
- Access Governance transition budget is frozen at no more than five collections and twenty logical indexes,
  counting each collection's implicit `_id` index. This permits exactly one migration-state collection and one
  owner-selected scalar claim index while legacy indexes remain for rollback.
- MongoDB atomic update/CAS behavior and real `localhost:27017` evidence.
- Audit owner approval of scalar semantics and final index candidate.
- Persistence owner approval of migration-state storage and schema budget.
- Operations owner approval for each Development/Production migration and cutover invocation.
- MOD-0021-FU01 may implement ingestion dual-write, but its operational completion remains blocked until this temporal transition is safely cut over.

Lookup/reference-data decision: none. Temporal storage versions and migration phases are code-governed invariants, not editable lookup data.

## 8. Runtime Constraints

- No global/member serializer switch. Existing legacy fields retain their current representation.
- Every new outbox insert and every update of `NextAttemptAtUtc` writes both legacy value and exact scalar ticks in one atomic update.
- Compatibility reads prefer a valid v1 scalar pair. A row marked v1 with missing/malformed/mismatching shadows fails closed; it never falls back silently.
- A legacy row may be decoded only by the migration/compatibility layer with checked UTC conversion and exact round-trip evidence.
- Migration is explicit, default-disabled and never an automatic hosted startup mutation.
- Before any row mutation, a complete preflight scans and classifies every target row. Malformed values, unsupported BSON shapes, duplicate/unknown migration state or unsafe tick conversion block mutation.
- One migration writer per deployment scope is enforced by a durable lease with monotonic generation. Every batch update and checkpoint verifies the active generation.
- Migration and cutover require a transaction-capable Mongo replica set. Topology capability is verified before any migration-state or outbox-row mutation; standalone Mongo remains supported for normal Audit Outbox operation but rejects the migration runner fail-closed.
- The Audit Outbox worker is fenced while migration/cutover state is not safe. It must not claim against a partially migrated population.
- Rows are processed in deterministic `_id` order in bounded batches. Each update uses CAS predicates on `_id`, legacy source facts and absent/older version.
- Crash after row updates but before checkpoint is safe: replay re-reads and classifies already-current rows; counters remain monotonic and no data is overwritten.
- `Completed` is insufficient. `CompletionVerified` requires zero unmigrated/malformed rows, exact checkpoint/count reconciliation and required index read-back.
- Scalar claim activation requires `CompletionVerified`, the selected scalar index, a matching activation version and no active migration lease.
- Scalar claim uses exact ticks for eligibility and order, then `Id` as deterministic tie-breaker.
- Existing legacy fields and indexes remain dual-written/readable for at least one rollback-compatible stabilization release.
- Rollback may return to compatibility code without reverse migration. Removal requires a later, separately approved follow-up.
- No logs contain audit payload, actor PII, credential, JWT or raw malformed field values.

## 9. Layout & Shell Contract

- `shell: none`.
- No Razor layout, view, route, menu, DataTable, localization resource or navigation entry exists.
- `_LayoutPlatformAdmin`, `_LayoutTenantShell` and `_Layout.cshtml` are unchanged.

## 10. Backend File Convention

This is a persistence hardening follow-up, not CRUD/CQRS UI work; `golden_reference: none` is intentional.

- One public type per file.
- BSON/driver logic remains in Infrastructure.
- Options remain in API configuration and default disabled.
- Migration orchestration is an explicit runner, not a hosted service.
- Repository owns atomic claim/update operations; worker owns scheduling only.
- Schema/index definitions remain in `PlatformSchemaManifest.AccessGovernance.cs`.
- No generic `Create/Update/Delete` command, handler, validator or controller is added.

## 11. Frontend File Contract

Not applicable. No frontend, browser, Gateway, route, shell or localization surface is created.

## 12. Validation Rules

| Input/fact | Required | Exact rule | Failure |
|---|---|---|---|
| Environment/options | Yes for run | explicit enabled flag, exact migration ID/version, bounded positive batch and lease values | fail before Mongo mutation |
| Legacy BSON fields | Yes | supported exact array shape, valid ticks/offset, checked DateTimeOffset construction | malformed preflight failure |
| Scalar shadows | paired | both present or both absent; `Int64`; valid UTC ticks; version matches | fail closed |
| Scalar/legacy equality | Yes when both exist | identical UTC instant for each field | conflict; no overwrite |
| Migration state | Yes | singleton exact identity/version; known phase; monotonic counters/generation | conflict |
| Lease | Yes during mutation | unexpired owner + exact generation on every write | lease lost; stop |
| Checkpoint | Yes | deterministic `_id`; monotonic count and position | recovery required |
| Batch | Yes | bounded; CAS field-level `$set`; no `ReplaceOne` | reject/abort |
| Completion | Yes | zero malformed/unmigrated rows plus counts/fingerprint/index read-back | no activation |
| Index candidate | Before selection | real-Mongo semantic results and explain evidence | owner cannot select |
| Claim | After cutover only | scalar eligibility/order + `Id` tie-breaker under active fence | fail closed |

## 13. Failure Path to Verify

- **Malformed legacy BSON** → preflight fails before any row/version/checkpoint mutation.
- **Half-populated or mismatching scalar shadow** → conflict; no legacy fallback and no overwrite.
- **Second runner** → cannot acquire the active lease; no row mutation.
- **Lease expires or generation changes mid-batch** → current runner stops before further writes; state is recoverable.
- **Crash before first batch** → replay starts from unchanged durable preflight state.
- **Crash after rows but before checkpoint** → exact replay classifies already-current rows and advances safely.
- **Crash after checkpoint but before completion verification** → replay resumes after checkpoint and performs full verification.
- **Concurrent producer/retry update** → CAS loses safely; row is re-read/reclassified, never whole-document overwritten.
- **Unknown/newer temporal version** → fail closed; downgrade must not rewrite it.
- **Candidate index gives `IXSCAN` but wrong answer/order** → candidate rejected.
- **Migration complete but index/fence mismatch** → worker remains fenced; scalar cutover denied.
- **Equal `CreatedAtUtcTicksV1`** → stable ascending `Id` tie-breaker.
- **Rollback after scalar activation** → compatibility release reads/dual-writes both representations without reverse migration.
- **Legacy cleanup attempted in this pack** → rejected as out of scope.

## 14. Authorization Convention

- No human HTTP surface or new permission is introduced.
- Runtime implementation authority is repository/operator scoped, not a tenant-user role.
- Migration/cutover invocation requires explicit operational authorization and exact environment facts; configuration alone is not authorization.
- Tenant boundaries in outbox payloads and rows are preserved; migration never changes `TenantId`, idempotency key or payload.
- Runner identity is recorded as opaque operational evidence, not accepted from a browser/request body.

## 15. Gateway / API Routing Decision

Decision: Gateway/API changes are unnecessary and forbidden.

- No public or internal HTTP endpoint is added by this pack.
- Operational invocation uses a separately approved explicit local/CLI seam in `Program.cs` only if Phase 1.5 freezes that option.
- Browser and Ocelot do not participate.

## 16. Acceptance Criteria

- [ ] DCP-002 verifier passes for `MOD-0021-FU02` with parent `MOD-0021` and registry contains exactly one canonical row.
- [ ] No global or member `DateTimeOffsetSerializer`/convention switch is introduced.
- [ ] Existing legacy `NextAttemptAtUtc` and `CreatedAtUtc` BSON fields remain unchanged and retained.
- [ ] New writes and every retry/claim/release update atomically dual-write valid v1 scalar ticks/version.
- [ ] Compatibility reads detect missing, half-written, malformed and mismatching shadows fail closed.
- [ ] Migration is explicit, default-disabled, non-hosted, durable, leased, checkpointed, bounded, CAS-based and replay-safe.
- [ ] Complete malformed-data preflight occurs before the first row mutation.
- [ ] Worker claims are fenced during unsafe migration/cutover states.
- [ ] At least two scalar index candidates are measured on real Mongo; owner selection records semantic and explain evidence.
- [ ] `CompletionVerified` proves zero unmigrated/malformed rows and selected-index readiness.
- [ ] Scalar claim range/order returns exact instant semantics for mixed offsets and deterministic order for ties.
- [ ] Existing legacy indexes remain through the rollback-compatible stabilization release.
- [ ] Rollback requires no reverse data migration.
- [ ] `audit_events` and every other DateTimeOffset surface remain unchanged and appear in a regression inventory.
- [ ] No Product backlog `BL-030` row is overwritten or aliased to this work.
- [x] Platform Release build and required test suites pass with zero skipped scoped tests.

## 17. Test Expectations

### Unit/contract

- UTC tick conversion at min/max supported instants and mixed offsets; exact instant round-trip.
- Dual-write for insert, retry failure, cancellation release and claim update.
- Compatibility-read matrix: legacy-only, exact dual, half-shadow, malformed, mismatch, unknown version.
- Option validation and default-disabled/non-hosted DI proof.
- Worker fence and activation-version matrix.
- Deterministic tie-breaker rendering/contract.
- Static guard: no `DateTimeOffsetSerializer` registration and no whole-document replacement in the migration.

### Real Mongo

- Standalone negative/normal-runtime coverage uses a fixture-owned standalone Mongo process on a dynamic loopback port; positive migration/cutover coverage uses a separate real isolated single-node replica set because the lease fence and row CAS share a Mongo transaction.
- The migration test fixture auto-provisions and removes both owned processes and their scope-validated temporary directories. `DITEN_FU02_REPLICA_MONGO` remains an optional validated override only for the positive replica-set URI; a resolvable `mongod` binary is still required for the isolated standalone proof. No skip/fake/in-memory fallback is permitted.
- Unique disposable DB and only `SchemaProfile.AccessGovernance`; no full-schema helper, fake, in-memory or skip.
- Raw BSON proves legacy arrays are retained and v1 shadows are scalar `Int64`.
- Mixed `+14:00`, UTC and `-12:00` fixtures prove exact ready/future/stale classification.
- Preflight malformed fixture proves zero row/checkpoint mutation.
- Lease contention and generation fencing.
- Bounded CAS batches, concurrent writer loss, crash at every checkpoint and exact replay.
- Same instant/different offsets produce identical shadow ticks.
- Completion read-back reconciles counts, checkpoint and zero unmigrated rows.
- Candidate index A/B explains record winning plan, multikey, keys/docs examined, returned rows and blocking sort; one owner-approved candidate is then pinned in schema tests.
- Scalar `FindOneAndUpdate` claims oldest instant then ascending `Id` for ties.
- Legacy compatibility rollback reads and processes migrated rows without reverse migration.
- Existing idempotency, worker retry/dead-letter and tenant isolation regressions remain green.

### Inventory/regression

- Enumerate `audit_events`, BaseEntity timestamps, eventing/outbox, notification and other Platform DateTimeOffset persistence surfaces.
- The inventory test is read-only and fails only on unreviewed additions; it does not migrate or register serializers.
- Audit Event query/retention tests remain unchanged/green.
- Access Governance schema budget and production-union tests pass.
- Full Platform suite and Platform API Release build pass.
- `git diff --check`, conflict marker, trailing whitespace, final newline and secret-pattern scans pass.

## 18. Ready-for-dev Checklist

- [x] Parent MOD-0021 ownership and measured Real-Mongo defect identified.
- [x] DCP-002 preflight reported `OK MOD-0021-FU02` against parent MOD-0021.
- [x] Registry/module-pack collision rechecked before mutation: none.
- [x] Backend-only `shell: none`, `golden_reference: none`, `form_field_count: 0` selected.
- [x] Global serializer switch rejected; Audit-Outbox-only scalar shadow direction recorded.
- [x] Compatibility, migration, cutover and rollback phases separated.
- [x] Provisional exhaustive allow-list and protected paths recorded.
- [ ] Audit owner approves exact scalar fields/version and fail-closed compatibility semantics.
- [x] User approved the migration-state collection and Access Governance transition ceiling of five collections / twenty logical indexes on 2026-08-28.
- [x] Candidate A was selected only after the real-Mongo explain matrix; the Access Governance profile is exactly 5 collections / 20 logical indexes.
- [ ] Operations owner approves default-disabled explicit invocation and worker fence behavior.
- [ ] Backlog owner assigns/reconciles a collision-free debt identity; existing `BL-030` is not reused.
- [x] Exact allow-list was frozen and the pack promoted to `ready-for-dev` by explicit user Phase 1.5 approval on 2026-08-28.
- [x] User separately granted Section 5 exact runtime/test code-start on 2026-08-28; operational migration remains unauthorized.
- [x] User approved replica-set-only migration/cutover and standalone pre-mutation fail-closed behavior on 2026-08-28; this is architecture/test authority, not an operational-run approval.

## 19. Implementation Notes

- DCP-002 mechanical preflight was supplied as passed on 2026-08-28: `OK MOD-0021-FU02` against parent `MOD-0021`. Pack author independently found no FU02 collision before writing.
- Measured evidence: MongoDB.Driver 2.27.0 persisted both target values as `[ticks, offsetMinutes]`; ascending `CreatedAtUtc` ordered by array semantics, and `NextAttemptAtUtc <= cutoff` produced mixed-offset false negatives/false positives. The existing `{Status, NextAttemptAtUtc}` multikey `IXSCAN` accelerated the wrong predicate.
- Existing source defaults use `DateTimeOffset.UtcNow`, but the persistence contract accepts arbitrary offsets. Operational correctness cannot depend on every historical/future producer voluntarily using offset zero.
- An index-only change cannot repair array comparison semantics. A serializer-only switch would make old/new documents heterogeneous under the same field name and could break every Platform DateTimeOffset surface. Both shortcuts are rejected.
- `AuditOutboxMessage` is an existing Infrastructure persistence model and does not inherit `BaseEntity`; the required frontmatter `entity_base: BaseEntity` records the parent Platform tenant-aware convention and does not authorize replacing or re-parenting the existing model.
- The schema-profile infrastructure exists on the integration/main line but is absent from the current dirty feature HEAD. Runtime code-start must first verify the implementation base contains its merged successor; this planning task does not merge/rebase.
- The canonical `docs/product-backlog.md` currently assigns `BL-030` to Material Master Class/Grade and Generic-versus-Printed Packaging Identity. This pack records that collision and uses no backlog identity.
- `audit_events` and other DateTimeOffset-bearing documents are follow-up/regression inventory. Any measured defect there requires its own owner decision and migration pack.
- Runtime/test implementation completed in the isolated `feature/pss/mod-0021-fu02-audit-outbox-temporal-storage-hardening` worktree based on `origin/main` `61ffac26`; no commit, push or operational migration was performed.
- Candidate A `{ Status, NextAttemptAtUtcTicksV1, CreatedAtUtcTicksV1, Id }` was selected after an exact repository-query real-Mongo matrix including the status OR branches and `Attempts < max`. In the 5,000-row future-heavy fixture it examined 51 keys versus Candidate B's 5,000; both candidates remained semantically correct and non-multikey, and both required the OR-plan blocking sort.
- With `DITEN_FU02_REPLICA_MONGO` unset, the auto-provisioned replica-set migration tests passed `24/24`, focused FU02 tests passed `72/72`, and the standard full Platform suite passed `2710/2710`, with zero failures and zero skipped tests. The earlier broader Audit Outbox/worker/DI/schema regression run passed `105/105`.
- Platform API Release build passed with zero errors and thirteen pre-existing warnings. `git diff --check` passed.
- Real-Mongo evidence covers lossless legacy-array plus scalar-shadow dual-write, malformed preflight zero-mutation, lease/generation fencing, bounded CAS migration, four crash checkpoints, `RecoveryRequired` replay, completion verification, activation/index fencing, deterministic claim ordering, rollback compatibility and exact 5/20 schema budget.
- Independent review invalidated the first acceptance attempt because atomically fencing the durable lease generation and outbox row CAS requires Mongo transactions; `localhost:27017` is standalone and correctly rejected transactions. The user then approved a replica-set-only migration contract. The final standalone negative test proved rejection before migration-state/outbox mutation while normal Audit Outbox remained operational. Positive migration/crash/replay/cutover evidence ran with zero skips on an isolated single-node replica set; the standard environment-unset test command auto-provisioned it on a dynamic loopback port, then stopped the owned process and removed its scope-validated temporary directory.
- Final independent review found the prior topology, atomic fencing, recovery, exact-index and standalone blockers closed. Operational migration remained unexecuted and unauthorized.

## 20. Follow-up Items

- Backlog owner assigns a canonical collision-free debt ID and links it to this FU02; no agent invents one.
- Phase 1.5 freezes migration-state storage, schema-profile budget, exact runtime/test allow-list and the explicit invocation seam.
- The user approved the Phase 1.5 architecture on 2026-08-28 and authorized promotion to `ready-for-dev` while
  explicitly withholding runtime code-start at that time. A later explicit user approval on 2026-08-28 granted
  Section 5 runtime/test code-start only; it authorizes no environment/process or operational data migration.
- A subsequent narrow approval on 2026-08-28 added only `AuditCollectionNames.cs` and `SchemaProfileBudget.cs` to
  the exact allow-list and froze the Access Governance transition budget at five collections / twenty logical
  indexes. Operational migration remained explicitly unauthorized.
- Real-Mongo candidate-index benchmark selected Candidate A; any later index replacement requires new measured evidence and owner approval.
- Development migration/cutover/rollback rehearsal requires separate operational authorization after tests pass.
- Normal Audit Outbox runtime may use standalone Mongo; only the explicit migration/cutover runner requires a transaction-capable replica set.
- Production/Staging migration, monitoring, backup/restore evidence and rollback drill require separate change approval.
- Legacy field/index removal is a later stabilization follow-up after at least one compatible release and fleet-wide completion evidence.
- `audit_events`, BaseEntity, eventing/outbox, notification and other DateTimeOffset surfaces remain a measured inventory; they are not silently migrated here.
- MOD-0021-FU01 trusted ingestion must dual-write v1 shadows when this pack's compatibility release is present and cannot claim operational completion before the temporal cutover gate closes.

> Module pack `ready-for-dev` durumundadır. Runtime/test implementation bağımsız inceleme blocker'ları
> kapanmıştır; Local Development operational-run ayrıca onay gerektirir.
> Backend-only olduğu için Golden Reference `none`; UI/DataTable sapması yoktur.
