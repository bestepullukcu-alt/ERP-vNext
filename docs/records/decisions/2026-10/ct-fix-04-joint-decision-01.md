---
decision_id: CT-FIX-04-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session within the authority the owner gave (Claude single repository writer, Codex independent verifier; joint local implementation, test and commit; no push, no PR update, no merge); the owner: "buna göre durma, devam et; Claude'un her zaman fikrini al; bu sefer %80'ler ve bağımsız denetim" (relayed by Codex)
decided_at_local: 2026-10-17 (scope agreed after the two v5 audits; contract notes agreed during the work)
decided_by: Codex and the AI session; the owner's words came through Codex and were relayed — the AI session did not see them directly
decision_source: the AI session's transcript, 0-based lines 25145 (the two v5 reports: assess and agree a narrow scope first), 25267 (the owner's start; the six relations), 25348 (İ5 exit rules; the AC binding through module-check, the report and the ACCEPTED check), 25388 (the record@version target), 25438 (künye comments; the evidence-source pack identity), 25650 (an evidence-source pack with a work_package; the commit-object check), 25928 (the anchors accepted as a minimum; the unclosed-comment negative; approval_status not left as a yellow note), 25930 (the file-name id boundary), 26031 (no override of a non-approved pack by a scope record), 26116 (option B agreed), 26182 (a required heading inside fenced code), 26327 (the card's pack line, the SOP cell kept readable), 26473 (the strengthened M6 test into the one candidate)
scope: CT-FIX-04 — the v5 audit findings A1–A3 and the v5 quality review's findings 1–3 on the Control Tower line, closed as relations (İ1–İ5), and the counter-examples Codex measured during the work (record version, künye comments, evidence-source pack identity, file-name id boundary, conditional approval). The quality review's finding 4 (instruction content) is İ6, fixed on the İ5-B line as İ5B-FIX-04 and recorded there.
relates_to: CT-FIX-03-JOINT-DECISION-01; CT-FIX-02-JOINT-DECISION-01; SOP §20.2 (Görev sınıfı); REP-001 §6.1–§6.2 and §7.1; R8 r3 (preparation vs in force); PR #147 (`chore/ct/pqg-01`); PR #149 (İ5-B)
recorded_by: the AI session (Claude Code) on 2026-10-17
---

# CT-FIX-04 — one function per relation: in force, WP binding, pack identity, bound list, report exit

## Why

The v5 full audit of 2026-10-17 judged the CT-FIX-03 sources (`79cd8c6f1`) **UYGUN DEĞİL** (17 / 20 criteria, evidence
coverage 19 / 20): `ct_route` skipped the comparison of a row with origin/main's profile (A1), `in_force_status` counted
a pending owner confirmation as registered (A2), and a report with `complete=false` exited 0 (A3). The v5 quality review
(subjective 68 / 100) found W1 matched inside W10, a pack file named MOD-0288 whose künye said MOD-0289 (or named its id
twice) passing the gate, a `CHECK-999` row skipped in a pack-bound acceptance, and an emptied `AGENTS-REFERENCE.md`
passing `sync_adapters --check`. The root cause: CT-FIX-03 unified how a field is read, not the relations between
objects, and the tests sampled one consumer per finding. This package fixes each relation once, in one function every
consumer calls, and tests it as a relation × consumer matrix through the real command lines.

## What was decided

| Relation | Before (`79cd8c6f1`) | Now |
|---|---|---|
| **İ1** in force (A1, A2) | `in_force_status` compared strict, vocabulary and baseline; `ct_route` checked main's row by hand and skipped it for a pending row; `ct_route` did not read the transition table | `in_force_status` also compares `owner_confirmation`: main's row not confirmed is `onaysız` (preparation, never in force, not a conflict); a branch row that differs from main's is `farklı profil` (a conflict). `ct_route` takes the row relation from the same function — a conflict gives no target in either mode — and reads the ref's transition table and SOP §7 rules through `profile_problems` |
| **İ2** WP ↔ scope record (quality 1) | a WP id anywhere in the record's text bound it; the row's `record` path was split at `@` and the version ignored | explicit only (`governance_binding`): (a) the row's `record` names the path — with `@commit`, a commit object (`^{commit}` and a prefix match: a tree, a blob or a branch named like an id is refused) holding the same bytes as at Expected HEAD (an older commit with the same bytes is fine); or (b) the künye's one `work_packages:` line lists the WP as an exact member (comma-separated ids, no empty element). A mention is not a binding; an explicit list without the WP is not overridden by the row; a broken version is not masked by the list. A künye with an HTML comment mark is not read (the shared reader; 0 of the 330 front matters in the repository had one) |
| **İ3** module ↔ pack identity (quality 2) | the gate compared the file name with the row; the acceptance reader compared the künye id only; an evidence-level source pack was matched by its file name | `pack_identity`: the künye id through the shared reader; its module identity is the id (`MOD-nnnn`, `MOD-nnnn-FUn`) or `split_id`'s base for a child id (which must be `MOD-nnnn`); the file name's MOD id ends at `-` or `.md` (`MOD-02880-…`, `MOD-0288X-…` name no module); both equal and in the row's or the record's module. Called by the gate (ürün), the acceptance reader (`read_pack` for check, draft and module-check) and the evidence-level source whenever it is a Module Pack, whatever other fields it carries |
| **İ4** bound pack ↔ list (quality 3) | `AC-<n>` rows were checked for membership and the mandatory flag; any other row was skipped | a list bound to a pack version is that version's criteria only: every row an `AC-<n>` of it with the same text and mandatory flag, at least one; a WP may deliver its own subset (module-check measures the module); a helper row (K1, CHECK-…) is refused and belongs to separate evidence. A record without `module_pack` is unchanged |
| **İ5** report exit (A3) | exit 0 whenever a report was printed | 0 = complete (no baseline offered and none at the default path, "Taban yok — hesaplanamadı", is complete); 1 = printed but incomplete, the reason in `incomplete_reasons`, on the "tamamlık: eksik" line and on stderr — a queue, registry, profile or acceptance problem, an offered baseline that is absent, an existing baseline that is invalid; 2 = not measurable (unchanged) |
| **Conditional approval** (Codex, the Employee scenario; option B) | a pack with `status: approved` dispatched whatever else its künye said | `pack_approval_limit`: `approval_status` absent or exactly `approved` → unchanged; `conditionally-approved…` → `--prepare` passes and says a new dispatch will stop; a dispatch is an ENGEL ("kapsam türetilmez") until the pack itself settles its scope and approval; any other value or an unreadable field → ENGEL in both modes. No record overrides it (a scope-record override was written and withdrawn: it would have been an indirect approval path) |
| **Documents** | — | SOP §20.2 "Görev sınıfı kuralı" (the table cell short, the rule as a list under it); REP-001 (`.antigravity/rules/status-reporting-and-evidence.md`) the `module_pack` row, the module-check text and the §7.1 exit codes; the operating card's pack authority, queue check, router and report lines |

## Known consequences (measured)

- Real registry: one queue (`sce-mvp6`, strict=no, `owner_confirmation: pending`); the local origin/main has no registry
  file, so every `ct_queue` answer on real data is unchanged — 14 real command lines, exit, stdout and stderr identical.
- Packs (144 `MOD-*` packs at the CT branch's HEAD): 139 pass `pack_identity` with their own module; 4 have no künye
  (refused before too, by the status reader); 1 (`MOD-0162-FU01C-ADDENDUM`) carries a künye id outside the grammar and
  can no longer be a product authority of the gate — no registered queue uses it; the 9 lettered-FU packs (`FU06B`,
  `FU01A` …) read their file id as the base module, consistent with `split_id`. This is not "full backward
  compatibility"; it is the stated real-data effect.
- `approval_status`: only MOD-0251 carries one (`conditionally-approved-for-draft-reference-validation-slice`); no
  registered queue has a MOD-0251 row. A new MOD-0251 work package — also a P2 follow-up — cannot be dispatched until
  the owner settles the pack's scope and approval. Running work and the packs are not changed.
- Governance records: 40 approved decision records, 0 with `work_packages:`. A new governance dispatch through the gate
  needs an explicit binding from now on (the row's `record` or the field); nothing is cancelled retroactively.
- Declared contract changes in existing tests (none removed): the ct_queue self-test (main pending → `onaysız`); the
  ct_route self-tests (main not confirmed or another task file → a conflict; no row → `kayıtlı değil`); the
  acceptance_record self-test (a pack-bound record of K rows only → ENGEL); the prompt_gate fixtures (the pack's id, the
  record's `work_packages`); R0 — the fixture's scope record gains `work_packages: W1` and its list row the pack's
  criterion text, V4A_F5's refused-acceptance report exits 1, V4B_B8's "main confirmed, branch pending" is a conflict in
  both modes and its stamp case moved to "both pending".
- The tools' module docstrings (`ct_queue.py` require / `--in-force`, `ct_route.py`) still describe the CT-FIX-03 rule;
  they were not edited so that the sources Codex hashed stay fixed. The SOP, REP-001 and the card carry the current
  text (a documentation lag, recorded).

## Not in this decision

- The 92 old packs' migration, a new general system, product code, the SCE / old queues, SOP §36.2 T1–T4, publication,
  and the optional cosmetics A-4, E-F1, B-1, C-F1, D-F1, A-5.
- The Employee scenario ("Employee modülünü yapalım." in a clean session): a system acceptance scenario, not product
  development, run and judged by Codex with its own fixed rubric; its result is recorded separately.
- Scores: acceptance is the fixed 20 criteria and zero critical false permits; a subjective score is an observation and
  no criterion is stretched for it. The independent auditors get no numeric target and bring their own inputs.
