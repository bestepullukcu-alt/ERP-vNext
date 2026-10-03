# runtime-scenarios — MOD-0186 Returns UI (for Q65b on the Mac)

DRAFT. `returns-ui.spec.mjs` was written and syntax-checked (`node --check`) in the chat lane; it has **never run**.

Prerequisites on the Mac (isolated environment, not the common checkout; README "Build note for Q65b"):
- HEAD `4a8d4d4b` archive + BC-SOURCE `ebd5d80c…7064` + **A12 360 overlay `7b6a0d1a…314d`** + the accepted Returns source
  `normal-source.tar.gz` `edb759a0…5a21` (reconcile SupplyChain `Program.cs` / Api `.csproj`) + Auth 22 `f50350b8…`, then this
  lane's `overlay/` module files, then the `_shared-integration/` items the environment needs to route (gateway routes, provider
  line, nav keys).
- Real-Auth identities with separate storage states: full (all Returns keys + `supplychain.returns.transition` +
  `supplychain.shipments.read`), read-only (`.read`), no-read, plus an LE-B identity; a Delivered Shipment with a non-null
  `lifecycleCorrelationId` (producer root uptake, RU-15) and a Draft Shipment.
- The shared `showConfirm` accept button selector must be confirmed on the target (`.swal2-confirm` is assumed, README A10).

| Scenario | Acceptance row |
|---|---|
| list through the same-origin adapter only; no scope/token in the browser; skeleton then table; PNG | RU-01, RU-04 |
| filter sends only `status` and `shipmentId` | RU-02 |
| QuickView without a by-ID request | RU-29 |
| resolve Delivered → select one line → quantity as a JSON string → 201 → row after reload; response trace = request trace | RU-VS1, RU-08, RU-09, RU-21 |
| ineligible shipment disables submit; unknown shipment leaves no shipment data | RU-11, RU-14 |
| late resolve response for a previous UUID never populates | RU-10 |
| authorize via offcanvas + shared confirmation; `occurredAt` with offset; only listed fields sent | RU-07, RU-20, RU-24 |
| Received labelled manual assertion; inventory reference offered | RU-18 |
| no read → `_AccessDenied` only; read-only → no create CTA, no row action | RU-05, RU-06, RU-07 |
| tr and ar (RTL) at 390/768/1024/1440, no horizontal overflow, PNG | RU-22, RU-23 (PNG as evidence only; RU-28 stays BLOCKED) |

Not scripted here (need fault injection, seeded states or a second profile, see NOT-VERIFIED.md): RU-03 envelope/absent
fields, RU-12 cross-LE list, RU-13 transition safe-not-found, RU-15 root seam, RU-16 idempotency replay /
`IDEMPOTENCY_KEY_REUSED` (BLOCKED by DN-01), RU-17 stale transition 422, RU-19 disposition code with whitespace, 503
same-key retry, RU-26 family routing and regression.
