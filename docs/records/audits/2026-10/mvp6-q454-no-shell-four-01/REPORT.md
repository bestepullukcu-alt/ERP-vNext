# verify from: /Users/natig/Projects/ERP-vNext-recovery/docs/records/audits/2026-10/mvp6-q454-no-shell-four-01 — `shasum -a 256 -c ARTIFACTS.sha256`

# Q454 — no-shell row for Carriers, Loads, Returns, Claims: STOPPED before any measurement

- Lane: Q454, frontend-ui-ux. Recorded 2026-10-05.
- Preflight: `Sun Oct  4 22:39:26 UTC 2026` · `feature/mvp6-logistics` · HEAD `cea01354e` · porcelain 80 · staged 0 ·
  no `.git/index.lock`.
- **No stack started, no browser session, no file changed outside this record. Nothing staged or committed.**

## Why it stopped

CT-QUEUE.tsv (545 lines) has **no Q454 row**: `grep -c Q454` = 0, and no row describes this work. Q446 (`:543`,
`DONE (CT ACCEPTED)`) records the finding ("no lane ever clicked Save View or column visibility") but does not open a
work item for it. SOP v2.5 §20.1 / R8: a WP whose row is missing stops.

Owner answer in the Code session, 2026-10-05: **"Stop, wait for the row"**. This is the same rule as Q372 ("Dur, satır
gelsin").

## Read-only premise check, for the re-dispatch (code reading only, NOT the measurement)

The dispatch asks whether the control is shared, "one answer for all four". From the code:

- **The persistence client is shared.** All four list pages call `window.personalizationClient`
  (`wwwroot/assets/js/personalization-client.js`), which saves and loads views on the server at
  `/api/personalization/views`: `getViews` `:124`, `saveView` `:141`, `updateView` `:153`. No module uses browser
  storage for the view.
- **The wiring is per module, so one live answer cannot be assumed for all four.** Each page has its own
  `getViews`/`saveView` call site, its own list of column indexes, and its own scope key:

| module | scope key (`index.js`) | columns covered | `getViews` / `saveView` |
|---|---|---|---|
| Carriers | `{ moduleKey: 'SupplyChain', pageKey: 'Carriers' }` `:11` | `[1,2,3,4]` `:14` | `:160` / `:185` |
| Loads | `{ moduleKey: 'SupplyChain', pageKey: 'Loads' }` `:12` | `[1,2,3,4]` `:15` | `:147` / `:165` |
| Returns | `{ moduleKey: 'reverse-logistics', pageKey: 'RETURNS' }` `:25`, marked "ASSUMPTION A6" | `[1,2,3]` `:27` | `:897` / `:942` |
| Claims | `{ moduleKey: 'claims-management', pageKey: 'CLAIMS' }` `:25`, marked "ASSUMPTION A6" | `[1,2,3,4,5]` `:27` | `:912` / `:941` |

- **Two naming schemes for the same kind of key.** Carriers and Loads scope views as `SupplyChain` plus a page name.
  Returns and Claims use the §33 manifest codes, and both say in a comment that the integration owner still has to
  confirm them. If the personalization service validates `moduleKey` against registered modules, one pair may be
  refused. That is exactly what the live click has to show. It is a hypothesis from reading the code, not a result.
- **The column-visibility button comes from the shell.** DataTables `buttons.colVis.js` is loaded by
  `Views/Shared/_LayoutTenantShell.cshtml:524`. Whether a toggle survives a reload depends on each module's own
  Save View path above, not on the button itself.

## Needed to proceed

A CT-QUEUE row for Q454, then a re-dispatch. The live session is then:
1. one stack and one tenant Admin whose token carries `legal_entity_id`;
2. for each module: toggle a column, Save View, reload, and capture a screenshot before and after;
3. also check the network response from `/api/personalization/views` for each of the four scope keys.

Agent verdict ≠ CT ACCEPTED. Nothing committed, nothing pushed, nothing staged.
