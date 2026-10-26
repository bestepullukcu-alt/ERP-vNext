---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E3
decision: accepted
commit:
---

# SupplyChain l10n glossary — Part A2 error-architecture convergence (executed, module by module)

    executed  : 2026-10-18 by CT (gmg-10), module by module, on the owner ruling (A1 = {Claims,Returns}
                camp, Err* error-code key convention). Scope: Carriers, Shipments, Loads — bring their error
                resx keys onto the Err* convention the {Claims,Returns} modules already use. 26 files.
    method    : measured each module's BACKEND wire codes first (the ERROR_KEY map must match what the
                service emits, not just what the old JS mapped), then renamed resx keys + updated the L10n
                bridge, touching JS only where the module's JS referenced the resx key names directly.

## Per module

**Carriers** — JS referenced the resx keys directly (`messageForError` switch → `L.InternalError`/
`L.AccessDenied`/…). Converted to the camp shape: a frozen `ERROR_KEY` map (wire `failure.code` → `Err*`
key) + `STATUS_FALLBACK_CODE` + `t()`, unknown → `ErrInternalError`. Renamed 8 resx keys (7 langs) + bridge
+ the direct JS refs. Backend wire-code set confirmed from the Carriers feature: CARRIER_NOT_FOUND,
CARRIER_CODE_CONFLICT, IDEMPOTENCY_KEY_REUSED, INVALID_CARRIER_TRANSITION, INVALID_REQUEST,
PERSISTENCE_UNAVAILABLE, INTERNAL_ERROR (+ FORBIDDEN/403) — all mapped. `ErrorOccurred` is a SharedResource
key (`SharedLocalizer`), left as-is.
 - renames: AccessDenied→ErrForbidden, ValidationError→ErrInvalidRequest, InternalError→ErrInternalError,
   CarrierNotFound→ErrCarrierNotFound, CarrierCodeConflict→ErrCarrierCodeConflict,
   IdempotencyKeyReused→ErrIdempotencyKeyReused, InvalidCarrierTransition→ErrInvalidCarrierTransition,
   PersistenceUnavailable→ErrPersistenceUnavailable.

**Shipments** — a bespoke bridge (`@Json.Serialize(new { camelProp = Localizer["PascalKey"] … })`) decouples
camelCase JS props from resx keys. So only the resx keys + the bridge's `Localizer[...]` args were renamed;
the camelCase JS layer (`L.accessDenied`, `L.notFound`, …, consumed by index/create/details.js) is
unchanged. Shipments JS: 0 files changed.
 - renames (8): AccessDenied→ErrForbidden, ValidationError→ErrInvalidRequest, InternalError→ErrInternalError,
   NotFound→ErrShipmentNotFound, InvalidTransition→ErrInvalidShipmentTransition,
   PodAlreadyCaptured→ErrPodAlreadyCaptured, IdempotencyKeyReused→ErrIdempotencyKeyReused,
   PersistenceUnavailable→ErrPersistenceUnavailable. (Note: the bridge now reads e.g.
   `accessDenied = Localizer["ErrForbidden"]` — the resx key is canonical; the camelCase prop name is
   Shipments' own JS style and was intentionally left, out of the glossary's key-name scope.)

**Loads** — already half-converged (domain `Err*` keys + `ErrInternalError`). Only the status-fallback used
plain `AccessDenied`/`ValidationError`. Renamed those two (resx + bridge + JS `L.` refs). Loads' dispatcher
keeps its `({...})[code] || fallback` shape (functionally identical to the camp; it already resolves to
`Err*` keys).

## Verification (CT, per module + cross-module)

- **Parity:** distinct key-name count identical across all 7 languages — Carriers 45, Shipments 72, Loads
  62 (unchanged by the 1:1 renames).
- **ERROR_KEY coverage (Carriers):** every mapped target (`ErrForbidden`, `ErrInvalidRequest`,
  `ErrInternalError`, `ErrCarrierNotFound`, `ErrCarrierCodeConflict`, `ErrIdempotencyKeyReused`,
  `ErrInvalidCarrierTransition`, `ErrPersistenceUnavailable`) present 7/7; bridge exposes all 8.
- **Values preserved:** spot-checked that each renamed key carries its original value under the new name
  (e.g. Carriers `ErrForbidden` = "You do not have permission for this action.", `ErrPersistenceUnavailable`
  = "The request outcome is unavailable."). An intermediate mis-iteration (a shell loop that did not
  word-split) briefly mis-renamed one Carriers key; it was caught by this value check and corrected forward
  — final values verified correct.
- **No dangling refs:** global scan for every old plain key name (`name="…"`, bridge `["…"]`,
  `Localizer["…"]`, `L.…`) across the three modules' resx + views + js returns 0. Cross-module: **no plain
  infra error key (`AccessDenied`/`ValidationError`/`InternalError`/`PersistenceUnavailable`/… ) survives as
  a resx key in any of the five modules.**
- **XML:** every changed resx parses.
- **JS syntax:** `node --check` passes on all five modules' `index.js` (incl. Carriers' rewritten
  dispatcher). Not run: live Razor/browser (full-stack build out of proportion); CI compiles the views.

## Still routed (unchanged by A2)

Part B native-language term calls; Part C SharedResource Active/Passive collision. A2 is keys + JS logic
only and did not need them.

## Files

- `REPORT.md` — this. Code: 21 resx + 3 `_IndexL10n.cshtml` + 2 `index.js` (Carriers, Loads).
