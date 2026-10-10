# MVP6-SHIPMENT-SHARED-UI-INDEPENDENT-VER-01 — SOP §22

Date: 2026-09-25  
Role: independent verifier; not the integration writer  
Branch / HEAD observed: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
Verdict: **PASS — exact shared-UI successor scope only**

## Exact input identity

- Writer handoff SHA-256: `14e10aedf0a6169754eb5dc47db963638c21e794ac106d00c4cdc817540e76d8`.
- Writer artifact manifest SHA-256: `6038d295caf2b5f77a89901374f0f02dc68fbf0d90f6a81a3b726aa95b3f7067`; all 15 listed artifacts verified.
- Immutable source archive SHA-256: `490d51be87d249265a2cc9fe6d1f8f3b23bd6976f4e5830d733bff2dfff613a3`.
- Combined 360-source manifest SHA-256: `7d7bec63a1f2e9e906864e5dea6dc96a00e94a280b830dd06f95a8b68246b4fc`; all 360 rows matched before and after verification.
- `CHANGED-FILES.tsv` SHA-256: `1a2fa3e1c8e6963710eca96305cba288129f5447badd0265227215bcb5a9d3e7`; all 12 target hashes matched.
- All three accepted accessibility rows matched their full successor hashes, including `_Form.cshtml` `c22e1b0...`, `create.js` `89fc1151...`, and `ShipmentFormContractTests.cs` `ef9e87b8...`. Full values are retained in `INPUTS.sha256` and the controlling manifests; shortened values here are descriptive only.

The tar contains the governed 360-source set rather than the project scaffolding. A plain extraction therefore produced the recorded `MSB1009` missing-project attempt. The verifier reconstructed the writer's declared method: exact HEAD archive plus the immutable 360-source overlay, then proved all 360 governed rows again before build. No mutable checkout source was used.

## Fresh independent evidence

Native SDK/runtime was `8.0.417` / `8.0.23`; no major roll-forward was used. Fresh restore completed with NuGet vulnerability-feed warnings caused by unavailable network metadata. Fresh build completed with **0 errors / 6 warnings** after disabling the shared compiler to avoid the first local compiler-process stall. Independent focused regression completed **19/19 PASS**. The independent Web DLL SHA-256 is `494f6869e515e09ba725979898dd600106d5a22d8e89a1822e2964fc20d6656d`.

The real ASP.NET pipeline was started from that binary on lane-local port `55183`. Each of list, detail, create, transition and POD returned HTTP 401, `INVALID_REQUEST`, `contractVersion=v1`, no `Location`, and matching valid body/header correlation IDs. The ordinary `/SupplyChain/Shipments` HTML page separately retained HTTP 302 to `/account/login`. This behavior maps to `Program.cs:79-102`; the five adapter metadata marks are at `SupplyChainShipmentsController.cs:53,68,76,87,101`.

The browser lane used the same binary on `55185` plus the previously accepted stateless presentation-fixture pattern on `55184`. It did not use Mongo and is not persistence/Auth/backend acceptance. At actual `window.innerWidth` 390 and DPR 1, all seven cultures rendered localized Details modal titles and Close accessible names, retained localized row labels/actions, and had no document overflow. Arabic rendered `lang=ar`, `dir=rtl`, title `التفاصيل`, and Close name `إغلاق`. At actual 768px, en/tr/fr/es/ar/ru produced the same localized modal behavior. Chinese fitted every column at 768px, so DataTables correctly hid the responsive control; the visible action remained localized as `详情`. No product or DOM override was introduced to force a modal where the responsive layout did not create one.

The shared payload includes `Details` and `Close` at `_DataTableL10n.cshtml:18-29`; modal title and Close application are in `dt-defaults.js:208-247,308-317`. Resource values are non-empty in all seven cultures. Browser console error/warning capture was empty.

The final-source accessibility regression also passed in-browser. At 390px, middle-row removal retained values `A/C`, reindexed to 12 unique IDs, preserved 12 exact label bindings, and restored focus to `shipmentLine_1_lineNumber`. At 768px, keyboard Add Line produced two rows, 12 unique IDs, 12 matching labels, focus on the new line, no `name` drift, and no overflow. The governing implementation is `create.js:15-50`. These browser results supplement the fresh focused test; they do not relabel earlier accessibility evidence.

## Inherited and NOT RUN boundaries

- The earlier accessibility acceptance is content-bound through the three preserved hashes; its runtime evidence remains inherited. This lane independently reran the related final-source focused and browser checks described above.
- A08 transition race, A09 stale POD, and A12 cross-LE are **NOT RUN**. The authorized immutable handoff supplied a single-tenant, read-only presentation fixture without concurrent mutation, stale POD, multi-LE, or Mongo persistence setup. Treating it as those scenarios would fabricate evidence.
- A10 fault proxy is **NOT RUN — explicitly unauthorized**. No proxy or fault seam was prepared.
- Durable PNG remains **OPEN**. The browser surface exposed no documented supported export mechanism, and no workaround was attempted.

## Scope and cleanup

This PASS covers only the exact 12-path shared-UI successor, preservation of the three accessibility paths, focused regression, JSON challenge/HTML redirect split, and seven-culture responsive/accessibility behavior described above. It is not CT acceptance, full Shipment UI/module acceptance, runtime persistence acceptance, rollout, or E5/G5.

No product source, shared source, pack, contract, guard, branch, staging area, commit, push or stash was changed. Only this owned audit directory and lane-local `/private/tmp` evidence were created. The browser viewport override was reset, the verifier tab was closed, and ports `55183`, `55184`, and `55185` were confirmed closed.

