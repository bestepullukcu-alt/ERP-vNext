# Owner decision — MVP6 lane placement (read-only lanes in Cowork) — 2026-09-26

Recorded by the Q102 lane (WP-MVP6-GOV-102 · Prompt Q102 v1 · AL-MVP6-GOV-102; Claude Code on the Mac; single ledger writer)
at 2026-09-26T22:04+03:00 on CT instruction (CT writes no files).
The owner gave this decision at ~21:25 +03:00 in the CT conversation, before the Q101 dispatch (Q101 start 21:26:56).

## Refines

`docs/records/decisions/2026-09/mvp6-lanes-claude-code-owner-decision-01.md`
(sha256 `fcb16a0d113645ac97944886336a19d55cae5669e6a238e3e6826447197411fb`, measured 2026-09-26T22:03+03:00).

That decision says "all lanes run in Claude Code on the Mac". This decision narrows it by lane type. Every other rule in it stays in
force: GIT_OPTIONAL_LOCKS=0, single ledger writer, verifier after the hand-off line, SOP §36.1 prompts, and Q03a no-commit.

## Decision

| Lane type | Where it runs | Darwin gate | Repository path |
|---|---|---|---|
| **Read-only** audit or VER lane (reads, hashes, static checks only) | **Cowork chat lane** allowed | **No** | `$HOME/mnt/ERP-vNext-recovery` |
| Any lane that **writes, applies patches, builds, tests or runs** anything | **Only Claude Code in Mac Terminal** | Yes (`uname -s` = `Darwin`) | the Mac checkout |

- A read-only lane that finds it needs to write something stops and returns to CT. It does not write from Cowork.
- `GIT_OPTIONAL_LOCKS=0` applies in both places. A read-only `git status` from Cowork without it left an empty `.git/index.lock` at ~21:47
  (`docs/records/audits/2026-09/mvp6-ct-verdict-q101-2026-09-26.md`).

## Applied so far

- **Q101** (read-only control audit) ran in a Cowork chat lane under this decision.
- **Q95 v2** (independent VER of Q93, read-only) was re-dispatched to Cowork after Q95 v1 stopped at the Darwin gate.

## Scope

Execution placement only. Grants no code, pack, contract, gateway, commit, push or stash authority.
