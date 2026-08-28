# MOD-0230 Development Work Packs - Slice 1

**Module:** MOD-0230 Case Intake & Triage
**Service:** `Diten.PvgService` (port 5011)
**Gate:** build / test only. Operational runtime remains **closed**.
**Authority:** [MOD-0230 pack](../module-packs/MOD-0230-case-intake-triage.md) > [domain-config](../domain-config.md) > `AGENTS.md` > `.antigravity/rules/`
**Plan:** [`docs/plans/pvg-fast-track-execution-plan-2026-08-09.md`](../../../../docs/plans/pvg-fast-track-execution-plan-2026-08-09.md)

---

## What a work pack is

One self-contained, agent-executable unit of implementation. Each pack states its preconditions, the exact
files it may create or edit, the implementation spec, what is forbidden, its acceptance criteria, its tests,
and a ready-to-paste agent prompt. A pack is done when its acceptance criteria are checked and its tests pass.

A pack **never** widens its own scope. If a pack cannot be completed without touching a file outside its
manifest, it stops and reports rather than editing that file.

---

## Sequence

```text
WP-01  REG-PV-BASE ports          ── no dependencies, pure C#, start here
   │
WP-02  Service scaffold           ── Diten.PvgService projects, DI, host wiring
   │
WP-03  Domain + persistence       ── EntityBase, entity, repository, indexes
   │
WP-04  CQRS + API surface         ── commands, queries, handlers, validators, controller
   │
   ├── WP-05  Failure-path tests  ── the 12 paths + leak scans (runs alongside WP-04)
   │
WP-06  Gateway route              ── ocelot route family (NET-001)
   │
WP-07  Tenant UI                  ── Golden Reference Compact view set
   │
WP-08  Module manifest            ── ⛔ BLOCKED, needs a governance decision first
```

| Pack | Title | Depends on | Est. | Status |
|---|---|---|---|---|
| [WP-0230-01](WP-0230-01-regpvbase-ports.md) | REG-PV-BASE consumption ports | - | 1.5 d | Ready |
| [WP-0230-02](WP-0230-02-service-scaffold.md) | `Diten.PvgService` scaffold | WP-01 | 1 d | Ready |
| [WP-0230-03](WP-0230-03-domain-persistence.md) | Domain entity + Mongo persistence | WP-02 | 1 d | Ready |
| [WP-0230-04](WP-0230-04-cqrs-api.md) | CQRS handlers + API controller | WP-01, WP-03 | 2 d | Ready |
| [WP-0230-05](WP-0230-05-failure-path-tests.md) | Failure-path + leak-scan suite | WP-04 | 1.5 d | Ready |
| [WP-0230-06](WP-0230-06-gateway-route.md) | Gateway route family | WP-04 | 0.5 d | Ready |
| [WP-0230-07](WP-0230-07-tenant-ui.md) | Tenant UI (Compact) | WP-06 | 2 d | Ready |
| [WP-0230-08](WP-0230-08-module-manifest.md) | Module manifest / catalog | WP-07 | 0.5 d | **⛔ BLOCKED** |

---

## Reference implementations to copy from

Do not invent patterns. Every pack points at a file already in this repo.

| Concern | Copy from |
|---|---|
| Golden Reference **Compact** backend | `services/Diten.DevEnablementService/src/.../Features/GoldenReferenceCompact/**` |
| Golden Reference **Compact** frontend | `frontend/Diten.Web/Views/DevEnablement/GoldenReferenceCompact/**` + `Controllers/GoldenReferenceCompactController.cs` |
| Tenant-owned entity + repository + indexes | `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/EntityBase.cs`, `.../Persistence/Repositories/LegalEntityRepository.cs` |
| Response envelope | `.../Application/Common/Models/Response.cs` (namespace `Diten.Shared.Core`) |
| Controller base + result mapping | `.../Api/Controllers/CustomBaseController.cs` |
| Permission attribute | `.../Infrastructure/Authorization/HasPermissionAttribute.cs` |
| Authorization, tenancy, correlation, redaction | `services/Diten.Platform.Common/src/Diten.Platform.Common/{Authorization,Tenancy,Observability}` |
| Audit | `services/Diten.Platform/src/Diten.Platform.Domain/Entities/Audit/AuditEvent.cs` and the audit feature |

---

## Rules that bind every pack

1. **No delete.** No `Delete*Command`, no `BulkDelete*Command`, no `DELETE` endpoint, no bulk-delete route - ever. The Golden Reference Compact template ships all four; **do not copy them.**
2. **No archive, no void, no export** in slice 1.
3. **No client `TenantId`.** It is resolved server-side from `ITenantContext`. Any DTO, command, or form payload carrying `TenantId` is a defect.
4. **Cross-tenant reads return 404 or empty.** Never 403 with a body that confirms existence.
5. **Fail closed.** If RBAC, masking, audit, workflow, evidence, trace, or telemetry cannot be evaluated, deny. Never invent a permissive fallback.
6. **No raw PHI/PII** in logs, traces, metrics, audit payloads, validation errors, or error responses. This covers `AdverseEventNarrative`, `TriageReason`, `PatientSubjectCode`, `EventOnsetDate`, `ReporterContactSummary`.
7. **No MedDRA data** anywhere - source, fixtures, seed, tests, comments.
8. **No seed data, no background jobs, no menu entry, no module-catalog registration** in slice 1.
9. **`.antigravity/**` is protected**, including `rules/ports.md`. Port 5011 registration needs separate approval.
10. **Permission keys** follow PKS-001: lowercase, dot-separated, `^[a-z][a-z0-9-]*$` per segment, ≥ 3 segments.

---

## Local verification

The cloud session cannot restore NuGet packages, so **builds and tests run on your machine.**

```bash
# per-pack build
dotnet build services/Diten.PvgService/src/Diten.Pvg.API/Diten.Pvg.API.csproj -v q

# per-pack tests
dotnet test services/Diten.PvgService/tests/Diten.Pvg.Application.Tests/Diten.Pvg.Application.Tests.csproj

# full stack once WP-06 lands (add the PvgService build line to run_all.sh first)
./run_all.sh

# governance gate - must stay green after every pack
python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0230 --name "Case Intake & Triage"
python3 .antigravity/scripts/verify_module_id.py . --check-all
```

---

## Before WP-02: one manual step

`services/Diten.PvgService/` currently exists as ignored `bin` / `obj` build output with **no tracked source**.
Delete it before scaffolding, or the new projects will collide with stale artefacts:

```bash
rm -rf services/Diten.PvgService
```
