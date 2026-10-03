# Q91 — MOD-0186 Returns tenant UI: DRAFT code overlay (chat lane)

🤖 Applying knowledge of @frontend-ui-ux + @l10n-agent + @integration-agent (integration items as overlay only).
Chat lane on the linked Mac folder (Linux VM bridge). Repo `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`
(verified at start and end). Start 2026-09-26T19:45:04+03:00 (Europe/Istanbul). Git read-only (`GIT_OPTIONAL_LOCKS=0`); no commit
(Q03a). **Status: DRAFT — not built, not tested, not run, not writer-complete.** The only writes are this folder.

Authority: pack §32/§33; UI scope `mvp6-returns-claims-ui-scope-owner-decision-01.md` (`2ce9aa7d…7df10`); pack sign-offs
`mvp6-returns-pack-signoff-owner-decision-01.md` (`649269c3…4611`) and `mvp6-self-registration-patch05-signoff-owner-decision-01.md`
(`626055e4…0284744`); draft overlays in chat lanes `mvp6-draft-overlays-owner-decision-01.md` (`05033624…a67c`). Phase 1.5: see F1.

## Pack hash read

`execution/domains/supply-chain-execution/module-packs/MOD-0186-reverse-logistics.md` =
**`fa7bd61e7635cbf5a84c433562fb1a371ed80a08626f6f39e4b881c66f8c3e27`** (expected prefix `fa7bd61e`), at start and at the end; working
copy `/tmp/q91/pack.md`. Frontmatter `name: Reverse Logistics`, `shell: tenant`, `golden_reference: slim`, `form_field_count: 6`.

## Inputs (read-only, sha256)

| Input | sha256 |
|---|---|
| Pack (§31 binding, §32 UI scope, §33 self-registration) | `fa7bd61e7635cbf5a84c433562fb1a371ed80a08626f6f39e4b881c66f8c3e27` |
| Phase 1.5 table `docs/roadmap/plans/mvp6-decision-prep-01/PH15-UI-186.md` (still "NOT APPROVED — prepared text only"; F1) | `08bc9d8608132ebf5c257b11625495634f4e9a90d7660b1ec9eae0c185bcd3ef` |
| CT queue `docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv` (row Q65 names "PH15-UI-186 approved" as a dependency) | `c8059e985adeff68f08b20347c631af38f3c2d695970268001f5bd5f507e75e4` |
| UI draft package `docs/roadmap/plans/mvp6-ui-pack-drafts-01/SHA256SUMS` / `returns/APPROVAL-DECISION.md` / `returns/ACCEPTANCE.md` | `b568c94f7f524dd0bff1e2fa3277571fed174c6214ac0a9efd3ab13200abb57f` / `edb2b06d…b3b3` / `8b8d65fe…6b14` |
| Returns annex `docs/analysis/contracts/returns-semantics-v3.0.0.md` | `00990a289069d25a62f7c883aac718e08b96b0386a572e7d1f93f0e447e98a11` (= pack §31) |
| Root annex `docs/analysis/contracts/shipment-root-semantics-v3.0.0.md` | `7d1327a12b9775a594631dd9eb3c3c4c90e7f8429581f9ff7f42dde419f7b8af` (= pack §31) |
| OpenAPI `docs/analysis/contracts/shipment-bundle.openapi.yaml` (current, info.version 3.1.0; bound ops `queryReturns`, `createReturn`, `transitionReturn`, `getShipment` only) | `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2` |
| — pinned 3.0.0 copy `docs/records/audits/2026-09/mvp6-combined-final-release-pack-01/publication/docs/analysis/contracts/shipment-bundle.openapi.yaml` | `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c` (= pack §31; the 3 paths + 19 referenced components are identical to current, F10) |
| Accepted Returns source `docs/records/audits/2026-09/mvp6-mod0187-normal-baseline-integration-01/normal-source.tar.gz` (the combined source that applies the accepted Returns `product.patch`; PH15-UI-186 names it) | `edb759a07475184e11ae7ef94698f6300572b72aaeb2a39c7e2be13b74795a21` |
| — pack §31 evidence: R01 VER `product.patch` / raw archive; patched targets `ReturnReferenceReader.cs` / `ReturnReferenceTests.cs` found in the source | `0bb36d02…e82c` / `1191b5d9…5473`; `eaa0aa73…eea5` / `c36c4621…c7f7` |
| — `ReturnsController.cs` / `ReturnContextMiddleware.cs` / `ReturnContractError.cs` / `ReturnPermissions.cs` / `ReturnModels.cs` / `ReturnLifecycle.cs` / `ReturnStatus.cs` / `ReturnRepository.cs` / Api `Program.cs` | `dbb036cc…4fe` / `464bd767…bb6` / `6b1a88f8…a152` / `9c3f64ed…05a6` / `b4fec394…8c35` / `5a085a15…44ff4` / `7e533fb5…bca8` / `7e0c0f46…4108` / `11c586e0…b0f1` |
| Precedents: Claims `mvp6-claims-ui-draft-01/SHA256SUMS` (closest); S&OP `mvp6-sop-ui-draft-01/SHA256SUMS`; Capacity `mvp6-capacity-ui-draft-01/SHA256SUMS` | `80da27c9…08d3`; `0aafc340…8093`; `f4e18ab9…dbd7` |
| CT verdicts `mvp6-ct-verdicts-q64a-q77a-q78-2026-09-26.md` (F3/F6/F8) / `…-q83-q84-…` / `mvp6-ct-verdict-q88-2026-09-26.md` | `356cd273…05f3` / `06e054ad…3cd8` / `a320af9b…6a79` |
| A12 360 overlay (conventions + build dependency) `docs/records/audits/2026-09/mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz` | `7b6a0d1abea649d920040a039972c5d4928825e9cf2cc185487b1fe5f0f3314d` |
| `AGENTS.md`; `.antigravity/scripts/verify_module_id.py`; Golden reference **slim** (pack frontmatter; conventions through the Claims precedent) | `51d92c75…7fcb`; `3d208928…2b63`; — |

Backend citations (normal-source, file:line): routes and base permissions `ReturnsController.cs:13-21`, root-carrying wire response
`ReturnsController.cs:22`; header/scope gate `ReturnContextMiddleware.cs:48-100` — X-Correlation-Id one UUID echoed as response trace
(52-54), 401 (63), signed `tenant_id`/`legal_entity_id`/`sub` + base key → 403 (64-65), bad correlation 400 (66), **X-Tenant-Id and
X-Legal-Entity-Id required** (67-68), one Idempotency-Key 1–128 scalars (69-75), header ≠ claim → 404 `RETURN_NOT_FOUND` (76), scope
query keys rejected (78-79), status/shipmentId query validation (80-83), 415 (82), body schema and exact target grant (84-99); error
shape `ReturnContractError.cs:4` (message = code with spaces); keys `ReturnPermissions.cs:4-16`; schemas `ReturnModels.cs:5-24`;
arrows `ReturnLifecycle.cs:4-9`; statuses `ReturnStatus.cs:2`; eligibility `ReturnRepository.cs:57`; 409/422/404/503 codes
`ReturnRepository.cs:26-221`; Shipment read `ReturnReferenceReader.cs:55-90` (root missing → 503, malformed → 502, 404, 5xx → 503,
Shipment 500 `SHIPMENT_ROOT_INVALID` → 502 `RETURN_SHIPMENT_ROOT_INVALID`).

## What was produced

| Part | Content |
|---|---|
| `overlay/` module files (21 = §32.10) | adapter controller (1 page + 4 adapters, nothing else), view models, marker class, 6 views (list page with the transition offcanvas, filter, table, create offcanvas with the line picker, QuickView, L10n bridge), 2 JS files, 7 resx (105 keys each, incl. 21 `Err*` keys, ar RTL), 3 test files |
| `overlay/services/…` (2 = §33) | `ReverseLogisticsManifestProvider` + M-01…M-08 tests (M-02 reflects `ReturnPermissions.ForTarget`, D5 = A) |
| `overlay/_shared-integration/` (13) | Program.cs line (spec), gateway fragment (GET+POST `/returns`, POST `/returns/{returnId}/transition`, OPTIONS, 5-header passthrough, getShipment confirmation), frontend note, icon/SortOrder proposal (`bx-undo`, 420), platform checklist, nav keys × 7 — **nothing applied** |
| `runtime-scenarios/` | Playwright spec + README for Q65b (never run) |
| Records | `FILE-PLAN.tsv` (24 section + 21 owned + 2 SR-D4 rows), `SOURCE-MANIFEST.tsv` (36 rows), `STATIC-CHECKS.txt` (**168 PASS / 0 FAIL**), `NOT-VERIFIED.md`, archive `returns-ui-draft-overlay.tar.gz` (deterministic, 36 files, byte-identical to `overlay/`), `SHA256SUMS` — record hashes are in `SHA256SUMS` |

Behaviour in short: the list reads `queryReturns` through the same-origin adapter with only `status` (single select, ShowAll
omits it) and `shipmentId`; bounded client-side DataTables v2, Save View through `personalizationClient`, no browser storage, no
timer. Create resolves the Shipment (stale-response guard; a failure or a new UUID leaves no shipment data in the DOM), lists its
lines (shipped quantity labelled as such, not "remaining"), lets the user pick lines and type a return quantity sent as a JSON
string, keeps `reasonCode` presence-only and omits empty evidence; eligible statuses Delivered/Closed disable submit otherwise.
Row actions follow the frozen arrows and the exact target key (plus `.transition` and `shipments.read`), open a module-owned
offcanvas for `occurredAt` (offset, default now), `dispositionCode` (Dispositioned) and the optional inventory reference
(Received/Dispositioned), and confirm through `window.showConfirm`; Reject and Cancel are dangerous. Every POST carries a
per-intent Idempotency-Key reused only with the same target and identical body text; a replay is shown as completed, then the
list reloads. The adapter fills `X-Tenant-Id`/`X-Legal-Entity-Id` from the signed session (the Returns family requires them),
sends the Shipment root as `X-Correlation-Id` on POST and never returns it to the browser.

## Acceptance coverage (pack §32.11)

| Row | Covered by (static, this lane) | Runtime (Q65b) |
|---|---|---|
| RU-VS1 | controller tests (root correlation, session scope headers, byte-exact body, key) + form/JS tests | spec "Early vertical slice" (BLOCKED: target, gateway, G-SHIPREAD, seed) |
| RU-01 | controller tests (token and scope server-side, browser scope ignored); JS tests (no :5000/:5061, no token/scope) | spec "Returns list" (BLOCKED: gateway) |
| RU-02, RU-03 | controller test (2 query keys, omitted when absent); JS envelope check (malformed → error) | spec filter; NV-06 |
| RU-04, RU-05 | form tests (3 states, UAS-001 gate first) | spec list / permissions |
| RU-06, RU-07 | controller tests (every key combination → 403, zero gateway calls); page-permission test | spec permissions (RU-06 BLOCKED G-SHIPREAD) |
| RU-08…RU-10 | controller (byte-exact body) + JS tests (contract order, string quantity, presence-only reason, stale guard) | spec slice / late resolve |
| RU-11…RU-14 | controller (404 mapping) + JS (safe-not-found, no shipment data left, eligibility display) | spec ineligible/unknown; NV-06 |
| RU-15, RU-16 | root resolution tests; intent reuse/block tests | BLOCKED (producer uptake; DN-01) |
| RU-17…RU-25 | JS/form tests (stale 422 reload, manual-assertion wording, disposition code, offset, trace ≠ root, 7 languages + RTL, showConfirm, no SoR duplication) | spec transitions / localization |
| RU-26 | route-surface tests; gateway fragment explicit paths | BLOCKED (target) |
| RU-27 | SOURCE-MANIFEST + archive hash | Q65b binding record |
| RU-28 | — | BLOCKED (PRES-183-04) |
| RU-29 | JS test (QuickView from the loaded row, no request); form test (no Edit) | spec QuickView |
| RU-SCR-01…06 | route/JS tests (no checkbox/bulk, edit/delete, import/export, server paging, multi-select) | RU-SCR-06 verifier record in Q65b |

## Build note for Q65b (exact pieces; same pattern as Claims F3 / S&OP F2 / Capacity build note)

The overlay compiles only in a composed tree, in this order:
1. HEAD archive `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (common checkout baseline).
2. **BC-SOURCE** `docs/records/audits/2026-09/mvp6-bc-successor-exec-02/BC-SOURCE.tar.gz` `ebd5d80c…7064`. It already carries the
   accepted Returns backend: all 43 Returns paths in it are byte-identical to `normal-source.tar.gz` `edb759a0…5a21` (checked file by
   file), so no separate Returns archive is needed. Its Api `.csproj`/`Program.cs` carry **no** ModuleRegistration reference or
   registration foundation (the same holds for normal-source `Program.cs` `11c586e0…b0f1`).
3. **A12 360 overlay** `docs/records/audits/2026-09/mvp6-shipment-a12-safe404-rework-01/successor-source.tar.gz` `7b6a0d1a…314d` —
   `Diten.Web.Security.JsonAdapterEndpointAttribute`, the JSON challenge branch in frontend `Program.cs`, the SupplyChain
   registration foundation (`IModuleManifestProvider`, `ModuleRegistrationHostedService`, `PlatformRegistrationOptions`, the Api
   `.csproj` reference to `Diten.BuildingBlocks.ModuleRegistration.Abstractions`), `Nav.Domain.SUPPLYCHAINEXECUTION`, the Shipment
   adapter/route for `getShipment`, ocelot with 5061 = Supply Chain. **Reconcile** SupplyChain `Program.cs` and the Api `.csproj`
   between BC-SOURCE and A12 (both touch them).
4. Auth 22 `f50350b8…` (real-Auth tokens with exactly one `tenant_id`, `legal_entity_id` and `sub`, and `permission` claims —
   `ReturnContextMiddleware.cs:33-47, 64-65`).
5. This lane's `overlay/frontend/**` and `overlay/services/**` (new files only).
6. For routing and the nav guard in the environment only: `_shared-integration/` items on the environment copy (2 gateway routes
   + passthrough, the `AddSingleton` line, the 2 nav keys × 7).
Seed: a Delivered Shipment with a non-null `lifecycleCorrelationId` (producer root uptake, RU-15) and role grants that include
`supplychain.returns.transition` and `supplychain.shipments.read` (G-SHIPREAD).

## FINDINGS

- **F1 — Phase 1.5 approval not recorded.** `PH15-UI-186.md` (`08bc9d86…d3ef`) still reads "NOT APPROVED — prepared text only";
  no owner decision or CT record approving it exists in `docs/records/decisions/2026-09/` or `docs/records/audits/2026-09/`
  (searched for PH15-UI-186 and every Returns/186 decision). Only `CT-QUEUE.tsv` row Q65 names "PH15-UI-186 approved" as a
  dependency. As the dispatch says, this lane continues as DRAFT; CT records the approval (as it did for Claims, Q64a F1).
- **F2 — Error-code set.** PH15-UI-186 binds 22 codes including `SHIPMENT_ROOT_INVALID`. The contract publishes **21** codes for
  the three bound Returns operations, and the backend's wire set is the same 21; `SHIPMENT_ROOT_INVALID` is only the Shipment's
  own code that the Returns reader turns into `RETURN_SHIPMENT_ROOT_INVALID` (`ReturnReferenceReader.cs:42, 81-82`). The draft
  localizes the 21 published codes; the adapter mirrors the same translation. CT confirms the 21-code set.
- **F3 — Build dependency.** The overlay builds only on BC-SOURCE + the A12 360 overlay (build note above).
- **F4 — Port 5061 in the common checkout** (carried from S&OP F3 / Capacity F3). The fragment follows the A12 shape.
- **F5 — Pack §32.14 wording.** "UI code not authorized until an integrated target exists" vs the owner decisions "modules first"
  and "draft overlays" — same as Claims F2 (CT: decisions take precedence; pack wording in a later revision). No pack edit here.
- **F6 — G-MODAL.** `showConfirm` cannot host inputs, so `occurredAt`, `dispositionCode` and the inventory reference live in a
  module-owned offcanvas inside the owned `Index.cshtml` (no 22nd path); the final confirmation goes through `window.showConfirm`
  — the placement CT accepted for Claims (Q64a F6).
- **F7 — G-DATETIME.** `occurredAt` is a plain LTR text input prefilled with local now and its explicit offset; the exact text is
  kept for retries. No shared date-time component was chosen.
- **F8 — Root echo.** The Returns backend echoes the incoming `X-Correlation-Id` (= Shipment root on POST) in the response header
  and in `error.correlationId` (`ReturnContextMiddleware.cs:52-54, 60`). The adapter passes success bodies through (no
  correlation in `ReturnResponse`), replaces the header and the error `correlationId` with the browser trace, keeps status and
  code, and logs trace ↔ root — the rule CT accepted for Claims (Q64a F8).
- **F9 — Scope headers differ from Claims and Capacity.** The Returns family **requires** `X-Tenant-Id`/`X-Legal-Entity-Id`
  (`ReturnContextMiddleware.cs:67-68`; a mismatch with the token is 404 `RETURN_NOT_FOUND`, line 76). The adapter fills both from
  the caller's signed claims on every Returns and Shipment call; the browser never sends or sees them.
- **F10 — Contract pin drift.** The checkout YAML is `info.version 3.1.0` (`6dc1dd48…`), the pack pins 3.0.0 `5dfe7c1b…` (§32.13
  "Loads 3.1.0 forward drift"). The three bound paths and their 19 referenced components are identical in both (compared
  structurally), so the draft is valid against either.
- **F11 — G-ICONMAP.** Proposed icons in `_shared-integration/icon-map.proposal.md`; the shared test edit belongs to the
  integration owner.
- **F12 — First-open progress.** Not specified by the pack (PH15 F-186-3); the create form shows no progress counter (as Claims).
- Carried, not decided here (pack §32.13): line-picker layout at 390/768 (NV-07), verifier tension (RU-SCR-06), G-TARGET, DN-01,
  producer root uptake, PNG, nav/personalization codes.

## ASSUMPTIONS

- **A1** Built against the bound operations of the published YAML; they are identical in the 3.0.0 pin and the current 3.1.0 (F10).
- **A2** The transition's `shipmentId` lookup hint travels as a query parameter on the approved MVC route
  (`POST /SupplyChain/Returns/api/{returnId}/transition?shipmentId=`), so the body stays byte-exact; it is not forwarded (Claims A2).
- **A3** Line picker: one table row per resolved Shipment line (checkbox + read-only line number, shipped quantity and UoM + a
  return-quantity text input); only selected rows are sent, in Shipment order. `lines` has `minItems: 1`, so submit needs one
  selected line. A source line without a text `lineNumber`/`uomId` is shown but not selectable.
- **A4** Adapter-originated failures use published codes only: `INVALID_REQUEST` with status 400/401/403/415 (annex D186-05);
  Shipment 404 → `SHIPMENT_NOT_FOUND` (resolve/create) or `RETURN_NOT_FOUND` (transition hint); Shipment 5xx/timeout → 503
  `DEPENDENCY_UNAVAILABLE`; Shipment 500 `SHIPMENT_ROOT_INVALID` → 502 `RETURN_SHIPMENT_ROOT_INVALID`; unreadable Shipment → 502
  `DEPENDENCY_RESPONSE_INVALID`; root missing/null/empty → 503 `RETURN_SHIPMENT_ROOT_UNAVAILABLE`, malformed → 502
  `RETURN_SHIPMENT_ROOT_INVALID`; transport failure toward Returns → 503 `PERSISTENCE_UNAVAILABLE` (outcome unknown); an unpublished
  upstream code is mapped by status. Wire message = the code with spaces (backend default).
- **A5** The antiforgery header is `RequestVerificationToken` (golden slim).
- **A6** Save View uses `moduleKey: 'reverse-logistics'`, `pageKey: 'RETURNS'` (the §33 codes) until the integration owner confirms.
- **A7** `showToast`/`showConfirm` receive resx text plus a shape-checked UUID reference only.
- **A8** The status column shows "Received"; the transition target and action carry "(manual assertion)".
- **A9** `inventoryTransactionReferenceId` is offered for Received and Dispositioned and omitted when empty; `dispositionCode` is sent
  only for Dispositioned, as typed (whitespace kept), with the annex rule length ≥ 1 mirrored client-side.
- **A10** The Playwright accept selector for the shared confirmation is assumed `.swal2-confirm`; Q65b confirms it.
- **A11** Manifest action sort orders (pack silent): CREATE 10, then 20…80 in the §33 table order.
- **A12** Nav key values in 7 languages are proposals (the pack supplies none; l10n review).
- **A13** `INVALID_REQUEST` text follows the status (400 form summary, 401 session, 403 denial, 415 media) through three
  `Notice*` keys; the 21 `Err*` keys map one-to-one to the published codes.
- **A14** `SHIPMENT_LINE_NOT_FOUND` (404) keeps the form open with its specific text; every other create 404 closes the form, clears
  the shipment data and reloads the list.
- **A15** Static checks ran in the VM workspace (`/tmp/q91`), not on the Mac; `verify_datatable_page.py` is left to Q65b (record-only).

## Applying the overlay (Q65b, isolated environment only)

1. Compose the tree (build note); verify `SHA256SUMS` and every `SOURCE-MANIFEST.tsv` hash.
2. Copy `overlay/frontend/**` and `overlay/services/**` to the same repo-relative paths (new files only).
3. Apply `_shared-integration/` items to the environment copy only; the integration owner applies them to the real shared files at final integration (SR-D4).
4. `dotnet build` / `dotnet test` (frontend + SupplyChain), static checks again, `verify_datatable_page.py` (record), then `runtime-scenarios/`.

## To-do

1. CT: record the PH15-UI-186 approval (F1); confirm the 21-code set (F2).
2. Q65b (local Mac): compose, build, test, runtime; record evidence.
3. Integration owner (later): `_shared-integration/` items, port reconciliation (F4), G-SHIPREAD role grants.

## End state

`git status --porcelain` against the start snapshot: the only new path from this lane is `docs/records/audits/2026-09/mvp6-returns-ui-draft-01/`; the other new paths (`mvp6-ct-verdict-q88-2026-09-26.md`, `mvp6-q87-ver-text-patch-q83/`, `mvp6-capacity-ui-ids-in-address-owner-decision-01.md`, `mvp6-effort-update-09/`, `mvp6-text-patch-q90-01/`) belong to the parallel lanes (Q90, Q87). No tracked file changed; no `.git/index.lock`; HEAD `4a8d4d4b`; MOD-0186 `fa7bd61e…3e27` at the end.

Hand-off: 2026-09-26T20:10+03:00 — Q91 writer hand-off (MOD-0186 Returns UI draft, chat lane): DRAFT, not writer-complete, uncommitted; 45 files (36 overlay + archive + 5 records + 2 runtime-scenarios + SHA256SUMS), SHA256SUMS 44/44, static checks 168 PASS / 0 FAIL; MOD-0186 fa7bd61e…3e27 read and unchanged. The ledgers are owned by Q90; CT records this hand-off.
