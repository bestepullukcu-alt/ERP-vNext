---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E3
decision: conditional
commit:
---

# Part A2 — Carriers & Shipments error-architecture convergence (scoped, not executed)

    raised    : 2026-10-18, carved out of the Part A pass by owner choice ("safe tier now, scope the rest")
                · author: CT (gmg-10)
    status    : DONE 2026-10-18, module by module (Carriers, Shipments, Loads). The error resx keys now use
                the Err* convention across all five modules; 0 plain infra keys remain. Execution + proof:
                docs/records/audits/2026-10/mvp6-supplychain-l10n-partA2-pass-01/REPORT.md. The scope below
                is kept for the record.

## Why it is separate from the Part A safe tier

The glossary ruling (A1 = {Claims,Returns} camp) includes the `Err*` error-code convention. But converging
Carriers and Shipments to it is not a key rename — those two modules use a different error-display
architecture:

- **{Claims, Returns}** (the target): a frozen `ERROR_KEY` map (wire `failure.code` → `Err*` resx key), a
  `STATUS_FALLBACK_CODE` map (HTTP status → wire code when the envelope carries none), and
  `failureText(f) = t(ERROR_KEY[f.code] || 'ErrInternalError')`. resx keys: `ErrInvalidRequest`,
  `ErrForbidden`, `ErrUnauthenticated`, `ErrInternalError`, `ErrIdempotencyKeyReused`, + domain errors.
- **Carriers / Shipments** (current): a small `messageForError(code, status)` switch mapping to
  `L.InternalError` / `L.AccessDenied` / `L.ValidationError` / `L.ErrorOccurred`. resx keys: plain
  `InternalError`, `AccessDenied`, `ValidationError`.
- **Loads** is already half-converged (has `ErrInternalError` + the domain `Err*` keys), so it is a smaller
  job than Carriers/Shipments.

## Scope of the change (measured)

Per module (Carriers, Shipments; Loads partial):
1. Introduce `ERROR_KEY` + `STATUS_FALLBACK_CODE` maps and `failureText()` in `index.js`, replacing the
   `messageForError` switch — mapping each wire code the module's API returns to an `Err*` key.
2. Rename the infra resx keys to the `Err*` form (`AccessDenied`→`ErrForbidden`,
   `InternalError`→`ErrInternalError`, `ValidationError`→`ErrInvalidRequest`, add `ErrUnauthenticated`),
   across all 7 languages, and update the `_IndexL10n.cshtml` bridge.
3. Enumerate each module's actual wire `failure.code` set (from the service contracts) so the `ERROR_KEY`
   map is complete — an incomplete map silently falls back to `ErrInternalError` and hides real errors.

## Risk / why it is owned carefully

A wrong or incomplete `ERROR_KEY` map shows the wrong message, or a generic one, for a real error — a
UX/correctness regression that only surfaces at runtime on the specific error path. This needs the module's
wire-code inventory and a runtime check of the error paths, which is why it is not folded into the safe
glossary pass. Recommended executor: the l10n-agent (resx + bridge) plus a frontend check of the error
paths, or CT with a per-module runtime verification. Prerequisite: the Part B native wording is not required
for A2 (A2 is keys + JS logic), so A2 can proceed independently of Part B.

## Files

- `PART-A2-ERROR-ARCHITECTURE.md` — this scope. The safe tier that WAS executed:
  `docs/records/audits/2026-10/mvp6-supplychain-l10n-partA-pass-01/REPORT.md`.
