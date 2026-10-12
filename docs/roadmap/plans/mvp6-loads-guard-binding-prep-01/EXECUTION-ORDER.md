# Coordinated execution order — publication (Q25) + guard binding (Q26)

Valid only after BOTH decision B (recorded) AND the MVP6-LOADS-PUBLICATION-GUARD-01 owner decision are recorded. Single writer: **AL-MVP6-LOADS-PUB01**. This order supersedes the sequencing (not the checks) of `docs/roadmap/plans/mvp6-loads-publication-dispatch-01/DISPATCH-v1.0.md` Steps 3–4; CT should issue a DISPATCH v1.1 that references this file.

## Write set (exhaustive, 5 paths)

| # | Path | Action | Required result SHA-256 |
|---|---|---|---|
| W1 | `docs/analysis/contracts/shipment-bundle.openapi.yaml` | patch `dc0ad05b…fdbed5` | `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2` |
| W2 | `docs/analysis/contracts/loads-semantics-v3.1.0.md` | created by same patch | `9d8a370664abb8221d5f8b23038f7c506e0621fadb2e5c83450ad93e63cf5034` |
| W3 | `docs/records/audits/2026-09/mvp6-loads-publication-guard-01/PROVENANCE.md` | byte copy of `PROVENANCE.md.txt` | `84f07a6b6dde53954f2c7e2302d8a3f0a2b762b5dffd94309175a1ed89306f0a` |
| W4 | `docs/records/decisions/2026-09/mvp6-loads-publication-guard-owner-decision-01.json` | candidate decision; change only `"status": "UNAPPROVED"`→`"APPROVED"` and `"approvedBy": "PENDING-OWNER"`→`"current-role-user-message-YYYY-MM-DD"` | computed; `payloadSha256` must stay `a77538b4…11e0` |
| W5 | `docs/reference/architecture/docs-path-authority.json` | candidate authority; replace only `"status": "UNAPPROVED",\n  "decision": null,` with `"status": "APPROVED",\n  "decision": {\n    "path": "<W4 path>",\n    "sha256": "<W4 sha>"\n  },` (4-space/2-space indent as in today's file) | computed; payload of the file must re-hash to `a77538b4…11e0` |

Evidence goes only to `docs/records/audits/2026-09/mvp6-loads-publication-guard-01/` beside W3, and only as `.md`, `.tsv`, `.txt` or `.trx`. **No `.json`, `.py` or other scanned extension may be written anywhere**: a new code-extension file naming `docs/analysis/…` would itself be an unsealed offender and turn the guard red. W3 must never be edited after writing. Use `git --no-optional-locks` for reads; never use `--index`.

## Step 0 — Preconditions (read-only; STOP on any mismatch)

1. HEAD `4a8d4d4b339528a88e6220fb8402e5a2c771136c`, branch `feature/mvp6-logistics`.
2. Decision B `36fe774a11a31c2b6e4f3c623f76cf22b4f2fe272474a8780f1b701753e5f35f` and the new guard owner decision record present; CT dispatch names both.
3. `sha256sum -c docs/roadmap/plans/mvp6-loads-guard-binding-prep-01/SHA256SUMS` all OK; `sha256sum -c` of the release-prep SHA256SUMS (21/21) OK.
4. Current authority `6d3865f80707ff0e9ca7c9cf2e2343f34d2961be2916a3eceb37168d1b0a8167`; YAML `5dfe7c1b…d21c`; v2 annex `a2187c93…2be1`; W2, W3 and W4 absent.
5. The 8 seal inputs match ANALYSIS.md §3; DocsPathGuardTests.cs `da5ec0cc…5050`.
6. Baseline run on the unchanged tree: `dotnet test` of the full architecture test project (command in Step 3). Record the pass/fail set. DocsPathGuard is expected to FAIL on the 19 offenders. Every other failure in this set is pre-existing and is the comparison baseline.

## Step 1 — Disposable rehearsal (no repository write)

1. Copy the working tree (without `bin/`, `obj/`, `.git`) to a new temp directory outside the repo; record the path.
2. There, perform W1–W5 exactly as in Step 2, and verify every hash.
3. Run `DocsPathGuardTests` in that copy with the same .NET 8 SDK. Required: **all DocsPathGuardTests pass** (39 in the 23 Sep run).
4. Any failure → STOP; the repository is untouched; report.

## Step 2 — Repository write, one step, single writer

1. Snapshot pre-step bytes and hashes of W1 and W5 into the temp directory (for rollback), plus `git --no-optional-locks status --porcelain`.
2. `git apply docs/records/audits/2026-09/mvp6-loads-root-amendment-release-prep-01/publication.patch` (no `--index`, `--3way`, `--reject`, or fuzz) → W1, W2.
3. Write W3, then W4, then compute W4's SHA-256 and write W5.
4. Post-checks: W1, W2 and W3 hashes as in the table; the payload of W5 hashes to `a77538b4…11e0`; W4 `payloadSha256` equals it; W5 `decision.sha256` equals SHA-256(W4); the v2 annex is unchanged; the porcelain diff against the snapshot contains only W1–W5 and the evidence folder.

## Step 3 — Production guard and architecture tests on native .NET 8 (Mac)

```sh
/Users/natig/.dotnet/dotnet test tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj \
  -c Debug --filter FullyQualifiedName~DocsPathGuardTests --logger "trx;LogFileName=PRODUCTION-DOCS-PATH-GUARD.trx"
/Users/natig/.dotnet/dotnet test tests/architecture/TenantArchitecture.ArchitectureTests/TenantArchitecture.ArchitectureTests.csproj \
  -c Debug --logger "trx;LogFileName=ARCHITECTURE-FULL.trx"
```

Required: all DocsPathGuardTests pass, including the production-mode positive scan and all negative fixtures; in the full project, no failure outside the Step 0 baseline set. A localhost socket or sandbox abort is an environment event: re-run once with permission, and never count it as PASS.

## Step 4 — Evidence

In `mvp6-loads-publication-guard-01/`: INPUTS.tsv, COMMANDS.tsv (every command with its exit code), CHANGED-FILES.tsv (W1–W5 with before/after hashes), both .trx files, the rehearsal path, and SHA256SUMS over the evidence (excluding PROVENANCE.md, which is pinned separately).

## Step 5 — Independent VER (different lane, read-only)

A lane other than AL-MVP6-LOADS-PUB01 re-verifies from the repository: the decision records (B and guard), W1–W5 hashes, byte-equality of W1/W2 with the release-prep artifacts, the payload recomputation, W4/W5 binding, 43 seals / 2 targets, the unchanged v2 annex, no writes outside the write set, and a fresh `DocsPathGuardTests` run. Q09 (C, producer uptake) stays HELD until this VER passes and CT records it.

## On failure — STOP, no partial state

| Where | Action |
|---|---|
| Step 0 or 1 | STOP. No repository write has happened. Report. |
| Step 2 post-check, or Step 3 guard/architecture failure beyond the baseline | STOP. Restore W1 and W5 from the Step 2.1 snapshot (verify `5dfe7c1b…d21c` / `6d3865f8…8167`), and remove W2, W3 and W4, which did not exist before. Confirm the porcelain output equals the Step 2.1 snapshot. Report with evidence written only to a non-repo temp path. No repair, re-apply or alternative binding without a new owner decision. |
| Rollback cannot complete (e.g. deletion blocked) | STOP immediately. Report the exact state of W1–W5 to CT as BLOCKED. Do not attempt a workaround. |
