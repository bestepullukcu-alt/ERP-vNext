# DISPATCH v1.0 — MVP6 Loads canonical publication (Q08 execution) — DRAFT for CT

Drafted 2026-09-25T20:48Z (23:48 +03:00). CT dispatches this draft; drafting it authorizes nothing by itself.

Role: **AL-MVP6-LOADS-PUB01** — single publication owner and the only writer of the two canonical paths below. No access to earlier conversations.
Repo: /Users/natig/Projects/ERP-vNext-recovery @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (feature/mvp6-logistics; STOP if different).
Process: docs/guides/operations/mvp6-development-process-v1.0.md §2 (queue states, stop rule).
Authority: docs/records/decisions/2026-09/mvp6-loads-root-amendment-owner-decision-b-01.md (read its hash at dispatch and STOP if it differs from the hash CT records), with A: mvp6-loads-root-amendment-owner-decision-a-01.md `e12d59750c4846269bf1b84341f9c437060e5b891740bb4077e8917ebf3eea9f`.
Package: docs/records/audits/2026-09/mvp6-loads-root-amendment-release-prep-01/ (below: PKG).

## Write scope (exhaustive)

- Modify: `docs/analysis/contracts/shipment-bundle.openapi.yaml`
- Create: `docs/analysis/contracts/loads-semantics-v3.1.0.md`
- Evidence only: `docs/records/audits/2026-09/mvp6-loads-publication-01/` (new)
Nothing else. `loads-semantics-v2.0.0.md` must stay byte-identical. Use `git --no-optional-locks` for read commands, and never use `git apply --index` or any other command that writes `.git/index`.

## Step 1 — Preconditions (STOP on any mismatch; record every hash)

1. HEAD and branch as above.
2. `sha256sum -c PKG/SHA256SUMS` from the repo root → 21/21 OK; SHA256SUMS self `61ddf10962d51f52f73c155c6e5c52c8dc753344ad115e9ae7905b31d0b36de9`.
3. `PKG/publication.patch` = `dc0ad05bbb47b05c3c62dadf138faab847337ddc9f387bb9f764e84e02fdbed5`.
4. YAML preimage = `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c`.
5. v2 annex = `a2187c934cf9176cae4cb8b9c70dfd1298154e5124cdc197d3029103636c2be1`.
6. `docs/analysis/contracts/loads-semantics-v3.1.0.md` absent.
7. Patch headers touch only the two write-scope contract paths.

## Step 2 — Disposable byte-equality proof (before touching canonical files)

1. Copy the YAML and v2 annex, at their preimage hashes and on the same relative paths, into a fresh temp directory outside the repo (e.g. `mktemp -d`), then `git init` there.
2. `git apply --check` and then `git apply` of PKG/publication.patch in that directory; both exit 0.
3. Proof: YAML = `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2` and byte-equal to `PKG/artifacts/shipment-bundle-v3.1.0-final-proposed.openapi.yaml`; new annex = `9d8a370664abb8221d5f8b23038f7c506e0621fadb2e5c83450ad93e63cf5034` and byte-equal to `PKG/artifacts/loads-semantics-v3.1.0.md`; v2 annex still `a2187c93…2be1`; no other file created.
4. Record the temp path, commands, exit codes and hashes. Any failure → STOP; do not touch canonical files.

## Step 3 — Canonical publication (only if Steps 1–2 PASS)

1. Re-check Step 1 items 4–6 immediately before applying.
2. From the repo root: `git apply PKG/publication.patch` (worktree only; no `--index`, no `--3way`, no fuzz, no `--reject`).
3. Post-check: YAML = `6dc1dd48…e7796aa2`, new annex = `9d8a3706…5034`, v2 annex = `a2187c93…2be1`; `git --no-optional-locks status --porcelain` shows no new change outside the write scope compared with the pre-apply snapshot.
4. On any post-check failure: STOP and report. Do not attempt repair, re-apply or revert without a new owner decision.

## Step 4 — Evidence

Under `docs/records/audits/2026-09/mvp6-loads-publication-01/`: INPUTS.tsv (authority, package and preimage hashes), COMMANDS.tsv (every command with its exit code), ACCEPTANCE.tsv (PUB-01…PUB-n: each Step 1–3 check, PASS/FAIL), raw/ (command output), before/after status snapshots, SHA256SUMS over the evidence set.

## Observe, do not fix

- If any existing guard, docs-path pin or architecture test (e.g. DocsPathGuardTests, `mvp6-loads-docs-path-owner-binding-01.json`) would reject the new canonical hash, record it as an open item for a separate guard-binding decision (R185-C05). Do not edit guard payloads, bindings or tests.

## Forbidden

Runtime/producer uptake, UI, gateway, guard or docs-path binding, pack promotion (MOD-0185 unchanged), rollout, CT-QUEUE edits, commit, push, any edit outside the write scope. Agent PASS is not CT acceptance.

## Then — independent VER (separate lane, dispatched by CT)

A lane other than AL-MVP6-LOADS-PUB01, with read-only access, re-verifies from the repo: authority record hashes, the three canonical hashes, byte-equality with the PKG artifacts, that v2 is unchanged, that nothing changed outside the write scope, and the PUB evidence SHA256SUMS. Q09 (C — producer uptake) stays HELD until that VER passes and CT records it.

Final report with Istanbul (+03:00) times. Return to CT.
