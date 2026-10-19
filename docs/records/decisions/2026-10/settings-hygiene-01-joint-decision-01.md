---
decision_id: SETTINGS-HYGIENE-01-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge)
decided_at_local: 2026-10-18
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 30059 and 30073 (the C5 pilot: the repository's settings showed 81 allow entries and an extra directory on the trust screen; no blanket approval), 30465 (the 6-rule proposal agreed as a strict subset; build / test run code, not read-only), 30500 (this branch after CT-FIX-07)
scope: .claude/settings.json only — a narrowing of the project's own pre-approved permissions. Not settings.local.json (untracked), .gitignore, another checkout, product code or the instruction core; not the user, local or managed settings layers.
relates_to: CT-FIX-07-JOINT-DECISION-01; the C5 pilot method records (Codex's)
recorded_by: the AI session (Claude Code) on 2026-10-18
---

# SETTINGS-HYGIENE-01 — the project's pre-approved permissions narrowed to six

## Why

A new session in this repository is asked to trust the project and, with it, 81 pre-approved command patterns and an
extra directory (`C:\tmp`). Most are leftovers of past sessions: one user's absolute paths, Windows paths and shells,
one-off probes, generators that write files, package installs, and wildcards with an unbounded scope. The C5 pilot
could not accept them, and a daily user should not have to.

## What was decided

- Kept, in the old order (6): the two golden-reference checks and the `MOD-0023` id check of the repository's own
  verification tools (fixed arguments), `dotnet build *` and `dotnet test *` (approved build and test — they run the
  repository's code and are not read-only), `git check-ignore *`.
- Removed (75): listed in `docs/records/audits/2026-10/settings-hygiene-01/removed-entries.tsv` by old line, the
  entry's SHA-256 and a category — not by their text, which holds personal paths.
- `additionalDirectories` removed. The PostToolUse hook unchanged. No ask, deny, wildcard or other new rule.

## Known consequences

- Commands that matched a removed pattern now ask for approval, as any other command does; no blanket "ask every Bash"
  default is added.
- Other settings layers (user, local, managed) keep whatever they allow; this file's cleanup does not close them.
- No test of the setting's effect on a live session was run; the checks are structural (`checks.txt`).
