# Q185 — MOD-0190 S&OP Mac re-test on draft v3: Web.Tests (A vs B) + nav smoke en/ar (SOP v2.5 §17/§36.2)

**Agent verdict: PASS.** Every acceptance line is met:

- P1–P6 PASS;
- compose hash-exact;
- build 0 errors;
- Web.Tests new failures vs A = 0, and **`NavManifestL10nGuardTests` PASS on B** (the Q84b failure F-Q84b-1 is gone);
- N1–N3 PASS in en + ar;
- NETCHECK 27017 = 0;
- FINAL-CHECK clean;
- SHA256SUMS present.

Two points need a CT reading: how N1 was judged (D-2) and which tenant the smoke used (D-1).

Agent PASS ≠ CT ACCEPTED — returning to CT.

| Field | Value |
|---|---|
| WP / prompt / template | Q185 · Prompt Q185 v1 · T2 v1 · role T2 Mac build/test/runtime · CT-QUEUE row Q185 (after Q184, Q65b) |
| Where | Claude app → Code tab → Local session on the Mac, folder ERP-vNext-recovery (`uname -s` = Darwin). .NET SDK 8.0.417 / runtime 8.0.23, mongod 8.0.18, Playwright (Chromium, headless) |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, read-only. `GIT_OPTIONAL_LOCKS=0`; `git status --porcelain` only; no diff/add/commit/push |
| Base stack | BASE-STACK v2 `ce8d60ab959ec7018c255ab1152983cfd8e301390f54b3d0ba23961bf2c4d882` |
| Overlay v3 | `mvp6-sop-ui-draft-03/sop-ui-draft-overlay-v3.tar.gz` `716e7c4c…4332` · SHA256SUMS `11782b43…dbe1` (4/4 OK) · FILE-PLAN v2→v3 `e16680c8…f9f4` · v2 FILE-PLAN `a4e093ec…d56a` |
| Key rule | `frontend/Diten.Web/Services/Navigation/NavNameLocalizer.cs` `e8299a5c…5dcc` (the same hash in the checkout and in tree B) |
| Reference run | `mvp6-q84b-sop-mac-01/` (REPORT `a324b35b…`; not edited). Its tools, lane-config and lane-scripts were copied to `~/mvp6-env/q185/` and adapted there |
| Workspace | `~/mvp6-env/q185/`: new folders only, nothing deleted, kept |
| Time | start 2026-10-01 14:32:42 +03 · end 2026-10-01 14:52 +03 |

## 1. Preflight (all PASS)

| # | Check | Result |
|---|---|---|
| P1 | `uname -s` | Darwin |
| P2 | `.git/index.lock` | absent; `GIT_OPTIONAL_LOCKS=0`; porcelain only |
| P3 | HEAD | `4a8d4d4b339528a88e6220fb8402e5a2c771136c` |
| P4 | 7 sha256 values + v3 SHA256SUMS | all equal the prompt values; `shasum -a 256 -c` 4/4 OK |
| P5 | `docs/records/audits/2026-10/mvp6-q185-sop-mac-retest-01/` | absent. The `2026-10/` folder was not there at preflight either (see `FINAL-CHECK.txt` for its birth time and the other lane's file in it) |
| P6 | other run / slot listeners | none; slots 1–9 had 0 listeners; only the owner's dev mongod on 27017 (pid 758) |

## 2. Compose (`COMPOSE.tsv`, `logs/compose/`, `tools/compose_q185.py`)

- **Tree A** `~/mvp6-env/q185/treeA/src`: BASE 14,566/14,566 → Q117 → Q121 → Q131, every preimage checked. **14,572 files, 0 mismatch / extra / missing.**
- **Overlay**, composed as Q84b did, with the v3 archive:
  - The v1, v2 and v3 archives were extracted to staging and checked against their members.
  - **v2 FILE-PLAN 11/11** (v1 member = `v1_sha256`, v2 member = `v2_sha256`).
  - **v3 FILE-PLAN 8/8** (v2 member = `v2_sha256`, **v3 member = `v3_sha256`**).
  - The other 39 v3 files are byte-equal to v2.
  - 34 module files, all new tree paths (before = absent).
- **L6 environment integration** (environment copy only): provider line, 4 ocelot routes (275 → 279), and the nav keys from the **v3** fragments × 7.
  - Tree B `SharedResource.{lang}.resx` now carries `Nav.Page.SANDOPPLANS` (and no `Nav.Page.SANDOP_PLANS`).
- **Tree B** `~/mvp6-env/q185/treeB/src`: 14,606 files, 0 mismatch / extra / missing.
- **Kit K01 source tree = tree B byte for byte:** 14,606 files, 0 differing paths.

## 3. Build (`BUILD-SUMMARY.tsv`, `logs/build/`)

On A and on B: SupplyChainService.sln 0 errors / 0 warnings; Diten.Web 0 / 15; Diten.Web.Tests 0 / 3. The warning sets are byte-identical (18 unique).

## 4. Web.Tests (`TEST-SUMMARY.tsv`, `logs/test/`)

| Tree | Total | Passed | Failed | New failures vs A |
|---|---:|---:|---:|---:|
| A | 158 | 158 | 0 | — |
| B | 216 | **216** | **0** | **0** |

`NavManifestL10nGuardTests` on B: **7/7 passed**, including `Every_nav_visible_manifest_page_has_a_Nav_Page_key_in_all_seven_languages` (failed in Q84b with v2).

## 5. Runtime nav smoke (`RUNTIME.tsv`, `browser/`, `png/` 15 files, `kit-a1/`)

- **Environment:** evidence kit v1.2, slot 6. Gateway 5600, Web 5601, Auth 5656, Platform 5657, MDM 5659, SupplyChain 5661, Mongo **36994** (`rsSopQ18501`).
- **Kit up:** PASS on the first attempt.
- **K09:** PASS (6/6 processes bound to tree `8865a3f9…`).
- **Browser path:** Web only. All responses are real; nothing was faked. Read-only: no product write.

| Check | en | ar | What was observed |
|---|---|---|---|
| **N1** sidebar entry for the S&OP page | PASS | PASS | One sidebar link to `/SupplyChain/SandopPlans`. Text: en "S&OP Workflow & Sign-offs"; ar "سير عمل S&OP والاعتمادات". Each equals the v3 fragment value for its locale. No raw key; in ar not the English name; `dir=rtl` |
| **N2** Ctrl+K by the localized page name | PASS | PASS | Query en "S&OP Plans" / ar "خطط S&OP" (the v3 `Nav.Page.SANDOPPLANS` values) → 1 hit linking to `/SupplyChain/SandopPlans` → click → the page opens. The localized page name is present in `/TenantSearch/data` |
| **N3** S&OP list (entry) page | PASS | PASS | GET 200; title "S&OP Plans" / "خطط S&OP"; one shell; **0 page errors, 0 native dialogs**, 0 foreign requests; the sidebar item is marked active |

**What proves the v3 key rename at runtime:**

- In ar, the Ctrl+K query "خطط S&OP" can match only through the localized page name, which the server builds from `Nav.Page.` + `Normalize("SANDOP_PLANS")` = `Nav.Page.SANDOPPLANS`.
- That Arabic text is in `/TenantSearch/data`, and the page title is the Arabic view string.
- In en, both fragment values equal the manifest's English names, so en alone cannot tell a key hit from the fallback. ar is the discriminating locale.

## 6. NETCHECK 27017 (`NETCHECK-27017.txt`): **0**

- **Sampler:** 864 one-second samples; 0 new client ports; only pid 758 (dev mongod) seen.
- **Dev mongod log:** 0 "Connection accepted" lines.
- **Kit netcheck:** 0 for every lane PID (2 runs).
- Port 57192 was never used.

## 7. Cleanup and FINAL-CHECK (`FINAL-CHECK.txt`, `kit-a1/CLEANUP-a1.tsv`)

- **Stop only.** 6 services and the lane mongod were stopped; 8/8 lane ports have no listener; 0 lane processes remain.
- **Kit seal:** PASS (`kit-a1/SECRET-SCAN-FINAL-a1.txt`). **Root pattern scan:** 124 units, 0 hits.
- **Storage states:** truncated to 0 bytes.
- **Deletions:** no rm, no rmdir, and no kit K11.
- **Repo:** the only `git status --porcelain` change vs the start is `?? docs/records/audits/2026-10/`. That folder holds this run's evidence folder and one file from **another lane** (`mvp6-ct-verdicts-q183-q184-q65b-2026-10-01.md`), which this session did not write or touch (`FINAL-CHECK.txt`).

## 8. Deviations

| ID | What | Why / effect | Reference |
|---|---|---|---|
| D-1 | The smoke actor is in the **platform system tenant** `00000000-…-0001` (`john.doe.def`), not tenant 97c5 | The sidebar and Ctrl+K list only modules the tenant is entitled to (`GetTenantNavigationMenuQueryHandler` → `TenantModuleAccessService`). A lane tenant has no entitlement row, which is why the sidebar was empty in Q84b/Q65b. The system tenant has access to every active, tenant-assignable module by rule, so **no setup write to entitlements was needed**. Contrast recorded: tenant 97c5 with the same S&OP keys shows no S&OP entry | `lane-config/actors.tsv`, `lane-scripts/nav_fixture.py`, `RUNTIME.tsv` (contrast row), `png/info-tenant97c5-sidebar-*.png` |
| D-2 | **N1 reading:** the sidebar label is the **module** name (`Nav.Module.SOPWORKFLOWSIGNOFFS`), not the page name | Product design of the shared sidebar: a single-page module is one link to its page, "module name shown, page name hidden" (`Views/Shared/Components/DynamicModuleMenu/Default.cshtml:46-47, 63-75`). S&OP has one nav-visible page. The module key is in the v3 fragment too (unchanged since v2). The renamed page key is exercised by N2 and by the guard test. Harness attempt `nav-en-a1` expected the page name and was superseded by a2 | `browser/nav-en-a1.json`, `lane-scripts/nav_smoke.a1.mjs` vs `nav_smoke.mjs` |
| D-3 | `_shared-integration` items applied to the environment copy (provider line, 4 routes, v3 nav keys × 7) | As Q84b D-1; allowed by the draft README | `COMPOSE.tsv` rows `L6-env` |
| D-4 | No separate test mongod was started for step 3 | Web.Tests use no database. The URIs were still exported by `mvp6-test-mongo-env.sh --slot 6`. The runtime lane mongod was the kit's, on the same slot port 36994 | `logs/test/`, `NETCHECK-27017.txt` |
| D-5 | K11 not run; `lane-config/stop-lane.sh` did the stop-only cleanup. The supervisor ran with `~/mvp6-env/venv` first on PATH (bcrypt for K07) | Prompt step 6 and the owner no-rm rule; as Q84b/Q65b | `kit-a1/CLEANUP-a1.tsv`, `logs/kit/kit-up-a1.log` |
| D-6 | S&OP demand fixtures were not configured (no `Testing` environment, no relaunch) | The smoke is read-only; no plan is created | `lane-config/overrides.tsv` |

## 9. Observations for CT

| ID | What |
|---|---|
| O-Q185-1 | F-Q84b-1 is fixed by v3: the guard passes, and the Arabic page name resolves at runtime |
| O-Q185-2 | Cause of the empty lane sidebar (O-Q64d-1 / O-Q84b-1): the lane tenant 97c5 has no module entitlement. This is not a module defect. A lane that must show the sidebar for 97c5 needs an entitlement setup step, or the system tenant as here |
| O-Q185-3 | The page name `Nav.Page.SANDOPPLANS` is never printed in the sidebar while S&OP has a single nav-visible page. It is a Ctrl+K keyword only. It would become the sidebar label if the module gains a second nav-visible page |

## 10. Counts

- **Build:** 6/6 targets, 0 errors; warnings A = B.
- **Web.Tests:** A 158/158; B 216/216; 0 new failures; NavManifestL10nGuardTests 7/7 on B.
- **Runtime:** N1, N2, N3 × en, ar = **6/6 PASS**, plus 2 logins PASS. 15 PNGs. 0 dialogs, 0 page errors.
- **NETCHECK 27017:** 0.

## 11. Files

`REPORT.md` · `COMPOSE.tsv` · `BUILD-SUMMARY.tsv` · `TEST-SUMMARY.tsv` · `RUNTIME.tsv` · `NETCHECK-27017.txt` · `FINAL-CHECK.txt` ·
`SECRET-SCAN-ROOT.txt` · `SHA256SUMS` · `logs/` · `png/` (15) · `browser/` · `kit-a1/` (sealed) · `lane-config/` · `lane-scripts/` · `tools/`.

No tracked repo file was edited. No write outside the Allowed Paths. No rm or rmdir. No ledger write. commit YOK, push YOK. No prompt was written for another WP.
