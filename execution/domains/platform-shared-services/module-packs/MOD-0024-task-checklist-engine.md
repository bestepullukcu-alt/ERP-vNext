# MOD-0024-task-checklist-engine — Task & Checklist Engine

## 1. Module Summary
- **Module ID:** MOD-0024-task-checklist-engine
- **Module Name:** Task & Checklist Engine
- **Domain:** Platform & Shared Services
- **Subdomain:** Workflow, Rules & Automation
- **Planned Wave:** W1/W2
- **UI:** YES (Core)
- **Purpose:** Provide the authoritative generic task and checklist primitives used by platform and business modules for operational work that is distinct from approval semantics.

## 2. Ownership and Boundaries
### Owned objects (SoR)
- Task
- ChecklistTemplate
- ChecklistRun
- TaskAssignment

### In-scope
- task create/assign/complete surfaces
- checklist template catalog
- checklist run workspace
- due dates and escalation hooks
- optional evidence on completion when configured

### Out-of-scope
- approval semantics
- second workflow engine
- domain-specific task vocabularies
- advanced optimization/scheduling

### Current MVP execution status
- Current repo has partial task-related seams; target-state ownership remains MOD-0024-task-checklist-engine.

## 3. Dependencies and Interfaces
### Consumed dependencies
- MOD-0018-rbac-abac-authorization RBAC / ABAC Authorization
- MOD-0021-audit-trail-service Audit Trail Service
- MOD-0023-workflow-designer Workflow Designer (optional integration)
- MOD-0031-evidence-linking-service Evidence Linking Service (optional/config-driven)

### Primary consumers
- ERP operational flows
- ES&BP review cycles
- platform operators

### Interface stubs
- API `tasks.*` — create/assign/complete tasks
- API `checklists.*` — template CRUD and run execution
- Event `task.created|completed` — lifecycle events

## 4. Repo Scope
**Inherited convention:** use the `PlatformSharedServices` folder convention defined in `domain-config.md`.

### Recommended backend scope
- `services/Diten.Platform/src/Diten.Platform.Domain/Aggregates/MOD-0024-task-checklist-engine/`
- `services/Diten.Platform/src/Diten.Platform.Application/Commands/MOD-0024-task-checklist-engine/`
- `services/Diten.Platform/src/Diten.Platform.Application/Queries/MOD-0024-task-checklist-engine/`
- `services/Diten.Platform/src/Diten.Platform.Application/Handlers/MOD-0024-task-checklist-engine/`
- `services/Diten.Platform/src/Diten.Platform.Application/Services/MOD-0024-task-checklist-engine/`
- `services/Diten.Platform/src/Diten.Platform.Persistence/Repositories/MOD-0024-task-checklist-engine/`
- `services/Diten.Platform/src/Diten.Platform.API/Controllers/MOD-0024-task-checklist-engine/`
- integration seam from MOD-0023-workflow-designer may emit operational tasks here

### Recommended frontend scope
- `frontend/Diten.Web/Controllers/PlatformSharedServices/MOD-0024-task-checklist-engineController.cs`
- `frontend/Diten.Web/Views/PlatformSharedServices/MOD-0024-task-checklist-engine/`
- `frontend/Diten.Web/wwwroot/js/platform-shared-services/mod-0024.js`

### Protected paths
- Inherit all protected paths from `domain-config.md`.
- Do not implement this module inside ES&BP, Demand, or Delivery feature trees.

## 5. UI Surfaces
- Task Inbox — list/inbox for my tasks and due dates
- Checklist Templates — catalog/detail
- Checklist Run — workspace for execution and evidence links

## 6. Runtime Constraints
- must not implement approve/reject/delegate
- task model stays generic
- completion/evidence rules are template-driven

## 7. Acceptance Criteria
- Tasks are auditable and permissioned.
- Checklist completion requires evidence only when configured.
- Boundary with Workflow remains explicit in contracts and code.

## 8. Testing Notes
- Run targeted backend build: `dotnet build services/Diten.Platform/src/Diten.Platform.API/Diten.WebAPI.csproj`
- Run targeted frontend build: `dotnet build frontend/Diten.Web/Diten.WebUI.csproj`
- task lifecycle tests
- checklist execution tests
- optional evidence gating tests
- UI build/tests

## 9. Implementation Notes
- Keep task primitives reusable and lightweight.
- Reuse checklist model across multiple consuming domains.

## 10. Follow-up Items
- Can slide to W2 if delivery capacity is constrained.
