# MVP6 process pilot 01 — working records

Started 2026-09-25 22:14 (Istanbul). Process: [mvp6-development-process-v1.0](../../../guides/operations/mvp6-development-process-v1.0.md).
Plan: [v9.1](../mvp6-development-plan-v9.1.md).

| File | Purpose | Written by |
|---|---|---|
| `CT-QUEUE.tsv` | Every open item with one state (READY / IN-PROGRESS / DECISION-REQUIRED / HELD / BLOCKED / REWORK / DONE), owner, dependency and re-dispatch trigger | CT |
| `BLOCKERS.tsv` | Blockers with type, owner, first recorded date and status; overlapping waits are not double counted | CT |
| `MILESTONE-EVENTS.tsv` | Timestamped task start, writer hand-off, VER and CT decision events — from 25 Sep onward, no backfill | CT (from lane reports) |
| `TIME-INTERVALS.tsv` | Human (reported/observed) and agent intervals, kept separate; empty until the owner names who records human time | Owner / CT |
| `EVIDENCE-REUSE.tsv` | Every inherit-or-rerun decision with the changed input and reason | CT / verifier |
| `LANE-PROMPTS.md` | Current lane prompts under the pilot | CT |

These are working records for the pilot, not approvals. No existing acceptance, decision or exact-hash scope changes.
