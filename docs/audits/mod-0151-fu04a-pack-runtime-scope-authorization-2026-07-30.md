# MOD-0151 FU04A — Pack Runtime Scope Authorization

**Date:** 2026-07-30
**Task type:** Governance / module-pack authorization only
**Verdict:** PASS

## 1. Preflight

- Authority order read: `AGENTS.md` → Commercial Suite domain config → MOD-0151 module pack → module-pack workflow/rules.
- DCP-002 canonical gate executed:
  `python .antigravity/scripts/verify_module_id.py . --check-id MOD-0151 --name "Territory Management"`.
- Result: exit code `0`.
- Identity decision: FU04A is an additive follow-up of Blueprint-canonical `MOD-0151`; no new MOD ID, alias, candidate
  identity or registry reservation was created.
- Canonical module name remains `Territory Management`.
- Existing pack status remains `ready-for-dev`; this task adds an explicitly named executable FU scope and does not
  authorize any scope outside it.
- Golden Reference decision remains `compact`; no new module/page family or form-count decision was introduced.

## 2. FU04 Current Gap Summary

FU04 historically delivered draft-only resource assignment create/update/list/detail/end/soft-delete, a basic
duplicate-primary guard, UI and contract flags. The current implementation analysis classified the business outcome
as PARTIAL because:

- every resource mutation is gated to a draft model;
- create produces `proposed`, but model activation does not own an explicit `proposed → active` transition;
- active-model end/create/replacement/transfer is unavailable;
- resource current-responsibility and dedicated history contracts do not exist;
- update can overwrite planning values instead of producing lifecycle history;
- the current Position migration removed or skipped some earlier role-based validation/conflict behavior;
- visit/route consumers cannot safely distinguish planning assignments from operational responsibility.

The historical FU04 report remains evidence for its then-authorized scope, but it is not treated as proof that the
new FU04A operational lifecycle exists.

## 3. Role-to-Position Transition Summary

FU04A makes Position the canonical responsibility identity:

- authoritative matching: `PositionRef` + normalized `PositionCode`;
- `PositionRef`: `PositionId?`, `PositionCode`, `PositionTitle`, `PositionType`, `SourceSystem`;
- Person/Employee/Position masters remain consume-only dependencies;
- `RoleCode` cannot be introduced as an authoritative new write/query/conflict/UI field;
- UI labels use **Position**, not Role;
- role-named rows in the historical pack are explicitly legacy Position-policy mappings;
- `territory-resource-role` is compatibility-only/deprecated and may only support legacy migration mapping;
- new tests assert Position policy, not hardcoded role behavior.

## 4. Pack Frontmatter Changes

The following additive scope was appended to `runtime_code_scope`:

`FU04A-resource-assignment-lifecycle-replacement-operational-visibility`

Preserved scopes:

- FU01 territory model/node backend;
- FU02 hierarchy UI;
- FU02A country/business-unit selector hardening;
- FU02B lifecycle/computed expiry/draft soft-delete;
- FU03 assignment rules/preview;
- FU04 resource assignments;
- FU05 account assignment apply/history.

No existing scope was removed or renamed.

## 5. FU04A Authorized Scope

FU04A authorizes:

1. draft planning (`proposed`) versus active operational (`active`) separation;
2. auditli, fail-closed model-activation resource transition;
3. active-model resource create and end with reason/effective date;
4. atomic resource replacement;
5. atomic resource transfer;
6. node/resource/position/model history queries;
7. effective-date current-responsibility query;
8. Position-based conflict, exclusivity and override hardening;
9. Position directory validation plus controlled snapshot seam;
10. lifecycle/current/history UI, tests, contract alignment, authenticated Gateway smoke and implementation report.

## 6. FU04A Explicit Exclusions

FU04A does not authorize:

- `AccountTerritoryAssignment` apply/history changes;
- Account or Contact mutation;
- Account/Contact entity fields for territory;
- workflow approval, submit/approve/reject, MOD-0023 integration or approval trace;
- evidence pack;
- import/export;
- visit/route planning implementation;
- Brand Scope or Product/Brand master;
- hard delete;
- Mongo hand-edit;
- RBAC seed/grant;
- MOD-0048 publish;
- `crm.territory.delete`;
- `crm.micro-zone.manage`;
- request payload `TenantId`;
- direct CrmService port business calls.

## 7. Draft vs Active Policy Decisions

| Question | Decision |
|---|---|
| Draft assignment operational? | No. `proposed` is planning-only and never returned as current responsibility. |
| Activation transition? | Yes. All valid proposed resource assignments transition atomically to active. |
| Activation conflict? | Blocking conflict/missing required Position policy fails closed with no partial transition. |
| Advisory warning? | Does not block by itself; warning is returned and audited. |
| Active create? | Allowed with reason, effective date, Position validation and conflict checks. |
| Active end? | Allowed; record becomes ended/effective-to and remains history. |
| Active direct update? | Critical fields cannot be overwritten; use End/Replace/Transfer. |
| Minor metadata patch? | May be allowed only for display snapshot/email-like fields, separately audited. |
| Terminal/non-operational model mutation? | Archived/inactive/superseded/expired/soft-deleted models are blocked. |

## 8. Replacement / Transfer Policy Decisions

Replacement and transfer are all-or-nothing operations.

Replacement:

- ends the old record without deleting it;
- creates a separate active record;
- preserves `ReplacedAssignmentId`, reason, correlation ID and previous/new Position codes;
- changes neither record if validation, conflict or concurrency fails.

Transfer:

- ends the source record;
- creates a separate target record;
- preserves source/target assignment IDs, reason, effective date, Position code and correlation;
- cannot silently change the person/resource identity;
- changes neither record if any step fails.

## 9. Current Responsibility Contract Decision

The contract answers:

> At the requested effective date, who is the primary operational resource for this node + business scope +
> Position?

A record is current only when all are true:

- tenant matches;
- stored model status is active;
- assignment status is active;
- effective date is inside the assignment interval;
- assignment is not soft-deleted, ended or rejected;
- business scope matches;
- `IsPrimary=true`;
- normalized `PositionCode` / `PositionRef` matches.

Zero matches returns a controlled empty result. Multiple matches are an integrity conflict. Draft/proposed rows are
never current.

## 10. Conflict / Override Policy Decisions

| Scenario | Policy |
|---|---|
| Same node + Position + BU + overlapping dates, two primary | 409; override cannot bypass. |
| Same person + Position + same BU + multiple primary nodes | Allowed with `multi-node-coverage` warning and audit. |
| Same person + Position + different BU + overlapping primary | Default 409. |
| Cross-BU override | Allowed only with `source=override`, non-empty reason and authorized manage actor. |
| Non-primary | Exempt from primary exclusivity; other validation remains mandatory. |
| Invalid dates/tenant/terminal model/missing required Position policy | Never overrideable. |

The implementation must actually consume the override decision; an unused `allowOverride` parameter does not meet
the acceptance criteria.

## 11. Position Validation / Metadata Decisions

- If canonical Position directory is available, backend validates ID/code/title/type consistency.
- If it is temporarily unavailable, an otherwise complete PositionRef snapshot may be accepted for planning with
  dependency warning and validation-mode audit.
- Operational activation requires either a previously validated Position policy snapshot or a compatibility policy
  mapping; absence of both fails closed.
- Node/coverage expectations are metadata-driven:
  - Medical Representative position: zone/microzone;
  - Area Manager position: area, with wider level only when metadata permits;
  - Regional Manager position: region, with division only when metadata permits;
  - Product Manager position: node-less BU/product-portfolio scope;
  - HOC/Commercial Manager: model-wide/wider scope only when policy permits.
- No new role switch/hardcoded `RoleCode` policy is authorized.

## 12. RBAC Notes

Canonical targets:

- `crm.territory.resource.read`;
- `crm.territory.resource.manage`.

FU04A implementation does not seed or grant them. If the catalog is not ready, open:

`MOD-0151 FU04A-RBAC — Resource Assignment Permission Catalog Alignment`

Temporary fallback is allowed only for FU04A:

- `crm.territory.model.read` for query/UI;
- `crm.territory.model.manage` for create/end/replace/transfer.

The fallback cannot create new permission literals or enable delete/micro-zone permissions.

## 13. Reference Metadata Notes

Existing consumed vocabularies:

- `territory-coverage-scope`;
- `territory-assignment-status`;
- `territory-assignment-source`.

Target Position policy metadata:

- `positionCode`, `positionType`;
- `requiresTerritoryId`, `allowsTerritoryId`;
- `requiredNodeLevels`, `allowedNodeLevels`;
- `requiresBusinessScope`, `allowsBusinessScope`;
- `canBePrimary`, `allowsMultiNode`, `allowsCrossBusinessUnit`;
- `requiresReason`, `allowsMutation`;
- `isOperationalStatus`, `isPlanningStatus`, `isTerminal`.

`territory-resource-role` is compatibility-only/deprecated. A target
`territory-resource-position-policy` or canonical Position-directory metadata may own the new policy, but this task
does not publish either source.

## 14. Guard Checks

| Guard | Result |
|---|---|
| Runtime code changed? | No |
| Backend/frontend changed? | No |
| Gateway changed? | No |
| RBAC seed changed? | No |
| MOD-0048 publish changed? | No |
| Mongo changed? | No |
| FU04A scope added? | Yes |
| FU01–FU05 scopes preserved? | Yes |
| Account assignment scope opened? | No |
| Account/Contact mutation allowed? | No |
| Workflow/evidence/import-export opened? | No |
| Visit/route planning implementation opened? | No |
| Brand Scope opened? | No |
| Hard delete allowed? | No |
| FU05/FU06/FU07/FU08/FU09 boundaries preserved? | Yes |
| Role-based language replaced or marked deprecated? | Yes |
| Position-based validation decisions recorded? | Yes |
| Position lookup dependency documented? | Yes |

## 15. Created / Updated Files

| File | Action |
|---|---|
| `execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md` | Updated — additive FU04A authorization and policies |
| `docs/audits/mod-0151-fu04a-pack-runtime-scope-authorization-2026-07-30.md` | Created — authorization evidence |

No service, frontend, Gateway, Auth, reference publish, Mongo or registry file was changed by this task.

## 16. Final Verdict

### PASS

- FU04A runtime scope was added additively.
- Draft planning and active operational responsibility were separated.
- Activation transition, active create/end, atomic replacement/transfer and current/history contracts were
  authorized.
- Position replaced Role as the canonical responsibility identity.
- Position validation and metadata policies were recorded.
- Explicit exclusions and FU05–FU09 boundaries remain closed.
- The implementation prompt can now be executed against the named FU04A scope.

## 17. Next Recommended Prompt

`@orchestrator MOD-0151 FU04A — Resource Assignment Lifecycle, Replacement and Operational Visibility Hardening`
