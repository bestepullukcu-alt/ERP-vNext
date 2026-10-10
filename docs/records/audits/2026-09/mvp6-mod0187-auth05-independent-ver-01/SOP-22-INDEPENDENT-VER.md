# SOP §22 — MVP6-MOD0187-AUTH-05-INDEPENDENT-VER-01

Date: 2026-09-21  
Role: AUTH-05 implementation authorundan bağımsız verifier  
Mode: isolated runtime verification; product/source fix yok

## Verdict

**PARTIAL — bağımsız fresh E2/E4 zinciri başarılı, fakat exact R01–R30 acceptance tamamlanmış değildir.**

Gözlenen bağımsız koşularda ürün hatası çıkmadı: fresh restore/build, exact 120 Claims core testi, authenticated HTTP akışı, JWT/RBAC negatifleri, tenant/LE enforcement, gerçek Shipment detail uptake, idempotent replay/conflict, concurrency, dependency refusal, Mongo scoped zero-write ve restart başarılıdır. Ancak controlling `runtime-acceptance-R01-R30.md` satırlarının çoğu daha geniş ve exact E4 gözlemler ister. AUTH-05'in tüm satırları `PASS`/`PASS bounded` yapan özeti bu verifier tarafından kabul edilmemiştir. R01–R26 `PARTIAL`; R27–R30 `PASS` olarak kalır. Açık kabul gözlemleri ürün arızası olarak yeniden etiketlenmemiştir.

Bu verdict CT acceptance, full-module acceptance, rollout, E5 veya G5 değildir.

## Preconditions and exact inputs

| Input | Independent result |
|---|---|
| AUTH-05 raw archive | SHA-256 `828143e8a0b81799f1388f6502aa378b73ad9f76bc2696496dcb64311243efad`, exact match |
| AUTH-05 source manifest | SHA-256 `4ae0f286479761512f50216e5bd8be721e48ae4839c062b57cfdd29696e2daa8`, exact match |
| Source archive | `integration-source-baseline.tar.gz`, SHA-256 `eaf786e647ef019f862c7dcc4f0b2ab29b2b5a36ff1bee98d2cca8d2863cc766`, exact match |
| Writer complete | AUTH-05 manifest `writerComplete:true`; upstream integration handoff `writerComplete:true` |
| Published YAML | `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c` |
| Claims annex | `16e65c26faeb53887607dd16de0de34bad61dcc89d3beb7f6b8adca0ec4eeb63` |
| Returns annex | `00990a289069d25a62f7c883aac718e08b96b0386a572e7d1f93f0e447e98a11` |
| DEV v2.0 | `6af5ca63fdd8d94a1dd2ad3eadab5803fc21931ce929124152486430649051ac` |
| VER v2.0 | `6419f090e3ea188ceea531fefb7424ff62eac9edeb4df16652c4a5239301354c` |

The disposable verifier source was `/private/tmp/mvp6-mod0187-auth05-ind-ver-dOZkrG/source-fresh`. It was copied without Git metadata, `bin`, `obj`, TestResults or prior evidence, then overlaid with the immutable source archive. Initial and final checks matched all 97 source entries. Claims contributes exactly 47 entries; Returns 46; Program.cs and three published contract/annex inputs complete the 97-entry binding.

## Fresh source → binary → process → HTTP/DB chain

- Restore exit `0`.
- Build exit `0`; 0 warnings, 0 errors.
- Fresh API DLL SHA-256: `3138a53a4eec057786afb2a32f1b5e73da189338a968072e38a368d338629368`.
- Fresh test DLL SHA-256: `156514e2f35d4fdf4adc1f892a93b2a4614d4b9d6f4bf06006c6f19d0651b4b5`.
- Program.cs before/final SHA-256: `a72a05a5e185e04d70ba221f086baacb8c8fca11ea061f289cb4b54fd430254c`.
- Exact Claims filter: 120 passed, 0 failed, 0 skipped.
- Lane API: `127.0.0.1:5067`; lane Mongo: `127.0.0.1:27917`, replica set `claims_auth05_ind`, fixed DB `diten_claims_auth05_ind`.
- Fresh process PIDs were `86391` (main), `87173` (dependency refusal), `87668` (restart), and the short-lived JWT-negative process recorded in raw evidence. Every process command points to the fresh DLL above.
- Health was `200` for each applicable launch. All API processes and Mongo were stopped; both listeners were absent at cleanup.

The first test command used an over-broad `FullyQualifiedName~Claims` filter. It selected Carrier/Loads methods whose names contain “Claims” and the explicitly restart-only Claims test, giving 121 passed / 6 failed. This is retained as a harness/filter failure. The corrected exact namespace filter produced the required 120/120 result without source or test edits.

## Independent runtime observations

- Authorized list `200`; missing/malformed bearer `401`; missing Claims permission `403`; tenant header/token mismatch `403`.
- Real persisted Shipment detail `200`; its authoritative `lifecycleCorrelationId` was consumed by Claim create.
- Create `201`; identical same-root replay `201`; changed valid payload `409 IDEMPOTENCY_KEY_REUSED`; Open→Investigating `200`; list-after `200`.
- Restart used the same freshly built DLL and same DB: producer detail/list/replay `200/200/201`, same Claim identity, and unchanged four-collection counts.
- Eight concurrent same-key creates produced one Claim, receipt, audit and outbox record. Eight different keys on one Shipment produced eight independent 8/8/8/8 records.
- Missing transition action permission returned `403` with unchanged scoped counts.
- Closed dependency port returned `503 CLAIM_REFERENCE_UNAVAILABLE`; fresh scope deltas were Claims/receipts/audit/outbox `0/0/0/0`.
- Final database evidence showed unique tenant+LE Claim-number and receipt-tuple indexes. Claims outbox statuses were only `Pending` through restart. No Claims publisher/transport/finance path was registered.

## Boundary that controls this verdict

The 120 core tests are executable component/Mongo evidence and the probes are selected live E4 evidence. They do not automatically satisfy every literal in the controlling RT matrix. Examples left visible in `R01-R30.md` include all eight Shipment statuses over live mock HTTP, complete Carrier wire cases, all 49 lifecycle pairs via real HTTP, 20-way races, transition replay/root precedence over HTTP, all four independently injected write-stage failures at the process boundary, and unknown-commit recovery after restart.

No source, Program.cs, Returns implementation, canonical, guard, pack, gateway or Git state was changed by this verification. Persistent output is restricted to this audit directory.

