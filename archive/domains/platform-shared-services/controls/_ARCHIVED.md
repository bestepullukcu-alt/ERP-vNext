---
status: ARCHIVED
archived_on: 2026-05-13
reason: Out-of-date or duplicated; superseded by current authoritative sources
---

# Platform & Shared Services — controls/ (ARCHIVED)

These 12 control documents were produced under the original SOP batch-execution model. They are **frozen historical reference** and must not be cited as current authority.

## Why archived

- Authority order (AGENTS.md §1) makes `AGENTS.md` and `.antigravity/rules/` authoritative for engineering standards.
- The `batches/` execution layer that consumed these documents is no longer used (AGENTS.md §12).
- Most controls/ files were either factually out-of-date (wrong repo paths, contradicting `Response<T>`/Layout/Gateway facts) or duplicated content already kept current elsewhere.

## Current authoritative replacements

| Old controls/ file | Current source of truth |
|---|---|
| 01_repo_map_output.md | `AGENTS.md` §2 (Klasör yapısı) |
| 02_shared_service_contracts_output.md | `execution/domains/platform-shared-services/decisions/runtime-decisions.md` |
| 03_ownership_map_output.md | `execution/domains/platform-shared-services/decisions/ownership-decisions.md` |
| 04_database_migration_standards_output.md | `AGENTS.md` §6 + `.antigravity/rules/mongo-indexing.md` |
| 05_reference_data_catalog_output.md | per-module pack reference data + `docs/platform/master-plan.md` §7.1 |
| 06_environment_commands_output.md | `AGENTS.md` §5 |
| 07_ui_shell_standards_output.md | `.antigravity/rules/views-organization.md`, `frontend-standards.md`, `frontend-datatable-template.md` |
| 08_api_event_standards_output.md | `.antigravity/rules/response-envelope.md`, `api-conventions.md` |
| 09_audit_evidence_standards_output.md | belongs in MOD-0021 module pack when authored |
| 10_open_items_decision_log_output.md | `decisions/runtime-decisions.md` |
| 11_batch_to_control_map.md | obsolete (batches/ unused) |
| 12_build_prompt_plan.md | obsolete (batches/ unused) |

If you find yourself reaching for a file in this folder, you are reading stale material — go to the column on the right.
