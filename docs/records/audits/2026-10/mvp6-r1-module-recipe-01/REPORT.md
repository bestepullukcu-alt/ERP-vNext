# R-1 (Q386) — the module recipe, extracted from what MOD-0183 paid for

- Lane: R-1, ledger Q386, documentation-writer. Read-only on code. One plan artifact written:
  `docs/roadmap/plans/mvp6-process-pilot-01/MODULE-RECIPE.md`. Recorded 2026-10-04.
- Preflight: `Sun Oct  4 12:51:26 UTC 2026` · `feature/mvp6-logistics` · HEAD `983dcd4d0` · porcelain 5 · staged 0.
- Step 0 read (sha256/16):
  - `AGENTS.md` `ce8c12ad80f93bb5`
  - `git-safety.md` `4181c697b96dc9f8`
  - `docs-organization.md` `096ed27bdd6c2349` (K5 at `:193`)
  - `REPLAN.md` `7610b8e29ad8b1db` (§2)
  - MOD-0183 `2285fe0d45dfbce5` (§9 `:257`, §11 `:294`, §12 `:349`, §22 `:507`)
- Sources read: Q362, Q371, Q374, Q382, Q339-R2, Q353 and Q358 REPORTs; C-03 `VERDICT.md`; Q366 REPORT §A–C; ledger rows
  Q326, Q329, Q363 and Q383; Q332 `SUMMARY.md`; commits `7897458b6`, `ff5777afe`, `571b469ac`, `a3c335256` and
  `18c5ab23a`.
- **Nothing staged or committed. No code changed.** Agent verdict ≠ CT ACCEPTED.
- **State of the tree I read.** The working tree carries one uncommitted edit of mine from the paused lane Q372-R2:
  `frontend/Diten.Web/Controllers/SupplyChainShipmentsController.cs` (+9/−4). Every line number in the recipe is taken
  from HEAD (`git show HEAD:`), not from that file.

## CT's shape, verified

| CT claim | measured | verdict |
|---|---|---|
| MOD-0183 shipped 10 views | 10 `.cshtml` under `Views/SupplyChain/Shipments` (11 files, the 11th is the localizer marker `ShipmentsIndex.cs`) | TRUE |
| 4 scripts | `create.js`, `details.js`, `index.js`, `index.l10n.js` | TRUE |
| 7 resx files | 7 `ShipmentsIndex.*.resx`, 72 keys each (en and ar counted) | TRUE |
| a 256-line Web controller | **251 at HEAD.** The working tree reads 256 because of my uncommitted Q372-R2 edit | **FALSE as stated.** CT measured a dirty tree |
| 2 gateway routes, of 271 | 271 routes; 2 serve a supply-chain family (`/api/shipment-bundle`, `/{everything}`) | TRUE |
| a manifest provider | `ShipmentTrackingPodManifestProvider.cs`, composed at `Program.cs:98` | TRUE |
| Carriers, Loads, Returns, Claims have 0 of each | views 0, scripts 0, resx 0, controllers 0, gateway routes 0, for all four (the 4 "load" hits in `ocelot.json` are `/api/v1/uploads`) | TRUE for the UI. **Not for the provider:** `CarrierManagementManifestProvider.cs` exists in the tree, unregistered, and its nav keys are already 7/7 (F-R1-3) |

Not in CT's shape, and relevant to R-2: Returns and Claims each have **UI draft overlays** recorded under
`docs/records/audits/2026-09/mvp6-returns-ui-draft-0{1,2}/` and `mvp6-claims-ui-draft-0{1..4}/`, plus Capacity and
S&OP drafts. This lane did not read them. They are prior art, not recipe lines.

## The seven required areas

All covered: §0 preconditions (CT's item 7), §1 gateway, §2 controller, §3 scripts, §4 views, §5 L10n, §6 manifest
provider. Two areas were added because a measured defect required them: §7 cross-module order, §8 running the proof.

CT's item 3 says "a 403 drawn as the denied card, not an empty table (Q329/Q362)". The records say:

- The defect was **recorded by Q231** (UX-STATES DEFECT row).
- It was **fixed in code by Q329**.
- It was **measured as not occurring by Q362** (D-1 "FALSIFIED" on the fixed code).

The line is kept (3.6) with that history. The 403 defect that users actually hit was a different one: Save read
"Check the entered values." (Q362 R-01), closed by Q371. That is line 3.7.

## Shared files: the answer

Measured, not assumed. The table is at the end of `MODULE-RECIPE.md`. In short:

1. **`ocelot.json`.** CT's belief is confirmed.
2. **SupplyChain `Program.cs`.** One `AddSingleton` per provider, in a file other lanes are also editing.
3. **`SharedResource.*.resx`, seven files.** Nav keys per module.
4. **`DefaultRolePermissionTemplate.cs:41`.** Only if the owner chooses the `AdminModules` path per module. The
   entitlement path touches no shared file.
5. **Web `Program.cs`.** Cross-cutting: the JSON-401 consumer and the adapter timeout. Fix once, before R-2, and then no
   module touches it.
6. **`_DataTableL10n.cshtml` / `dt-defaults.js`.** Only when a module needs a new DataTable key.

R-4's "three parallel modules" therefore collide on items 1–3 for certain and on 4 conditionally. Sequencing route
additions alone, as REPLAN §3 says, is not enough.

There is also an order constraint that is not a file collision: Claims' carrier path needs Carrier's keys (recipe 7.2).

## Lines not tied to a measured defect

| line | why | kept or dropped |
|---|---|---|
| 0.6, second half (empty Platform key → 401) | CODE (Q353 §B). The first half, blank key → registration skipped, is measured (Q339-R2 P3c) | kept, marked CODE |
| 7.1 (Returns/Claims need `shipments.read`; 403 → 503) | Q353 §D is CODE; never run live | **kept, marked CODE.** It decides the R-2 precondition, and dropping it would hide a known 503 |
| 7.2 (Claims with carrier) | Key absence measured; the failing claim itself not run | kept, marked CODE for the claim |
| 5.1 (seven-language parity) | A baseline, not a defect | kept as the row the other L10n lines hang on; says "—" in the defect column |
| 6.4 (API-only key never reaches Auth) | Measured behaviour, expected by the pack; not a defect | kept as "expected" so R-2 does not chase it |
| DataTable v2 / `data-dt-standard="v2"` (pack §9) | No defect record | **dropped** |
| Golden Compact page set (pack §11) | Pack rule; no defect was hit | **dropped** |
| RTL with no horizontal scroll | Measured OK (Q362, Q371); never a defect | **dropped** |
| `_DataTableL10n` / `dt-defaults.js` `Details`/`Close` keys (`7897458b6`) | No record names the defect the change fixed | dropped as a recipe line; **kept in the shared-files table** because it is a measured touch of a shared file |
| 3.2 and the Q372-R2 halves of 2.3 and 3.3 | Measured, but in a record that is **not sealed** (Q372-R2 was paused at the suites) | kept, each saying so. If Q372-R2 is abandoned, these three cite a scratch file only |

## Findings

- **F-R1-1 — the JSON-401 marker has no consumer.**
  - `JsonAdapterEndpointAttribute` is applied five times and read nowhere: `grep -rn JsonAdapter frontend/Diten.Web
    --include=*.cs` returns 2 files, the attribute and the controller.
  - `Program.cs:72-78` sets only `LoginPath` and `LogoutPath`; there is no `OnRedirectToLogin`.
  - Q202a imported the marker and its test from the A12 base (`mvp6-q202a-integration-write-01/WRITTEN-FILES.tsv:18,40`),
    but not the consumer.
  - This is the cause of the one "pre-existing" frontend failure that Q371, Q374, Q382 and Q372-R2 all carried (302 vs
    401) without investigating.
- **F-R1-2 — an unchanged retry of a Create intent gets 400.**
  - `create.js:76` mints a fresh `X-Correlation-Id` per Save.
  - `ShipmentRepository.cs:70` refuses a known key with a different correlation as 400 `INVALID_REQUEST`. The check
    runs after the fingerprint check (`:69`), so an edited retry correctly gets 409.
  - So the case Q371's per-form key exists for, an unchanged retry after a lost response, gets 400, and the page shows
    the validation text.
  - Measured at the API in the Q372-R2 scratch run (`b-drive.jsonl`: new correlation → 400; same correlation → 200,
    same shipment id). **Not measured through the page.**
  - The fix belongs to the page (resend the correlation with the key) or to a contract ruling. The Shipment contract's
    create operation (`shipment-bundle.openapi.yaml:32-113`) is silent on correlation for a replay.
- **F-R1-3 — Carrier's nav keys are already ahead of its UI.**
  - `NavManifestL10nGuardTests.cs:252-253` scans every `*ManifestProvider.cs` under `services/`, registered or not.
  - `CarrierManagementManifestProvider.cs` exists, so `Nav.Module.CARRIERMANAGEMENT` and `Nav.Page.CARRIERS` are 7/7 in
    `SharedResource` at HEAD.
  - The ship rule (`MOD-0184:473`) says the nav keys never ship ahead of the UI. The guard and the rule disagree, and
    the tree follows the guard. This needs a ruling.
- **F-R1-4 — Q371's contract citation is the Carrier section.** Q371 Fix 3 quotes "Different valid payload under a
  committed key returns 409" and "Unknown commit uses 503 and same-key recovery" from "lines 322–346". Those lines sit
  under `/carriers` (`:204-330`) and `/carriers/{carrierId}/status` (`:330`), whose description names Carrier-only
  semantics (`:29`). The Shipment create operation states only "same key → original result, 200". Q371's behaviour
  matches the service, but its authority was the wrong operation. The record is not corrected (K4); this finding is the
  correction.
- **F-R1-5 — CT's 256 is my dirty tree.** HEAD has 251 lines. A shape measurement taken on a working tree shared with
  a paused lane counts that lane's edits.
- **F-R1-6 — Q329 has no record folder.** The POD-note defect (recipe 4.2) is attributable only to ledger row Q329 and
  the fix comment at `details.js:175`. Q332 `SUMMARY.md:36` already noted the gap.

## Paused lane, stated so it is not lost

Q372-R2 stopped when the owner rejected the SupplyChain suite run. Left as they were:

- the controller edit, in the working tree;
- my suite mongod on `127.0.0.1:57373` (pid in `~/mvp6-env/q372-20261004-1359/run/mongod-suite.pid`), still running;
- that lane's scratch folder `~/mvp6-env/q372-20261004-1359/`.

Q372-R2 has no record written yet. Its existing folder `mvp6-q372-observability-close-01/` holds another session's
stop report, untouched.

Nothing committed, nothing pushed, nothing staged.
