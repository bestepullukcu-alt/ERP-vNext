# Owner decision — MVP6 lanes run in Claude Code on the Mac — 2026-09-26

Recorded by the Q93 v2 lane (Claude Code on the Mac; single ledger writer) at 2026-09-26T21:15+03:00 on CT instruction (CT writes no files).
Decision given by the owner at ~20:20 +03:00 (CT conversation).

## Decision

- **All lanes run in Claude Code on the Mac.**
- **CT verifies from Cowork.**
- **Q03a still applies: no commit** (`docs/records/decisions/2026-09/mvp6-commit-strategy-owner-decision-q03a-01.md`).

## Lane rules

1. **Darwin gate first** — `uname -s` must print `Darwin`; anything else stops the lane ("wrong environment").
2. `export GIT_OPTIONAL_LOCKS=0` before any git command.
3. **Single ledger writer** — one lane at a time writes `CT-QUEUE.tsv` / `MILESTONE-EVENTS.tsv`.
4. **A verifier starts only after the writer's hand-off line** exists in `MILESTONE-EVENTS.tsv`.
5. Prompts use the **SOP §36.1** format (`docs/guides/operations/control-tower-sop.md`).

## Scope

Execution environment and lane discipline only. Grants no code, pack, contract, gateway, commit, push or stash authority.
