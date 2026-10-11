# MVP6-MOD0184-UI-DEV-02 — v2.0 — HELD

**Target:** frontend implementation agent  
**Lane:** UI DEV, single writer for the 21 UI-owned paths  
**State:** HELD; release only after START-DECISION-PACK Decisions A and B are executed and exact handoffs exist.

## Required immutable inputs

- applied pack target `28019ebe9fd6e34ca719d244cc0129fe2046ee0f692a9ffd6a5a6eed16028f3f`;
- materialized source manifest `dec28b6aebd7552df82d380cf8b601aabc4d28c7d21349ff43807bd0747e4634` plus the integration writer-complete manifest containing all 16 exact shared targets;
- contract/annex hashes from SCOPE-01;
- unchanged 21-path allowlist and `PHASE15-ACCEPTANCE.tsv`;
- a registered, target-bound UI checkout. A HEAD-only checkout is invalid.

## Work

Implement only the 21 UI-owned paths from SCOPE-01. Build `/SupplyChain/Carriers` with tenant shell, GoldenReferenceSlim create-only flow, DataTables v2 list, status action, UAS-001, Premium SweetAlert2/shared primitives, and seven module resource sets. Use a same-origin MVC proxy whose only service egress is Gateway 5000. Preserve independent read/create/status grants and the Carrier contract's exact field, error, correlation, lifecycle, and replay semantics.

Do not modify the 18 integration/shared paths, `_LayoutTenantShell.cshtml`, Program.cs, backend Carrier code, contract/annex, gateway policy, or any non-Carrier module. Do not add detail/edit/delete/bulk/import/lookup/server paging/search/sort or a new permission/endpoint. A shared mismatch is an exact integration-owner rework finding, not a UI-writer fix.

## Evidence and handoff

Run the focused controller/form/JS/resource tests, DataTables/Golden Slim verification, direct 401/403/UAS tests, error/replay negatives, and composed Gateway 5000 browser/API scenarios allowed by the integrated baseline. Record source→build→process→browser/HTTP evidence, exact changed-file manifest, all failed/discarded attempts, acceptance mapping, protected-path hashes, SOP §22, and writer-complete. UI-isolated PASS is not composed integration PASS.

No rollout, gateway/shared write, E5/G5, commit, push, or stash.
