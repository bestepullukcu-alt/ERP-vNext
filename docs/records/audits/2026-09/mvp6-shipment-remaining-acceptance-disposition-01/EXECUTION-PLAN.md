# Narrow execution plan

## Common fixture and process boundary

Use one fresh combined A+B source manifest, native .NET 8 Release binaries, an isolated Mongo replica set on a non-27017 port and lane-owned Auth/Platform/MDM/SupplyChain/Gateway/Web ports. Create distinct real-Auth identities and browser profiles; do not swap cookies inside one session.

Common identities:

- `actor-a`: tenant `T1`, legal entity `LE-A`, exact read/dispatch/cancel/POD permissions required by the scenario.
- `actor-b`: tenant `T1`, legal entity `LE-A`, same scenario permissions but a different actor/session.
- `actor-le-b`: tenant `T1`, legal entity `LE-B`, same permission names and no access to LE-A rows.

Every mutation records the exact shipment ID, persisted lifecycle root, payload hash, idempotency key, request correlation, response correlation and before/after counts for shipment, lifecycle/history, POD/receipt, audit and outbox.

## PLAN-A08 — stale transition race

Fixture: create `S-A08` in `Draft`, persist authoritative root `R-A08`, and confirm both actor-a and actor-b initially render `Draft` from independent browser profiles.

1. Both sessions open the same detail before either mutation and obtain the same business root.
2. Actor-a submits `Draft → Planned` with intent key `K-A08-A`; expect success, one lifecycle/audit/outbox delta and state `Planned`.
3. Without refreshing, actor-b submits its already-open `Draft → Planned` choice with distinct key `K-A08-B` and the same root.
4. Expect HTTP 422 and exact `INVALID_SHIPMENT_TRANSITION`; UI must show no success, retain/display the returned support correlation, and trigger detail/list refresh.
5. After refresh actor-b sees `Planned` and only the current allowed choices. Counts after actor-b remain identical to the post-actor-a counts.

The two requests may be synchronized with a browser barrier, but the controlling order is actor-a response committed before actor-b release. This proves stale-browser handling, not simultaneous backend scheduling.

## PLAN-A09-409 — duplicate stale POD

Fixture: create and advance `S-A09-DUP` to `Dispatched`, with `DispatchedAt` before both POD timestamps and authoritative root `R-A09-DUP`.

1. Actor-a and actor-b independently open the detail and POD modal before any capture.
2. Actor-a submits POD payload `P-A` with key `K-A09-A`; expect success, `Delivered`, one POD/receipt and corresponding lifecycle/audit/outbox records.
3. Actor-b submits its stale modal with a different key `K-A09-B`.
4. Expect HTTP 409 `POD_ALREADY_CAPTURED`, no false success and no second POD/receipt/lifecycle/audit/outbox delta.
5. UI refreshes to `Delivered`, renders the authoritative POD and removes the POD action.

## PLAN-A09-422 — stale eligibility POD

Fixture: create and advance `S-A09-STATE` to `Dispatched`; both sessions open detail and actor-b opens the POD modal.

1. Actor-a transitions `Dispatched → Exception` with key `K-A09-STATE-A`; expect success and one transition delta.
2. Actor-b submits the already-open POD modal with key `K-A09-STATE-B`.
3. Expect HTTP 422 `INVALID_SHIPMENT_TRANSITION`, zero POD/receipt delta and no mutation beyond actor-a's transition.
4. UI displays the exact code/support trace, produces no success state, refreshes detail and removes POD eligibility for `Exception`.

## PLAN-A12 — cross-LE browser isolation

Fixture: actor-a creates `S-A12` in `T1/LE-A`. Create a separate unknown UUID and one soft-deleted LE-A fixture for comparison. Actor-le-b uses a distinct browser profile and a real Auth token for `T1/LE-B` with the same permission names.

1. Actor-a opens list and detail and records the positive row, root and allowed action surface.
2. Actor-le-b opens the exact LE-A detail URL. Expect the same localized safe-not-found page used for unknown/deleted IDs: no shipment number, status, root, source, lines or action controls; only the permitted support reference.
3. Actor-le-b list must not contain `S-A12`.
4. Through the same-origin adapter, actor-le-b attempts one transition and one POD request against `S-A12` using syntactically valid headers/body/key. Expect safe 404 `SHIPMENT_NOT_FOUND`, not 403/422/409, with no existence-bearing details.
5. Compare normalized DOM and error payloads for cross-LE, unknown and deleted cases; only correlation values may differ. Verify zero scoped and owner-scope persistence deltas.

## PLAN-A10 — stable intent under failure

With authorization in `DECISION-NEEDS.md`, place the evidence-only proxy on a lane port:

1. **503 before forward:** return 503 without forwarding. Browser retries the logical intent; request body hash and idempotency key must be identical. Backend counts remain zero before the successful retry.
2. **500 before forward:** return 500 without forwarding. The same key/payload must remain available for retry; no write occurs.
3. **Unknown response after commit:** forward exactly one create, transition and POD mutation separately; after upstream success is observed, drop the browser-facing response. Confirm the committed state from DB/HTTP evidence, then let the UI retry. The retry must carry the exact key/payload and return `idempotentReplay=true` without duplicate writes.
4. **Changed payload:** after an ambiguous committed intent, change one business field while reusing the key. Expect 409 `IDEMPOTENCY_KEY_REUSED`, stop retry and retain the authoritative committed state.

Run each operation in a fresh fixture to keep create/transition/POD evidence independent. Archive proxy mode, body hash, key, upstream response, client observation and DB counts. A proxy-generated status proves UI retry behavior only; it is not evidence that the production controller generated that status.
