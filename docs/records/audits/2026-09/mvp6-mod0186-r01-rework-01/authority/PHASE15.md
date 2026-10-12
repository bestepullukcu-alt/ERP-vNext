# Phase 1.5 — technical closure

Verdict: **PASS for isolated core dispatch**. This is a design/source gate, not HTTP/JWT, deployment, or full-module acceptance. The conditional owner grant and exact replacement grant are reproduced with their real user-message pointers in `authority.md`; no new approval is inferred from historical proposals.

| # | Gate | Verdict | Concrete plan and evidence |
|---|---|---|---|
| 1 | All fields mapped | PASS | `wire-fields.tsv` is extracted from the pinned canonical schemas. `physical-design.md` maps commands, summary/event fields, immutable audit values, scope and base fields. `ReturnOrder`, `ReturnLine`, `ReturnEntitlement` implement the mapping; raw decimal strings remain strings. |
| 2 | Field names | PASS | `shipmentLineNumber` is an ordinal string, not a fabricated UUID line ID. `RmaNumber` is server-generated RMA plus uppercase UUID-N. No stock master or additional wire fields. `operation-status.tsv` binds all three published operations. |
| 3 | Repository isolation / soft-delete | PASS | `ReturnRepository.Scope` uses trusted tenant/LE; `Visible` adds IsDeleted=false. Receipt history is separately scoped and survives aggregate deletion. `ReturnSchema` implements the exact five collection/index names in `physical-design.md`; no UoM in entitlement key, no receipt TTL. Snapshot quantity/UoM/item/sku comparison precedes CAS debit; release is limited to Rejected/Cancelled. |
| 4 | Base entity | PASS | Existing Domain/Common/EntityBase supplies Id/TenantId/IsDeleted/DeletedAt/CreatedAt/UpdatedAt/Version; `ReturnOrder : EntityBase` adds LE and actors. Shared entity and serializers are unchanged. |
| 5 | CQRS / transaction / pipelines | PASS | Separate commands/query, three handlers and three validators appear in the 46-path allowlist. Existing AddApplication scans them and registers Validation, Logging, ExceptionHandling and Performance. `ReturnPersistenceRegistration` reuses the shared Mongo client/database. Reference HTTP precedes the local transaction; five write groups commit atomically. |
| 6 | DataTable golden reference | N/A | Backend-only scope; shell none, form count 0. No DataTable page is authorized. |
| 7 | Compact UI section parity | N/A | No Razor, Web ViewModel, frontend, localization or form/section ownership. |
| 8 | Required/null/error/header/root/replay parity | PASS | `wire-fields.tsv`, `error-matrix.tsv`, `operation-status.tsv`, `context-parity.md` bind the published schemas and annex. ReturnWire validators and ReturnContextMiddleware implement the approved module-specific matrix; HTTP enforcement remains an integration acceptance output. No Claims permission or fingerprint policy is copied. |
| 9 | Lookup/reference boundary | N/A for lookup; PASS for boundary | No PSS lookup or catalog creation. Fresh create observes only Shipment detail; transitions/replays do not re-read it. Inventory/Warehouse HTTP is zero, including GET. Optional reference text remains opaque and its raw command value is audited. |

## Reproduced gate measurements

- Fresh DCP-002: exit 0 (`evidence/returns-dev/dcp-002.txt`).
- Exact published YAML, applied replacement target and protected input hashes: `technical-checks.txt`, `protected-input-recheck.tsv`.
- Registered worktree and current dirty/untracked transfer: `baseline.txt`, `input-manifest.tsv` (413 inputs; 412 protected inputs plus the separately promoted pack).
- Exactly 46 unique effective owned paths, all Returns-specific; no worker or Program.cs. `owned-paths.txt` is the controlling allowlist.
- Pack acceptance table defines R01–R11. There is no R12 row; the new prompts use the actual range rather than inventing an acceptance criterion.

Program.cs remains a separately owned HTTP integration change. Published producer runtime uptake is required for successful integrated create acceptance; it is not a prerequisite to implement/test this isolated core against the approved seam. No new repository/index/reference policy decision is pending.
