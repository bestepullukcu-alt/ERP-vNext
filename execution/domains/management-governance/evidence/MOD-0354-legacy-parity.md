# MOD-0354 Legacy Enterprise Strategy Parity Gate

Status: **PENDING USER ACCEPTANCE**. This matrix is evidence for side-by-side review; it does not authorize
redirect, retirement, deletion, migration or a `full parity complete` decision.

| Old URL | Old entities | Fields | Commands/actions | Lifecycle | Permissions | Integrations | New owner/module | New URL | Field mapping | Disposition | Migration requirement | Browser evidence |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `/DecompositionTreeBuilder` initial/loading | Prototype tree fixture and optional `structureId` | code, title, type, status, owner, due date, readiness, issues | search, filters, view switching | prototype draft/readiness states | none in legacy controller | local prototype assets | `MOD-0354` | `/management-governance/delivery-execution/structures` | `structureId` → authoritative `StructureDefinitionId`; code/title → node code/title | Replaced | No migration in this slice | New default-off/zero-read DOM evidence PASS; populated browser comparison pending Gateway |
| `/DecompositionTreeBuilder` tree/outline/map | Prototype nodes, dependencies and inspector | parent, code, title, description, order plus prototype task fields | add/edit/move/filter/validate/readiness | mixed structural and task lifecycle | none in legacy controller | Drawflow/prototype JavaScript | `MOD-0354` for hierarchy/order/dependency; other owners for task behavior | `/management-governance/delivery-execution/structures` | parent/code/title/description/order → typed DWS projection | Same for structural fields; Out of Scope for task fields | Owner-reviewed data migration decision required separately | Side-by-side comparison pending user acceptance |
| `/DecompositionTreeBuilder` owner/date/status controls | Prototype task-like fields | owner, due date, status, progress | assign/start/complete-like prototype interactions | task/execution lifecycle | none in legacy controller | prototype only | `MOD-0024` / execution owner, not MOD-0354 | none in MOD-0354 | none | Out of Scope | No copy or default tenant assignment | Absence guard PASS; owner review pending |
| `/DecompositionTreeBuilder` approval control | Prototype approval state | approval label/readiness | approve | local prototype approval | none in legacy controller | prototype only | `MOD-0023`, not MOD-0354 | none until separately authorized | none | Intentionally Removed from MOD-0354 | No migration or activation | Absence guard PASS; owner review pending |

## Byte-preservation evidence

- `Controllers/DecompositionTreeBuilderController.cs`: `580dbb3bfd5ef2088a77beeb918439da7045730048e2c77bafdcaf082e8d2317`
- `Views/DecompositionTreeBuilder/Index.cshtml`: `6b85f9dd3b9753a891cc9b851c5f3f47fecbce61ab34774713cb794e4f9fd8ea`

The legacy and new URLs remain simultaneously available for a later different-port browser review. Browser
screenshots/network logs for populated read, `403`, non-disclosing `404`, `409` and live Gateway `503` remain open.
