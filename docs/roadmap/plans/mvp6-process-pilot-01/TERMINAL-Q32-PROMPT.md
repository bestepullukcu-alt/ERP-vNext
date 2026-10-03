# Terminal verifier lane — independent VER of the Loads publication + guard binding (queue Q32)

Written by CT 2026-09-26. Run in a NEW Claude Code session in Terminal on the Mac (you are not AL-MVP6-LOADS-PUB01 and have no access to its session).
🤖 Applying knowledge of @read-only-auditor and @testing-agent.

Repo: /Users/natig/Projects/ERP-vNext-recovery @ 4a8d4d4b (feature/mvp6-logistics; STOP if different). Read-only for the repository: Git only with GIT_OPTIONAL_LOCKS=0, no git writes, no edits outside your output folder. Do not change `.claude/` settings.
Task: EXECUTION-ORDER Step 5 in `docs/roadmap/plans/mvp6-loads-guard-binding-prep-01/EXECUTION-ORDER.md` (sha256 48e1e92f…b277). Inputs: CT intake `docs/records/audits/2026-09/mvp6-ct-loads-publication-intake-2026-09-26.md`; writer evidence `docs/records/audits/2026-09/mvp6-loads-publication-guard-01/` (REPORT.md first); DISPATCH `docs/roadmap/plans/mvp6-loads-publication-dispatch-01/DISPATCH-v1.1.md`.
Check with your own commands, PASS/FAIL each:
 1. Decision B (36fe774a…) and guard decision record hashes; prep SHA256SUMS 16/16; writer SHA256SUMS 24/24.
 2. W1–W5 current sha256 = the approved values; W1/W2 byte-equal to the release-prep artifacts; recompute the W5 payload (canonicalTargets raw text + "\n" + sealedInputs raw text, SHA-256) = a77538b4…d611e0; W4↔W5 binding; 2 targets / 43 seals; v2 annex unchanged.
 3. No writes outside W1–W5 + the evidence folder: compare `git status --porcelain` and file mtimes against the CT intake list.
 4. Fresh `dotnet test` of DocsPathGuardTests (native /Users/natig/.dotnet/dotnet, SDK 8.0.417) with `--results-directory` inside your output folder (never overwrite existing TRX). Also run the full architecture test project and confirm the only failures are the 3 pre-existing ones (JwtClockSkew ×2, MongoTestDatabase ×1).
 5. Judge the writer's reported deviations (results-directory, echoed exit codes, W3–W5 via external python script) — do they change any result?
 6. Secret scan of the writer evidence and your own output.
Output: docs/records/audits/2026-09/mvp6-loads-publication-guard-independent-ver-01/ → SOP-22-VER.md, COMMANDS.tsv, TRX files, ARTIFACTS.sha256. Keep unique names per attempt; secret scan as last write. Final report with Istanbul start/end. Return to CT; CT decides. Q09 producer uptake stays HELD.
