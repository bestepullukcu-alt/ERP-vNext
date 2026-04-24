# Platform & Shared Services

## Purpose
`platform-shared-services` is the live Domain Package for shared platform primitives consumed by ERP Suite, ES&BP, DWSE, and adjacent orchestration layers. The domain exists to provide reusable, versionable, auditable services for identity/authorization, workflow, tasks, documents, evidence linkage, platform configuration, eventing, and operational telemetry without leaking business-domain semantics into the platform layer.

## Operating stance
- **Execution model:** module-first, one batch at a time.
- **Authority order:** module pack → domain package → `AGENTS.md` → `.antigravity` → archive/reference artifacts.
- **Current MVP posture:** ship the minimum viable shared backbone first; keep gateway, integration monitoring, and dedicated alerting surfaces deferred or thin where the current repo/runtime does not support them.

## In-scope modules
### Core W1 backbone
- MOD-0012 Secrets & Configuration Vault
- MOD-0018 RBAC / ABAC Authorization
- MOD-0021 Audit Trail Service
- MOD-0023 Workflow Designer (Approvals / SLAs / Escalations)
- MOD-0024 Task & Checklist Engine
- MOD-0028 Document Management (Templates / Versioning)
- MOD-0031 Evidence Linking Service

### Thin-mode or deferred platform seams
- MOD-0032 API Gateway
- MOD-0035 Event Bus / Message Queue
- MOD-0037 Integration Monitoring & Reconciliation
- MOD-0041 Logging & Monitoring
- MOD-0042 Alerting & Incident Runbooks

## Consumed control-point dependencies (not owned by this domain)
- MOD-0005 Policy & Control Library
- MOD-0006 Policy Exception / Waiver Register
- MOD-0007 Decision & Rationale Log

## Domain ownership
This domain owns shared platform capabilities and their authoritative objects:
- authorization roles, permissions, policies, assignments
- append-only audit events and audit query views
- workflow definitions, workflow instances, approval-task semantics
- generic tasks, checklist templates, checklist runs
- documents, document versions, templates, collections
- evidence links and evidence-oriented reusable UI surfaces
- secrets/configuration profiles and their access governance
- current-MVP internal event dispatch seam
- current-MVP lightweight observability seam

This domain does **not** own:
- business objects such as goals, objectives, initiatives, projects, invoices, payments, risks, or strategy entities
- business lifecycle semantics such as ERP close logic or ES&BP governance meaning
- external provider consoles for gateway, broker, vault, or observability products
- protected business-domain repo areas listed in `domain-config.md`

## Domain package contents
- `domain-config.md` — operational domain frame
- `decisions/` — runtime, ownership, and deferred-item decisions
- `controls/` — domain-level shared execution controls required before coding
- `batches/` — one-batch-at-a-time Codex execution prompts
- `module-packs/` — one execution pack per module

## Recommended execution sequence
1. Batch 01 — module skeletons + boundary setup
2. Batch 02 — shared-service seams + runtime wiring
3. Batch 03 — MOD-0018 RBAC / ABAC
4. Batch 04 — MOD-0021 Audit Trail
5. Batch 05 — MOD-0028 Document Management
6. Batch 06 — MOD-0031 Evidence Linking
7. Batch 07 — MOD-0023 Workflow Designer
8. Batch 08 — MOD-0024 Task & Checklist Engine
9. Batch 09 — MOD-0012 Vault
10. Batch 10 — MOD-0032 API Gateway (**documentation-only / deferred in current MVP unless re-scoped**)
11. Batch 11 — MOD-0035 Event Bus / Message Queue (**govern lightweight internal seam only**)
12. Batch 12 — MOD-0037 Integration Monitoring & Reconciliation (**deferred / documentation-only unless re-scoped**)
13. Batch 13 — MOD-0041 Logging & Monitoring (**govern lightweight telemetry seam only**)
14. Batch 14 — MOD-0042 Alerting & Incident Runbooks (**deferred / documentation-only unless re-scoped**)

## Final operating rule
Use this package to define **how Platform & Shared Services works**. Use the module packs to define **what to build now**. Do not merge the two layers.
