# Evidence — CT-FIX-05, the v6 audit fixes on the Control Tower line (2026-10-17)

Authority: `CT-FIX-05-JOINT-DECISION-01` (`docs/records/decisions/2026-10/ct-fix-05-joint-decision-01.md`). Branch
`chore/ct/ct-fix-05` on `f23455d278ee65b4e25ddb892bad3567c3ce7e4c` (CT-FIX-04), local only. The AI session is the
single writer and ran every check in this folder; Codex measured independently, in its own copies (listed by SHA-256
at the end). The core-format finding (F5) and the module-request answer are İ5B-FIX-06, recorded in `../i5b-fix-06/`
on the İ5-B line.

## What changed (final sources, SHA-256)

| File | Item | SHA-256 |
|---|---|---|
| `scripts/ct_queue.py` | F1 `in_force_status` validates origin/main's whole registry before the queue is picked; `check_registry` requires a normal repo-relative path; F2 (b) `reassessment_binding`, `record_version_problem` (shared with `governance_binding`), `local_commit_id`; `summarize` counts what `require` accepts; C2 `PACK_FILE_FULL_ID`, `pack_module_identity`; E1 `pack_approval_decision` per mode | `ca210997f368a9ae587d4860b4c28a247bf964897439b226507d796a38991f9b` |
| `scripts/prompt_gate.py` | C1 `check_governance_paths` (governance allowed paths outside `services/`, `frontend/`, `gateway/`, no symbolic link at or under the fixed prefix); E1 through the shared decision | `c19942f86de64d9bf50b0293a3c8e8dd2101b365441c64291be2f94d213cb665` |
| `scripts/acceptance_record.py` | F3 the evidence is a run record (`run_record.verify_record`), raw exit, the E–C difference, artifact bytes and size, `inceleme bekler — <neden>`; C2 the acceptance reader's identity through `pack_module_identity`; E1 `read_pack` per mode (accept, draft) | `82907078582a27a8705c42c59f5258994ef905a9db0b9f3a0aca69a22d670ecf` |
| `scripts/run_record.py` (new) | F3 the runner and the record's verifier (`run-record-v1`) | `37bd43bae3296df1a569f73aa29d1bc15732ab703177321989aa171354ef09cf` |
| `scripts/known_failures.py` | F4 only a completed `Failed` row is a known failure; any other failing outcome is YENİ-AYRINTI | `55abd9ac2f6e7dc2ccc2ec8890c68b26d25878d33d906e44b6aac1bc8e9a0771` |
| `scripts/status_report_v2.py` | F6 a `--previous` that cannot be verified makes the report incomplete (exit 1) | `1e26c8dbdc7f6834da9fd25f250130445e2966c037ca7e19e49ac40a2bf04248` |
| `scripts/ct_route.py` | the module docstring only (the conflict rule in both modes); no behaviour change | `b13dded90003d829ad823a0b3855be34f84f19a3fc13b4f9ec9af0e420e138ad` |
| `scripts/tests/test_ct_cli_regression.py` | R0: the CT-FIX-05 classes; the fixture's evidence is a real run record; `V4A_F3_Kunye`'s retry record lists the retry | `af5f7d84d78f5397138d7e35bfa4e42438dd152e8d585f0ee959bc6c887308c0` |
| `docs/guides/operations/control-tower-sop.md` | §20.2 the product full-id rule (C2) and the governance allowed-path rule (C1); §28.2 the retry's `work_packages` and `@` | `51a55c8734b355132879135901ad5232eea55471028992c46b724ba0d2c3c8f1` |
| `.antigravity/rules/status-reporting-and-evidence.md` | REP-001 §6.1: `evidence` = run record, `acceptance_list` under `docs/records/audits/`, `inceleme bekler — <neden>`, a `geçti` row cites a run artifact, the F3 limits; the module_pack / module-check text (C2, E1); §7.1 `--previous` (F6) | `fa39202f1aca2ee171dedfc363d6653bf1a1cdb651603b2a15355140e4afacdd` |
| `docs/guides/operations/control-tower-operating-card.md` | the run-record row; the known-failures sentence (Failed only) | `72834f628f4ce2dee2a05e9ba0f6b2977f4463eda7abd6674884666a67fe6160` |
| `docs/records/decisions/2026-10/ct-fix-05-joint-decision-01.md` (new) | the decision | in the commit |
| this folder (new) | `red-first.txt`, `guard-removal.txt`, `parity.txt`, `real-data-effects.txt`, `arch-08.txt`, `run.sanitized.trx`, `drivers/` | in the commit |

The eight code and test files are byte for byte candidate 4's (below) and the copies the guard removal ran on.

## The relation × consumer matrix (real command lines, R0)

| Item | Consumer · mode | Normal | Broken |
|---|---|---|---|
| **F1** origin/main's registry as a whole | `ct_queue validate` / `require` / `require --in-force` / `guard` / `summary` / `writers`, `status_report --mode v2`, `prompt_gate check` and `--prepare` | a valid main registry with another queue on its own task file: `require --in-force` 0 "— kayıtlı" | two queues on one task file, or an unrelated row with an empty field: every consumer 1 "kayıt geçersiz", the gate 1 ENGEL in both modes; the same task file spelled `./q.tsv`, `q.tsv/`, `x/../q.tsv`, `/q.tsv`: validate, require, summary, `--in-force`, the gate and the report 1; a branch registry with `./q.tsv`: validate 1 "normal" |
| **F2 (b)** a -R3 retry ↔ its reassessment record | `ct_queue require` / `validate`; `summary` | `work_packages: W2-R3` or `W2-R3, W9`: 0 "DAĞITILABİLİR W2-R3"; the row's `record@commit` (40 and 9 hex) holding the same record: 0 | no `work_packages`, `W2` only, another WP, `W2-R4`, written twice, an empty element: 1 / 1 "§28.2"; `@` a 40-hex id that is no object, not hex, a tree id holding the same record, a commit whose record differs: 1 "sürüm"; `summary` counts the refused retry under "DENETİMDEN GEÇMEYEN 1 kimlik", not as dispatchable |
| **F3** evidence ↔ the accepted code | `run_record.py run` | a run on a clean tree into a new directory under `docs/records/audits/`: 0, the record written, the raw exit kept | an output directory outside `docs/records/audits/`, a non-normal path, an existing directory, an untracked file, a FIFO in the output, a change outside the output during the run: 2, no record |
| | `acceptance_record check`; the report | the fixture's real run record at the code commit: 0 "ACCEPTED'e izin verir" | a run that exited 7: 1 "ham çıkışı 7" (the only ENGEL); the record for older code: check 1 "eski kanıt yeni kodu", report 1; an evidence commit that also changes code: 1 "kanıt dışında da farklı"; a row citing a file the run did not produce: 1 "artefaktı değil" |
| **F4** outcome ↔ known failure | `known_failures check` | a registered test, `Failed`: 0, BİLİNEN = 1 | the same test `Timeout`, `Error`, `Aborted`: 1 "sonuç türü …", BİLİNEN = 0 |
| **F6** the report ↔ `--previous` | `status_report --mode v2 --previous` | a verified previous report: 0 "doğrulandı" | a changed visible text, a path not at the commit, not `path@commit`: 1, `"complete": false`, "önceki rapor" |
| **C1** governance WP ↔ allowed paths | `prompt_gate check` (yönetişim) | `source.txt`, `docs/**`: 0 | `services/…/**`, `frontend/x.js`, `gateway`, `**`, `{services,docs}/**`, `docs/../services/x.cs`: 1 "yönetişim işinin izinli yolu"; `docs/alias` a symbolic link to `../services`: `docs/alias/New.cs`, `docs/alias/sub/New.cs`, `docs/alias/**`, `docs/**`: 1 "sembolik bağ" |
| **C2** the file's full id ↔ the künye id | `prompt_gate check` (ürün), `acceptance_record check` | `MOD-0288-FU06B-…` with `id: MOD-0288-FU06B`: 0 / 0 | the same file with `id: MOD-0288-FU07B`: 1 / 1 "dosya adındaki kimlikle" |
| **E1** a second approval field ↔ acceptance | `acceptance_record check` / `module-check` / `draft --part list` | `approval_status` absent or `approved`: unchanged | `conditionally-approved-…`: 1 "inceleme gerekir" / 2 / 0 "taslak hazırlık içindir"; `rejected`: 1 / 2 / 2 "onaylı değil". The gate's split (dispatch ENGEL, `--prepare` 0 with the note) is CT-FIX-04's, now through the same decision |

## Checks and results

| Check | Result |
|---|---|
| Red first (`red-first.txt`) — this package's R0 against the unchanged base code `f23455d27` (with the new `run_record.py` copied in) | 74 tests, 56 failed subtests in 15 test methods, all in the CT-FIX-05 classes; the earlier classes, the three positive controls and the runner's own refusals test green |
| Green — the CT worktree | `scripts/tests` 121 / 121; self-tests ct_queue 137 · prompt_gate 103 · acceptance_record 148 · status_report 80 · ct_route 197 · known_failures 51 · run_record 29 |
| Guard removal (`guard-removal.txt`, driver `drivers/harness2.py.txt`) — one guard undone at a time in a copy outside the repository | 18 / 18 proven: the file parses, the named tests fail, the control test passes in the same copy. G-F3f (the runner hashes a non-regular file) is red by a hang (240 s): its test's runner stayed blocked on the FIFO and was ended by its PID after its start time and command line were checked |
| Real-data parity (`parity.txt`) | 16 read-only command lines at candidate 3, then at candidate 4: 13 identical; 3 expected differences — the `summary` count line's wording (the count 40 unchanged, none refused), the pack probe (below) and the Employee discovery report's own SHA-256 line (it covers the HEAD line) |
| Real-data effects (`real-data-effects.txt`) | C2: one pack's verdict changes — `MOD-0021-all-phases-implementation-plan.md` (künye `MOD-0021-PLAN`, file id `MOD-0021`) is refused as a product authority; E1: the one pack with a second approval field, MOD-0251 (Core HR Employee master): dispatch stop, prepare note, accept review, draft note; F3: at candidate 4 no REP-001 v2 acceptance record (`format: rep-001-v2`) exists under `docs/records/` (`git grep`: 0), so no current count changes; the older acceptance data in earlier formats were not converted and are not re-judged (Codex: 1103 entries); F1, F2: the real registry and queue answers are unchanged (parity), and the real queue (579 rows) has no -R3 or later retry id (its retries are `Q246-R2`, `Q339-R2`) |
| Combined candidate's Python tests (once, at candidate 4) | the seven self-tests as above; `scripts/tests` 153 / 153; `sync_adapters --check` OK 9; the tracked tree clean afterwards |
| The architecture run (`arch-08.txt`, `run.sanitized.trx`) | one run, on candidate 4: 144 tests, 137 passed, 7 failed — by name the 7 of arch-07; `known_failures.py check` BİLİNEN = 7, YENİ = 0 (rooted and raw TRX); `dotnet test` exit 1 stays |

### The combined candidate

| What | Identity |
|---|---|
| Base | candidate 3, `0609bc5882380580201e21a89fcbc2302cff884d`, tree `6ca563a0034e290ec2417ecddef93248d391507c` |
| Overlaid | 11 CT-FIX-05 files (base `f23455d27`) and 4 İ5B-FIX-06 files (base `a7cb60992`); every file's base blob equals candidate 3's and `scripts/run_record.py` is absent there — a clean overlay (`drivers/build.py.txt`) |
| Candidate 4 | `3d4f0f4050b1b162555277f2296f42648c299eb7`, tree `5df69f6d0c45f41af9b6d6716753cea09b7dc98e`; 15 files differ from candidate 3 (14 changed, 1 new); manifest SHA-256 `0fc241d50693108b39c99cb1c1d17ce3974f29f3a1eb938cc0dff56524b1ec90` |
| After the run | this README, the decision record and this folder's files only. The architecture result belongs to `3d4f0f405`; the committed tree adds documents, not code |

- Lock `locked_run.py` (SHA-256 `9fa85857fee462256751518a7a8ebab9d63e69348b834e43a09ea08bd68728e5`, recorded as
  `../ct-fix-01/locked_run.py.txt`); `dotnet 8.0.416`; restore from the local package cache only; the build output of
  the CT-FIX-02 / 03 scratch clone reused (no new full clone). The candidate commits have no ref and carry a synthetic
  author (`candidate@example.invalid`).
- **The scratch clone's folder was reused.** Its HEAD moved from candidate 3 to candidate 4 for this run, so its working
  copy is no longer candidate 3's. Candidate 3's commit and tree stay in its object store (candidate 4's parent); the
  earlier manifests are read with `git show <commit>:<path>`.
- **The clean-tree record is the tracked tree only.** The driver checked `git status --untracked-files=no` before the
  checkout and after the run; the untracked state before the run was not recorded, so this is not evidence of a fully
  clean tree before the run (Codex). Measured separately afterwards, at 2026-10-17T20:11:39Z: `git status --porcelain
  --untracked-files=all` in the scratch clone was empty (107 ignored paths — build output — are not covered by it), and
  the candidate's 15 files were byte for byte the two worktrees' (read with `git cat-file`).
- Raw TRX SHA-256 `207b1083b831edf31c5aa4fd82acf96918450a015daf484f459f0a02daf2ced3` (not in Git: local paths); rooted with
  `../ct-fix-01/normalize_root.py.txt` (N1 = 318, N2 = 0) → `abc3714ff3507d73f0101588857c25ef8f94ebae9c67e3f6663e7a73051ae942`;
  sanitised with `../ct-pre-merge-fixes-01/sanitize_trx.py.txt` (R1 = 0, R2 = 151, R3 = 1, R4 = 1; leftover home 0, user 0)
  → `run.sanitized.trx` `2cf33faae9398786614601a0a0b8cecf044927925efc75311b24bc303009b874`.
- The generated texts in this folder come from the raw outputs through `drivers/make_evidence.py.txt`.

## Codex, independently (its files hold local paths; their SHA-256 only)

| What | Result | SHA-256 |
|---|---|---|
| Copied intermediate sources: relationship 101 / 101, core format 20 / 20, governance scope 12 / 12, run record 11 / 11, run-record format 5 / 5, binding 13 / 13, pack contract 24 / 24, consumer binding 16 / 16 (old evidence for new code, a corrupted artifact and a raw exit 7 stop in acceptance check, queue validate, module-check and the report; the normal case passes in all four), size binding 2 / 2, manual binding 2 / 2 (a real `inceleme bekler — <neden>` row is refused too) | Codex's index; "intermediate copied-source validation" | index `396ea134a21e67e72ba1a3065f809c6e33deb10304d336d72a9ea56125103078` |
| A FIFO in the run's output | exit 2 | `1307569b6061313dbb0e24741c9e357b7e992e4e7715fe5036940881fdf3cdfb` |
| Candidate 4's identity | the 15 blobs and the tree | `b6940d444f927903f0dde26d1aad34bcd78f11b69ed2f9ecb2497b629fef9a68` |
| The consumer-binding snapshot's six production scripts against candidate 4's | 4 byte for byte equal; `ct_queue.py` and `acceptance_record.py` differ in the module docstring and the self-test only — with those two removed the executable AST is equal 6 / 6. A limited source binding of the 16 CLI cases to the final candidate, not a test of the whole final tree | `a4138452a4031d28f1876021b8689a75bf1f62f5da79b32b9874dea6537c2922` |
| Protected scope, candidate 3 → 4 | the `services`, `frontend`, `gateway` trees, the SCE queue, the CT registry and SOP §36.2 → end unchanged | `a4a514d96a6228f4e2af264573bf1294ee4e477bed2f921f859688ad03ac3e21` |
| arch-08 read independently from the XML | 144 results, 137 passed, 7 failed; raw and sanitised names, outcomes and messages equal; "full before-run untracked status not established by this check" | `01870ff526e60b860248afc09e841e8590af17c5d14dcf53cc5cc08db61af5ee` |
| arch-08's sanitisation reproduced from the raw TRX with the recorded scripts | the sanitised bytes identical (`2cf33faa…`); N1 = 318, N2 = 0, R2 = 151, R3 = 1, R4 = 1 | `085be96023fe993e86deab8336950ce974a9c7f031651a0ad99342a47230b79c` |
| The final validation matrix (preregistered) | Codex's | `959a929cbcccf2f313de573efe7edc9a5acfec4d83516372ecff6748929c68a0` |

Codex states its own limits: these are its fixed counter-example sets, not a general quality score and not the
20-criterion audit.

## Limits

- GitHub CI on these sources has not run (NOT VERIFIED); nothing is pushed, no PR description is changed.
- **F3 is a binding, not a proof of meaning.** A run record shows which command ran on which commit, with which raw
  exit, and which bytes it wrote. It does not show that the command measures the criterion. A record written by hand
  that is self-consistent (the right keys, the hashes of files committed with it) cannot be told apart from the
  runner's by the checker. Ignored files are outside the clean-tree check, and a change reverted before the run ends is
  not seen. Acceptance stays the CT's judgement on top of the binding; the measured wrong inputs above were refused.
- E1: a conditional pack gives no new automatic acceptance (review); the list draft still comes, with a visible limit;
  earlier acceptances are not re-judged.
- C1 bounds the write paths a governance prompt may declare; it does not stop reading, and it does not check what an
  executor actually writes — that is the commit's diff.
- Real data has no registered strict queue and no -R3 row, so F1 and F2 are proven on synthetic fixtures and by Codex's
  counter-examples, not on a live dispatch.
- The live measurements on candidate 4 are Codex's, run with the owner's explicit data and destination approval, and
  recorded in `I5B-FIX-06-JOINT-DECISION-01` on the İ5-B line. They are separate results: the loading pair passed
  narrowly, and the Employee blocked run passed its bounded checks. The Employee **normal run is NOT VERIFIED**: the
  interactive CLI stopped at its login step before any model response, and that login is the owner's step.
