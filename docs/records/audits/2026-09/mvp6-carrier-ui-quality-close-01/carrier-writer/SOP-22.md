# MVP6-CARRIER-UI-QUALITY-CLOSE-01 — Carrier writer SOP §22

Date: 2026-09-23  
Role: Carrier-owned narrow source writer  
Writer state: **COMPLETE**

## Authority and baseline

- Registered checkout: `/Users/natig/.codex/worktrees/mvp6-carrier-ui-dev/ERP-vNext-recovery`.
- Accepted 21-path preimage manifest SHA-256: `e96578d0e874e8796469ee763aa9b6e723c7f0c9a966b5fe8a46b535279b26cb`.
- Fresh pre-edit comparison was reconstructed and verified: the two changed preimages match their recorded hashes and the remaining 19 paths remained byte-identical.
- Prior browser evidence: `mvp6-carrier-browser-evidence-01`, ten distinct localization warnings and zero JavaScript errors.

## Finding and narrow change

All ten reported warnings were false positives from the Carrier-owned `warnMissing` predicate. The predicate treated a localized value equal to its key as missing. English legitimately uses `Active`, `Apply`, `Cancel`, `Filter`, `Passive`, `Reset`, `Save`, `Status`, and `Unknown`; English and French legitimately use `Actions`.

The bridge now reports a missing entry only when the normalized payload lacks the own property, the value is not a string, or it is empty after trimming. It still detects a genuinely removed key. No `.resx`, shared resource, layout, backend, gateway, permission, Auth, pack, or contract file changed.

Changed Carrier-owned paths:

1. `frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Carriers/index.l10n.js`
2. `frontend/Diten.Web.Tests/JavaScript/CarrierIndexBehaviorTests.cs`

Exact source delta SHA-256: `57f90dadedd6e2304e3775fd1ea0bd7def29ad9d265ccbbeb892afdc04ba1adb`.  
New 21-path manifest SHA-256: `3b7086f0cc839c33f0753076e732aa436b9a8e5cd186f1484f2b0225364d033c`.

## Evidence

| Check | Result | Evidence |
|---|---:|---|
| Seven-language resource ownership/presence | PASS, 70/70 | `raw/L10N-RESOURCE-MATRIX.tsv` |
| Old predicate against the ten legitimate equal values | RED, 10 warnings | `raw/l10n-warning-probe.log` |
| New predicate against the same payload | GREEN, 0 warnings | `raw/l10n-warning-probe.log` |
| Negative mutation: remove `Save` | PASS, exactly one `Save` warning | `raw/l10n-warning-probe.log` |
| Focused Carrier tests | PASS, 14/14 | `raw/carrier-ui-quality-close.trx` |
| Frontend build | PASS, 0 warnings / 0 errors | `raw/frontend-build.log` |
| Disposable patch application | PASS; both outputs byte-equal target | `raw/patch-apply.log` |

Build binary SHA-256: `2b5406181d133dd9dce55c30f75f67ca3355b2648969280906b9abac308ff365`.  
Test binary SHA-256: `001fdfeca4dbe84291358d87ba2d869719230b739739e8cdd7450bd693e95834`.

## Evidence boundary

This writer closes the Carrier-owned warning predicate defect. The browser verifier must independently load the final source and confirm the console in the live browser. Responsive viewport evidence, shared `Search [CTRL + K]`, screenshots, status offcanvas, and Auth-dependent list/create/replay/status remain outside this writer result. No browser PASS is asserted here.

The first parallel build/test attempt collided on the shared `obj` static-web-assets cache. A second test invocation without roll-forward stopped because only ASP.NET Core 10 was locally installed. A third sandboxed invocation could not write the registered worktree `obj` directory. These discarded environment attempts are retained in `DISCARDED-ATTEMPTS.tsv`; the authorized sequential roll-forward run is the controlling 14/14 result.

No commit, push, or stash was performed.
