# MVP6-WP-COMMIT-PLAN-01 (Q69) — commit plan for owner decision Q03 (NOT APPROVED)

- Lane AL-MVP6-COMMIT-PLAN-01 (INS, chat lane, read-only + this folder). Repo `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched). All git commands ran with `GIT_OPTIONAL_LOCKS=0`, read-only; scratch in VM `/tmp` only.
- Snapshot 2026-09-26 ~14:18 Istanbul. Start 14:18:11; end in the final report.

## Inventory (from `git status --porcelain`)

417 entries = 17 modified tracked files + 400 untracked entries (4,065 files) → **4,082 files, 142 MB**. Every file is assigned to exactly one commit, the held set or an exclusion (`INVENTORY.tsv` per porcelain entry; `pathspec/` per file). Reconciliation: 57+35+46+12+6+24+833+527+169+804+651+197+668+40 committed + 4 held + 9 excluded = 4,082.

| Group | Files | Where |
|---|---:|---|
| Product code, SupplyChainService (Carrier 35, Loads 46, Shipment root 11, `Program.cs` 1) | 93 | C02–C04 |
| Contracts + docs-path guard (authority, test, rule, decision JSON, 43 sealed inputs) | 57 | C01 |
| Module packs 0183–0187 + DCP-009 | 6 | C05 |
| Decision records | 26 | C06 (24), C14 (1), C01 (1) |
| Audit records (262 entries) | 3,238 | C07–C12, C14, C01, H01 |
| Plans, backlog, process guide | 670 | C13, C01 (2) |
| Evidence kit (scripts + guide) | 27 | C14 |
| Generated output (TestResults TRX, `rework-results.json`) | 8 | excluded |
| `.claude/settings.local.json` | 1 | excluded |

## Flagged

- **Generated output:** 5 TRX under `services/…/Tests/TestResults/`, 2 under `tests/architecture/…/TestResults/`, and `rework-results.json` at the repo root. None of them are ignored by `.gitignore`.
- **Local settings:** `.claude/settings.local.json` is tracked; today's change (+134 lines) is this Mac's agent permission lockdown.
- **Over 1 MB (K7):** 33 files, 80.6 MB; 31 records/plans and 2 generated TRX. Mostly text logs, manifests and TRX, plus 7 `.tar.gz` source/evidence archives.
- **Secret scan** (paths only; values never printed). 1,024 pattern hits in 4,082 files and inside 123 archives. 947 matched values also exist in HEAD (the dev/test defaults already committed). 15 values are new, in 49 outer files:
  - the dev JWT signing secret CT named (`mvp6-mod0186-r01-rework-01/scripts/start-api.sh` and its evidence tar);
  - a raw bearer JWT in `mvp6-root-r2-runtime-evidence-dev-02/restart.env` (issuer `mod0183-tests`, expired 2026-09-20 11:18 UTC) and its evidence tar;
  - lane probe signing secrets (`SECRET`, `JwtSettings__Secret`), including `services/Diten.SupplyChainService/tests/loads/runtime_probe.py` in the product tree;
  - test constants in MDM/Platform tests inside Carrier source archives;
  - superseded kit drafts;
  - two false positives (`MustChangePassword`, `secret_keys` names).
- **Guard coupling:** `docs-path-authority.json` (untracked) pins 46 files by SHA-256; 46/46 match; only 1 is in HEAD.
- **Backup is stale for commit purposes:** the 09:41 backup has 3,652 untracked entries against 4,065 files now.

## Recommended (each NOT APPROVED)

- **Q03a:** A — ordered sequence C01–C14.
- **Q03b:** A — exclude X1/X2, hold 4 H1 files, commit S1 and K7.
- **Q03c:** A — keep `feature/mvp6-logistics` until the end of MVP6.

Texts are in `DECISION-TEXT.md`; sequence and gates in `COMMIT-SEQUENCE.md`.

## ASSUMPTIONs

1. **Record family:** decided from the WP folder name (module keywords); `mvp6-ct-*` and mixed-module folders go to C12. CT may move a folder between record commits without changing the plan's logic.
2. **Guard order:** based on the guard design (per-file checks plus presence of pinned files) and on the final tree being green, as recorded after the Loads publication. It was not run here (no .NET in this lane); G6 runs it at every commit on the Mac.
3. **Build order:** C02/C03 add new types only and should compile alone; `Program.cs` (C04) needs both. If C02/C03 do not build alone, the gate records it and build-green is required at C04.
4. **Secret values:** judged "lane-local test" by variable name, file type and absence from HEAD only; the owner confirms under Q03b. A value that turns out to be a shared-environment secret is rotated, and its files move to H01.
5. **Snapshot:** other lanes (Q68, Q70) may add files after the snapshot; Step 0.3 classifies them. This plan's own 21 files are listed in C13.

## Repository state

Only this folder was written. No ledger edits, no git writes. Uncommitted.
