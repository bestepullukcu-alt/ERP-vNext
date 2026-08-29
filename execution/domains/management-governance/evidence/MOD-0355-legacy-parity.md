# MOD-0355 Legacy Enterprise Strategy / Management Governance Parity Evidence

Status: `PARTIAL — DEFAULT-OFF / NON-PRODUCTION`

This inventory is comparison evidence only. It does not authorize redirect, migration, retirement or deletion.
The generic Management Governance shell and placeholders remain byte-preserved and `QUARANTINE`.

| Old URL | Old entities | Fields | Commands/actions | Lifecycle | Permissions | Integrations | New owner/module | New URL | Field mapping | Disposition | Migration requirement | Browser evidence |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `/management-governance/subdomains/business-process-management` | `ManagementGovernanceSubdomainSummary` prototype | name, description, health, tags | open placeholder module | prototype status only | hard-coded prototype flags | mock frontend adapter | MOD-0355 | `/management-governance/process-modeling/models` | no authoritative entity mapping | Replaced | none until explicit user acceptance | old route preserved; new route default-off `503` evidence pending authenticated browser run |
| `/management-governance/modules/mod-process-architecture` | generic module placeholder | name, description, owner, risk | generic approve/assign/escalate presentation | `Active` prototype label; not lifecycle truth | hard-coded prototype flags | mock frontend adapter | MOD-0355 | `/management-governance/process-modeling/models` | no persisted process architecture fields exist in legacy page | Missing | authoritative data migration not authorized | absence recorded; page-by-page screenshot pending |
| `/management-governance/modules/mod-process-design` | generic module placeholder | name, description, owner, risk | generic approve/assign/escalate presentation | `Active` prototype label; not model-version lifecycle | hard-coded prototype flags | mock frontend adapter | MOD-0355 | `/management-governance/process-modeling/models/{id}` | no model/version graph fields exist in legacy page | Missing | authoritative data migration not authorized | absence recorded; editor remains read-only/default-off |
| `/management-governance/modules/mod-process-performance` | generic module placeholder | name, description, owner, risk | placeholder navigation | `Monitor` prototype label | hard-coded prototype flags | mock frontend adapter | future process-performance owner | none in this slice | none | Out of Scope | none | protected placeholder remains available |
| `/management-governance/modules/mod-process-improvement` | generic module placeholder | name, description, owner, risk | placeholder navigation | `Active` prototype label | hard-coded prototype flags | mock frontend adapter | future process-improvement owner | none in this slice | none | Out of Scope | none | protected placeholder remains available |

## Browser acceptance still required

- Open the old and new routes on different ports and compare each row above.
- Capture loading, empty, populated-read, Draft, Review, Published, Retired, `403`, `404`, `409` and `503` screenshots and network logs.
- The current bounded implementation can only prove the safe default-off `503`/zero-read state because no Gateway route is authorized.
- Explicit user acceptance is required before any `full parity complete`, redirect, migration, retirement or deletion decision.
