# Logistics acceptance — corrections after independent VER

- date: 2026-10-11
- by: Control Tower, on findings from Agent Lane `AL-SCE-LOGISTICS-VERIFY` (VER, read-only, commit `0672c382f`)
- ledger: this record

A verification lane was dispatched to check CT's own ticks, because CT wrote them and cannot accept its own
work (`MOD-0185:463` and siblings: *"CT acceptance follows VER, not inferred from agent PASS"*). It returned
five findings. CT re-verified each before acting; four are upheld and corrected here, one is an owner ruling.

## Two ticks retracted

**`MOD-0183:430` — the claimed measurement has no artifact.** The line was ticked as measured "with a real
second tenant and its own MDM legal entity". Three checks, all negative:

- the only record dated 2026-10-11 is `mvp6-g5-logistics-golden-flow-01/REPORT.md`, and its line 25 lists
  `:430` under `Kalan` — remaining;
- `scripts/g5/g5_golden_flow.py` never mints a second tenant; its only scope probes are a missing, a
  malformed and a random `X-Legal-Entity-Id` on one tenant (`:181-183`);
- the R-2 evidence folder is one tenant on another service (`runtime.json` tenant `41e51ae4…`, port 5161)
  and `runtime-result.txt` matches `404|403|tenant` zero times.

The wiring is provable statically and was proved: tenant mismatch → 404 (`ShipmentContextMiddleware.cs:29`),
`NotReferenceable` → 404 (`:38-40`), missing permission → 403 (`HasPermissionAttribute.cs:11-12`). But
`LegalEntityMiddlewareWiringTests` injects a stubbed validator outcome, so it proves the middleware acts on a
decision, never that MDM refuses a foreign entity. Closing this needs a real second tenant.

**`MOD-0186:124` — the stated basis was factually wrong.** The tick listed four collections. There are five:
`ReturnSchema.cs:12` creates `return_entitlements` as well, uniquely indexed at `:15`. It is not an index
artefact — `ReturnEntitlement` carries `SourceQuantity`/`UsedQuantity`/`Version`, and `Debit`
(`ReturnRepository.cs:185-213`) and `Release` (`:214-226`) move it. That is a local, versioned, per-shipment-line
consumed-quantity balance. The criterion's second sentence passed only because nothing is *named* stock,
inventory, balance or movement.

## Two ticks amended in place

**`MOD-0183:428`** said "(six state changes, six outbox events)". The chain is create → Planned → Dispatched →
InTransit → POD: **five** state changes, and POD writes **two** rows, so six rows. Read literally the old
parenthetical would have disproved the two-event claim it was offered to support. Also made precise: these are
rows in `sce_shipment_outbox`, not published events — no `IEventTransportPublisher` is registered, so every row
stays `Pending`.

**`MOD-0183:433`** said 19 validated live responses. CT re-counted from the script: `conform()` issues **12**
(`g5_golden_flow.py:173`, `:174`, the six-tuple loop at `:176-179`, `:180-183`). Two limits the old wording hid
are now recorded on the line: the violation counter is blind to an undeclared status (`:147` returns a note;
`:163-164` increments neither `ok` nor `bad`), so `IHLAL 0` can print while the contract is violated — the
undeclared 404 was found by a human reading printed notes; and the contract still declares `x-status: FROZEN`
while its bytes moved to `c3c4e37a…` against the Q218 pin `6dc1dd48…`
(`mvp6-q218-contract-pin-01/HASHES.md:5`), with no record updating the pin.

## Open for the owner

1. **`MOD-0186:124` intent.** Does "no stock collection, projection or movement SoR" survive
   `return_entitlements`? This is a ruling, not a grep. CT has retracted the tick either way, because its
   stated basis was wrong.
2. **Service port.** Every runtime tick was measured on **5066**, while the packs — authority rank 1 — say
   **5061** (`MOD-0183:417`, `:166`, `MOD-0184:498`, `MOD-0186:627`, `MOD-0187:577`) and `AGENTS.md:86` agrees
   on 5061. The contract pins no port (`servers.url` is the relative `/api/shipment-bundle`). The behavioural
   measurements are not invalidated, but they were taken against a topology no in-scope authority describes.
   CT changed the MOD-0190/0192 packs to 5066 earlier the same day on its own judgement and flags that as
   overreach pending this ruling.

## What this says about the method

Four defects in CT's own work, none of which CT found. The verification lane is not a formality — a module at
96.7% was carrying a tick whose measurement never happened. The remaining independent-VER items
(`MOD-0185:463`, `MOD-0186:468`, `MOD-0187:457`) should be read in that light: they are not paperwork.

One limit on this VER: it ran read-only and could execute nothing (v9.2 §3 — runtime VER belongs to the
Terminal lane). Every count that needs a live stack remains inherited from CT's narration. A runtime VER is
still owed.
