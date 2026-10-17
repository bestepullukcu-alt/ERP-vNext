# Evidence — CT-FIX-04, the v5 audit fixes as relations on the Control Tower line (2026-10-17)

Authority: `CT-FIX-04-JOINT-DECISION-01` (`docs/records/decisions/2026-10/ct-fix-04-joint-decision-01.md`). Branch
`chore/ct/ct-fix-04` on `79cd8c6f1e20f70cd6c86c92765c160002cc973a` (CT-FIX-03), local only. The AI session is the
single writer; Codex measured independently, in its own copies, at every step. The instruction-content relation (İ6,
the v5 quality review's finding 4) is İ5B-FIX-04, recorded in `../i5b-fix-04/`.

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `scripts/ct_queue.py` | İ1 `in_force_status` (owner confirmation); İ2 `work_packages_field`, `governance_binding`; İ3 `PACK_FILE_ID`, `pack_identity`; `pack_approval_limit`; `kunye_fields` refuses an HTML comment mark | `38565914fc37a8ec5c3574e3c05520dca72e7cdcb9362ea50a64e5337821e954` |
| `scripts/prompt_gate.py` | İ2 / İ3 through the shared routines in `check_authority`; `check_approval_limit` | `ab337e0ddeb85e4efb06695ec9afac3ef9930fd4462be03aaa38f2d55efe86dd` |
| `scripts/acceptance_record.py` | İ3 `read_pack` (check, draft, module-check), the evidence-level source pack; İ4 the bound list | `daec51c32eb2caf69e6cefd196bebc442bca13d3f3625dda00f222f2fa98c9de` |
| `scripts/status_report_v2.py` | İ5 baseline problems are incomplete reasons; an incomplete report exits 1 | `3918ff63b4e81f8460a75114315687b5c0f7cf02cb1e285f2a5ebac9831d01f0` |
| `scripts/ct_route.py` | İ1 `_main_relation`, `_main_conflict` (both modes), `profile_problems` in `_snapshot` | `3eb8f209d70f9170ec83509aa6198951cc22c3f0acf99284f0c15717edea824a` |
| `scripts/tests/test_ct_cli_regression.py` | R0: the CT-FIX-04 classes (İ1 consumers, İ1 route, İ2, İ3, İ4, İ5, conditional approval) and the declared changes | `e5d48a4135dc7b875cc0042e74857bb4b89ad71bb6cd8a7156cda417609481a9` |
| `docs/guides/operations/control-tower-sop.md` | §20.2 "Görev sınıfı kuralı" | `a395ac1b14b1ae3d4535921f4f54838867c32a22c21e5312819f05eaf573b796` |
| `.antigravity/rules/status-reporting-and-evidence.md` | REP-001: `module_pack`, module-check, §7.1 exit codes | `55e6c27e2eb86f169766cb792d572551885578aba4454cacf8ed5b67db033637` |
| `docs/guides/operations/control-tower-operating-card.md` | pack authority, queue check, router and report lines | `01b05f4e91bdec857b58d00d1da07716af357755c3c3af8cd2ff02a975036624` |
| `docs/records/decisions/2026-10/ct-fix-04-joint-decision-01.md` (new) | the decision | `74d2f686c18f8fdf9c9af4eb6ecb0e6d02355a910c0325dac506dcea49313aaf` |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `parity.txt`, `arch-06.txt`, `run.sanitized.trx`, `drivers/` | listed below |

The five production files are byte for byte Codex's fixed `candidate2` set; Codex's later checks ran on that set.

## The relation × consumer matrix (real command lines, R0)

Every cell is a fixture case with an exact exit code and, where it matters, a named line. "Preparation" (a candidate
shown) and "dispatch / in force" (a permission) are separate cells.

| Relation | Consumer · mode | Normal | Broken |
|---|---|---|---|
| İ1 in force | `ct_queue require` | both confirmed 0 "— kayıtlı"; both pending 0 "— onaysız" | branch ≠ main (confirmation either way, strict) 1 "çelişiyor" |
| | `require --in-force` | 0 | both pending 1; conflict 1 |
| | `guard` / `summary` / `writers` / `validate` | 0 / 0 (both pending) | conflict 1 "çelişiyor" |
| | `prompt_gate check --prepare` | 0 "DAĞITIM İZNİ DEĞİL"; both pending 0 "onaysız" | conflict 1 |
| | `prompt_gate check` (dispatch) | 0 "— kayıtlı" | both pending 1 "yürürlükte değil"; conflict 1 |
| | `status_report --mode v2` | 0 complete; both pending 0 "YÜRÜRLÜK: onaysız" | conflict 1 "çelişiyor"; branch strict=no 2 (the report serves strict queues only) |
| | `ct_route` plain / `--in-force` | both confirmed 0 / 0; both pending 0 stamped / 1 | conflict 1 / 1; the ref's READY→DISPATCHED step removed 2 / 2 |
| İ2 WP ↔ scope record | `prompt_gate check` (yönetişim) | `work_packages: W1` 0; `W2, W1` 0; the row's `record` path 0; `path@older-commit` (same bytes; full and 9-hex) 0 | a mention only, `W10`, `W1-R1`, "W1 değil", twice, an indented second, commented, inside an HTML comment in the künye, `W1,,W2`, `W1,`, `W 1` → 1; list `W2` + the row's record → 1 "aşmaz"; `@` no object / not hex / empty / a tree id / a blob id / a branch named like an id (same bytes) / another content → 1 "sürüm bağı bozuk", with and without `work_packages` |
| İ3 pack identity | `prompt_gate check` (ürün) | id = file name = row module 0 | file MOD-0288 id MOD-0289; a second, indented or quoted id; no id; file MOD-0289 id MOD-0288; file `MOD-02880-…` / `MOD-0288X-…` → 1 |
| | `acceptance_record check` / `module-check` / `validate` / report | — | `module_pack` at `MOD-0289-…` or `MOD-02880-…` with id MOD-0288: module-check 2, check 1, validate 1, module-check (through the queue) ≠ 0, report 1 |
| | `acceptance_record check` (evidence-level source) | this module's pack 0 | a pack named MOD-0288 whose id is MOD-0289, with or without `work_package: W1` → 1 |
| İ4 bound list | check / validate / module-check / report | AC-1 exact 0 / 0 / 0 / 0; a WP's subset of AC-1, AC-2: 0 / 0 / 1 / 0 | CHECK-999 alone, K1 alone, AC-1 + CHECK-999, AC-1 another text, AC-1 optional, AC-9 → 1 / 1 / ≠ 0 / 1 |
| İ5 report exit | `status_report --mode v2` | no baseline offered 0 "Taban yok — hesaplanamadı"; a valid baseline 0 | an offered absent baseline, a draft approval, a wrong ledger hash, a broken ledger → 1, `"complete": false`, "tamamlık: eksik", stderr "rapor eksik — çıkış 1" |
| Conditional approval | `prompt_gate check` dispatch / `--prepare` | no field 0 / 0; `approved` 0 / 0 | conditional 1 "kapsam türetilmez" / 0 "HAZIRLIK: yeni dağıtımda ENGEL olur"; rejected, an unknown value, twice, indented → 1 / 1 |

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — this package's R0 against the unchanged base code `79cd8c6f1` | 55 tests, 101 failed subtests in 13 test methods; the controls and the unchanged cases green |
| Green — the CT worktree | R0 55 / 55; self-tests ct_queue 132 · prompt_gate 101 · acceptance_record 130 · status_report 80 · ct_route 197 · known_failures 46 |
| Guard removal (`guard-removal.txt`, driver `drivers/harness.py.txt`) — one guard undone at a time in a copy outside the repository | CT 19 / 19 red. M6 (the version must be a commit object) was green on the first run: the tree and branch cases held other bytes and the content check refused them. The case was strengthened to the same bytes; the rerun is red (4 subtests); its final form also fails on the base code (14 subtests) |
| Real-data parity (`parity.txt`) | 14 / 14 command lines identical (exit, stdout, stderr), before at `75cf72304`, after at `ff219eb75`; the real registry's one queue is not registered on the local origin/main, so the İ1 rule changes no real answer |
| Real-data effects (not parity) | 144 `MOD-*` packs: 139 identity ok, 4 without a künye (refused before too), 1 ADDENDUM id outside the grammar, 9 lettered FU packs read as the base module; `approval_status` only on MOD-0251; 0 of 330 front matters with an HTML comment; 0 of 40 approved decision records with `work_packages:` (see the decision's consequences) |
| Combined candidate's Python tests (once) | the six tools' self-tests as above; `scripts/tests` 125 / 125; `sync_adapters --check` OK 9; the working tree clean afterwards |
| The architecture run (`arch-06.txt`, `run.sanitized.trx`) | one run, on the combined candidate: 144 tests, 137 passed, 7 failed — by name the 7 known of CT-FIX-03's arch-05; `known_failures.py check` BİLİNEN = 7, YENİ = 0 (rooted and raw TRX); `dotnet test` exit 1 stays |

### The combined candidate

| What | Identity |
|---|---|
| Base | `75cf72304ad48f27562c759264277a7bf5e9be57`, tree `f61c33b5d3d40eaf9a81be3261ce4fee24fb4088` (the CT-FIX-03 + İ5B-FIX-03 tested tree) |
| Overlaid | 9 CT-FIX-04 and 3 İ5B-FIX-04 files; every one's base blob (`79cd8c6f1` / `97fec6a41`) equals the blob in `75cf72304` except `test_ct_cli_regression.py`, whose `75cf72304` version lacked the two cases added after CT-FIX-03's architecture run (this package's file is the base version plus its own changes) — no merge needed |
| Candidate 1 | `e5037692ae8217ef51ed4bdaaf776f3a8a9f97a7`, tree `39227bcf94c99144363ab4dcdc54b891ac16d5cb` — superseded before any run |
| Candidate 2 (pilot, architecture run) | `ff219eb75b951a64aec9af4b5ffe33dbc4e95ceb`, tree `4022be91e1fe0f83ddba636f18f1e5f52cef2cce` — candidate 1 with only the strengthened M6 case in the R0 file |
| After the run | this README, the decision record and this folder's files only. The architecture result belongs to `ff219eb75`; the committed tree adds documents, not code |

- Lock `locked_run.py` (SHA-256 `9fa85857fee462256751518a7a8ebab9d63e69348b834e43a09ea08bd68728e5`, recorded as
  `../ct-fix-01/locked_run.py.txt`); `dotnet 8.0.416`; restore from the local package cache only; the build output of
  the CT-FIX-02 / 03 scratch clone reused (no new full clone). The candidate commits have no ref and carry a synthetic
  author (`candidate@example.invalid`).
- Raw TRX SHA-256 `689d3785df603c8c13accde3f19325b42a1023f675c066dcb5c8d4add783b688` (not in Git: local paths); rooted with
  `../ct-fix-01/normalize_root.py.txt` (N1 = 318, N2 = 0) → `c6b5b6d666a01c9d79721975b856512af7c9b0e57e4e2983a74fb37603343df0`;
  sanitised with `../ct-pre-merge-fixes-01/sanitize_trx.py.txt` (R1 = 0, R2 = 151, R3 = 1, R4 = 1; leftover home 0, user 0)
  → `run.sanitized.trx` `904d40149a1856912ad0c630f4fe8049c6ddd3b3e9e4dbdda9b1469789edf4f8`.
- Operator note: a first `known_failures.py check` call named the suite `TenantArchitecture` instead of the registry's
  `TenantArchitecture.ArchitectureTests` and so listed all 7 as new; the second call with the registry's name gives the
  result above. Nothing changed between the two calls.

## Codex, independently (its files hold local paths; their SHA-256 only)

| What | Result | SHA-256 |
|---|---|---|
| The base code `79cd8c6f1` against its counter-examples (33 CT, 6 adapter CLI cases; binding, acceptance consumers, report baselines) | every gap reproduced | `80dbd4e420bb496bd9cb7b8a51997b002c464dd0169cadb3df6999d043df6c37` · `36d3c25b5d4dcbceff9e191c9d95ba334e824211a04df023fb964be27ca851a7` · `2c8aaea4789586d007ec0107cc3090bffbab7c2d82c1ef80ed5f142b144d6a70` · `299f80e65833f12778c4106d5da574ae15b05bdf3e86ca23bb0b8035e9407b2a` · `a12773784cf0608fc20540b994f4c4dd35a7704534a935f36c2f74c663bbfe52` |
| Candidate 1 sources (an intermediate state) | 77 / 77 expected; then 2 open identity cases and 2 open approval cases, fixed in this package | index `2e8e0e116644c3376ce868b6a010bf72678b53b6fa3e6d8a6822fc90067a7c20` |
| Candidate 2 sources (= the final five production files) | 88 / 88 CT command-line cases (general, binding, acceptance consumers, report, approval, identity) | index `01d20107dca05031ab24decd834cb27d48e5b396e650d2ad12d3fc528b027c31`, sources `7bf59829b141aa9f367f356c022654f0f7b6c031ee165ecc50466bd7c5d1d688` |
| The combined candidate's manifest, read with `git show` | 12 / 12 blobs equal to the two worktrees; the 7 production files equal its hash sets | `4a970b81f1f046d0472d2dc551298d1f93cc63af3e13dd64f16f04854677f944` |

Codex states its own limits: these are its fixed counter-example sets, not a general quality score and not the
20-criterion audit.

## Limits

- GitHub CI on these sources has not run (NOT VERIFIED); nothing is pushed, no PR description is changed.
- Real data has no registered queue on origin/main and no strict queue, so İ1 and İ2 are proven on synthetic fixtures
  and by Codex's counter-examples, not on a live dispatch.
- Anchors and identity checks are minimums: an AC criterion's meaning, a scope record's scope and a prompt's agreement
  with it are not read by any tool.
- The tools' module docstrings lag the new İ1 rule (kept so the hashed sources stay fixed); the SOP, REP-001 and the
  card carry the current text.
- The Employee scenario is Codex's measurement on candidate 2 with its own fixed rubric; it is not recorded here.
