# Human Capital Management Domain

Human Capital Management owns internal HCM domain applications and governance contracts for the HR Foundation MVP and later HR completion waves.

## Scope

- Core HR employee and employment master capabilities.
- HCM Foundation domain apps listed in the Enterprise Blueprint.
- HCM-owned module packs under `execution/domains/human-capital-management/module-packs/`.

## Current Module Packs

- `MOD-0251 Core HR / Employee Master` - approved for the reduced P2 draft/reference-validation slice. The slice is implemented and browser-validated for create, save/update with ETag, reload, person/organization-unit/position/legal-entity reference validation, and non-submit review.

## Current Runtime Position

MOD-0251 P2 is draft/reference-validation only. The full Employee Master lifecycle is not complete and remains outside the approved slice.

Explicitly out of scope until later approved prompts:

- Submit.
- Approval/rejection.
- Activation.
- MOD-0023 workflow behavior.
- `employee.created`.
- Evidence upload/link.
- Export/status/Data Quality Queue.
- Government identifier capture/tokenization.

Repo packaging or commit remains unsafe while the workspace has no valid `HEAD` and all files are untracked.

## Authority

Authority order remains:

1. Module Pack
2. Domain Config
3. `AGENTS.md`
4. `.antigravity/` standards
5. Archive/external references

Runtime expansion beyond the approved P2 draft/reference-validation slice is not authorized by this governance scaffold.
