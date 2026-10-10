# MVP6-SHIPMENT-UI-PRESENTATION-VER-01 — SOP §22

## Verdict

**REWORK (presentation-only scope).** The exact final source is reproducible and the seven-language, RTL, 390/768 responsive, contained-detail-table, filter, error-summary, and bounded action-surface checks largely pass. Two observable accessibility/localization defects and one shared quality-gate profile gap prevent closure. Durable PNG remains OPEN because the browser surface provides no documented permitted save/export capability.

This verdict does not reopen the functional CT acceptance, repeat its policy/runtime tests, or change the accepted B01/B02 and Root R2 disposition. It is not full-module, rollout, E5/G5, Auth, Gateway, or release acceptance.

## Authority and immutable input

- Branch/HEAD observed: `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`.
- Controlling CT: `mvp6-shipment-ui-functional-ct-consolidation-01/SOP-22.md` SHA256 `c3fb837e2f5db2e3d466a14c51b5de440d688f628ca71b16b39f84df8b6fb97e`.
- Controlling acceptance: SHA256 `e8fccf13083a945bfda95f72a019767eaadd054018eb0bafbc1445be8fc03664`.
- Final 354-source manifest SHA256: `e6551f4552bd05682dcd9f2753044aa0b803dd4df90875952cca89608ab98ab3`.
- Disposable source reconstruction matched **354/354** manifest entries. The mutable checkout matched only 91/354 and was not used as source authority.
- Native .NET 8 build exited 0. Final Web binary SHA256: `b7bf5cf8949e587da087565956dc3071e302c6cbe23513a6b4724dcf561df5ae`.

## Execution boundary

A stateless presentation fixture ran on `127.0.0.1:6030`; Diten.Web ran on `127.0.0.1:6031`. This lane used no Mongo database and did not access operational port 27017. The fixture supplied bounded UI data and an ephemeral local JWT only; it is not evidence for Auth, Gateway, persistence, replay, tenant/LE enforcement, or backend acceptance. Those controlling results remain inherited and were not rerun.

## Results

1. All seven resource sets have the same 63 keys with zero missing/empty values. Live list surfaces rendered in all seven cultures; Arabic rendered with `lang=ar` and `dir=rtl`. Browser console captured zero error/warning entries.
2. At actual `window.innerWidth` 390 and 768 with DPR 1, list/create/detail produced no document-level horizontal overflow. The 390 detail line table scrolls within its `.table-responsive` owner; the 768 table fits.
3. The DataTable uses v2, same-origin MVC, `window.DtDefaults`, server paging and `stateSave:false`. At 390 and 768 the action column collapses and remains reachable through the responsive control.
4. Static create fields and filters have bound labels. Invalid create submission exposes a focusable `role=alert` summary and moves focus to it. Keyboard Enter opens the approved transition and POD dialogs; their roles and field labels are present.
5. The generic repository DataTable verifier returned **49 PASS / 35 FAIL**. Its failures primarily demand deliberately unsupported Edit/QuickView/bulk/direct-Gateway/full generic CRUD behavior or reject equivalent absolute partial syntax. The accepted Shipment scope forbids adding those features. Until a scope-aware gate exists or the controlling gate policy is explicitly dispositioned, UI183-A14 cannot be marked fully closed.

## Exact open findings

- **PRES-183-01, module-owned:** `_Form.cshtml:23` renders six repeatable-line labels without `for` and inputs without unique `id`/accessible name. Live English and Arabic checks return empty `input.labels` for all six.
- **PRES-183-02, shared-owned:** `dt-defaults.js:304-310` supplies the responsive modal title from shared `window.L10n`, but Shipment's bounded payload does not provide shared responsive chrome localization; Arabic displays `Details` and `Close` in English.
- **PRES-183-03, shared quality-gate owner:** the generic Compact validator has no bounded Shipment profile and cannot simultaneously enforce its full-CRUD assumptions and the controlling prohibition on unsupported operations.
- **PRES-183-04, evidence environment:** no durable PNG was generated. Inline screenshots were inspected only and are not claimed as artifacts.

Exact expected behavior and closure tests are in `ACCESSIBILITY-FINDINGS.tsv`. The acceptance disposition is in `PRESENTATION-ACCEPTANCE.tsv`.

## No-change and cleanup

No product, shared layout, Gateway, Auth, pack, contract, or Git state was changed. Only this owned audit directory was written. The disposable build modified only its temp `bin/obj` outputs. Browser viewport was reset, the temporary tab was closed, both local processes were stopped, and the ephemeral JWT secret was removed. Because the repository was already broadly dirty, no repo-global no-change claim is made beyond the exact source reconstruction and this lane's owned writes.
