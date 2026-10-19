# Evidence — CT-ROUTE-01, which Control Tower answers a contract or delivery question (2026-10-16)

Authority: `CT-ROUTE-01-JOINT-DECISION-01` (joint decision of Codex and the AI session; the owner approved the local
implementation before any PR — relayed by Codex). Branch `chore/ct/pqg-01` on `b0faa372b`, local only. The ControlTower
(Legacy) session gave the single-writer window for the two shared documents directly; the AI session wrote only those
lines and closed it.

## What changed — four source files

| File | Change |
|---|---|
| `scripts/ct_route.py` (new, read-only) | `--kind delivery\|contract`, `--module` or `--wp/--queue`, `--ref`, `--in-force`, `--contacts`; reads the registry, DCPs, queues and packs at one fixed SHA and the same again at the local `origin/main` for in force; prints a question draft and never sends it. Reuses `ct_queue` (`Table`, `REGISTRY_COLUMNS`, `check_registry`, `known_modules_from`), `status_report_v2.Source` and `verify_module_id`'s readers; none of them changed |
| `.antigravity/rules/capability-pack-standard.md` | CAP-001, after §4's list: the optional `## 8.1 Delivery slices` table, the `dcp:` prefix of a registry `scope`, an example (+17 lines) |
| `docs/guides/operations/control-tower-operating-card.md` | §0: one line on the `dcp:` prefix; §1: one item for `ct_route.py` (+3 lines) |
| `execution/delivery/ct-contacts-template.tsv` (new) | the header `queue_id · kişi · iletişim` only — an example, not a source; a person is never in the repository |

Pinned sources (SHA-256): `ct_route.py` `8cd2b2f7ded7c59f045b00890d6408c87f11d894ba212fc4455f4f304644ae17` (1191 lines),
CAP-001 `ee5241de42f5a571b2d57996beef020e5473a24f67c4fc4aef7b00c496012a73`, card
`dcc25aad29fef64899b438d650ba9a859e51665c000b1a0f4d23f3df1543e990`, template
`b17d9b5aa4d6025d9c6aac0927b0a49bd98839478ff9c70c87d35eb791e5b5ea`.

## Checks and results

| Check | Result |
|---|---|
| `ct_route.py --self-test` on the pinned file | 167/167 — the same output in the worktree, in the pinned-source clone and in the merged tree |
| Other self-tests (pinned sources and merged tree, outputs identical) | ct_queue 69 · acceptance_record 100 · prompt_gate 71 · status_report 75 · hook 21 · known_failures 24; `ct_queue validate` OK |
| Tests first | the self-test was written against stubs first (1/59 passing); Codex's findings were reproduced on the earlier code by Codex's own probes before the fixes (CLI 12/13, the five broken main rows 0/5 rejected, a two-id pack and three main-side WP changes taken as success) |
| Sabotage, `sab_route.py.txt` → `sabotage.txt` | 115 guards removed one at a time in scratch copies: each turns a named self-test red for its own reason; 0 crashed. The first pass found three gaps and one harness form, fixed before pinning: an e-mail assertion read the wrong output; two one-line-value cases were missing; one sabotage crashed instead of answering |
| Real-git end to end, `e2e_route.py.txt` → `e2e.txt` | 38/38 expectations pass in a scratch clone (synthetic commits only): fixed-SHA reads (a broken working tree ignored), preparation vs in force, a rival queue on main (`--module` 1, `--wp` 0), the five broken main rows (2), three WP changes on main, no registry / no `origin/main`, a two-id pack (2), a broken registry at the ref (2), a malformed prefix (2 for `--module`, 0 for an unrelated `--wp`), FU and base queries, the real DCP-009 künye copied into a synthetic DCP, the real draft pack MOD-0148 (1), the SCE free-text row (1), contacts outside / inside the clone / inside another worktree / through a symlink |
| The same driver from the merged tree, `e2e-merged.txt` | 38/38 — the base is a single-parent synthetic commit whose tree is the merged tree `0a62a475…`; the commit of the tool and documents on it is empty, because the base already holds them |
| Wiring sabotage, `sab_e2e.py.txt` → `sabotage-e2e.txt` | 6 wirings removed one at a time, each in a new clone: reads from the working tree (17 expectations red), in force on the ref instead of `origin/main` (15), no registry contract at a snapshot (6), a main without a registry taken as unmeasurable (2), packs read by `verify_module_id`'s last id (2), the worktree list dropped from the contacts roots (1). They ran with the driver before its optional start point was added; nothing else in it changed |
| Real files, read only, nothing changed | the 16 DCPs with a künye read with a slice table added in memory (DCP-009 included); the 3 without one are named; the 159 packs with an id read exactly as the old reader read them |
| Codex's probes, run by the AI session on the pinned code | CLI 14/14 · the five broken main rows 5/5 · parser 14/14 |
| Codex, independently | 40 scenarios of its own on `94444b31…` (fixture-v3: CLI 14/14, main rows 5/5, edges 7/7, parser 14/14); everything outside the self-test is byte-identical in the pinned file (function parity, measured); on the pinned file itself: self-test 167/167, ct_queue 69/69, status_report_v2 75/75, `diff --check`, protected files unchanged (`independent-checks-codex.json`, copied unchanged) |
| Merged tree | `git merge-tree --write-tree 3912d568b <pinned sources on b0faa372b>`: exit 0; tree `0a62a475b1bda39bb3138d4d0e63227e051336b6` — the tree Codex computed independently; no merge commit |
| Architecture suite on the merged tree (one run) | 131/138; `known_failures.py check` BİLİNEN=7, YENİ=0, exit 0 (raw and sanitised); fingerprints = raw = the previous merged-tree run; the tree unchanged before and after |
| `run.sanitized.trx` | clone root normalised first, then R1–R4; SHA-256 `0af9f12b93d0d38d9f0baa2c83b012064eec97e1662e78f5cb4ba7de0a7cb42e`; user name, home, clone and scratch roots: 0. The `computerName` fields are sanitised; the same digits as the raw three-digit value occur by chance inside a GUID and a numeric table cell — not an identity remnant (Codex checked this field by field) |
| Protected files | registry, queue template, the SCE queue, `ct_queue.py`, `prompt_gate.py`, `acceptance_record.py`, SOP from §36.2: unchanged |
| Remote `main` | `3912d568b18193f627539a1b27965167ac4077b6` — Codex, read-only `git ls-remote`, 2026-10-16 in this Codex session; the exact time was not recorded; no fetch. The tool itself reads only the local `origin/main` |
| Pre-publication scan | 3868 added lines (the new files and the two documents' added lines), 12 patterns (local paths, user / host name, the owner's e-mail domains, private key, password, key and token forms): 2 matches, both synthetic paths in the self-test's symlink cases (`/tmp/wt`, `/private/tmp/wt`), not a machine path. A heuristic over these patterns; it does not prove the absence of every secret |

## Files

| File | What it is |
|---|---|
| `checks.txt` | The outputs above; local paths replaced (`__SCRATCH__`, `__HOME__`, `__TMP__`) |
| `e2e_route.py.txt` · `sab_route.py.txt` · `sab_e2e.py.txt` | The end-to-end driver and the two sabotage harnesses |
| `e2e.txt` · `e2e-merged.txt` · `sabotage.txt` · `sabotage-e2e.txt` | Their outputs |
| `run.sanitized.trx` | The architecture suite on the merged tree, sanitised |
| `independent-checks-codex.json` | Codex's path-free summary, copied unchanged |

## Limits — NOT VERIFIED or not machine-checkable

- No real Control Tower is assigned: the registry is unchanged; every opted-in row and slice table is synthetic.
- In force is the local `origin/main` only; today it has no registry, so every real mapping reads "not in force".
- The coordinator is a DCP's `owner` field read as a role; whether a person holds it is not checked.
- This commit adds these records on top of the tested sources; the tested tree is `0a62a475…` (sources + `main`).
- Publication is the owner's decision.
