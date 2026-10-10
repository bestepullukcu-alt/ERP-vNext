# MOD-0187 Claims — tenant UI DRAFT overlay v2 (Q64c)

> **Status: DRAFT (not writer-complete).** Built and unit-tested on the Mac against the Q97 composed tree. **Runtime (browser)
> NOT re-run for v2.** Agent PASS ≠ CT ACCEPTED.
> Per owner decision **D6**, this folder holds the draft code **only as an archive**. There is no open `overlay/` folder here.
> Extract to `/tmp` or `~/mvp6-env/` to use it.

| Field | Value |
|---|---|
| WP / prompt | WP-MVP6-187-UI-064c · Q64c v1 (Phase 4 rework, `@orchestrator` + `/add-module` → frontend-ui-ux → testing-agent) |
| Pattern (§17.3) | Table/bulk list + offcanvas actions (pack §32; no Details page); expert operator, high-volume scan and process |
| Base | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (repo read-only; 19 diff paths unchanged) |
| Pack | MOD-0187 `96c9a0aeb94d6941141c6a8cb96d159c29991c8a8eeb589db909ab2677471ae2` (`ready-for-dev`) §32/§33 |
| Contract | `docs/analysis/contracts/claims-semantics-v3.0.0.md` `16e65c26…` (untracked), §D187-05 |
| Predecessor | v1 `mvp6-claims-ui-draft-01/claims-ui-draft-overlay.tar.gz` `bc0f5819…` (Q64a, CT ACCEPTED as DRAFT) |
| Inputs applied | Q64b `mvp6-q64b-claims-build-01/FIXES.patch` `1b7a24ee…` (D-01, D-02, FIX-01), Q64b S-01/S-02, Q101 F06 real gaps |
| Workspace | `~/mvp6-env/q64c/`: `v1/`, `v2/`, `tree/` (APFS clone of Q97 `~/mvp6-env/claims` + v2), `diff-232549/`, `pack-232603/`. Q97's `claims/` was not modified. |

## Contents

| File | What |
|---|---|
| `claims-ui-draft-overlay-v2.tar.gz` | **The draft.** 39 files: `overlay/frontend/**`, `overlay/services/**`, `overlay/_shared-integration/**`, `runtime-scenarios/**`. Deterministic: fixed mtimes, uid/gid 0, no AppleDouble/xattrs, `gzip -n`. A rebuild gives the same hash. |
| `SOURCE-MANIFEST.tsv` | archive path → sha256 for all 39 files |
| `FILE-PLAN.tsv` | 39 rows. For each file: future repo path, class, pack justification, v2 change and reason, sha256. Rows 37–39 are new: the FIX-01 hunk, the scenario README, and the `_shared-integration/README.md` that v1 did not list. |
| `CHANGES.md` | v1 → v2 diff with the reason for each change (C-01…C-06), plus what was not changed and why |
| `STATIC-CHECKS.txt` | node, xmllint, JSON, resx parity, error-code set, greps, verifier totals |
| `verify_datatable_page-v2.txt`, `verify_datatable_page-v2-proxy.txt` | full verifier output (record only, CU-SCR-06) |
| `Diten.Web.Tests.trx`, `Diten.Web.Tests.log` | `dotnet test` of `frontend/Diten.Web.Tests` on the v2 tree |
| `SHA256SUMS` | sha256 of every file in this folder except itself |

## Results

| Check | v1 (Q64b, as delivered) | v2 |
|---|---|---|
| `Diten.Web.Tests` (.NET SDK 8.0.417, Debug) | 241/241 | **242/242** (Claims 84/84; +1 new test). 0 warnings in Claims files. |
| New test sabotage | — | `@model` line removed → `Create_form_is_strongly_typed…` **FAILS** (`Assert.StartsWith`); restored → passes |
| `verify_datatable_page.py --reference slim` | 63 PASS / 27 FAIL | **70 PASS / 21 FAIL**. The 6 targeted F06 checks pass; the remaining 21 are pack-OUT, shared-file or direct-gateway rules (Q107). |
| same, `--api-profile proxy` | — | 72 PASS / 20 FAIL |
| `node --check` (index.js, index.l10n.js, spec) | — | 3/3 |
| `xmllint` (7 resx + 7 nav fragments, wrapped) + gateway JSON | — | 15/15 |
| resx parity | 7/7 | **7/7** (95 keys each, 0 empty, 0 echo) |
| error codes: UI `ERROR_KEY` vs pack §32.8 vs contract §D187-05 | — | **18 = 18 = 18** |
| greps: native dialogs, inline handlers, browser storage, token/ports, English in views | — | 0 real hits (see STATIC-CHECKS §4) |
| FIX-02/FIX-03 files vs Q64b `claims-fixes` tree | — | 7/7 byte-identical before the Q64c changes (served `index.js` `bd15a9f3…`) |
| FIX-01 hunk on the A12 copy | — | applies; result = Q64b fixed file byte-for-byte |
| Runtime (Playwright, kit lanes) | 12/2/3 spec; harness FAIL on D-01/D-02 | **NOT RUN for v2**: C-04 (Save View/Reset) and C-06 (spec) are unverified in a browser |

## FINDINGS

- **Q64c-F1:** Q101 F13 does not apply to Claims. Claims already uses `PageDescription` (7/7), so nothing was renamed (CHANGES.md).
- **Q64c-F2:** v1 `FILE-PLAN.tsv` did not list `overlay/_shared-integration/README.md`, although it was in the v1 archive. v2 lists it as row 39.
- **Q64c-F3:** The Save View saved-state shape changes from v1's `columnVisibility[]` to golden `colVis{}` + `columnOrder`.
  A view saved by v1 in a lane DB loads as the factory column state (filters/search/order are still read).
- **Q64c-F4:** In v1, runtime scenarios and code lived in open folders. D6 moves both into the archive. The v1 folder (open `overlay/`)
  is protected and was left as is; its hold-from-commit is a CT/owner item (D6).

## Next (for CT)

1. The runtime re-run of v2 (kit lane; row-scoped harness + the fixed spec) must include Save View/Reset: Apply → dirty, Reset →
   factory + clean, save → clean.
2. Q107 disposes the 21 remaining verifier FAILs.
3. F-PACK-32.2 (§32.2 layout wording) and Q114 (`supplychain.carriers.read`) stay with the pack owner.
4. The integration owner applies `gateway-tests-ocelot-count.patch.txt` in the same change as the gateway fragment.

Agent PASS ≠ CT ACCEPTED — returning to CT.
