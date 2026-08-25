# Business Process Management (BPM)

**Short code:** `BPM`
**Governance identity:** `CAND-CAP-0007 — Business Process Management`
**Delivery charter:** [DCP-005](../../portfolio/delivery-capability-packs/DCP-005-business-process-management.md)
**Production service:** not selected and does not exist.

## Purpose

This governance-only domain defines the ownership boundary for process architecture, versioned process models,
process-performance bindings, and process-improvement records. It exists before implementation so these objects
cannot drift into MOD-0023 workflow, MOD-0024 tasks, MOD-0025 rules, semantic/KPI masters, or MOD-0117 PPM.

## Current status

- `CAND-CAP-0007` is a temporary documentation/governance identity, not a Blueprint-canonical MOD.
- DCP-005 and the first member pack are `draft` and non-executable.
- First planned member: `CAND-CAP-0007-FU01 — Process Architecture & Modeling`.
- FU02 Process Performance and FU03 Process Improvement are future members listed in DCP-005 only; their pack
  files do not exist.
- Executable Process Runtime / BPMN engine is deferred and excluded.
- No service, runtime, frontend, Gateway, WorkCenter, collection, permission, route, or production scaffold is
  authorized.

## Authority

1. Approved/ready member Module Pack
2. [domain-config.md](domain-config.md)
3. repository [AGENTS.md](../../../AGENTS.md)
4. `.antigravity` engineering rules

DCP-005 governs family sequencing but never replaces a member Module Pack.

## Identity guard

`CAND-CAP-0007` and its FU identities never enter runtime literals, namespaces, permission keys, collection
names, or event `ModuleCode` values. Existing frontend `mod-process-*` values are prototype/navigation
identities, not canonical aliases or implementation evidence.

## Related governance

- [Domain config](domain-config.md)
- [FU01 draft pack](module-packs/CAND-CAP-0007-FU01-process-architecture-modeling.md)
- [Module ID registry](../../registries/module-id-registry.md)
- [Blueprint reconciliation](../../portfolio/blueprint-master-plan-reconciliation.md)
