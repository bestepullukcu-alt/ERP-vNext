# 10_open_items_decision_log_output.md — Platform Decisions

**Status:** Closed for current MVP baseline

| Decision | Final choice | Rationale | Status |
|---|---|---|---|
| API Gateway mode (MOD-0032) | Deferred / not implemented in current MVP environment | No gateway runtime or provider-mode implementation selected | Closed |
| Event Bus mode (MOD-0035) | Native lightweight internal mode via MediatR | Repo uses in-process mediator pattern; sufficient for current MVP | Closed |
| Observability mode (MOD-0041/0042) | Native lightweight mode via `ILogger` + correlation context | Current repo supports basic structured logging/correlation without external stack | Closed |
| Integration Monitoring mode (MOD-0037) | Deferred / not implemented in current MVP environment | No separate integration monitoring subsystem selected | Closed |
| Vault mode (MOD-0012) | Local config / environment abstraction; no external vault in current MVP | Repo uses appsettings + environment variables; external vault deferred | Closed |
| Workflow modeling | MVP approvals-focused, no BPMN engine | Aligns with module spec and pragmatic MVP stance | Closed |
| Evidence enforcement | Policy-driven / config-driven | Prevent hardcoded evidence rules in business/domain flows | Closed |
| Event schema governance | Code + contract review baseline | Adequate for current MediatR/internal eventing model | Closed |
| Lint policy | None configured in repo | Do not invent a lint step not present in repo | Closed |
| Migration policy | No migration CLI; Mongo model evolution + seed/runbook approach | Matches repo persistence model | Closed |

## Notes
- “Deferred” is intentional and must not be reinterpreted as implicit native/external implementation.
- If the platform later adopts an external gateway, broker, vault, or observability stack, update this decision log before changing batch prompts or agent rules.
