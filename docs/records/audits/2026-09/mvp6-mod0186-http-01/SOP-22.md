# MVP6-MOD0186-HTTP-01 — SOP §22 runtime evidence

Date: 2026-09-21  
Lane: Returns evidence DEV / integration runtime  
Verdict: **PARTIAL / REWORK**

## Scope and authority

This run verifies the accepted Returns core through the composed SupplyChain API. It does not grant rollout,
gateway/canonical/guard changes, worker or publisher activation, stock writes, commit, push, or full-module acceptance.
The controlling promoted MOD-0186 pack SHA-256 is
`1c80cca70ef774d18f5d30174566e68c9b6332b61dd1f564d54de3429886482f`.
The published Returns annex is bound by the 97-entry transfer manifest; the Shipment root annex SHA-256 is
`7d1327a12b9775a594631dd9eb3c3c4c90e7f8429581f9ff7f42dde419f7b8af`.

## Source → binary → process

- Clean base HEAD: `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- Full composed source snapshot: 341/341 verified,
  archive SHA-256 `a03f9d593e97c6d37052f540bbf25a08bfe0de004227502359454924f0998ac3`.
- Exact Returns/Claims/composition overlay: 97/97 verified,
  archive SHA-256 `eaf786e647ef019f862c7dcc4f0b2ab29b2b5a36ff1bee98d2cca8d2863cc766`,
  manifest SHA-256 `4ae0f286479761512f50216e5bd8be721e48ae4839c062b57cfdd29696e2daa8`.
- Program.cs SHA-256: `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c`.
- Fresh Release build: PASS, 0 warnings / 0 errors.
- API binary SHA-256: `0b130a3aa7454d1064e09590ebab3fd601cb85c6b7c9b06df9e5503cf2766f6d`.
- Real Kestrel/JwtBearer process: `127.0.0.1:51861`; pre-restart PID 77836, post-restart PID 78811.
- DB-010 environment: isolated MongoDB 8.0.18 replica set `returns_http_01` on `127.0.0.1:27186`,
  isolated database `returns_http_01_db`. Operational Mongo on 27017 and Claims Mongo on 27892 were untouched.

The API used `Returns:ReferenceBaseUrl=http://127.0.0.1:51861/`. Every successful fresh create therefore read
the real composed `GET /api/shipment-bundle/shipments/{shipmentId}` endpoint. The process log contains 92 actual
Shipment-detail outbound reads and zero Inventory/Warehouse outbound HTTP lines.

## Result

The coherent main run recorded 51 checks: 50 PASS and one exact REWORK. The separate new-process restart run
recorded 4/4 PASS. Real JWT/RBAC, tenant/LE isolation, soft-delete invisibility, create/list/transition,
idempotent replay and changed payload, UUID value equality and root-before-fingerprint precedence, numeric and
instant fingerprint equivalence, UoM/snapshot drift, 6+6 and 4+6 concurrency, release rules, five transaction
write-boundary rollbacks, unknown-commit retry/recovery, client response-loss replay, persistence after restart,
and Pending-only outbox were exercised through HTTP and inspected in Mongo.

The local outbox remained `19 Pending / 0 non-Pending` across restart. No Inventory/Warehouse/stock collection
was created. No Inventory or Warehouse HTTP request occurred. Received produced `EvidenceType=manual-assertion`
and preserved the opaque empty Inventory reference without validation or stock claim.

## Exact REWORK

For a Shipment document whose persisted `LifecycleCorrelationId` is malformed, the real Shipment detail endpoint
returns `500 SHIPMENT_ROOT_INVALID`. `ReturnReferenceReader` maps every dependency 5xx response to
`503 DEPENDENCY_UNAVAILABLE`. The approved Returns acceptance expects the consumer malformed-root condition as
`502 RETURN_SHIPMENT_ROOT_INVALID` when a malformed root reaches the wire.

Observed composed path:

`malformed persisted root → Shipment 500 SHIPMENT_ROOT_INVALID → Returns 503 DEPENDENCY_UNAVAILABLE`

Expected Returns classification when the malformed value is observable:

`malformed wire root → Returns 502 RETURN_SHIPMENT_ROOT_INVALID`

This is preserved as REWORK rather than reclassified as PASS. No source fix, waiver, or repository PASS was made.
The direct producer response and the corresponding Returns response are both retained in the raw archive.

## No-change and teardown

Both source inventories reverified after runtime execution (341/341 and 97/97). The registered integration
worktree status is byte-for-byte equal to its handoff `target-status-after-transfer.txt`; exit code 0 is retained.
No product/test source, Program.cs, Claims/shared, gateway, canonical/guard, git index, commit, push or stash changed.
Both lane listeners were stopped; final API and Mongo listener captures are empty.

This verdict is only the MOD-0186 composed HTTP/DB acceptance result. It does not establish producer rollout,
gateway uptake, E5/G5, release, or full-module acceptance.
