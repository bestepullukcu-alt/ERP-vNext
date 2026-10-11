# Runtime VER — MOD-0183…0187 — what a live stack says

- date: 2026-10-11
- by: Agent Lane `AL-SCE-LOGISTICS-RUNTIME-VER` (VER), recorded and spot-verified by Control Tower
- commit: `e4ea2722c`
- stack: Auth 5056 · Platform 5057 · MDM 5059 · SupplyChain **5066** · Gateway 5000; four lane mongods
  on slots 3–6. The operational 27017 was never contacted.

An earlier VER the same day could execute nothing, so every runtime number in the logistics packs rested on
CT's narration. This lane re-ran them. **Every re-measurement reproduced**, including both numbers CT had
corrected earlier that day — and one acceptance line advanced from "no evidence at all" to "four of five legs
measured", with the fifth exposing a live defect.

## Reproduced

| line | claim | measured |
|---|---|---|
| `0183:424` | 0 warnings, 0 errors; five layers; four behaviours | `Build succeeded. 0 Warning(s) 0 Error(s)`, also with `--no-incremental`; behaviours at `DependencyInjection.cs:35-38` |
| `0183:425` | one document in each of five collections | 1/1/1/1/1 |
| `0183:426` | 201 then 200, same id, `idempotentReplay`, one record | exactly that |
| `0183:427` | 200/422/200/422/200, four lifecycle entries | reproduced, and **stronger**: the two rejections wrote nothing to *any* of history, outbox, audit or receipts |
| `0183:428` | **CT's corrected** five state changes, six outbox rows, POD writes two | history **5**, outbox **6**, `PodCaptured` ×1 alongside `ShipmentDelivered` ×1, `{"Pending":6}` |
| `0183:429` | one correlation across 36 documents | exactly 36 |
| `0183:433` | **CT's corrected** 12 validated responses, not 19 | static count 12, live `uyan 12 · IHLAL 0` |
| `0185:126` | 422 `INVALID_LOAD_TRANSITION`, 409 `SHIPMENT_ALREADY_ASSIGNED`, count unchanged | 7 → 7 |
| `0186:123` | 422 over-quantity, 422 negative, 201 valid, rejections write nothing | 2 → 2 |
| `0186:125` | `ReturnSchema` creates exactly five collections | five, `return_entitlements` among them |
| `0187:124` | amount precision and transition matrix | `10.005` stored and returned as the string `'10.005'` |
| `0187:125` | settling writes no payment/AP/AR record | Settled 200; the filter over all 25 collections returns none |
| chain | 10 steps | 10/10 |

Both of CT's corrections were right, and the original figures they replaced were wrong. The retraction
discipline held.

## MOD-0183:430 — four legs closed, the fifth blocked by a live defect

The lane minted a genuine second tenant through the product's own APIs and measured own read 200,
cross-tenant read 404, cross-LE read 404, cross-tenant write 404 on three paths, and a clean control re-read.
That is precisely what CT claimed in the morning without having done it.

The fifth leg does not hold. A token with **no** permissions answers **503 `DEPENDENCY_UNAVAILABLE`**, because
`ShipmentContextMiddleware` (`Program.cs:145`) runs before `app.UseAuthorization()` (`:146`) and validates the
legal entity by forwarding the caller's own token to MDM, where `MdmLegalEntityScopeValidator.cs:58` maps
`Forbidden` to `Unavailable`. `[HasPermission]` never executes. 403 appears only when the caller also holds
`mdm.legal-entities.read`.

**A user provisioned with only SupplyChain permissions therefore gets 503 on every SupplyChain call.** This is
owner item Q470, and the fix for it was reverted in `e9b0f3b19`. The acceptance line stays open.

## Findings that need an owner or a work package

| # | finding | evidence |
|---|---|---|
| **F1** | The harness's blind counter, reproduced firing. Run against the pre-3.2.1 contract, an undeclared 404 printed `404 ILAN EDILMEMIS`, `uyan` fell to 11, `IHLAL` stayed **0**, exit **0**. So the original "19 responses, 0 schema violations" was recorded in exactly the state where the counter could not see a violation. | `git show 47eeec74b^:…yaml` + `conform` |
| **F2** | Port drift is live in the repo, not only in the packs: `AGENTS.md` §3 says SupplyChain 5061, and seven SCE pack lines agree, while `ocelot.json` routes `/api/shipment-bundle` to 5066 and `/api/crm` to 5061. `MOD-0183:417` is the worst, asserting the port "exactly matches the OpenAPI `servers.url`". | independently confirmed by `appsettings.json` `"Urls": "http://127.0.0.1:5066"` |
| **F3** | Contract pin drift: the file hashes `c3c4e37a…`, the Q218 pin is `6dc1dd48…`, and `x-status: FROZEN` still stands. No record updates the pin. | `shasum -a 256` |
| **F4** | **SupplyChain cannot be started from a checkout.** Its `appsettings*.json` carry no `JwtSettings` and no `Mongo` section, there is no `UserSecretsId` and no `launchSettings.json`, yet `Program.cs` requires both. Every other service in this flow has its Development values committed. This is why no runtime number for these modules has ever been reproducible by a second party. | source |
| **F5** | A 403 reports itself as `INVALID_REQUEST`. `HasPermissionAttribute.cs:16` hard-codes that code for both 401 and 403, the same code a malformed payload returns. A consumer cannot tell a permission gap from a bad body. | measured on four 403s |
| **F6** | `run_all.sh` still carries the 2026-10-10 gateway-killing trap — `lsof -ti :5000,…` with no `-sTCP:LISTEN` — and never starts SupplyChain; its port list stops at 5060. `scripts/dev-up.sh` does it correctly but also omits SupplyChain. | source |
| **F7** | Two runtime preconditions appear in no acceptance line: the one-correlation-root rule extends to Loads, Returns and Claims (`409 CORRELATION_ROOT_MISMATCH` / `409 CLAIM_CORRELATION_MISMATCH`), and a claim needs a **Delivered** shipment, with eligibility checked before amount — so re-measuring `0187:124` against a Draft shipment wrongly looks like broken amount validation. | measured |
| **F8** | Confirmed at runtime: the carrier assignment surface is still empty (`MOD-0183:499`), and all six outbox rows stay `Pending` because no `IEventTransportPublisher` is registered. | measured |
| **F9** | `g5_sc` holds eight `sce_*` collections, not five; the extra three belong to the uncomposed SourceIntake. `0183:425` is correctly worded but invites a misread. | measured |

## CT's own spot-verification

CT re-checked the three load-bearing claims rather than accepting them: `MdmLegalEntityScopeValidator.cs:58`
maps `Forbidden` to `Unavailable`; `Program.cs:145/146` puts the middleware before authorization;
`HasPermissionAttribute.cs:16` hard-codes `INVALID_REQUEST`. All three hold.

## Scope the lane did not claim

Warehouse intake (`0183:432`) has no producer and was not attempted. The static half of `0183:431` was not
re-swept. The "all examples and schemas" lines remain open and were not widened into. MOD-0190 and MOD-0192
are uncomposed and out of scope. Platform and MDM build with 37 and 5 warnings respectively; `0183:424`
claims nothing about them and the SupplyChain solution itself is genuinely 0/0.
