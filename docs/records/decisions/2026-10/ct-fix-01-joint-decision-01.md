---
decision_id: CT-FIX-01-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session; the owner said "ok" to the two narrow fixes, their independent verification and a final joint pilot (relayed by Codex)
decided_at_local: 2026-10-17T01:59+03:00 (the owner's "ok", relayed) · 02:08 (scope agreed) · 02:47 (final source review agreed)
decided_by: Codex and the AI session; the owner's answer came through Codex and was relayed — the AI session did not see it directly
decision_source: Codex's messages in the AI session, transcript lines 18680 (the owner's "ok"), 18841 (scope), 19483 (final source review), 0-based; the two independent audit reports as the owner pasted them, line 18601; Codex's classification of findings 02 and 13, line 18651
scope: CT-FIX-01 — the critical Control Tower findings 05, 06, 09, 11 and 13 of the 2026-10-17 independent audits, fixed fail-closed in scripts/ct_queue.py, scripts/prompt_gate.py, scripts/acceptance_record.py, scripts/status_report_v2.py and .antigravity/scripts/verify_module_id.py; one self-test fixture value in scripts/ct_route.py; one new test file in scripts/tests. Finding 02 and the loading claims (İ5B-FIX-01) are fixed on the İ5-B line and recorded there.
relates_to: CT-ROUTE-01-JOINT-DECISION-01; CT-CI-01-JOINT-DECISION-01; REP-001 (`yol@commit`, unchanged); the operating card §0 (terminal states); PR #147 (`chore/ct/pqg-01`); PR #149 (İ5-B)
recorded_by: the AI session (Claude Code) on 2026-10-17
---

# CT-FIX-01 — the Control Tower gates refuse broken or conflicting input

## Why

Two independent audits of 2026-10-17 (another Claude account, and Codex) found the same verdict, **UYGUN DEĞİL**: five
critical gates let a broken or conflicting input through to a dispatch or an acceptance answer. Line 18680, copied
byte-for-byte (first sentence):

> Sahip, Codex'in iki dar düzeltme + bağımsız doğrulama + son ortak pilot önerisine 'ok' dedi.

## What was decided — per finding

| Finding | Before | Now |
|---|---|---|
| **05** task source | `validate` rejected a duplicate registry row, but `require`, `guard` and the prompt gate read the first row; a branch could register `strict=no` where main has `strict=yes`; `baseline_commit` was any value `cat-file` resolves | `load_for_dispatch()` runs the checks `validate` reports (`dispatch_problems`) and stops with DUR for every consumer but `validate`, which still lists them. `in_force_status` also compares `strict`, `vocabulary`, `baseline_commit` with main ("farklı profil"). `baseline_commit` must be a full 40-hex id |
| **06** strict queue | a state defined twice took its last row (FAILED became dispatchable); one transition row could re-open a terminal state | for `sop7` only: READY is the only dispatchable state; ACCEPTED, REWORK, FAILED, SUPERSEDED, CANCELLED are always terminal and a row leaving one is a problem (states/transitions check) and a ProfileError. SCE (`sce-mvp6`) keeps its own rules |
| **09** duplicate discovery | a short row, an empty id, an empty name, a cut header or a row split from its header was skipped silently: "no match", exit 0 | discovery only: every registry table starts with the contract header (9 columns) and a separator; any other shape stops with "tamamlanamadı" (exit 2) naming the line. `--check-id` / `--candidate` keep their reader |
| **11** pack / governance approval | the first `status:` line in the first lines won; a status outside a front matter counted; a pack's first word counted | one closed front matter with exactly one `status:`. A pack's whole value is `ready-for-dev` or `approved`. A governance record approves with `approved` or `decided`, optionally ` (…)` and ` — <text>` (the forms its records use) |
| **13** fixed commit evidence | a hex id resolved through any ref — a branch named like a hex was accepted as a "fixed" commit (acceptance, prompt gate incl. the discovery report and `--review`, report) | a hex id resolves only to a commit that starts with it (`commit_id`), and the read uses the full id; named refs (HEAD, origin/main, a report REF) are unchanged |

## The governance status — narrow meaning

One existing record changes its reading: `docs/records/decisions/2026-09/mvp6-loads-uptake-ph15-owner-decision-01.md`,
status `approved (PH15-185-UPTAKE) + F-1: fix together with the uptake`. Of 79 decision records, the old reader counted
38 as approvals and the new one counts 37; this record is the only difference.

It holds two explicit owner approvals and is **not** reclassified as "no approval" or "invalid". The new prompt gate
only gives it no automatic pass, because its combined status form is not one the gate reads. The record's own scope,
and the approved SCE work running under R8, are not cancelled. A new dispatch on it needs an unambiguous front matter
(a new record; this one is not edited). The 167 module packs: 92 approved before and after, no pack's decision changed.

## What did not change

The operating card, SOP, REP-001, the registry, the state and transition files, the SCE queue, every earlier record,
`list_screen_touch_hook.py`, `known_failures.py` and the `--check-id` / `--candidate` modes. No push, PR update, merge or
rebase; the branch is local.

## Limits

- CI on the fixed sources has not run (NOT VERIFIED).
- The live loading of the new İ5-B core block is not measured (NOT VERIFIED); the 2026-10-17 measurement is for
  `c02b28c16`'s core only.
- The joint pilot was replayed on a test fixture whose origin/main is an imitation; it is no proof of the remote state.
- Evidence: `docs/records/audits/2026-10/ct-fix-01/README.md`.
