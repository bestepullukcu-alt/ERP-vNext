# Human Capital Management Domain

Governance-only bootstrap for native HR/HCM delivery.

This domain exists to hold module packs, ownership decisions, and execution
contracts for Human Capital Management modules after DCP-008 canonicalization
and EA identity decisions. It does not create a production runtime service,
API, frontend, gateway route, database collection, or module implementation.

## Bootstrap Decision

| Decision | Value |
|---|---|
| Domain slug | `human-capital-management` |
| Branch short code | `hcm` |
| Runtime service candidate | `Diten.HumanCapitalService` |
| Bootstrap type | Governance-only |
| Source roadmap | `execution/portfolio/delivery-capability-packs/DCP-008-hr-tep-delivery-roadmap.md` |

## Scope

Native HR/HCM ownership is intentionally separate from
`platform-shared-services`. PSS remains the shared substrate for authorization,
audit, workflow, documents/evidence, external source readiness, payroll/time
provider contracts, payroll integration governance, and the minimal person
reference directory projection.

No native HR/HCM module pack may be prepared until its identity is validated
through DCP-002 and any required EA CAND-CAP / canonical MOD decision is
recorded.

## Current Blockers

- `MOD-0297`, `MOD-0298`, and `MOD-0299` are blocked for HR/HCM use because the
  registry maps them to deprecated platform alias chains.
- Native R1/R3 HR/HCM IDs listed in DCP-008 that are absent from Blueprint must
  be treated as `CAND-CAP REQUIRED — EA DECISION`.
- Runtime service ownership remains candidate-only; no
  `services/Diten.HumanCapitalService/**` scaffold exists or is authorized by
  this bootstrap.

## First Module-Pack Candidates

Prepare only after EA canonicalization / CAND-CAP decisions:

1. HR Capability Block Shell.
2. Employee Profile & Employment Record Projection.
3. HR Governance & Sensitive Access Controls.

The Excel IDs for the first two candidates must not be used as runtime
identities until EA resolves the DCP-002 findings.
