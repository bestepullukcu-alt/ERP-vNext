# Master Data Management (MDM)

**Short code:** `MDM`
**Module ID policy:** new ERP product module packs use registry-controlled `MOD-NNNN` IDs.
**Production service:** `services/Diten.MdmService/` does not exist yet.

## Purpose

Master Data Management owns ERP business master-data systems of record and the contracts by which platform and
business domains consume those records. This governance scaffold exists before production implementation so MDM
ownership, boundaries, and module-pack gates are explicit.

## Current Governance Status

- Domain governance scaffold exists.
- Production service scaffold does not exist yet.
- No MDM production implementation is authorized by this scaffold.
- First reserved module: `MOD-0220 Corporate Secretarial / Entity Management` - Legal Entity Foundation slice.
- Authoritative planning Excel mapping has been confirmed for `MOD-0220`.
- Authoritative Enterprise Blueprint repository migration remains pending for the MOD-0220 mapping.

## Authority Order

1. Module Pack - `module-packs/{ID}-{slug}.md`
2. Domain Config - `domain-config.md`
3. `AGENTS.md`
4. `.antigravity/rules/`
5. Archive / external references

## New Module Flow

1. Prepare a draft module pack.
2. User reviews the draft.
3. Promote the pack to `approved` or `ready-for-dev` only after explicit approval.
4. Call `@orchestrator` only for an approved / ready-for-dev pack.
5. Keep draft packs planning-only; they authorize no code.

## Out Of Scope

- Platform authorization evaluation and data-scope resolver algorithms.
- Platform provisioning/support lookups such as PSS-011 countries.
- Gateway routes, frontend UI, and production service scaffolding until separately approved.
- Other domain services and module packs.

## Related Governance Sources

- [domain-config.md](domain-config.md)
- [module-packs/](module-packs/)
- [Module ID Registry](../../registries/module-id-registry.md)
- [Master Development Plan](../../portfolio/master-development-plan.md)
- [Blueprint / Master Plan Reconciliation](../../portfolio/blueprint-master-plan-reconciliation.md)
