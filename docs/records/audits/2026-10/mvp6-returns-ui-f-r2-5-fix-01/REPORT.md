---
module: MOD-0186
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E3
decision: accepted
commit:
---

# MOD-0186 Returns UI — F-R2-5 fixed: the list (read) error panel no longer shows write-mutation wording

    fixed     : 2026-10-18 · author: CT (gmg-10)
    file      : frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Returns/index.js:348
    verdict   : one-line defect fix, aligned to the already-accepted Claims behaviour. Applied (frontend is
                not a protected path). Verified by inspection + precedent; a browser re-check is for the UI
                VER (the web stack is down this session).

## The defect (R-2 F-R2-5)

The Returns **list error panel** rendered its message as:

```js
if (message) message.textContent = failure.code ? failureText(failure) : t('ListErrorState');
```

So a failed list **load** (a read) with any envelope code showed `failureText(failure)` — for a 503 that
is `ErrPersistenceUnavailable` = *"Returns are temporarily unavailable. The outcome is not confirmed; try
the same request again."* That is **write-mutation wording** (idempotent-retry / "outcome not confirmed"),
wrong for a GET. The module already carries the correct read string, `ListErrorState` = *"Returns could not
be loaded."*, but it was gated behind `!failure.code`.

Only the **list panel** (`returns-error-state` / `returns-error-message`) is affected; the create/transition
(write) surfaces use a different display path where "the outcome is not confirmed" is correct, and were not
touched.

## The fix

```js
if (message) message.textContent = t('ListErrorState');
```

The list panel now always shows the read string; the correlation reference is still rendered below it
(unchanged). This **exactly matches the already-accepted Claims lane** — `SupplyChain/Claims/index.js:341`
uses `t('ListErrorState')` unconditionally (R-4c, later than R-2, had corrected this same pattern). So the
fix is precedent-verified, not a fresh UX judgment; the trade-off (a 401/403 on a list shows "could not be
loaded" + reference rather than a per-code notice) is the one Claims already ships.

## Cross-module check

- **Claims** (`index.js:341`): already correct — no change needed.
- **Carriers / Loads**: do not use this list-panel pattern (no matching line); not affected.

So F-R2-5 was unique to Returns among the four; it is now closed and consistent with Claims.

## Verification

- `git apply --check` clean; applied; the line now reads `t('ListErrorState')`.
- By inspection: only the read panel's message selection changed; write surfaces and the reference row are
  untouched.
- By precedent: identical to the CT-ACCEPTED Claims behaviour.
- Not browser-verified this session (web stack down) — the independent Returns UI VER should confirm the
  panel renders "Returns could not be loaded." on a forced list-load 503.

## Files

- `REPORT.md` — this record. Code change: `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Returns/index.js`.
