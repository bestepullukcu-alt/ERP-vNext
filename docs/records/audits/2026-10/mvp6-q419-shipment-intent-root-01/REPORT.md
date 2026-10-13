# verify from: cd docs/records/audits/2026-10/mvp6-q419-shipment-intent-root-01 && shasum -a 256 -c ARTIFACTS.sha256

# Q419 (ledger Q393) — Shipments Create: one X-Correlation-Id per intent

| Field | Value |
|---|---|
| Lane / agent | Q419 · frontend-ui-ux · single writer on `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/create.js` |
| Placement | Claude app → Code tab → Local, Darwin. G2 placement waiver applied — Cowork withdrawn by owner. |
| Read first | `AGENTS.md`, `git-safety.md`, `code-style.md`, `.antigravity/agents/frontend-ui-ux.md`, MODULE-RECIPE §3 (rows 3.1, 3.2, 3.3) and §8, docs-organization K5/K6 |
| Branch / HEAD | `feature/mvp6-logistics` @ `cea01354e`; staged 0 at start and end; no `.git/index.lock` |
| Start / end (Europe/Istanbul) | 2026-10-04 22:57:57 +03 / 23:24 +03 |
| Verdict | **Fixed in the tree, proven live, unstaged, uncommitted.** Agent verdict ≠ CT ACCEPTED. |

## 1. CT's reading, checked

- **Confirmed: `create.js:76` (before) sent `'Idempotency-Key': intentKey` and `'X-Correlation-Id': uuid()`.** That is a
  fixed key with a new correlation on every Save.
- **Confirmed: the receipt check order.** `ShipmentRepository.cs` checks the fingerprint first (409
  `IDEMPOTENCY_KEY_REUSED`), then the correlation (400 `INVALID_REQUEST`). The source-link check that comes earlier in
  `MutateAsync` does not apply: `CreateShipmentHandler` passes no `source`.
- **Confirmed: the Web adapter forwards the browser's `X-Correlation-Id` unchanged.**
  `SupplyChainShipmentsController.cs:176` `TryForwardUuidHeader`. So the page decides the correlation the service sees.
- **Confirmed: `details.js` is correct.** `:155` sends `intent.correlationId`, which is `authoritativeRoot()` (`:17`),
  the Shipment's `lifecycleCorrelationId` (recipe 3.3). Not touched.
- **Design source:** Loads `index.js` `newIntent` mints key and correlation together when the form opens, with a
  comment that the create's correlation is the new Load's root. The create handler sets
  `LifecycleCorrelationId = context.CorrelationId`, so for Shipments too the create's correlation **is** the new
  Shipment's root. This is a backport of Loads' rule. Returns differs by design: its adapter resolves the source
  Shipment's root server-side.

## 2. The change

`create.js`, sha256 `796b5ea1…afaf1` (was `49aa812f…1320`). Diff: `evidence/create.diff`.

- `:14` `const intentRoot = crypto.randomUUID();` — minted once per page with `intentKey` (`:9`), with a comment at
  `:10-13` citing recipe 3.2, `ShipmentRepository.cs:70` and Loads.
- `:80` the POST sends `'X-Correlation-Id': intentRoot`.
- The `uuid` helper is removed; it had no other caller.

A new Create page is a new intent, with a new key and a new root. Nothing else in the file changed. `node --check` passes.

## 3. How it was run

**Stack.** A copy of the tree (`services/`, `frontend/`, `gateway/`, `docs/analysis/`, the SupplyChain module packs) in
`~/mvp6-env/q419-20261004-2301/stack/`. Six services were built there (0 errors each) and started with R-4b's
`start.sh`, against an own mongod on 57419 (`rsq419`, `enableTestCommands=1`).

**Offset ports.** Lane R-4c started its stack on the canonical ports 5000/5001/5056/5057/5059/5061 while this lane was
building. Nothing of R-4c's was touched; my start script refused to start on an open port. This lane ran on 5300 gateway,
5301 web, 5356 auth, 5357 platform, 5359 mdm and 5361 supplychain. The port references were rewritten **in the copy's
config files only** (`evidence/port-rewrite.txt`: 30 files, e.g. ocelot 126 lines). The Web was reached as
`127.0.0.1:5301`, a different cookie host from R-4c's `localhost`.

**Tenant and users.** A STARTER tenant was registered through Platform's admin API, with users invited through Auth and a
legal entity and organisation set up (R-4b's scripts, lane-renamed). The admin's token carries all five
`supplychain.shipments.*` keys (`evidence/fixture-ids-and-keys.txt`: ids, booleans and key names only). Passwords were
served by a local helper (5399, origin-restricted) into the page and never printed. The throwaway JWT secret was kept
mode 600.

**Lost-response instrument.** A `fetch` wrapper injected into the page for each run. It is not product code. It lets the
first Save reach the server and commit, records the response, then throws a network `TypeError` so the page sees a lost
response. Every request's key, correlation, body hash, status and body was posted to the helper before the page
continued (`evidence/Q419-*.json`).

**Served bytes.** Before each run, the `create.js` the browser received was hashed in the page; the hash is in each
record (`servedSha`).

## 4. Live results

| Run | Served `create.js` | Save 1 (response dropped) | Save 2 | Page shows | Shipments stored |
|---|---|---|---|---|---|
| RED — unchanged retry | `49aa812f…` (tree before) | 201, key `e6dd42f7`, corr `79e7f33b` | same key, same body, **new corr `dfe58fa6`** → **400 `INVALID_REQUEST`** | "Check the entered values. Support reference: dfe58fa6-…" | **1** (root `79e7f33b`), 1 receipt |
| GREEN (a) — unchanged retry | `796b5ea1…` (fix) | 201, key `6942a512`, corr `87b6083f` | same key, same body, **same corr** → **200**, same shipment id, `idempotentReplay: true` | navigates to Details of `913a1f8e…` | **1** |
| GREEN (b) — edited retry (`shipToReference` changed) | `796b5ea1…` | 201, key `0e84e1d5`, corr `ce133be4` | same key, **edited body**, same corr → **409 `IDEMPOTENCY_KEY_REUSED`** | "The request identity belongs to different content. Support reference: ce133be4-…" | **1**; edited value stored: **0** |
| SABOTAGE — original file served again | `49aa812f…` | 201, key `31893966`, corr `657778b0` | same key, same body, **new corr `862c84ae`** → **400 `INVALID_REQUEST`** | "Check the entered values. …" | **1** |
| RESTORED — fix served again | `796b5ea1…` | 201, key `2b525d0e`, corr `95b89776` | same key, same body, same corr → **200**, `idempotentReplay: true` | navigates to Details of `8048d94f…` | **1** |

The database counts are in `evidence/db-counts.txt`. In every run the stored shipment's lifecycle root equals the first
Save's correlation.

## 5. Suites

| Suite | Result | Gate in dispatch | Note |
|---|---|---|---|
| Frontend `Diten.Web.Tests` (lane copy, fixed `create.js`) | **390 / 0 / 390** | 297/0/297 | The suite has grown since the gate. `StableIntentPreservesPayloadAndKey` still passes; it pins `const intentKey = crypto.randomUUID();` and `'Idempotency-Key': intentKey`, both kept. |
| SupplyChain (lane copy, own suite mongod 57425, all module URIs + guard URI) | **464 / 2 / 466** | 432/1/433 as a range | Failure 1: `ClaimReplayTests.Receipt_TwoIndependentTestProcesses_DurableRecovery`, by design. Failure 2: `ReturnReferenceTests.OtherProducerFailures_RemainDependencyUnavailable(status: 403, …)`, expected 503, got 403 — **lane Q420's in-flight edit**, see F-Q419-4. SupplyChain tests never load `create.js`. |

## 6. Findings

- **F-Q419-1 (confirmed live):** Before this fix, an unchanged retry after a lost response got 400 `INVALID_REQUEST`,
  rendered as "Check the entered values." The shipment had in fact been created. Evidence: `evidence/run-RED.json`,
  `db-counts.txt`.
- **F-Q419-2:** No frontend test pins the correlation behaviour. The source-text test pins only the key, so the sabotage
  file passes the frontend suite. Recipe 8.3 again: behaviour is proven only by the live runs above. Shown by:
  `frontend/Diten.Web.Tests/JavaScript/ShipmentDetailActionTests.cs:12`. Not added: test files are outside this lane.
- **F-Q419-3:** After a 409 `IDEMPOTENCY_KEY_REUSED` the Shipments form stays usable and keeps the spent key and root,
  so every further Save from that page gets 409 again. Loads stops the form instead ("the intent is spent", Loads
  `index.js` intent comment). Not changed: outside the dispatch.
- **F-Q419-4 (another lane):** `services/…/Infrastructure/Features/Returns/ReturnReferenceReader.cs` was modified at
  23:00:24 by lane Q420 (comment "Q420: a 403 from the Shipment read means the caller lacks
  supplychain.shipments.read…", `:83-86`). The existing test case
  `ReturnReferenceTests.cs:49` `[InlineData(403, …)]` still expects 503, so it fails. My copy (23:01) contains that
  edit; the copy equals the tree.
- **F-Q419-5:** "docs-organization K5 and K6" in the dispatch are the `vendor/` and user-guide rules
  (`docs-organization.md:79`, `:83`), not about scripts. Read; nothing applied.
- **F-Q419-6:** MODULE-RECIPE has no numbered "3.1"/"3.2" headings; they are rows 3.1 and 3.2 of the §3 table
  (`MODULE-RECIPE.md:56-57`). Row 3.2 reads "OPEN for Shipments (Q393)". It can now close, on CT's ruling.
- **F-Q419-7:** The canonical stack ports were taken mid-lane by R-4c. A lane that needs the whole stack cannot assume
  5000–5061 are free. This lane's offset band (53xx) worked with config-only rewrites in a copy.

## 7. Not done / boundaries

- No stage, no commit.
- `details.js`, `SupplyChainShipmentsController.cs` (Q372-R2), the service and every test file were not touched.
- Signed out of the test session at the end.
- Stopped by recorded pid, only this lane's processes: web, gateway, supplychain, mdm, platform, auth, helper, and
  mongods 57419 and 57425. Ports 5300, 5301, 5356, 5357, 5359, 5361, 5399, 57419 and 57425 are closed.
- R-4c's and Q420's processes were never touched.
- The scratch folder stays (no `rm`).

Return to CT; CT decides.
