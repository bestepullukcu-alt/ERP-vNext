# 11_batch_to_control_map.md — Batch to Control Map

**Purpose:** Define which control documents must be opened together with each batch prompt.

| Batch | Batch focus | Required control inputs |
|---|---|---|
| 1 | Module skeleton + boundary | 01_repo_map_output.md, 03_ownership_map_output.md, 06_environment_commands_output.md |
| 2 | Shared service integration seams + deployment modes | 02_shared_service_contracts_output.md, 03_ownership_map_output.md, 06_environment_commands_output.md, 10_open_items_decision_log_output.md |
| 3 | MOD-0018 RBAC / ABAC Authorization | 03_ownership_map_output.md, 05_reference_data_catalog_output.md, 08_api_event_standards_output.md, 09_audit_evidence_standards_output.md |
| 4 | MOD-0021 Audit Trail Service | 03_ownership_map_output.md, 08_api_event_standards_output.md, 09_audit_evidence_standards_output.md |
| 5 | MOD-0028 Document Management (Templates/Versioning) | 01_repo_map_output.md, 04_database_migration_standards_output.md, 06_environment_commands_output.md, 07_ui_shell_standards_output.md, 09_audit_evidence_standards_output.md |
| 6 | MOD-0031 Evidence Linking Service (object ↔ evidence) | 03_ownership_map_output.md, 04_database_migration_standards_output.md, 07_ui_shell_standards_output.md, 08_api_event_standards_output.md, 09_audit_evidence_standards_output.md |
| 7 | MOD-0023 Workflow Designer (Approvals/SLAs/Escalations) | 02_shared_service_contracts_output.md, 03_ownership_map_output.md, 04_database_migration_standards_output.md, 05_reference_data_catalog_output.md, 07_ui_shell_standards_output.md, 08_api_event_standards_output.md, 09_audit_evidence_standards_output.md, 10_open_items_decision_log_output.md |
| 8 | MOD-0024 Task & Checklist Engine | 02_shared_service_contracts_output.md, 03_ownership_map_output.md, 04_database_migration_standards_output.md, 05_reference_data_catalog_output.md, 07_ui_shell_standards_output.md, 08_api_event_standards_output.md, 09_audit_evidence_standards_output.md |
| 9 | MOD-0012 Secrets & Configuration Vault | 02_shared_service_contracts_output.md, 03_ownership_map_output.md, 04_database_migration_standards_output.md, 06_environment_commands_output.md, 08_api_event_standards_output.md, 09_audit_evidence_standards_output.md, 10_open_items_decision_log_output.md |
| 10 | MOD-0032 API Gateway (thin registry / provider mode) | 02_shared_service_contracts_output.md, 03_ownership_map_output.md, 04_database_migration_standards_output.md, 06_environment_commands_output.md, 08_api_event_standards_output.md, 10_open_items_decision_log_output.md |
| 11 | MOD-0035 Event Bus / Message Queue (lightweight internal seam) | 02_shared_service_contracts_output.md, 08_api_event_standards_output.md, 10_open_items_decision_log_output.md |
| 12 | MOD-0037 Integration Monitoring & Reconciliation (deferred) | 02_shared_service_contracts_output.md, 10_open_items_decision_log_output.md |
| 13 | MOD-0041 Logging & Monitoring (lightweight telemetry seam) | 01_repo_map_output.md, 06_environment_commands_output.md, 08_api_event_standards_output.md, 10_open_items_decision_log_output.md |
| 14 | MOD-0042 Alerting & Incident Runbooks (deferred) | 02_shared_service_contracts_output.md, 10_open_items_decision_log_output.md |

## Operating rule
- Open only the control documents listed for the active batch.
- Keep the batch prompt, domain package, and module pack aligned.
- Do not drag unrelated control material into a batch unless the batch uncovers a blocking dependency.
