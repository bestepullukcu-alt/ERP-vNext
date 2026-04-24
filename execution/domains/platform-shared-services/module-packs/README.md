# module-packs/README.md — Platform Module Pack Index

| Module ID | Module name | Wave | Current MVP status |
|---|---|---|---|
| MOD-0012-secrets-configuration-vault | Secrets & Configuration Vault | W1 | native local configuration/environment abstraction; no external vault provider. |
| MOD-0018-rbac-abac-authorization | RBAC / ABAC Authorization | W1 | implement RBAC-first with minimal ABAC conditions and resource tags. |
| MOD-0021-audit-trail-service | Audit Trail Service | W1 | Current repo has a partial enterprise-strategy audit seam; target-state ownership stays with MOD-0021-audit-trail-service. |
| MOD-0023-workflow-designer | Workflow Designer (Approvals / SLAs / Escalations) | W1 | Current MVP posture: approvals-focused workflow only; no BPMN engine. |
| MOD-0024-task-checklist-engine | Task & Checklist Engine | W1/W2 | Current repo has partial task-related seams; target-state ownership remains MOD-0024-task-checklist-engine. |
| MOD-0028-document-management | Document Management (Templates / Versioning) | W1 | Current repo has partial upload/template support; full document SoR is target-state for MOD-0028-document-management. |
| MOD-0031-evidence-linking-service | Evidence Linking Service (object ↔ evidence) | W1 | service + embeddable UI component with policy/template-driven completeness logic. |
| MOD-0032-api-gateway | API Gateway | W1 | deferred / not implemented. Only documentation or future-ready placeholder work is allowed unless scope changes. |
| MOD-0035-event-bus-message-queue | Event Bus / Message Queue | W2 (W1 if event-first) | native lightweight internal event dispatch via MediatR; no broker, DLQ, or replay subsystem. |
| MOD-0037-integration-monitoring | Integration Monitoring & Reconciliation | W2/W3 | deferred / not implemented. |
| MOD-0041-logging-monitoring | Logging & Monitoring | W2/W3 | native lightweight telemetry seam using `ILogger`, correlation middleware/context, and basic health checks. |
| MOD-0042-alerting-incident-runbooks | Alerting & Incident Runbooks | W2/W3 | deferred as a dedicated operational surface; basic observability hooks only. |

## Usage
- Open the domain package first.
- Open the target module pack next.
- Open only the control files relevant to the active batch.
- Use the module pack as the most specific execution truth for coding.
