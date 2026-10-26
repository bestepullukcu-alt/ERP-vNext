---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E3
decision: accepted
commit:
---

# SupplyChain l10n glossary — Part A safe-tier convergence pass (executed)

    executed  : 2026-10-18 by CT (gmg-10) on the owner ruling
                (mvp6-supplychain-l10n-glossary-owner-decision-01, A1 = {Claims,Returns} camp)
    scope     : the low-risk subset of Part A — three pure-label key renames + three value corrections
                across Shipments/Carriers/Loads/Claims/Returns. The heavier Err*/error-architecture
                convergence of Carriers & Shipments was carved out to PART-A2 (below) by owner choice
                ("safe tier now, scope the rest"). 31 files changed; verified by CT.

## What changed

**Key renames (key name + `_IndexL10n.cshtml` bridge + `_DataTable.cshtml` + JS `L.`/`t()` refs, all 7 langs):**
- `EmptyList` → `EmptyState` — Carriers, Loads (Shipments already had it)
- `Actions` → `ActionsHeader` — Carriers, Shipments (incl. the `_DataTable` column header)
- `ListUnavailable` → `ListErrorState` — Carriers, Loads, Shipments

**Value corrections (no rename):**
- **A2** `AccessDenied` (Carriers, 7 langs): "You **no longer** have permission…" → the canonical
  "You **do not** have permission for this action." (converged to the Loads/Shipments form). The UAS-001
  denied card is the generic not-authorised card, not a revocation.
- **A3** `CarrierId` (Shipments, 7 langs): "Carrier"/"Transporteur"/… → the id form "Carrier ID"/
  "ID du transporteur"/… (converged to Loads; owner confirmed it is the carrier-identifier field).
- **A4** `ShipmentStatusException` (Claims + Returns, fr & es only): "Incident"/"Incidencia" → the Shipments
  source value "Exception"/"Excepción". The other five languages already matched the source.

## Verification (CT)

- **Scope:** 31 files — 7 resx × {Carriers, Loads, Shipments} + 2 resx × {Claims, Returns} (fr/es) + 5
  cshtml (2 `_IndexL10n`, wait: Carriers/Loads/Shipments `_IndexL10n` + Carriers/Shipments `_DataTable`) +
  2 JS (Carriers, Loads). `git status` shows no stray files; Shipments JS unchanged (it renders those keys
  server-side, so no `L.` ref to convert).
- **XML:** every changed resx parses.
- **Parity:** distinct key-name count identical across all 7 languages in each module — Shipments 72,
  Carriers 45, Loads 62, Returns 106, Claims 95. New keys present 7/7.
- **No dangling refs:** global scan for the old key names (`name="…"`, bridge `["…"]`, `Localizer["…"]`,
  `L.…`) across all five modules' resx + views + js returns **0**.
- **Value spot-checks:** Carriers.en AccessDenied = "You do not have permission for this action.";
  Shipments.en CarrierId = "Carrier ID"; Claims.fr ShipmentStatusException = "Exception".
- **Not run:** no Razor build / live preview (structural change across the full web app + gateway + services
  is out of proportion); CI/integration compiles the Razor views and runs the UI. The cshtml edits are
  shape-identical (`Localizer["X"]`, `["X"] = Localizer["X"].Value`), so they do not change Razor syntax.

## Deferred — not done here (by design)

- **PART-A2 (architectural tier):** converge Carriers & Shipments error handling to the {Claims,Returns}
  `ERROR_KEY`/`STATUS_FALLBACK_CODE`/`failureText()` scheme and the `Err*` error-code keys
  (`AccessDenied`→`ErrForbidden`, `InternalError`→`ErrInternalError`, `ValidationError`→`ErrInvalidRequest`,
  …). This rewrites each module's error-display logic — app logic, not a glossary edit — so it is its own
  task (`PART-A2-ERROR-ARCHITECTURE.md`).
- **Part B** native-language term calls and **Part C** SharedResource collision — unchanged, still routed
  to a translator and the SharedResource owner.

## Files

- `REPORT.md` — this. Code: the 31 files above.
