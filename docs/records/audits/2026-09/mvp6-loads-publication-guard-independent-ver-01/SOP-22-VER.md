# MVP6 Loads 3.1.0 publication + docs-path guard binding — independent VER (Q32) — SOP §22

**Lane:** Terminal verifier lane (Claude Code in Terminal on the owner's Mac, new session). Not AL-MVP6-LOADS-PUB01; no access to its session. `@read-only-auditor`, `@testing-agent`.
**Task:** EXECUTION-ORDER Step 5 (`docs/roadmap/plans/mvp6-loads-guard-binding-prep-01/EXECUTION-ORDER.md`, sha256 `48e1e92fdf2ec0046f7cd068c0d5981a4931511a5df2af84dd4362861015b277`, verified).
**Branch / HEAD:** `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (verified at start).
**Start:** 2026-09-26 11:32:28 +03:00 (Istanbul) · **End:** 2026-09-26 11:37:30 +03:00 (Istanbul) — ARTIFACTS.sha256 then `SECRET-SCAN.txt` as last write.
**Agent verdict:** **VER PASS on all 6 checks.** This is not CT acceptance. CT decides. Q09 (producer uptake, C) stays **HELD** until CT records this VER.
**Worktree:** pre-existing dirty tree. This lane wrote only this folder. Git was read with `GIT_OPTIONAL_LOCKS=0` and nothing was written to Git. `.claude/` was not changed. `dotnet test` rebuilt the git-ignored `bin/`/`obj/` under `tests/architecture/…`, and `git status --porcelain` was identical before and after the test runs.

## Results

| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Decision B `36fe774a11a31c2b6e4f3c623f76cf22b4f2fe272474a8780f1b701753e5f35f` matches. The guard decision record `mvp6-loads-publication-guard-owner-decision-01.md` is `0ce587a1749bbbe44b41ef4bf1812de0edf971ec2daef963fee71301f1d62744`, equal to the writer's INPUTS.tsv; it binds OWNER-DECISION-TEXT `8cd55b82…990d` (matches), payload `a77538b4…11e0` and approvedBy `current-role-user-message-2026-09-26`. DISPATCH-v1.1 is `cae0f885…755b`. Prep SHA256SUMS (`fe808bb9…71f0`) gives **16/16 OK**. Writer SHA256SUMS gives **24/24 OK**. Release-prep SHA256SUMS (`61ddf109…6de9`) gives 21/21 OK | **PASS** | `raw/check1-decisions-sums.txt` |
| 2 | W1–W5 current sha256 equal the approved values (table below). W1 and W2 are byte-equal (`==` on bytes) to `release-prep-01/artifacts/{shipment-bundle-v3.1.0-final-proposed.openapi.yaml, loads-semantics-v3.1.0.md}`, and W3 is byte-equal to `PROVENANCE.md.txt`. The W5 payload, recomputed with this lane's own string-aware bracket matcher (canonicalTargets raw text + `"\n"` + sealedInputs raw text, UTF-8, SHA-256), is **`a77538b4784e1749b7ebe2e7d38d2e8400803c63ec8e6f49d3382929d7d611e0`**, and its 21 403 bytes are byte-equal to `authority-payload.txt`. W4 `payloadSha256` equals that payload. W5 `decision.path` is the W4 path and W5 `decision.sha256` equals SHA-256(W4). W4 equals the candidate with exactly the 2 allowed replacements, and W5 equals the candidate with exactly the 1 allowed status/decision block. There are **2 targets / 43 seals**, and every target, seal and provenance hash in W5 matches the file on disk. The v2 annex is unchanged at `a2187c93…2be1` | **PASS** | `raw/check2-w1-w5-payload-binding.txt` (script bytes `raw/ver_check2.py.txt`) |
| 3 | No writes outside W1–W5 + the writer evidence folder. Porcelain start→post-step2 (writer snapshots) adds only W2, the evidence folder and W4. Post-step2→now adds only the CT intake record and this VER folder. W1 was already `M` and W5 was already `??`, which matches the CT intake ("tracked diff 17 files"; `git diff --name-only` = 17). Files with mtime ≥ 11:17 (excluding .git/bin/obj and this folder): W1–W5 at 11:21:32 and the writer evidence at 11:22–11:24. The remaining files are CT's own 11:26:30 records (intake, CT-QUEUE.tsv, MILESTONE-EVENTS.tsv, TERMINAL-Q32-PROMPT.md), all outside the writer's time window. PROVENANCE.md (W3) mtime is 11:21:32, so it was not edited after it was written | **PASS** | `raw/check3-porcelain.txt`, `raw/check3-mtimes.txt` |
| 4 | Fresh runs used native `/Users/natig/.dotnet/dotnet` (SDK 8.0.417, runtime 8.0.23, Darwin), with DocsPathGuardTests.cs at `da5ec0cc…5050`. `DocsPathGuardTests` gave **39/39 pass** (exit 0). The full architecture project gave **56 tests: 53 pass / 3 fail** (exit 1). The 3 failures are exactly `JwtClockSkewGuardTests` ×2 (HumanCapitalService and TalentEcosystemService `Program.cs`) and `MongoTestDatabaseGuardTests` ×1 (Platform test files). All three concern files outside the write set, and they are the same set as the writer's baseline minus the DocsPath scan. DocsPathGuard is 39/39 inside the full run as well. New TestRun IDs and 11:34 start times confirm fresh runs, not copies | **PASS** | `VER-DOCS-PATH-GUARD-attempt1.trx`, `VER-ARCHITECTURE-FULL-attempt1.trx`, `raw/check4-*.txt` |
| 5 | Writer deviations judged (below): none changes any result | **PASS** | `raw/check5-deviations.txt` |
| 6 | Secret scan of the writer evidence (26 files) and of this folder. The pattern set was proven with a canary (3/3 detected). Both folders are **CLEAN**. This is the last write | **PASS** | `SECRET-SCAN.txt` |

## W1–W5 (recomputed by this lane)

| # | Path | SHA-256 now | Approved |
|---|---|---|---|
| W1 | `docs/analysis/contracts/shipment-bundle.openapi.yaml` | `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2` | = EXECUTION-ORDER |
| W2 | `docs/analysis/contracts/loads-semantics-v3.1.0.md` | `9d8a370664abb8221d5f8b23038f7c506e0621fadb2e5c83450ad93e63cf5034` | = EXECUTION-ORDER |
| W3 | `docs/records/audits/2026-09/mvp6-loads-publication-guard-01/PROVENANCE.md` | `84f07a6b6dde53954f2c7e2302d8a3f0a2b762b5dffd94309175a1ed89306f0a` | = EXECUTION-ORDER |
| W4 | `docs/records/decisions/2026-09/mvp6-loads-publication-guard-owner-decision-01.json` | `64db811693d3a25143ebe94435114739ac8016d289c8d26a296c12d516491234` | computed; = CT intake / lane report |
| W5 | `docs/reference/architecture/docs-path-authority.json` | `65a8ccbdd09acb97be8bed0195b3732fba241667e494a314d4dc4a192b660a4e` | computed; = CT intake / lane report; payload `a77538b4…11e0` |

## Writer deviations — judgement

1. **`--results-directory` added to every `dotnet test`.** The two 23 Sep TRX files in `tests/…/TestResults/` still have their 23 Sep mtimes (11:20:45, 11:26:03). The four TRX files in the evidence folder are byte-identical (`cmp`) to the originals in `/private/tmp/mvp6-q25-loadspub.4mTLMX/trx/`. Project, configuration, filter and SDK are unchanged. The flag only moves the output file, so it cannot change an outcome. This lane used the same flag, pointing into its own folder. **No effect on results.**
2. **Echoed exit codes wrong in one Step-3 line.** The writer's TRX files are authoritative (39/39 passed; 53/56 with 3 failed), and they agree with the exit codes this lane captured directly from its own runs (0 and 1). **No effect on results.**
3. **W3–W5 written by an external python3 script.** The script that ran (`/private/tmp/mvp6-q25-loadspub.4mTLMX/w345.py`) is byte-identical to `raw/w345.py.txt` (`ccf22f9b…a42a`). It is fail-closed: it checks the three input hashes, requires exactly one occurrence per replacement and checks the read-back. Independently of that script, check 2 shows W3/W4/W5 are exactly the candidates plus only the authorised edits, so the method has no effect on the bytes. No `.py` file was written in the repository. **No effect on results.**
4. **Temp rollback copies kept.** `/private/tmp/mvp6-q25-loadspub.4mTLMX/snap/` holds W1-pre `5dfe7c1b…d21c` and W5-pre `6d3865f8…8167`, which equal the EXECUTION-ORDER preimages, so rollback remains possible until CT decides. Not a result deviation.

## Deviations of this lane

- My COMMANDS.tsv row at 11:35:11 originally held a multi-line inline command. After the fact I joined it into one line with ` ⏎ `; its content is unchanged.
- I did not run a separate sabotage run of the production guard in a copied tree. The 39 DocsPathGuardTests include the guard's own negative fixtures (28 scenarios, e.g. `unknown-path`, `unregistered-tool`, `traversal`, `wildcard`, `symlink`, `payload-tampering`, `wrong-source-hash`, `unapproved`, `missing-decision`), all of which pass. The writer's baseline shows the same guard failing on the unbound tree, with 19 offenders.
- The helper scripts ran from the session scratchpad, outside the repository. Their bytes are kept as `.txt` in `raw/`.

## Notes for CT (not raised against the writer)

- W5 (`docs-path-authority.json`) is untracked (`??`) and W1 carries earlier tracked modifications. Nothing is committed. The publication exists only in the working tree until a separate commit decision is made.
- The 3 pre-existing architecture failures (JwtClockSkew ×2, MongoTestDatabase ×1) are outside this task.

## Output files

`SOP-22-VER.md`, `COMMANDS.tsv`, `VER-DOCS-PATH-GUARD-attempt1.trx`, `VER-ARCHITECTURE-FULL-attempt1.trx`, `raw/*.txt`, `ARTIFACTS.sha256` (covers every file except itself and `SECRET-SCAN.txt`), `SECRET-SCAN.txt` (last write). Only `.md`, `.tsv`, `.txt`, `.trx` and `.sha256` files were written; none of them is a guard-scanned extension.

Return to CT; CT decides.
