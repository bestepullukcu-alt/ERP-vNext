# runtime-decisions.md — Platform & Shared Services

## Fixed current-MVP runtime decisions

| Decision area | Final choice | Implication |
|---|---|---|
| API Gateway (MOD-0032) | Ocelot runtime in production at `gateway/Diten.ApiGateway` (port 5000); MOD-0032 hardening backlog (rate limiting, quota, policy engine, consumer model) is deferred | All frontend traffic must traverse the Gateway (AGENTS.md §3). Do not bypass to service ports. Hardening additions are MOD-0032/MOD-0033 scope. |
| Event Bus (MOD-0035) | Native lightweight internal mode via MediatR | Internal event dispatch is in-process only; do not model broker/DLQ behavior into current MVP. |
| Observability (MOD-0041 / MOD-0042) | Native lightweight mode via `ILogger` + correlation context | Keep logging/health hooks lightweight; external observability stacks are future-state. |
| Integration Monitoring (MOD-0037) | Deferred / not implemented in current MVP | No failed-message replay console or reconciliation workbench in current MVP. |
| Vault (MOD-0012) | Local config / environment abstraction | Do not assume external vault providers; secrets stay behind appsettings/env seams and masked UI behavior. |
| Persistence | MongoDB + MongoDB C# Driver + repository pattern | No EF-style migration assumptions; use seed/runbook evolution. |
| Workflow modeling | Approvals-focused MVP | No BPMN engine or heavyweight workflow notation in current MVP. |
| Evidence enforcement | Policy-driven / config-driven | Do not hardcode evidence rules in business pages or module logic. |
| Event schema governance | Code + contract review baseline | Version API/event contracts; keep envelope compatibility explicit. |
| Lint policy | None configured | Do not invent a lint command in the repo-level workflow. |

## Domain-wide contract rules
- All APIs/events use Contract Envelope v1.
- All create/update/approve actions in Platform modules emit audit.
- Correlation IDs must flow from request → service → audit → internal event handlers.
- Errors must use the existing standard envelope `{ code, message, details[], correlation_id }`.
- Commands should be idempotent where feasible.
