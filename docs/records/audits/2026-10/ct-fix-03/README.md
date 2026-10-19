# Evidence — CT-FIX-03, the v4 audit fixes and U1–U3 on the Control Tower line (2026-10-17)

Authority: `CT-FIX-03-JOINT-DECISION-01` (`docs/records/decisions/2026-10/ct-fix-03-joint-decision-01.md`). Branch
`chore/ct/ct-fix-03` on `46d513e0a71f6412b995644eaf43a4dcd512a054` (CT-FIX-02, PR #147's head), local only. The AI
session is the single writer; Codex verified independently, in its own copies, at every step. The adapter half
(V4A-F1, V4B-B5, B5b and the AGENTS-REFERENCE part of B3) is İ5B-FIX-03, recorded in `../i5b-fix-03/`.

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `scripts/ct_queue.py` | R1 `kunye_fields` / `kunye_status` / `nested_lines` / `body_status_lines`; R2 the SOP §7 profile (`SOP7_STATES`, `SOP7_REQUIRED_EDGES`, `profile_problems`) | `f0dd3bdc1f3b02cc8d82b0b4459d40c551995efe41e0dafc93c1a4315f1949b1` |
| `scripts/acceptance_record.py` | R1 readers; R3 the bound pack through `parse_pack`, the SINIR line; U3 `draft --part record\|list`; the unfilled-marker stop | `a2694c2d81c086fa8b4e34b42254005211e4f54ab69f5821b0ad693404dbc346` |
| `scripts/prompt_gate.py` | R1 test; R4 `parse_report`; U2 `draft`, the unfilled-marker stop | `5e593e742617b5c324c4a65b1e015b24393a4990ce5bdaed399c0e359b90696a` |
| `scripts/ct_route.py` | R1 `front_matter` through the shared reader; B8 the pending stamp, one truth, `--in-force` | `f8e02106d130771212c42ccc8d8794581fb708740746fdb22e3a3cc671e0935f` |
| `scripts/known_failures.py` | R5 `reconcile` (Summary / Counters / outcomes) | `726bc39a223f5a2ba1893780432725a0d66955b2c7e84d04f99b20c25762682f` |
| `scripts/status_report_v2.py` | R2 the self-test fixture is SOP §7's whole vocabulary | `e56c65014c1e5886a6ccbab44f60295ba132b6a7cf7a900d511e750dd16f1cba` |
| `.antigravity/scripts/verify_module_id.py` | R4 F4, F8, `parse_report`, the pack-id read, the EKSİK wording | `ede8f509c2a15de89a827028e32699db3e4ec736e7ceaf64a6d82f1f223b49ac` |
| `.antigravity/scripts/verify_datatable_page.py` | R6 `--ok-line` | `f52204e9d2910db1d297ef439fc5610fab5a24ceba131ba28f8a3f43cf19a942` |
| `.antigravity/scripts/list_screen_touch_hook.py` | R6 the hook needs the success line | `8b16eda5aa8dc603e855c49d049c9e3bd89141b5805fb1b9142ee5adc11636b9` |
| `.antigravity/agents/orchestrator.md` | D1 the dated rule count | `738a9aa188e78b8aa0d96d471dac0245410ec9beece5ffff1cf4050996200ccb` |
| `docs/guides/operations/control-tower-operating-card.md` | D1 `require`, the profile sentence; U1 "Bugün hangi komut" | `780fe1d47d3bc4be79581b8e7e75d07708ea2961b1f82ed532f7ff160efc3e43` |
| `scripts/run_phase1_gates.sh` | CI: the tools venv for `scripts/tests` (guard contract kept) | `f6583da6492fb0ff73e80041df2df9d7d1c07c8f55bdac15f02d85cf22717916` |
| `scripts/requirements-tools.txt` | CI: openpyxl 3.1.5, et-xmlfile 2.0.0 | `15c95c0d85606708fd48519f8d3266083770c3e760ca3c50d87eba967e3c35c7` |
| `scripts/tests/test_ct_cli_regression.py` (new) | R0 the CLI regression set (39 tests) | `ae3bc586a891a76fb240f3d9f27aa88738c7ec943816b0de7697b41e1e1bb12a` |
| `scripts/tests/test_ct_card_commands.py` (new) | U1 the card cannot drift from the tools' `--help` | `46d3caee26a19d45e269b2e58d2b9ca311cc3c69e65f5ef57bcfa0726c0a0c72` |
| `docs/records/decisions/2026-10/ct-fix-03-joint-decision-01.md` (new) | the decision | `7ef828fc8afa4ba402d895d7d3ba455da83f1ff8e4860841ad63c2d970830422` |
| `docs/records/audits/2026-10/ct-fix-03/pilot/pilot_driver.py.txt` (new) | the U1–U3 pilot driver | `76254ec7fd519ae775cb3a4fff52344249429f6179e79fb39cc44d7e24a6a79e` |

## The tested trees

| What | Identity |
|---|---|
| Base of the combined tree | `ca2bf5891f3e3cea642ea53f2c6f05075a7c76ab`, tree `1199b3ed07f8a21eb5e57b99b90c88d24abcd902` (the CT-FIX-02 + İ5B-FIX-02 tested tree; ref-less scratch commit) |
| Overlaid | 15 CT-FIX-03 files and 4 İ5B-FIX-03 files whose base blobs (`46d513e0a` / `c1941dbcc`) equal the blobs in `1199b3ed0`; two three-way merges (base `46d513e0a`, the `1199b3ed0` version, CT-FIX-03): `orchestrator.md` keeps the tree's `{ID}-{slug}` line, `run_phase1_gates.sh` keeps the tree's PlanningService build line; `git merge-file` exit 0 for both, reproduced by Codex byte for byte |
| Candidate 1 (arch-04) | `da488263ecd7ab8aa38ab6e0a5a62e5437e51a07`, tree `616a3bcf684a2bb5732cdfee91ccdbff2d9e5106` |
| Candidate 2 (arch-05) | `75cf72304ad48f27562c759264277a7bf5e9be57`, tree `f61c33b5d3d40eaf9a81be3261ce4fee24fb4088` — candidate 1 with only `scripts/run_phase1_gates.sh` changed (merged blob `3c65beac299a94f0e0edc8a6cb345daa0da79194f861462974c606eab982d062`) |
| After arch-05 | test and document additions only: the two R4 pack-id cases in `test_ct_cli_regression.py` (its SHA-256 in arch-05's tree was `5a1561da8a725c20467fd9ae5ee64c5f5476463dd56cb0bca9acced97f105164`), this README and the evidence files. The architecture result belongs to `75cf72304`, not to the committed tree |

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — the R0 + U1 tests (37 + 3) against the unchanged base code | 40 tests, 177 failed subtests across every package (F2 88, F3 42, F4 1, F5 14, F6 12, F7 2, F8/B6 4, B8 1, U2 4, U3 7, U1 card 2); the controls green. The two later pack-id cases: 3 failed subtests on the base code |
| Green (`green-final.txt`) — candidate 2 | self-tests ct_queue 127 · acceptance_record 123 · prompt_gate 91 · status_report 80 · list_screen_touch_hook 28 · known_failures 46 · ct_route 189, all passed; `scripts/tests` 97 / 97; `sync_adapters --check` OK. The two later pack-id cases: 2 / 2 on the final sources |
| Guard removal (`sabotage.txt`, drivers in `drivers/`) — one guard undone at a time, in clones outside the repository, every file restored to its manifest | R1 + R2: 14 / 14 red. U1–U3: 9 / 9 red. R3–R6 + B8: 11 of 12 red; one (B8b, the `--in-force and pending` branch) kept its refusal through the other guard (`pending` makes `ok` false, so `in_force and not ok` refuses) — not a gap, and the production code was not changed for the count |
| Excluded | the first R1b sabotage run (`results-r1b.json`, `run-r1b.log`): it ran in the writing worktree while R2 was written; which file version it read is not reliable |
| Real data | 39 approved decisions, 92 approved packs unchanged (MOD-0021's plan only changes its reason); 223 closed front matters, 23 indented non-list lines, all inside explicit objects; SCE 40 dispatchable ids unchanged; the real sop7 vocabulary and transition table pass the new rules; 45 committed TRX files: old and new answers identical; 152 real list screens: the verifier's output without `--ok-line` byte for byte the same; real `--discover` (564 refs, 679 pack blobs, 0 conflicts): findings, gaps and refs identical, two new source lines; `--check-all` byte for byte the same |
| CI helper (`ci-helper.txt`) | success (the venv's python3, openpyxl 3.1.5, PATH restored), pip failure exit 1, venv failure exit 1 — the discover line is never reached on a failure; no download (a `--system-site-packages` venv, `PIP_NO_INDEX=1`) |
| Pilot (`pilot/`, driver `pilot/pilot_driver.py.txt`) | P1 ilk dağıtım, P2 MVP'ler arası soru, P3 kabul ve kuyruk doğrulama (no status report is run): 15 cases, 0 wrong passes, 0 wrong stops; the four agreed acceptance items hold (the five measured prompt values filled and not retyped; AC ID / text / mandatory copied by hand 0; no wrong pass or stop; no additional mandatory command). Draft calls are counted apart (P1 1, P3 2). The "before" counts are a computed model, not a person's work; 11.3 s is tool time, not a person's time; no token counter, no saving claimed |
| Architecture run 1 (`arch-04-regression.txt`) | candidate 1: 144 tests, 134 passed, 10 failed — the 7 known and 3 NEW in `CiServiceTestsGuardTests` (the first venv step added two double-pipe operators and replaced the pinned discover line). The single-run goal was therefore not met; the fix changed only `run_phase1_gates.sh`, the guard tests were not changed |
| Architecture run 2 (`arch-05.txt`, `run.sanitized.trx`) | candidate 2: 144 tests, 137 passed, 7 failed — the 7 known; `dotnet test` exit 1 stays; `known_failures.py check` BİLİNEN=7, YENİ=0, exit 0 on the raw and on the sanitised TRX |

### The architecture runs

- Lock: `locked_run.py` (SHA-256 `9fa85857fee462256751518a7a8ebab9d63e69348b834e43a09ea08bd68728e5`, the recorded
  `../ct-fix-01/locked_run.py.txt`); `dotnet 8.0.416`; restore from the local package cache only; the build output of
  the CT-FIX-02 run's scratch clone reused (no new full clone). Driver `drivers/run_arch_05.sh.txt`.
- Run 2, raw TRX SHA-256 `e7c7eb2698a3c4cf6da97de746c0e3c2b3659496b2d8515c2bc54ad37beb69f5` (not in Git: it holds local
  paths). Sanitised with the recorded scripts unchanged: `../ct-fix-01/normalize_root.py.txt` N1 = 318, N2 = 0 →
  `2504ccbe58b19135cbd70571e7dc1613fd2079d80102290f127a75ed7ac030aa`; `../ct-pre-merge-fixes-01/sanitize_trx.py.txt`
  R1 = 0, R2 = 151, R3 = 1, R4 = 1 → `run.sanitized.trx` `4ba011d73671693db2ed967d8318b5d316476e91de6efe24069947cd0d4c6db7`;
  leftover home and user 0. Codex reproduced both steps byte for byte; the 7 failure names and messages equal the
  CT-FIX-02 run's.

## B10 — the stored test commits

- `ca2bf5891f3e…` is the commit the v4 audit named (tree `1199b3ed0…`). 23 of its 30,233 objects are not reachable from
  the three PR heads `46d513e0a71f6412b995644eaf43a4dcd512a054` (#147), `c1941dbccdaa99d348f74b8a82815d11768fca76`
  (#149) and `af7bda6e56bde0e981eaa16d0e56db56a03cf8e2` (#148) — measured on GitHub's PR API on 2026-10-17 as those PRs'
  heads. They are in a 41,417-byte pack, SHA-256 `c4f7a10c8e3cc8008c1f496a373e461784b9d58dfe07c47b3ca82e05821032c7`.
- This package's candidate `75cf72304…` (with `da488263…` and `ca2bf5891…`): 65 of 30,275 objects are not reachable
  from the same three heads; pack 275,687 bytes, SHA-256 `369e0b268b9e8b6735333740279a66861014371ad02f0c49d385215d307b2390`
  (it holds B10's 23).
- Restore (AI session and Codex, separately): an empty bare repository with the local object store as a read-only
  alternate, the pack added — both trees match, `git fsck --connectivity-only` 0, `ls-tree -r` identical (28,086
  entries). Separately, a set proof: every object the commits reuse is reachable from the history of the three
  declared prerequisite heads, and the objects that are not are exactly the pack's. The alternate alone does not prove
  portability; the two together do, with the three heads as the stated prerequisite. The packs are kept in
  the delivery package outside the repository (a commit object carries a non-synthetic author address). Each is
  "three prerequisite commits + pack", not a standalone bundle.

## Codex, independently (its files hold local paths; their SHA-256 only)

| What | Result | SHA-256 |
|---|---|---|
| Final R0 on `snap-ct-final-02` | 37 / 37, the 17 sources unchanged before and after | `7301b7fa40df95d8558a13d96a6a5b561ae4ec948a4010617bfd7a701b9e6b47` |
| U drafts, real command lines | 22 / 22 | `7febd38bdfb05feab0abe1310bd4f085dd2d42e781ffa79f98e2a94d52e50d40` |
| Filled drafts through the real checks | 7 / 7 | `208f5f1f6a948f3a40f13e1a65c0e16003d1fa6c9920a67cc9f4d4ca200bec64` |
| Malformed queue rows | 4 / 4 exit 2 | `5b34565eef7c03bb4ab7cf6d56b88f9428e0cc0efd228dded6a40119e93f7e38` |
| The card against real copies | normal 0, a missing option 1, a missing subcommand 1 | `05e79e45b0220cdff965eaea0de1a80ffc9db42069c37d39d4a0ea216de94bd6` |
| Pilot, again | 15 / 15, the four agreed items true | `9f32afbc7aaf37d48de7bb9f867120a25f46740b47ff4f5045957a054273eafa` |
| The two three-way merges | byte for byte the same | `79593ac1b24ad281c7ef4cdf2352c2b4bde30e1f8622f571ae446323645c8ca4` |
| Combined candidate 1 sources | 21 blobs, 21 changed paths, clean | `92a80929dd345e0fd472ace5d36b8b200ed285cafed1a651bb5058d38e381d4e` |
| This package's combined pack | 30,210 of 30,275 from the three heads, the 65 exactly the pack; restore in a new bare repository, missing 0 | `f159811d2ecd3ca044ed47846b6a36ee683e06641c6d370be2074f38b562e227` |
| B10 prerequisites / restore | 30,210 of 30,233 from the three heads; the 23 equal the pack; restore OK | `06941189a3cbfc5d406fbe3125ae363ff4831fc9c2e12bccc3791183785fd390` / `a957897c4964016aeee6802752af705fcafad0f3ccd4a8d1fb59b3115b5e35aa` |
| Earlier rounds (R1–R6, B8) | the counter-example matrices on old vs new code, all as expected; recorded in its earlier index | `4b39ccd0db29b50b4e33da9d223eb50ad8b5619835693d0aa6432a76753b0f20` |
| The final results (arch-04 / arch-05 classification, the final CI helper 4 / 4 in a real shell, the sanitisation reproduced, the combined pack) | as above; its frozen final index | `b2b0473c6c44838add2199cef388b127968288497de30118d1c057ae827a19cf` |
| The three discovery pack-id cases (the two R0 cases and its own) | 3 / 3, sources unchanged | `6ea0de4969a6c2416be95993a25a465d66c8d28d40c9b56ce3e5360813b549c3` |

## Limits

- GitHub CI on these sources has not run (NOT VERIFIED); nothing is pushed. The PR descriptions are drafts outside the
  repository; publishing them needs its own authority.
- Two architecture runs, not one: the first found a regression of this package (above).
- The pilot is a synthetic run by the AI session in the Control Tower role; a person's trial is still needed.
- The verifier's success line was never seen on a real clean list screen: all 152 real screens have deviations; the
  clean path is tested with stub verifiers and Codex's unit test of `print_gaps`.
- `ct_route --contacts` prints the address from the user's own address book outside the repository when asked; no
  contacts output is in this evidence.
- The historical pilot's missing timestamps are not recovered; this package's pilot steps carry UTC timestamps.
