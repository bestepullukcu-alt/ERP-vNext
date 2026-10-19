---
decision_id: CT-FIX-05-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge); the owner: "buna göre durma, devam et; Claude'un her zaman fikrini al; bu sefer %80'ler ve bağımsız denetim" (relayed by Codex)
decided_at_local: 2026-10-17 (scope agreed after the two v6 audits; contract notes agreed during the work)
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 27327 (Codex Denetim001's v6 report, F1–F6; the frozen v6 sources stay unchanged), 27413 (both audits in; F1, F2 (b), F4, F5, F6 agreed; three F3 corrections; C1, E1, C2 to be decided separately), 27511 (the C1, E1, C2 proposals; no new system, skill or permission; no daily token-saving claim), 27550 (relationship probes — the `./q.tsv` path, the summary's claim; C2 narrowed to the full pack id), 27589 (F3 needs a fresh run directory), 27627 (the revised F3 and C1, E1, C2 agreed with their bounds; "a fixed contract matrix and open limits"), 27776 (C1: a symbolic link on the way), 27820 (the run-record format tightened; the documents exception narrowed to new files under `docs/records/audits/`), 27954 (FIFO artifacts, the artifact size), 28336 (the REP-001 §6.1 wording of the code commit), 28594 (arch-08's two record limits), 28756 (the staged closure)
scope: CT-FIX-05 — the v6 audits' findings on the Control Tower line, F1 (origin/main's registry as a whole), F2 (b) (a -R3 retry bound to its reassessment record), F3 (the evidence bound to the accepted code by a run record), F4 (only a completed Failed row is a known failure), F6 (an unverifiable --previous), C1 (a governance work package's allowed paths), C2 (the pack file's full id), E1 (a conditional or rejected pack in acceptance), and the counter-examples Codex measured during the work. F5 and the module-request answer are İ5B-FIX-06 on the İ5-B line.
relates_to: CT-FIX-04-JOINT-DECISION-01; I5B-FIX-06-JOINT-DECISION-01; SOP §20.2 and §28.2; REP-001 §6.1 and §7.1; PR #147 (`chore/ct/pqg-01`); PR #149 (İ5-B)
recorded_by: the AI session (Claude Code) on 2026-10-17
---

# CT-FIX-05 — the whole registry, a bound retry, run-recorded evidence, outcome kinds, a verified previous report, governance scope, pack identity and approval

## Why

The v6 audits of 2026-10-17 (the same fixed package, two independent auditors) judged CT-FIX-04 + İ5B-FIX-05
(candidate 3, `0609bc588`) not suitable. Codex Denetim001: UYGUN DEĞİL, with six findings — F1 the queue's row was
taken from origin/main's registry without the file being validated (a second queue on the same task file, or a broken
unrelated row, passed every consumer); F2 a -R3 retry was opened by any approved decision record, and the row's
`@version` was cut off; F3 an acceptance's evidence named a file at a commit and nothing tied it to the accepted code
(old evidence accepted new code); F4 a registered test that timed out read as a known failure; F5 an indented core rule
passed (İ5-B); F6 an unverifiable `--previous` left the report complete. Claude Denetim001 reproduced all six and added
C-1 (a governance work package could declare product paths), C-2 (a lettered child pack read as its base) and E-1 (a
conditional pack accepted automatically). The common cause: the relation × consumer matrix was still incomplete (F1,
F2, F6) and two value domains were open (F4, F5); F3 was a declared limit, now a contract.

## What was decided

| Item | Before (`f23455d27`) | Now |
|---|---|---|
| **F1** origin/main's registry | this queue's row was picked and the rest of the file not checked; paths compared as written | the whole file is validated first — every field of every row filled, then `check_registry` (ids, task files, commits, paths) — and any problem is "kayıt geçersiz" for every consumer; a registry path is a normal repo-relative path (no leading `/`, no `\`, already normalised, not under `..`), on main and on the branch |
| **F2 (b)** a -R3 retry | any approved reassessment record whose text resolved the cause; the row's `@version` dropped | `reassessment_binding`: the record's künye has one `work_packages` line that lists the retry's id exactly (`W2-R3` — not `W2`, not another WP; twice or with an empty element refused); a version after `@` is a commit object holding the same record (`record_version_problem`, shared with `governance_binding`). `summary` counts as dispatchable only what `require` accepts and names the rest ("DENETİMDEN GEÇMEYEN k kimlik"). Whether the decision resolves the cause stays the CT's judgement |
| **F3** evidence ↔ the accepted code | `evidence` was `path@commit` of any file | the evidence is a run record (`run-record-v1`) written by `scripts/run_record.py run --out docs/records/audits/<…>/<new directory> -- <command>`: a new directory under `docs/records/audits/`, a clean tree before and after (untracked files included), nothing changed outside the directory during the run, only regular files in the output (a FIFO, socket, device or link is refused), the raw exit kept. The record's `commit` is the acceptance's code `commit`, its `tree` that commit's tree, its `tool.sha256` the runner's blob at that commit; the evidence commit E differs from C only in the run directory and in new acceptance documents under `docs/records/audits/` (files not tracked at C); every `geçti` row cites a run artifact whose bytes and size at E are the record's. A raw exit ≠ 0 gives no automatic ACCEPTED (review; the known-red policy is a separate decision); a mandatory row `inceleme bekler — <neden>` keeps the acceptance open. Strict format: exact key sets, no duplicate key at any depth, `cwd` ".", UTC times in order. The runner never stages or commits (raw logs and argv may carry private data) |
| **F4** outcome ↔ known failure | any failing outcome of a registered test was BİLİNEN | only a completed `Failed` row is; `Timeout`, `Error`, `Aborted` are YENİ-AYRINTI "(sonuç türü …, kayıtlı hata Failed)", and such a row is not hidden behind a `Failed` row of the same name |
| **F6** the report ↔ `--previous` | an unverifiable previous report was a note; complete, exit 0 | an incomplete reason ("önceki rapor …"), `"complete": false`, exit 1 |
| **C1** governance allowed paths | not checked | `check_governance_paths` (yönetişim only): no `/`, `\`, `./`, `//`, `..` or `.` segment; the fixed prefix before the first glob character is not empty and not under `services`, `frontend` or `gateway` (case-folded); no symbolic link at any component of the prefix or under it at the commit. Reading is not restricted |
| **C2** the pack file's id | the file's `MOD-nnnn[-FUn]` against the künye id's base — a lettered child read as its base | `PACK_FILE_FULL_ID`: the file's full id (up to its first lower-case segment or `.md`) equals the künye id; then `pack_module_identity` (the id, or `split_id`'s base for a child, which must be `MOD-nnnn`) is the module — for the gate and the acceptance reader alike (`parse_pack` included) |
| **E1** a second approval field | a conditional pack stopped a dispatch and passed preparation; acceptance ignored the field | `pack_approval_decision(text, mode)` for dispatch, prepare, accept and draft: conditional → dispatch stop ("kapsam türetilmez"), prepare note, accept review ("yeni otomatik kabul yok, inceleme gerekir — eski kabuller değişmez"), draft note ("taslak hazırlık içindir"); rejected, an unknown value or an unreadable field → stop in every mode |
| **Documents** | — | SOP §20.2 (C1, C2) and §28.2 (F2); REP-001 §6.1 (F3, C2, E1; the code-commit sentence in Codex's wording) and §7.1 (F6); the operating card's run-record row and known-failures sentence; the `ct_route.py` module docstring (the conflict rule in both modes — text only) |

## Known consequences (measured)

- Real data, 16 read-only command lines at candidate 3 then candidate 4 (`docs/records/audits/2026-10/ct-fix-05/parity.txt`):
  13 identical; the 3 differences are the `summary` count line's wording (the count 40 unchanged, none refused), the
  pack probe and the Employee discovery report's own hash line.
- **C2:** one real pack changes verdict — `MOD-0021-all-phases-implementation-plan.md` (künye `MOD-0021-PLAN`, file id
  `MOD-0021`) is no longer a product authority of the gate or the acceptance reader. No queue row uses it; the file is
  not changed (Codex).
- **E1:** MOD-0251 (Core HR Employee master, `conditionally-approved-for-draft-reference-validation-slice`) is the only
  pack with a second approval field: dispatch stop, prepare note, accept review, draft note.
- **F3:** at candidate 4 no REP-001 v2 acceptance record exists under `docs/records/`, so no current count changes; the
  older acceptance data in earlier formats are not converted and not re-judged. The next real acceptance needs a run
  record.
- **F1, F2:** the real registry's paths are normal and its one queue is unchanged; the real queue has no -R3 or later
  retry id.
- Declared contract changes in existing tests (none removed): R0's fixture evidence is a real run record and its list
  lives under `docs/records/audits/`; `V4A_F3_Kunye`'s retry record lists `W2-R3`; the self-tests grow (ct_queue 137,
  prompt_gate 103, acceptance_record 148, known_failures 51, run_record 29 new).

## The limits kept open

- A run record binds a command, a commit, a raw exit and bytes; it is not a proof that the command measures the
  criterion. A hand-written record that is self-consistent cannot be told apart by the checker; ignored files are
  outside the clean-tree check; a change reverted before the run ends is not seen. Acceptance stays a judgement on top.
- C1 bounds declared write paths, not reading and not what an executor actually writes (the commit's diff shows that).
- E1 gives no new automatic acceptance for a conditional pack; earlier acceptances are not re-judged.

## Not in this decision

- A new general system, skill, permission or allowlist; product code; the SCE and old queues; SOP §36.2 T1–T4;
  publication (push, PR, merge); the migration of older acceptance data.
- Daily token savings, a subjective score target, or a general guarantee: the measured wrong inputs were refused.
- The live loading and Employee measurements (Codex's), recorded in I5B-FIX-06-JOINT-DECISION-01.
