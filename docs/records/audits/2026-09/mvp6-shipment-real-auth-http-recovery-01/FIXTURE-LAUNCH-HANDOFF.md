# Secret-free browser/evidence handoff

This handoff does not close browser or PNG acceptance.

## Reproducible identities

- Tenant: `00000000-0000-0000-0000-000000000001`
- Legal entity: `10000000-0000-0000-0000-000000000001`
- Full Shipment actor: `john.doe.def@diten.com`
- Read-only actor: `charlie.brown.def@diten.com`
- Shipment under test: `0857dd87-b133-4b2e-8d90-92ed4aa0bcbd`

The lane fixture creates tenant membership, Platform assignments, one active MDM legal entity, and exact Shipment permissions. These are isolated test data; they are not permission or policy changes. Credentials, JWT keys, access tokens, refresh tokens, and bearer values are deliberately absent.

## Launch order and lane endpoints

1. Start a DB-010 replica set and wait for PRIMARY.
2. Seed Auth, Platform, MDM, and SupplyChain lane databases.
3. Start MDM, Auth, Platform, then SupplyChain.
4. Obtain a new session through `POST /api/tenant-auth/login` with `X-Tenant-Id`; do not inject or reuse a bearer from this archive.
5. Route subsequent UI traffic through the separately approved Gateway/browser lane.

Evidence-lane ports used here: Mongo `40994`, Auth `5756`, Platform `5757`, MDM `5759`, SupplyChain `5761`. These are disposable evidence ports, not permanent allocations. Database names are recorded in `raw/config/effective-config-redacted.json`.

## Cleanup

Stop the four service processes, stop only the lane-owned Mongo process, confirm all five ports are free, and delete ephemeral token/secret files. Preserve the evidence archive and checksums.
