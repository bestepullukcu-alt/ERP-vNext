# MVP6-SHIPMENT-PORT-DISPOSITION-01 — SOP §22

## 1. Verdict

**DISPOSITION READY FOR OWNER REVIEW; SHIPMENT SHARED INTEGRATION REMAINS BLOCKED.**

The reported conflict is real at the permanent configuration/allocation
level. It is not a currently running-process collision: read-only inspection
found no listener on `5061`. `Diten.CrmService` and
`Diten.SupplyChainService` both bind `5061` when launched, while 35 current CRM
gateway descriptors also target `5061`. Both services cannot compose on one
host with those settings.

## 2. Authority finding

The conflict is between accepted sources, so no lower-level configuration may
silently decide it:

- the ready-for-dev CRM prerequisite pack selected `5061` in July and its
  runtime, routes, fleet scripts and later module packs adopted that value;
- AGENTS.md and the Supply Chain domain decision explicitly assigned `5061`
  to SupplyChain on 2026-09-11 under DCP-009;
- the global ports rule is stale: it currently ends at `5064` and omits active
  SupplyChain/PPM/CRM allocations.

Because module packs outrank AGENTS.md and domain config, the later central
SupplyChain decision does not erase the still-active CRM pack statements. An
explicit owner reconciliation is required.

## 3. Recommended single disposition

Preserve SupplyChain on `5061` and migrate CRM to candidate port `5065`.
This is the narrowest coherent choice because `5062`, `5063` and `5064` are
already assigned, while no tracked launch/gateway/fleet allocation and no
listener were found for `5065`. This report does **not** allocate `5065`; the
recommendation is encoded as an UNAPPROVED exact candidate for one owner
decision.

`CRM-PORT-MIGRATION-CANDIDATE.patch` changes 35 exact files and nothing in
SupplyChain. It reconciles central port records, active Commercial module-pack
port references, CRM launch/comments, all 35 CRM gateway destinations, fleet
scripts and 10 CRM smoke scripts. Patch SHA-256:
`70dfa5b55b7cd255a109b8f1c8fe8b5437888ed4b0cdef6a0ce6f3d4109a0a43`.
Manifest SHA-256:
`826a95cbb2af672395935ce2c95dd82255bd297089b1bbc0effe80c5c8a7ca58`.

## 4. Shipment shared-successor impact

The controlling 12-path Shipment patch used Carrier gateway preimage
`239e1903...`. CRM migration produces gateway preimage `7ab90b19...`, so the
old Shipment patch/hash approval cannot transfer. A regenerated, unapplied
successor was produced:

- patch: `ef895ba69fc7eac4c251f4c2b672fb7f325a0ed226f5dbcf88a40a086a69982a`;
- manifest: `bfa84f57d836d2539db221c2ca1f4989cf554023686a473a779d6eee1576aa36`;
- gateway target: `77363833c9ee973db6af6e798141afe9843515a324b4b00b37d86a80709d6618`.

Disposable composition contains 35 CRM routes on `5065` and four Shipment
route descriptors on `5061`. The other 11 Shipment shared paths keep the
controlling package's target bytes. The historical patch was not modified.

## 5. Permanent ports versus evidence ports

Permanent port records govern launch profiles, fleet scripts and gateway
destinations. Lane-specific free ports remain valid for isolated evidence when
passed through configuration. Such an evidence port does not resolve or amend
the permanent allocation. SupplyChain probes that deliberately use canonical
`5061` remain unchanged; CRM smoke settings move only if the owner approves
the CRM migration.

## 6. Verification performed

- exact source/hash inventory and chronology review;
- read-only listener checks for `5061` and candidate `5065`;
- parsed current/Carrier predecessor gateway: 35 CRM descriptors on `5061`;
- disposable application of the 35-file migration: 0 byte mismatches;
- JSON parse and route-count validation;
- disposable application of the regenerated 12-path Shipment successor: 0
  byte mismatches;
- composed topology check: CRM `5065`, Shipment `5061`.

No service was started or stopped. No runtime/build acceptance is claimed.

## 7. ASSUMPTION entries

- **ASSUMPTION PORT-A1:** `5065` is the preferred next permanent local port
  because the tracked active registry occupies `5062`–`5064` and no `5065`
  allocation/listener was found. Owner approval must convert this into policy.
- **ASSUMPTION PORT-A2:** CRM business semantics do not depend on the numeric
  port; the candidate changes port references only. Runtime verification must
  falsify this after application.
- **ASSUMPTION PORT-A3:** The Carrier predecessor remains the required shared
  baseline. Any preimage drift stops application and requires new hashes.

## 8. Remaining gates

1. Explicit approval of the exact CRM `5065` migration candidate.
2. Single integration owner applies it with all 35 preimages matching.
3. Production/configuration checks prove CRM `5065` and SupplyChain `5061`
   can run together; gateway routes reach the intended service.
4. Separate approval and application of the regenerated Shipment 12-path
   successor.
5. Only then may the controlling package reassess UI DEV dispatch.

No runtime DEV GO, port assignment, gateway application, commit, push or stash
was performed.
