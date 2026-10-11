# Q313 — PII in the intake collections: measurement and options for CT

Reading date 2026-10-03, branch `feature/mvp6-logistics` @ `4a8d4d4b3`. Code and document reading only. Nothing was
changed, run, or read from a database, and no PII value is reproduced here. **This is a recommendation. CT rules.**

Paths are under `services/Diten.SupplyChainService/src/` unless written in full.
`Coordinator` = `Diten.SupplyChainService.Application/Features/SourceIntake/WarehouseIntakeCoordinator.cs`.

## 1. What was measured

### Q1 — which states write a snapshot (`EVIDENCE-WRITES.tsv`)

There is **one** call into `RecordEvidenceAsync`: the local `Block` at `Coordinator:68-73`. It always stores
`{"code", "evidence"}` (`Coordinator:70-71`). Eleven call sites feed it.

| payload | sites | carries PII |
|---|---|---|
| **Full canonical Warehouse snapshot** | `SOURCE_ID_MISMATCH :33`, `SOURCE_DRIFT :38`, `SOURCE_NOT_READY :40`, `EMPTY_SOURCE_LINES :42`, `SHIPMENT_MAPPING_INCOMPATIBLE :51`, `SOURCE_DRIFT :54` | yes — recipient and full address |
| Correlation IDs only | `CONFLICTING_CORRELATION :28`, `CONFLICTING_PERSISTED_ROOT :56` | no |
| Raw untrusted `upstreamCorrelation` string | `INVALID_UPSTREAM_CORRELATION :23` | not by classification; unvalidated input stored verbatim |
| Dependency error code + status | `:30` | no |
| Exception type name | `COMMIT_FAILED :66` | no |

So **six of eleven** evidence states store the whole snapshot, not only drift.

**Widened (stop condition met): the same snapshot is stored in two more collections, and for every intake, not only
failures.**
- `sce_shipment_source_intents.IntentJson` — the serialised `SourceIntent`, whose `Snapshot` is the canonical body
  (`Persistence/Features/Shipments/SourceIntakeStore.cs:29`; `Domain/Features/SourceIntake/SourceIntent.cs:5`;
  `WarehouseShipmentMapper.cs:41,56`).
- `sce_shipment_source_links.Snapshot` — written in the committed shipment transaction
  (`Persistence/Features/Shipments/ShipmentRepository.cs:125-132`).

The evidence collection is the smallest of the three exposures, not the largest. A fix aimed only at
`RecordEvidenceAsync` would leave every successfully intaken address in two other collections.

### Q2 — what is in the snapshot (`SNAPSHOT-FIELDS.tsv`)

The snapshot is the Warehouse `OutboundShipment` body, canonicalised (`WarehouseShipmentMapper.cs:12-29`).

Against the readiness record, line 176 — "Security/privacy: recipient/address/note/evidence references
confidential/PII":
- `shipTo.name` is the **recipient**.
- `shipTo.country`, `city`, `postalCode`, `line1`, `line2` are **address**.
- Identifiers, lines and packages are not listed.

**The schema is open.** The contract sets no `additionalProperties` (`docs/analysis/contracts/warehouse-outbound.openapi.yaml`,
0 matches). The reader checks only for duplicate names (`Infrastructure/SourceIntake/FrozenReadSchema.cs:14-15`).
`Canonical()` serialises everything it receives. Any extra field the producer adds is stored unclassified.

### Q3 — who reads it (`READERS.tsv`)

- **`sce_shipment_source_evidence` has no reader in `src/`** — no repository read, query handler, API route or
  projection. Only `tests/…/SourceIntakeTests.cs:170` and `tests/source_restart_probe.py` read it.
- `source_intents` is read back by `SourceIntakeStore.FindAsync`/`PrepareAsync` (`:16-36`). It is used for the hash and
  the prepared shipment, never returned to a caller.
- `source_links` is read for `Hash` only (`ShipmentRepository.cs:57-59`). Its `Snapshot` is never read.
- Every read is filtered by TenantId and LegalEntityId. No API, gateway, frontend or Platform reader exists.
  **No PII is readable without tenant scope through the application.** The more serious stop condition did not
  trigger.
- No TTL on these collections (`ShipmentSchema.cs:12-22`).
- **`WarehouseIntakeCoordinator` has no caller in `src/`** (F-Q265-2), so no production path writes any of these rows
  today. Only tests do. The exposure is latent: it starts the day intake is wired.

### Q4 — what is required

Readiness record `docs/roadmap/plans/mod-0183-readiness-and-development-prompt.md`:
- `:176` — "Security/privacy: recipient/address/note/evidence references confidential/PII; tokens and full payloads never logged."
- `:177` — "Audit actor, scope, operation, entity, old/new state, command/event IDs and safe failure code; restrict snapshot access."
- `:181` — "Rollback disables ingress/publisher without deleting regulated history; retention follows shared evidence policy, no invented TTL."

Pack `execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md:322`: "Provenance
snapshots remain internal and cannot extend the frozen create DTO." The pack says *internal*; it does not say
*restricted* or *redacted*. The readiness record is a plan, and AGENTS.md §1 puts the pack above it; the pack carries no
privacy line for snapshots.

**The "shared evidence policy" is named and not defined for this module.**
- No document in `docs`, `execution`, `.antigravity` or `AGENTS.md` defines it
  (`grep -rniI "shared evidence policy|evidence policy|evidence retention"`).
- The domain names its backbone as `Audit (MOD-0021)` and `Evidence/Docs (MOD-0029)`
  (`execution/domains/supply-chain-execution/domain-config.md:41`).
  - MOD-0029 is `planned`.
  - MOD-0021's retention covers audit events through Platform APIs (`docs/reference/modules/platform/audit-trail/api.md:19-20`),
    not a SupplyChain-owned Mongo collection.
  - MOD-0031 Evidence Linking is `review / planned` (`execution/registries/module-id-registry.md:114`).
- So there is no policy to follow. The record forbids inventing a TTL.

## 2. Options

### A — redact at write
- **What:** replace the snapshot with a field subset that excludes `shipTo`, plus the full-snapshot hash.
- **Where:** in all three writers, not only `RecordEvidenceAsync`.
- **What it costs drift diagnosis:**
  - Drift is detected by **hash** (`Coordinator:37,53`), so detection is unaffected.
  - What is lost is the ability to see *which* field changed. Diffing two subsets shows changes in identifiers, status,
    lines and packages but not in the address; an address-only drift shows equal subsets with different hashes.
  - That is still an actionable signal: "the address changed" is itself the finding.
- **What it costs replay:** `source_intents.IntentJson` holds the prepared `SourceIntent`. Its `Shipment` already carries
  only an opaque ship-to reference (`WarehouseShipmentMapper.cs:51`), so commit does not need the address. A test that
  reads the link `Snapshot` (`SourceIntakeTests.cs:124-126`, reads `shipTo.line1`) would have to change.
- **What it costs governance:** it changes stored provenance of a regulated flow. The readiness record says history is
  not deleted (`:181`), and a provenance-format change should be an owner decision.
- **Size:** three writers plus a mapper change. Product code, so a separate DEV WP; tests are covered by Q215.

### B — restrict at read
- **What it is:** add the access control `:177` asks for.
- **What has to exist first, and does not:**
  1. A reader to restrict. **There is none.** No route, handler or role reads these collections. Restricting a read
     that nobody performs closes nothing.
  2. A permission for snapshot access. No such key exists (`Infrastructure/Authorization/ShipmentPermissions.cs`
     holds read/create/dispatch/pod.capture/cancel/reconcile).
- **What it can still mean today:** database-level control, a separate collection or database with narrower
  credentials, or field-level encryption of `shipTo`. Each needs an infrastructure owner and a key-management decision.
  None is defined in this repository.
- **Cost:** high, and it leaves the data in place in three collections.

### C — retention
- **No policy exists to follow** (Q4), and the record forbids inventing a TTL.
- A TTL would also contradict "without deleting regulated history" (`:181`) unless the policy classifies these rows as
  non-regulated.
- **This option is blocked on an owner or Platform decision about which policy (MOD-0021, MOD-0029, MOD-0031) governs
  SupplyChain provenance.**

### D — not in the dispatch: gate on the wiring, decide before it lands
- Nothing writes these rows in production today (Q3).
- The exposure begins when `WarehouseIntakeCoordinator` gets a caller.
- So the cheapest correct step is to **record the gate**: intake is not wired until A, B or C is decided.
- Cost: a ledger row and a pack note. No code.
- Risk: it only holds if whoever wires intake reads the gate.

### E — not in the dispatch: close the open schema first
- Whatever A–C decide, an open `OutboundShipment` lets unknown fields flow into all three collections unclassified
  (Q2).
- Rejecting or dropping undeclared properties at `FrozenReadSchema.Warehouse`, or canonicalising only declared fields,
  bounds what can ever be stored.
- Cost: small product change. It may conflict with forward compatibility of WAREHOUSE-OUTBOUND, which is the producer
  owner's call.

## 3. Recommendation (CT rules)

1. **Take D now.** Record that Warehouse intake must not be wired until the snapshot question is decided. It is free,
   and today is the last point where no production row exists to migrate.
2. **Then take A as the target, applied to all three collections at once.**
   - Keep the full-snapshot hash, so drift detection and replay are unchanged.
   - Drop `shipTo`, since commit uses only the opaque ship-to reference.
   - Doing it before any production data exists avoids a migration of regulated history.
   - Pair it with E, so the field subset is a whitelist, not a blacklist.
3. **Do not pursue B or C now.** B restricts a read nobody performs. C has no policy to follow, and inventing one is
   forbidden.
4. Either way, **do not answer this with R-04.** R-04 measured logs; this is storage, and the two do not overlap.

Owner input needed for A: whether a provenance record of a regulated flow may omit the address it was made from.
