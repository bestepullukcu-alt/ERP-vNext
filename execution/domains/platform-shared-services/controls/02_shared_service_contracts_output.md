# 02_shared_service_contracts_output.md — Platform Shared Service Contracts

**Status:** Ready as current-environment baseline

## Mandatory rule
Platform modules must not reimplement shared services or external providers. They must consume approved seams and stay within the current environment decisions.

## Current environment decisions
- API Gateway: deferred / not implemented in current MVP
- Event Bus: native lightweight internal mode via MediatR
- Observability: native lightweight mode via `ILogger` + correlation context
- Integration Monitoring: deferred / not implemented in current MVP
- Vault: appsettings / environment variables, no external vault in current MVP

## Contract register

| Service | Owner module/team | Actual interface / package / endpoint | Integration type | Auth model | Timeout / retry / failure rule | Consumer modules | Status |
|---|---|---|---|---|---|---|---|
| AuthN (IdP) | Existing app security layer | `services/Diten.Platform/src/Diten.Platform.API/Security` | app-local security hooks | app/user context | Not fully documented in repo; treat as existing seam | MOD-0018/0023/0028 and related UIs | Partial |
| RBAC/ABAC | MOD-0018 target-state | Not found in repo; to be introduced under Platform paths | internal service/API | user + service | Must be idempotent where feasible; deny by default | all | Target-state |
| Audit | Current enterprise strategy audit store | `MongoEnterpriseStrategyAuditStore`, `IEnterpriseStrategyAuditSink`, `IEnterpriseStrategyAuditStore` | internal service/store | service identity | append-only; no update/delete | all | Partial/current |
| Workflow | MOD-0023 target-state | Not found in repo; to be introduced under Platform paths | internal API/event | user + service | version-pinned instances; audit transitions | ES&BP/ERP/DWSE | Target-state |
| Tasks | Mixed current use | `ITaskRepository` in persistence/application seam | internal repository/service | user + service | auditable create/assign/complete | ES&BP/ERP/DWSE | Partial/current |
| Docs | Partial current upload support | current upload support via Web/API area; full Platform docs module not yet present | internal API | user + service | RBAC-gated; immutable versions | all | Partial/current |
| Evidence | MOD-0031 target-state | Not found in repo; to be introduced | internal API | user + service | audit link/unlink; query by object | all | Target-state |
| Vault | MOD-0012 current-environment seam | `appsettings.json`, `appsettings.Development.json`, environment variables | config/env seam | service identity | never expose plaintext to UI; mask in logs | integrations | Current |
| API Gateway | MOD-0032 | Deferred / not implemented in current MVP | N/A | N/A | N/A | integrations | Deferred |
| Event Bus | MOD-0035 | MediatR registered in application layer | in-memory internal mediator | service/app context | in-process delivery only; no DLQ in current MVP | integrations / internal handlers | Current |
| Observability | MOD-0041/0042 current-environment seam | `ILogger`, correlation middleware/context | built-in logging + middleware | service/app context | console/basic logging only; no external alert routing | all | Current |

## Hard rules
- No module may call undocumented/private endpoints.
- Keep shared-service seams behind explicit interfaces where possible.
- Treat Gateway and Integration Monitoring as deferred, not implicit.
- Treat Vault as config/env abstraction in current MVP; do not imply external vault capability.
