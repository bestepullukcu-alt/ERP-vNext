# MOD-0029-FU37C Manual-Link Scope Guardrails — Implementation Audit

Date: 2026-07-25  
Verdict: PASS WITH RUNTIME GAP

## 1. Scope

Manual Master Register ↔ Controlled Document reconciliation, scope compatibility and downstream fail-closed readiness only.

## 2. Authority

MOD-0029-FU37C attachment, MOD-0029-FU37 parent decisions, PSS domain contract and repository AGENTS.md were applied.

## 3. Invariants

Scope, scope owner, company/corporate owner, collection instance and folder must match. Permission never waives compatibility.

## 4. Legacy Reconciliation

Manual link remains available only as an explicitly authorized reconciliation path. A non-empty, audited reason is mandatory.

## 5. Company Compatibility

Company scope requires matching scope owner, OwnerCompanyId, ControlledDocument CompanyId/OwnerCompanyId, collection and folder.

## 6. Corporate Compatibility

Corporate scope requires matching corporate owner, collection and folder plus an explicit Corporate CreateDocument grant.

## 7. Tenant Isolation

Both repositories remain tenant-scoped; cross-tenant targets resolve through the established non-leaking 404 behavior.

## 8. Registration Snapshot

Unified registration writes scope, owner, corporate owner, collection and folder snapshots and marks the generated relation Compatible.

## 9. Downstream Guardrails

Approval readiness, training readiness and Release Gate 1 fail closed for missing, unvalidated or invalid controlled-document relations.

## 10. API and Permission

The link endpoint requires both master-register link and controlled-document-registration reconcile permissions.

## 11. Frontend

The modal carries a legacy-reconciliation warning, requires a reason and lists only exact scope/owner/collection/folder candidates. Backend validation remains authoritative.

## 12. Localization

Warning and reconciliation reason strings are present in en, tr, fr, es, ru, zh and ar resources.

## 13. Automated Evidence

Application build passed. FU37C and Master Register target tests passed using isolated output because the live Platform API locked the default build artifacts. The FU37C PowerShell verifier passed.

## 14. Residual Gaps

No runtime smoke was performed in this delivery. Reverse navigation remains outside FU37C scope. No commit or push was made.
