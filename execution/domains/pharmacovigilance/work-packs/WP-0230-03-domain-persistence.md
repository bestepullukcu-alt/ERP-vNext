---
id: WP-0230-03
title: Domain entity and Mongo persistence
module: MOD-0230
service: Diten.PvgService
depends_on: [WP-0230-02]
gate: build/test only
status: ready
estimate: 1 d
---

# WP-0230-03 - Domain entity and Mongo persistence

## Objective

Model the Safety Case intake record as a tenant-owned entity with the 16 user-entered fields from the MOD-0230
pack, and back it with a Mongo repository whose every query is tenant-filtered by construction.

## Preconditions

- [ ] WP-02 complete: `EntityBase`, `RepositoryBase<T>`, `MongoDbContext`, `ITenantContext` exist and compile.

## File manifest

```text
services/Diten.PvgService/src/Diten.Pvg.Domain/
├── Entities/CaseIntakeTriage.cs
└── Repositories/ICaseIntakeTriageRepository.cs

services/Diten.PvgService/src/Diten.Pvg.Persistence/
└── Repositories/CaseIntakeTriageRepository.cs

services/Diten.PvgService/tests/Diten.Pvg.Application.Tests/Persistence/
└── CaseIntakeTriageRepositoryTests.cs
```

## Implementation spec

### Entity - `CaseIntakeTriage : EntityBase`

16 user-entered fields, exactly as tabled in the MOD-0230 pack. Do **not** redeclare `Id`, `TenantId`,
`CreatedAt`, `UpdatedAt`, `IsDeleted`, `DeletedAt`, or `Version` - they come from `EntityBase`.

| Property | Type | Required | Sensitivity |
|---|---|---|---|
| `IntakeChannel` | `string` | Yes | public-metadata |
| `SourceType` | `string` | Yes | public-metadata |
| `SourceReference` | `string?` | No | confidential |
| `ReceivedAtUtc` | `DateTimeOffset` | Yes | regulated-safety |
| `ReporterType` | `string` | Yes | public-metadata |
| `ReporterContactSummary` | `string?` | No | **PII** |
| `PatientSubjectCode` | `string?` | No | **PHI** |
| `EventOnsetDate` | `DateOnly?` | No | **PHI** |
| `AdverseEventNarrative` | `string` | Yes | **PHI** |
| `SuspectProductText` | `string?` | No | regulated-safety |
| `Seriousness` | `string` | Yes | regulated-safety |
| `IntakePriority` | `string` | Yes | regulated-safety |
| `TriageOutcome` | `string?` | Yes at triage | regulated-safety |
| `TriageReason` | `string?` | conditional | **PHI** |
| `RouteTargetQueue` | `string?` | Yes at route | confidential |
| `EvidenceLinkReferences` | `IReadOnlyList<string>` | No, max 20 | confidential |

Plus system-generated fields excluded from `form_field_count`:

| Property | Type | Note |
|---|---|---|
| `CaseNumber` | `string` | Server-generated, unique per tenant. Never client-supplied |
| `LifecycleState` | `string` | `Received` on create. Only `IPvgWorkflowTransitionGate` may move it |
| `CorrelationId` | `string` | Captured at creation from `ICorrelationContext` |

**`ToString()` must be overridden** to return `$"CaseIntakeTriage[{Id}]"` and nothing else. The default record/
object formatting of an entity holding `AdverseEventNarrative` is a PHI leak the moment anything logs it.

### Repository

```csharp
public interface ICaseIntakeTriageRepository
{
    Task<IReadOnlyList<CaseIntakeTriage>> GetListAsync(CaseIntakeTriageFilter filter, CancellationToken ct = default);
    Task<CaseIntakeTriage?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CaseIntakeTriage> CreateAsync(CaseIntakeTriage entity, CancellationToken ct = default);
    Task<bool> UpdateAsync(CaseIntakeTriage entity, CancellationToken ct = default);
    Task<bool> ExistsBySourceAsync(string sourceType, string sourceReference, Guid? excludeId = null, CancellationToken ct = default);
    Task<string> NextCaseNumberAsync(CancellationToken ct = default);
}
```

**No `DeleteAsync`. No `BulkDeleteAsync`.** The Golden Reference template ships both - do not copy them. Their
absence from the interface is the enforcement mechanism.

Implementation mirrors `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LegalEntityRepository.cs`:
`RepositoryBase<CaseIntakeTriage>` with collection `pvg_case_intake_triage`, every filter composed with the
inherited `TenantFilter`, and `IsDeleted == false` on all normal reads.

### Indexes - `EnsureIndexes()`

| Name | Keys | Unique |
|---|---|---|
| `ux_pvg_case_intake_triage_tenant_casenumber` | `TenantId`, `CaseNumber` | Yes |
| `ix_pvg_case_intake_triage_tenant_source` | `TenantId`, `SourceType`, `SourceReference` | No |
| `ix_pvg_case_intake_triage_tenant_received` | `TenantId`, `ReceivedAtUtc` desc | No |
| `ix_pvg_case_intake_triage_tenant_state` | `TenantId`, `LifecycleState`, `TriageOutcome` | No |
| `ix_pvg_case_intake_triage_tenant_priority` | `TenantId`, `IntakePriority`, `Seriousness` | No |

**No text index.** A text index over `AdverseEventNarrative` would make PHI searchable and exportable through
a surface nobody has approved.

### `CaseNumber` generation

Server-side, monotonic per tenant, format `PV-{yyyy}-{seq:D6}`. Uniqueness is enforced by the unique index;
on a duplicate-key exception, retry once, then fail with 409 and a safe reason code. Never derive it from
`SourceReference` or any client input.

## Forbidden

- Any delete or bulk-delete method, at any layer.
- A query that can execute without `TenantFilter`.
- A text index, or any index over `AdverseEventNarrative`, `TriageReason`, `PatientSubjectCode`, or `ReporterContactSummary`.
- Accepting `TenantId`, `Id`, `CaseNumber`, `LifecycleState`, or `CorrelationId` from any caller-supplied object.
- Mongo migrations, seed data, or collection bootstrapping outside `EnsureIndexes()`.
- Redeclaring any `EntityBase` field.

## Acceptance criteria

- [ ] Entity has exactly the 16 user-entered fields plus the 3 system fields, none duplicated from `EntityBase`.
- [ ] `ToString()` overridden to expose only the Id.
- [ ] Repository interface contains no delete of any kind.
- [ ] Every repository method composes `TenantFilter`.
- [ ] All five indexes created; no text index exists.
- [ ] `CaseNumber` is server-generated and unique per tenant.
- [ ] Project builds.

## Tests

| Test | Asserts |
|---|---|
| Cross-tenant `GetByIdAsync` returns `null` | Tenant isolation, no leak |
| Cross-tenant `UpdateAsync` returns `false` and mutates nothing | Tenant isolation on write |
| `GetListAsync` never returns another tenant's rows | Filter composition |
| Soft-deleted rows absent from list and detail | `IsDeleted` honoured |
| `ExistsBySourceAsync` is tenant-scoped | Duplicate detection cannot cross tenants |
| Concurrent `NextCaseNumberAsync` produces no duplicate | Unique index + retry |
| Reflection: repository type exposes no member matching `/[Dd]elete/` | Delete is structurally impossible |
| Reflection: entity `ToString()` output contains no field value | PHI cannot leak via interpolation |

## Verify

```bash
dotnet build services/Diten.PvgService/src/Diten.Pvg.Persistence/Diten.Pvg.Persistence.csproj -v q
dotnet test  services/Diten.PvgService/tests/Diten.Pvg.Application.Tests/Diten.Pvg.Application.Tests.csproj --filter "FullyQualifiedName~Persistence"
grep -rn "Delete" services/Diten.PvgService/src/Diten.Pvg.Domain services/Diten.PvgService/src/Diten.Pvg.Persistence   # expect only IsDeleted/DeletedAt
```

## Agent prompt

> Implement WP-0230-03 in `/Users/natig/Projects/ERP-vNext-recovery`.
>
> Read first: `execution/domains/pharmacovigilance/work-packs/WP-0230-03-domain-persistence.md`, the
> **Entity Fields** and **Validation Rules** sections of
> `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md`,
> `.antigravity/rules/{entity-base-template,repository-standard,mongo-indexing,multi-tenancy}.md`.
>
> Copy the pattern from `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/LegalEntityRepository.cs`.
>
> Hard constraints: no delete or bulk-delete anywhere; every query composes `TenantFilter`; no text index and no
> index over any PHI/PII field; `TenantId`, `Id`, `CaseNumber`, `LifecycleState`, and `CorrelationId` are
> server-set only; override `ToString()` to expose the Id alone.
>
> Do not create handlers, commands, controllers, or views - those are WP-04. Report build and test output.
