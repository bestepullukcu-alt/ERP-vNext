# DISPATCH v1.1 — MVP6 Loads canonical publication + guard binding (Q25) — issued by CT 2026-09-26 00:40

Supersedes the sequencing of DISPATCH-v1.0.md (its checks remain valid). v1.0 stays unchanged.

```
Role: AL-MVP6-LOADS-PUB01 — single writer for the Loads 3.1.0 publication and the docs-path guard binding. No access to earlier conversations.
Executor: Claude Code running LOCALLY on the owner's Mac. STEP 0 first: `uname -s` = Darwin; /Users/natig/.dotnet/dotnet SDK 8.0.417 / runtime 8.0.23; ≥ 3 GB free disk. STOP if not.
Repo: /Users/natig/Projects/ERP-vNext-recovery @ 4a8d4d4b339528a88e6220fb8402e5a2c771136c (feature/mvp6-logistics; STOP if different).
Authority (both required; STOP if either hash differs):
 - Decision B: docs/records/decisions/2026-09/mvp6-loads-root-amendment-owner-decision-b-01.md  sha256 36fe774a11a31c2b6e4f3c623f76cf22b4f2fe272474a8780f1b701753e5f35f
 - Guard binding: docs/records/decisions/2026-09/mvp6-loads-publication-guard-owner-decision-01.md (read and record its sha256; it binds OWNER-DECISION-TEXT.md 8cd55b8200f648a114df51b4a44e1ea56f011f3c2de2d9f396d493707184990d and payload a77538b4784e1749b7ebe2e7d38d2e8400803c63ec8e6f49d3382929d7d611e0; approvedBy value current-role-user-message-2026-09-26)
Procedure: follow docs/roadmap/plans/mvp6-loads-guard-binding-prep-01/EXECUTION-ORDER.md (sha256 48e1e92fdf2ec0046f7cd068c0d5981a4931511a5df2af84dd4362861015b277) exactly — Step 0 preconditions incl. baseline architecture-test run, Step 1 disposable rehearsal with the real DocsPathGuardTests, Step 2 one-step repository write of W1–W5 only, Step 3 production DocsPathGuardTests + full architecture tests, Step 4 evidence, failure rules (STOP and restore). Prep package SHA256SUMS sha256 fe808bb979b42a340f0cabf3472c88019f38103c1de3872c11ce4e68223471f0 must verify 16/16.
Also read: docs/roadmap/plans/mvp6-loads-publication-dispatch-01/DISPATCH-v1.0.md (checks), docs/roadmap/plans/mvp6-loads-guard-binding-prep-01/{README,ANALYSIS,PAYLOAD-HASH}.md, docs/guides/operations/mvp6-development-process-v1.0.md §§2, 5.
Evidence: docs/records/audits/2026-09/mvp6-loads-publication-guard-01/ only, and only .md/.tsv/.txt/.trx files (never .json/.py/.sh/.js). PROVENANCE.md is written once and never edited.
Forbidden: any file outside W1–W5 and the evidence folder; producer uptake (C), UI, gateway, pack promotion, rule/test/schema edits, historical-evidence edits, commit, push, stash, git --index operations. Agent PASS is not CT acceptance.
Final report with Istanbul start/end times, per-step PASS/FAIL, all W1–W5 hashes, test counts. Return to CT. Independent VER (EXECUTION-ORDER Step 5) is dispatched separately by CT to a different lane.
```
