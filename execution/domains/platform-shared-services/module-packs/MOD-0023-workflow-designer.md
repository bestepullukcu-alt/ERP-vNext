# MOD-0023-workflow-designer — Workflow Designer (Approvals / SLAs / Escalations)

## 1. Module Summary
- **Module ID:** MOD-0023-workflow-designer
- **Module Name:** Workflow Designer (Approvals / SLAs / Escalations)
- **Domain:** Platform & Shared Services
- **Subdomain:** Workflow, Rules & Automation
- **Planned Wave:** W1
- **UI:** YES (Core)
- **Purpose:** Provide the authoritative approvals-focused workflow capability for versioned definitions, runtime instances, approval tasks, and SLA/escalation rules.

## 2. Ownership and Boundaries
### Owned objects (SoR)
- WorkflowDefinition
- WorkflowInstance
- ApprovalTask
- SLA/EscalationRule

### In-scope
- workflow definition CRUD/publish
- workflow instance start
- approval-task inbox/query
- run history/timeline
- SLA/escalation metadata
- evidence gating when configured

### Out-of-scope
- operational task ownership
- full BPMN orchestration
- heavy simulation/modeling
- domain-specific business semantics

### Current MVP execution status
- Current MVP posture: approvals-focused workflow only; no BPMN engine.

## 3. Dependencies and Interfaces
### Consumed dependencies
- MOD-0018-rbac-abac-authorization RBAC / ABAC Authorization
- MOD-0021-audit-trail-service Audit Trail Service
- MOD-0024-task-checklist-engine Task & Checklist Engine (integration only)
- MOD-0028-document-management Document Management (optional)
- MOD-0031-evidence-linking-service Evidence Linking Service (optional/config-driven)

### Primary consumers
- ERP approval flows
- ES&BP governance flows
- platform admins
- approvers

### Interface stubs
- API `workflow.definitions.*` — versioned definition CRUD/publish
- API `workflow.instances.start` — start workflow for object
- API `workflow.tasks.query` — inbox queries
- Event `workflow.task.created|completed` — lifecycle events

## 4. Repo Scope
**Inherited convention:** use the `PlatformSharedServices` folder convention defined in `domain-config.md`.

### Recommended backend scope
- `services/Diten.Platform/src/Diten.Platform.Domain/Aggregates/MOD-0023-workflow-designer/`
- `services/Diten.Platform/src/Diten.Platform.Application/Commands/MOD-0023-workflow-designer/`
- `services/Diten.Platform/src/Diten.Platform.Application/Queries/MOD-0023-workflow-designer/`
- `services/Diten.Platform/src/Diten.Platform.Application/Handlers/MOD-0023-workflow-designer/`
- `services/Diten.Platform/src/Diten.Platform.Application/Services/MOD-0023-workflow-designer/`
- `services/Diten.Platform/src/Diten.Platform.Persistence/Repositories/MOD-0023-workflow-designer/`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/MOD-0023-workflow-designer/`
- src/Backend/Diten.Application/Handlers for lifecycle/event handling
- possible timer/escalation services under application/services or infrastructure

### Recommended frontend scope
- `frontend/Diten.Web/Controllers/PlatformSharedServices/MOD-0023-workflow-designerController.cs`
- `frontend/Diten.Web/Views/PlatformSharedServices/MOD-0023-workflow-designer/`
- `frontend/Diten.Web/wwwroot/js/platform-shared-services/mod-0023.js`

### Protected paths
- Inherit all protected paths from `domain-config.md`.
- Do not implement this module inside ES&BP, Demand, or Delivery feature trees.

## 5. UI Surfaces
- Workflow Designer — workspace for steps, routing, SLA/escalation
- Approvals Inbox — list/inbox with bulk actions
- Workflow Run History — report/insights timeline

## 6. Runtime Constraints
- instances are version-pinned
- approval actions must be RBAC-gated and audited
- workflow module must not own operational tasks
- evidence rules must be config-driven

## 7. Acceptance Criteria
- Definitions are versioned and published explicitly.
- Running instances keep their pinned version.
- Approve/reject/delegate actions are permissioned and audited.

## 8. Testing Notes
- Run targeted backend build: `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.WebAPI.csproj`
- Run targeted frontend build: `dotnet build frontend/Diten.Web/Diten.WebUI.csproj`
- definition versioning tests
- instance-start tests
- approval transition tests
- UI inbox/designer build/tests

## 9. Implementation Notes
- Escalations can stay timer/lightweight in MVP.
- Keep the workflow object model reusable across domains.

## 10. Follow-up Items
- Advanced modeling and orchestration remain later-wave backlog.
