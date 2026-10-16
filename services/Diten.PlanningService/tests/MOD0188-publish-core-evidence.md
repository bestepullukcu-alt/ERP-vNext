# MOD-0188 internal publish core — test evidence (2026-10-07)

This is an internal MOD-0188 producer slice. It does not publish or freeze Demand v2,
deliver an event, expose a route, verify live Platform/MDM assignments, or close
AC-D17–D19. The production `IInternalPublishAuthority` adapter remains fail-closed.

## Canonical snapshot checksum

Scheme: `MOD0188-CANONICAL-1/SHA-256`. For each field below, encode a little-endian
signed 32-bit byte count followed by that many UTF-8 bytes. No separators or BOM.
GUIDs use lowercase `D`; dates use invariant `yyyy-MM-dd`; timestamps use UTC
round-trip `O`; decimal quantities use invariant `G29`; integer values use
invariant base-10. Hash the concatenated byte sequence with SHA-256 and display
uppercase hexadecimal. Physical Mongo `_id`, document order, lifecycle state,
and lifecycle `stateVersion` are excluded, so superseding cannot change the
content checksum.

The field order is:

1. Scheme marker, TenantId, LegalEntityId, revisionId, planningCycleId,
   planningPeriodKey, as-of date, calendar ID/version, time zone, horizon
   start/end, content version, expected part count and expected row count.
2. Creator ID; count and sorted IDs of all significant manual editors; reviewer
   ID, review time and reason; publisher ID and publish time.
3. Each of the 52 weeks in numbered order: number, local start and local end.
4. Exclusion count, then exclusions sorted by SKU GUID and ordinal Warehouse:
   SKU, Warehouse, reason, actor and time.
5. Parts sorted by SKU GUID and ordinal Warehouse. Each part contributes SKU,
   Warehouse and base unit, followed by its 52 rows in numbered order. A row
   contributes number, local start/end, value kind, quantity, source,
   manual reason, manual actor and manual time.

Before hashing, the producer checks exact 52 week boundaries, selected-series
identity and scope, one physical part per selected series, exact part/row
counts, known nonnegative quantities (including real zero), unit, and row
uniqueness/order. The internal snapshot reader recalculates this checksum and
checks lifecycle state against the mandatory Draft publication audit. Missing,
duplicate, mismatched or corrupted content yields a closed error, never a
partial snapshot. The future central v2 contract must review this technical
scheme before freezing; this note is not a central contract decision.

## Real Mongo verification

An owned temporary MongoDB 7.0.43 single-member `rs-mod0188` ran only on
`127.0.0.1:31994`; tests used the explicit URI
`mongodb://127.0.0.1:31994/?replicaSet=rs-mod0188` and the shared
`mod0188_tests` database with a new TenantId for each test. No operational
27017–27021 port was used. Test cases cover same-period replacement across
cycle/calendar/as-of changes, overlapping distinct periods, competing cycles,
same-key races and later replay, permission/scope/actor separation, multi-part
52-week snapshot, corrupt part/row, stale version, audit/outbox failure rollback,
and historical Superseded content.

Focused real-Mongo tests: **10 passed, 0 failed, 0 skipped**. Full MOD-0188
suite: **228 passed, 0 failed, 0 skipped**. API build: **0 warnings, 0 errors**.
The local .NET 8 SDK was used with an alternate intermediate-output path.
The owned `mongod` PIDs 2488 and 35572 were each checked absent after their
separate runs; port 31994 was checked free and both unique temporary data/log
folders absent. Cleanup used only those owned PIDs and folders.

Sensitivity check: temporarily adding `planningCycleId` to the official period
key let two simultaneous cycles both become Published. The targeted race test
failed with two Published results. The one-line sabotage was immediately
reverted; the same targeted test and the complete suite then passed. No
weakened line remains in the source.

This core accepts the currently persisted manual Draft shape; method-generated
weeks still need their own verified provenance before they can be published.
The test authority is a fixture. Live actor–LegalEntity assignment, Demand
publish permission and SKU/Warehouse data scope still require the approved
Platform/MDM contract and integration evidence. No public publish API or event
delivery exists in this slice.
