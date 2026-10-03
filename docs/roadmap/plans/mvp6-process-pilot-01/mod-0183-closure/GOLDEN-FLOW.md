# MOD-0183 golden flow — the five elements SOP §18.0 row 1 asks for

C-06, written 2026-10-03.

SOP §18.0 row 1 requires **actor, trigger, interaction sequence, expected response and success
result** to be written **together**. They existed separately: the sequence as one prose line in
`docs/roadmap/plans/mod-0183-readiness-and-development-prompt.md:293-294`, the actor type in pack
§14, and the behaviour executed by a test. Nothing stated them as one flow, which is why Q231 marked
row 1 **NOT MET** with the note "PARTLY WRITTEN".

This document covers the **backend half only**. The UI journey is C-10 and is blocked behind C-01,
because the pack still says the module creates no Razor UI while ten views exist.

Nothing here is new behaviour. Every row below cites the code or test that already evidences it.

---

## 1 · Actor

**Tenant user or service actor** (pack §14). Identity is not taken from headers — it is taken from
token claims, and the headers must agree with them:

| claim | requirement | enforced at |
|---|---|---|
| `sub` | exactly one non-empty UUID → becomes the audit `ActorId` | `ShipmentContextMiddleware.cs:17-22` |
| `tenant_id` | exactly one non-empty UUID, must equal `X-Tenant-Id` | same |
| `legal_entity_id` | exactly one non-empty UUID, must equal `X-Legal-Entity-Id` | same |
| `permission` | the §14 names below | read into `context.Permissions` |

A mismatch between a claim and its header returns **404 "Shipment not found"**, not 403 — existence
is not leaked (pack §8, §13). **Known deviation:** through the gateway this is answered **400
"Tenant mismatch"** before the service can answer 404, with an RFC 7807 body instead of the contract
envelope. That is open as Q257 and is the only path the UI uses.

Permissions consumed by this flow: `supplychain.shipments.create`, `.read`, `.dispatch`,
`.pod.capture`, and `.cancel` for the abort path.

## 2 · Trigger

A tenant actor holding `supplychain.shipments.create` submits one shipment, carrying:

- `Authorization: Bearer <HS256>` — issuer and audience validated, `HmacSha256` only (`Program.cs:26-41`)
- `X-Tenant-Id`, `X-Legal-Entity-Id` — UUIDs, matching the token claims
- `X-Correlation-Id` — a non-empty UUID. Absent or malformed returns **400** and publishes no event
- `Idempotency-Key` — 1 to 128 characters on every POST

**One correlation UUID spans the whole flow.** It is not re-minted per step. The readiness record
requires this and the test asserts it on every outbox row.

## 3 · Interaction sequence

Seven steps. The reload after each mutation is part of the flow, not a convenience.

```
create Draft → reload → Planned → Dispatched → POD Delivered → reload POD → Closed
```

## 4 · Expected response

Measured, not assumed — every status below is asserted by
`ShipmentTests.cs:120 GoldenFlow_Replay_Outbox_AndRestartAreDurable`:

| # | step | request | expected |
|---|---|---|---|
| 1 | create | `POST /api/shipment-bundle/shipments` | id returned; `LifecycleCorrelationId` stored equals the request correlation |
| 2 | reload | `GET /{id}` | **200**, `status` = `Draft`, `lifecycleCorrelationId` echoes the same UUID |
| 3 | plan | `POST /{id}/transition` → `Planned` | **200** |
| 4 | dispatch | `POST /{id}/transition` → `Dispatched` | **200** |
| 5 | capture POD | `POST /{id}/pod` | **201** |
| 5a | same POD, same key | `POST /{id}/pod` | **201** — idempotent replay of the first result, no second record |
| 5b | same POD, **new** key | `POST /{id}/pod` | **409** `POD_ALREADY_CAPTURED` |
| 6 | close | `POST /{id}/transition` → `Closed` | **200** |
| 7 | reload | `GET /{id}` | **200**, `status` = `Closed`, `pod` present |

Error codes are frozen in the contract (pack §274). HTTP 500 carries **`INTERNAL_ERROR`** only —
corrected under C-02, where two emissions were returning a code the contract reserves for 400.

## 5 · Success result

The flow has succeeded when all of the following hold. The counts are the test's own assertions.

| what | expected | why it is the success condition |
|---|---|---|
| `sce_shipment_history` | **5** rows | append-only lifecycle; a delivered or cancelled shipment is never edited back (pack §8) |
| `sce_shipment_audit` | **5** rows | one audit row per mutation, inside the transaction; every row carries `ActorId` |
| `sce_shipment_receipts` | **5** rows | one replay identity per mutation — what makes 5a return 201 instead of writing again |
| `sce_shipment_outbox` | **6** rows | five lifecycle events plus the POD event; **every row carries the one correlation ID** |
| audit rows | contain **no** `Note` field | recipient, address, note and evidence references are confidential/PII and are never logged (readiness record §8) |

### Durability — part of the success result, not a separate test

After a process restart, with the same database:

- `GET /{id}` → **200**, still `Closed`, same `lifecycleCorrelationId`
- replaying the original create with its original idempotency key → **200**, the **same** shipment id,
  `idempotentReplay: true`, `status: Draft`
- counts unchanged: history **5**, outbox **6** — the replay wrote nothing

A retry is therefore **not** a second business event, which is the constraint pack §8.1 O-3/O-4 now
carries contractually.

---

## What this document does not close

Row 1 still needs the **UI journey** (C-10, blocked behind C-01) and a **live run** (R-09). This is
the written flow and its backend evidence; by SOP §32 K1 closure is behaviour measured live, and the
evidence cited here is an in-process test host with a test-signed token, not a started service behind
the gateway. CT's C-03 run reached **HTTP 200 through the gateway** on step 2 of this sequence only.

## Regenerating this document's evidence

```
dotnet test services/Diten.SupplyChainService/tests/Diten.SupplyChainService.Tests/Diten.SupplyChainService.Tests.csproj -c Debug --filter 'FullyQualifiedName~GoldenFlow_Replay_Outbox_AndRestartAreDurable'
```

That run needs `MOD0183_TEST_MONGO` set to an isolated replica set; without it the test fails in
under 5 ms with `Isolated Mongo required.` and never executes (see ledger Q258).
