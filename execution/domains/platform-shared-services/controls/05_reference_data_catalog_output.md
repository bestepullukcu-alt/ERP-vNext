# 05_reference_data_catalog_output.md — Reference Data Catalog (Platform)

**Status:** Ready baseline

| Reference set | Description | Example values | Owning module | Used by |
|---|---|---|---|---|
| Permission scopes | Normalized permission keys | `platform.rbac.read`, `platform.audit.read`, `platform.docs.write` | MOD-0018 | all |
| Workflow states | Approval lifecycle states | `draft`, `published`, `pending`, `approved`, `rejected` | MOD-0023 | workflow/inbox |
| Task states | Operational task lifecycle | `open`, `in_progress`, `done`, `cancelled` | MOD-0024 | tasks |
| Document types | Document categories | `Invoice`, `Contract`, `Evidence`, `Template` | MOD-0028 | docs |
| Evidence status | Evidence completeness states | `Missing`, `Partial`, `Complete` | MOD-0031 | evidence |
| Policy taxonomy | Policy/control categories | `Control`, `Standard`, `Policy` | MOD-0005 | policy |
| Waiver status | Waiver lifecycle | `Requested`, `Approved`, `Expired` | MOD-0006 | waivers |
| Integration status | Runtime/integration status | `Healthy`, `Degraded`, `Failed` | MOD-0037 | ops |
| Observability severity | Logging/alert severity | `Info`, `Warning`, `Error`, `Critical` | MOD-0041/0042 | observability |
| Deployment mode | Platform provider mode | `Deferred`, `NativeInternal`, `ExternalProvider` | Platform control pack | agent / decisions |

## Notes
- Keep taxonomies small in MVP.
- Do not bind reference data to business-domain semantics unless explicitly approved.
- Where current repo behavior is weaker than target-state taxonomy, use the target-state set for new Platform modules.
