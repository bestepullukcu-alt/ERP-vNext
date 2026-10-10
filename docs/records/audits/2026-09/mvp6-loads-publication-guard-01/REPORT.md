# MVP6 Loads 3.1.0 publication + docs-path guard binding (Q25) — lane report

Lane: AL-MVP6-LOADS-PUB01, the single writer (`@integration-agent`, `@testing-agent`). Claude Code on the owner's Mac.
Procedure: DISPATCH-v1.1 → EXECUTION-ORDER.md (`48e1e92f…b277`). Authority: decision B (`36fe774a…f35f`) + MVP6-LOADS-PUBLICATION-GUARD-01 (`.md` record `0ce587a1…2744`).
Start 2026-09-26 11:17:46 +03:00 · repository write 11:21:32 · tests done 11:22:15 · evidence sealed ≈11:25.

**Agent result: all steps PASS.** This is not CT acceptance. Independent VER (EXECUTION-ORDER Step 5) is dispatched separately by CT; Q09 (producer uptake) stays HELD.

## Per step

| Step | Result | Evidence |
|---|---|---|
| 0 Preconditions: Darwin, SDK 8.0.417 / runtime 8.0.23, 24 GB free; HEAD/branch; authority hashes; prep SHA256SUMS 16/16; release-prep 21/21; preimages; W2/W3/W4 absent; 8 seal inputs; DocsPathGuardTests.cs `da5ec0cc…5050`; patch touches only the two contract paths | PASS | `INPUTS.tsv`, `raw/step0-*.txt` |
| 0.6 Baseline full architecture run, unchanged tree | 56 tests: 52 pass / 4 fail. The failures are the DocsPathGuard scan (exactly the 19 expected offenders in the 8 files) plus `JwtClockSkewGuardTests` ×2 and `MongoTestDatabaseGuardTests` ×1, which are pre-existing and form the baseline | `BASELINE-ARCHITECTURE-FULL-attempt1.trx`, `raw/step0-baseline-architecture-full-attempt1.txt` |
| 1 Disposable rehearsal (`/private/tmp/mvp6-q25-loadspub.4mTLMX/rehearsal/repo`, working tree without .git/bin/obj): W1–W5 performed, same hashes as below; real DocsPathGuardTests, with the root resolved to the copy (built dll under the copy) | PASS, **39/39** | `REHEARSAL-DOCS-PATH-GUARD-attempt1.trx`, `raw/step1-*.txt` |
| 2 Repository write in one step: status unchanged since start; W1/W5 snapshot; `git apply` (worktree, no --index/--3way/--reject/fuzz) → W1, W2; W3 → W4 → W5 | PASS | `raw/step2-*.txt`, `CHANGED-FILES.tsv` |
| 2.4 Post-checks: hashes; W1/W2 byte-equal to the release-prep artifacts; W5 payload re-hash `a77538b4…11e0`; W4 `payloadSha256` equal; W5 `decision.sha256` = SHA-256(W4); 2 targets / 43 seals; v2 annex unchanged; porcelain diff adds only W2, the W3 folder and W4 (W1 and W5 were already non-clean lines) | PASS | `raw/step2-postchecks.txt`, `raw/porcelain-status-*.txt` |
| 3 Production DocsPathGuardTests (native .NET 8, Debug) | PASS, **39/39** (production positive scan + all negative fixtures) | `PRODUCTION-DOCS-PATH-GUARD-attempt1.trx` |
| 3 Full architecture project | 56 tests: **53 pass / 3 fail**; the 3 failures are exactly the Step 0.6 non-DocsPath baseline set; no new failure | `ARCHITECTURE-FULL-attempt1.trx` |
| 4 Evidence (only .md/.tsv/.txt/.trx) | Written | this folder; `SHA256SUMS` |

## W1–W5

| # | Path | SHA-256 after |
|---|---|---|
| W1 | `docs/analysis/contracts/shipment-bundle.openapi.yaml` | `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2` (before `5dfe7c1b…d21c`) |
| W2 | `docs/analysis/contracts/loads-semantics-v3.1.0.md` | `9d8a370664abb8221d5f8b23038f7c506e0621fadb2e5c83450ad93e63cf5034` |
| W3 | `docs/records/audits/2026-09/mvp6-loads-publication-guard-01/PROVENANCE.md` | `84f07a6b6dde53954f2c7e2302d8a3f0a2b762b5dffd94309175a1ed89306f0a` |
| W4 | `docs/records/decisions/2026-09/mvp6-loads-publication-guard-owner-decision-01.json` | `64db811693d3a25143ebe94435114739ac8016d289c8d26a296c12d516491234` |
| W5 | `docs/reference/architecture/docs-path-authority.json` | `65a8ccbdd09acb97be8bed0195b3732fba241667e494a314d4dc4a192b660a4e` (before `6d3865f8…8167`); payload `a77538b4784e1749b7ebe2e7d38d2e8400803c63ec8e6f49d3382929d7d611e0` |

The v2 annex is unchanged at `a2187c93…2be1`.

## Deviations and notes

1. **`--results-directory <temp>/trx` added to every `dotnet test`.** The literal Step 3 command writes `TestResults/PRODUCTION-DOCS-PATH-GUARD.trx`, and that file already exists as 23 Sep evidence. CT addition 4 forbids overwriting and requires unique attempt names. The TRX files were produced in `/private/tmp` and copied here with `-attempt1` names. The filter, configuration, SDK and project are unchanged.
2. **W3–W5 were written by a python3 script** that does exact single-occurrence replacement, per CT addition 2. It lives outside the repository; its bytes are kept here as `raw/w345.py.txt`, since no `.py` file may be written here.
3. **Exit-code echo artefact in Step 3** (explained in `COMMANDS.tsv`). The results come from the TRX outcomes.
4. **Artifacts outside the evidence folder:** `dotnet test` rebuilt `bin/`/`obj/` under `tests/architecture/…`, which is git-ignored. `tests/…/TestResults/` was not touched. No other repository path changed. No git write other than the two authorised `git apply` runs (rehearsal copy and repository); no `--index`; no commit, push or stash.
5. `PROVENANCE.md` is W3 and is never edited. It is excluded from `SHA256SUMS`, as EXECUTION-ORDER Step 4 requires. `SECRET-SCAN.txt` is the last write and is also outside `SHA256SUMS`.
6. The temp workspace (`/private/tmp/mvp6-q25-loadspub.4mTLMX`: rehearsal copy, rollback snapshot, TRX originals) is kept until CT decides, because the rollback snapshot is still valid. It holds no secrets.

Return to CT; CT decides.
