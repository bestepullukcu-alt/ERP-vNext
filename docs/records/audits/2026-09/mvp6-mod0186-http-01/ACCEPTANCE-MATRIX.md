# Returns HTTP acceptance matrix

| AC | Verdict | HTTP/DB evidence |
|---|---|---|
| R01 producer/root | **REWORK** | Required root missing/null returned 503. Real valid Shipment detail supplied exact persisted UUID. Malformed persisted root produced Shipment 500 then Returns 503; expected consumer malformed classification is 502 when malformed wire value is observable. |
| R02 quantity/concurrency | PASS | UoM mismatch 422; sequential 6+6 exceeded; simultaneous 6+6 produced one 201/one 422; simultaneous 4+6 produced two 201. |
| R03 contribution/release | PASS (bounded) | Rejected released entitlement once and a new quantity-10 return succeeded; Closed and soft-deleted aggregate did not create an implicit release. |
| R04 exact quantity/fingerprint | PASS | `2` and `2.0` replayed the same receipt; zero/negative coverage remains in accepted core suite, not double-counted here. |
| R05 manual Received | PASS | Actual Authorized→InTransit→Received returned 200; DB audit has `EvidenceType=manual-assertion`; missing target grant returned 403. |
| R06 opaque reference/no external writes | PASS | Empty/null optional references accepted in applicable transitions; 0 Inventory/Warehouse HTTP; no Inventory/Warehouse/stock collection. |
| R07 lifecycle/concurrency | PASS (HTTP sample + core matrix) | Exact InTransit→Cancelled returned 422; valid chain passed. The accepted 68 core lifecycle matrix is referenced, not counted as HTTP evidence. |
| R08 JWT/RBAC/scope/precedence | PASS | Real signed HS256 JWT Bearer: anonymous 401+challenge, missing grant 403, header mismatch 404, matching foreign tenant/LE lists 200 with total 0, root mismatch precedes payload drift. |
| R09 replay/restart/unknown commit | PASS | Create and transition replay retained original result; equivalent instant replay passed; new Kestrel PID replayed the durable 201 receipt; one injected unknown commit response retried to 201 and replayed; client-discarded response body recovered by replay. |
| R10 atomicity/outbox/drift | PASS | Failpoint after each of five insert positions returned 503 with identical before/after totals in all five scoped collections. Drift returned 409. Restart preserved 19 Pending and 0 non-Pending events. |
| R11 evidence/bindings | PASS | 341/341 + 97/97 post-run hashes, fresh build, binary hash, two process PIDs, 55 redacted HTTP request/response records, Mongo state and process logs retained. |

The single R01 REWORK controls the overall **PARTIAL / REWORK** verdict. Core/model PASS is not used as a
substitute for HTTP/JWT/concurrency evidence, and overlapping core checks are not added to the HTTP count.
