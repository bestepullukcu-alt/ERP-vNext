# 09_audit_evidence_standards_output.md — Audit & Evidence Standards (Platform)

**Status:** Ready

## Audit (mandatory)
Audit events must include:
- `correlation_id`
- `tenant_id`
- `actor_id`
- `action`
- `outcome`
- `object_refs`
- `before` / `after` where applicable
- `occurred_at`

## Audit rules
- Append-only; no updates/deletes.
- All approvals and privileged admin actions must be audited.
- Correlation IDs must propagate from request → service → audit.

## Evidence (per-module)
- MOD-0031: `EvidenceLink` is SoR.
- MOD-0028: stores artifacts; does not decide evidence completeness.
- Evidence requirements must be policy/template-driven.

## Closure / approval linkage baseline
Approve/reject actions must:
- emit audit
- optionally require evidence completeness before transition, when config-driven

## Current repo note
A partial enterprise strategy audit seam already exists in persistence. Treat that as current-state evidence for audit storage patterns, not as a reason to collapse target-state Platform ownership.
