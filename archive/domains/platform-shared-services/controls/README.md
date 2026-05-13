# controls/README.md — Platform & Shared Services Domain Controls Pack

**Purpose:** Provide the minimum domain control artifacts required to safely start Codex work on Domain 1 (Platform & Shared Services) in the current Diten repo.

## How to use
1. Treat these files as the authoritative domain controls pack for Platform & Shared Services.
2. Keep canonical repo-root `AGENTS.md` aligned with the values in this pack.
3. Use these files before running any Platform batches.
4. Run one batch at a time and only with targeted verification commands.

## Repo context confirmed
- Solution root: `DitenEnterpriseApp.sln`
- Backend umbrella root: `services`
- Frontend root: `frontend/Diten.Web`
- Backend runtime project: `services/Diten.Platform/src/Diten.Platform.API`
- Frontend runtime project: `frontend/Diten.Web`

## In-scope modules (Domain 1)
- MOD-0012 Secrets & Configuration Vault
- MOD-0018 RBAC / ABAC Authorization
- MOD-0021 Audit Trail Service
- MOD-0023 Workflow Designer
- MOD-0024 Task & Checklist Engine
- MOD-0028 Document Management
- MOD-0031 Evidence Linking Service
- MOD-0032 API Gateway
- MOD-0035 Event Bus / Message Queue
- MOD-0037 Integration Monitoring & Reconciliation
- MOD-0041 Logging & Monitoring
- MOD-0042 Alerting & Incident Runbooks

## Related control points (Domain 0 dependencies)
- MOD-0005 Policy & Control Library
- MOD-0006 Policy Exception / Waiver Register
- MOD-0007 Decision & Rationale Log

## MVP guardrails
- Single ownership (SoR) per object.
- No duplication of external provider runtimes when Mode A is selected.
- Contract envelope v1 + correlation_id propagation everywhere.
- Audit required for all create/update/approve actions.
- Evidence applicability is per-module.
- Keep Platform work out of protected business-domain paths.

## Current environment stance
- API Gateway: deferred / not implemented in current MVP environment
- Event Bus: native lightweight internal mode via MediatR
- Observability: native lightweight mode via `ILogger` + correlation context
- Integration Monitoring: deferred / not implemented in current MVP environment
- Vault: local configuration / environment variable abstraction, no external vault in current MVP
- Persistence: MongoDB + MongoDB C# Driver + seed/runbook evolution, no migration CLI
- Lint: none configured in repo

## Protected paths
- `services/Diten.Domain/Aggregates/DemandIdea`
- `services/Diten.Application/EnterpriseStrategy`
- `frontend/Diten.Web/Views/DemandIdeas`
- `frontend/Diten.Web/Views/DeliveryExecutionManagement`
- `frontend/Diten.Web/Views/EnterpriseStrategyBusinessPerformance`
