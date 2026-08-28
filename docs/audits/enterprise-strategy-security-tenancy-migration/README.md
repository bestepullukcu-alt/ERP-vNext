# Enterprise Strategy Security, Tenancy and Migration Evidence

Status: **GOVERNANCE PREPARATION ONLY / NOT EXECUTED**

This directory is reserved by the bounded FU01 `READ-ONLY-INVENTORY / NON-MIGRATING / NON-RUNTIME /
NO-WRITE` amendment. No target data copy has been designated or inspected by this checkpoint, and no measured
inventory result is claimed.

## Future exact artifacts

- `collection-inventory-v1.json` — machine-readable measurement generated only by an explicitly authorized
  manual run.
- `collection-inventory-v1.md` — human review ledger binding each collection to a named mapping owner,
  ownership class and disposition.

Neither file is created as a placeholder. Their absence means the inventory gate is open.

## Manual target-copy gate

The future manual runner must refuse to construct a Mongo client unless all variables are present:

- `ESBP_INVENTORY_MONGO_URI`
- `ESBP_INVENTORY_DATABASE`
- `ESBP_INVENTORY_TARGET_COPY_ID`
- `ESBP_INVENTORY_RUN_ID`
- `ESBP_INVENTORY_ACKNOWLEDGE_READ_ONLY_TARGET_COPY=I_ACKNOWLEDGE_READ_ONLY_TARGET_COPY`

The URI is secret-bearing runtime input and must never appear in committed evidence, logs or snapshots. A
local listener or a database named `DitenEnterpriseDb` is not automatically an approved target copy.

## Allowed measurement

- collection names;
- exact `countDocuments` counts;
- existing `listIndexes` metadata;
- bounded field-name/BSON-type prevalence;
- deterministic digests of the allowed metadata.

Raw documents, field values and sensitive samples are not evidence outputs.

## No-write invariant

The runner may not insert, update, replace, delete, bulk-write, create/drop/rename collections, create/drop
indexes, execute `$out`/`$merge`, start transactions, call `MongoDbIndexConfigurations.EnsureIndexesAsync`,
call any ES `*MongoMigration.EnsureAppliedAsync`, or resolve `DbInitializer`. It is not registered in service
startup, dependency injection, an API, a hosted worker or automatic test discovery.

An unknown collection is recorded as `unreviewed` and blocks later migration work. It is never silently
assigned to Enterprise Strategy or to a default tenant.
