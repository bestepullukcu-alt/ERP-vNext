# 03_ownership_map_output.md — Platform Ownership Map

**Status:** Ready  
**Interpretation:** Target-state ownership map for Platform & Shared Services.  
This is not a statement that every object is already implemented in the current repo.

| Object | Owner module | Repo area | Notes |
|---|---|---|---|
| Role | MOD-0018 RBAC / ABAC Authorization | target-state under `services` Platform areas | Not implemented yet in repo |
| Permission | MOD-0018 RBAC / ABAC Authorization | target-state under `services` Platform areas | Existing security hooks exist, but not full SoR |
| Assignment | MOD-0018 RBAC / ABAC Authorization | target-state under `services` Platform areas | Not implemented yet in repo |
| ABAC Policy | MOD-0018 RBAC / ABAC Authorization | target-state under `services` Platform areas | Not implemented yet in repo |
| AuditEvent | MOD-0021 Audit Trail Service | current partial seam in `services/Diten.Persistence` | Current enterprise strategy audit storage exists; target-state owner remains MOD-0021 |
| WorkflowDefinition | MOD-0023 Workflow Designer | target-state under `services` Platform areas | Not implemented yet in repo |
| WorkflowInstance | MOD-0023 Workflow Designer | target-state under `services` Platform areas | Not implemented yet in repo |
| ApprovalTask | MOD-0023 Workflow Designer | target-state under `services` Platform areas | Not implemented yet in repo |
| Task | MOD-0024 Task & Checklist Engine | existing related seam in `services/Diten.Application` / persistence | Current task-related repository exists; target-state owner is MOD-0024 |
| ChecklistTemplate | MOD-0024 Task & Checklist Engine | target-state under `services` Platform areas | Not implemented yet in repo |
| ChecklistRun | MOD-0024 Task & Checklist Engine | target-state under `services` Platform areas | Not implemented yet in repo |
| Document | MOD-0028 Document Management | current partial support in Web/API upload area | Full document SoR not yet implemented |
| DocumentVersion | MOD-0028 Document Management | target-state under `services` Platform areas | Not implemented yet in repo |
| Template | MOD-0028 Document Management | partial existing template concepts tied to strategy domain | Target-state owner is MOD-0028; avoid business-domain coupling |
| EvidenceLink | MOD-0031 Evidence Linking Service | target-state under `services` Platform areas | Not implemented yet in repo |
| Secret | MOD-0012 Secrets & Configuration Vault | current config/env seam via appsettings + environment variables | Current MVP uses config/env, not external vault |
| ApiService | MOD-0032 API Gateway | deferred | Module deferred in current MVP |
| ApiRoute | MOD-0032 API Gateway | deferred | Module deferred in current MVP |
| Credential | MOD-0032 API Gateway / MOD-0012 Vault seam | deferred / config-env seam | No gateway runtime in current MVP |
| Topic | MOD-0035 Event Bus / Message Queue | app-internal via MediatR | Native lightweight internal mode |
| Subscription | MOD-0035 Event Bus / Message Queue | app-internal via MediatR handlers | Native lightweight internal mode |
| LogSignal | MOD-0041 Logging & Monitoring | `ILogger` + correlation middleware/context | Native lightweight observability mode |
| AlertRule | MOD-0042 Alerting & Incident Runbooks | deferred | No alerting subsystem in current MVP |

## Notes
- Use this map for Platform module ownership decisions and batch prompts.
- Do not reinterpret current business-domain implementations as Platform SoR unless explicitly migrated.
- Protected business-domain paths remain out of scope for Platform implementation.
