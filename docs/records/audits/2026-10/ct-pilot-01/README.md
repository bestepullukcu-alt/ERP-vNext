# Evidence — CT-PILOT-01, one synthetic lifecycle of a new Control Tower (2026-10-16)

Authority: `CT-PILOT-01-JOINT-DECISION-01` (joint decision of Codex and the AI session under
`CT-COMPLETION-DELEGATION-OWNER-DECISION-01`). Branch `chore/ct/pqg-01` on 5ecd8b3c1, local only. The ControlTower
(Legacy) session gave its single-writer agreement for the REP-001 §7.1 change before it was written.

## What was run

- **Where:** an isolated scratch clone of this repository (push URL disabled), branch `pilot-03` from 5ecd8b3c1. The six
  tools' sha256 were equal to 5ecd8b3c1 before and after the run (`summary.json`). Driver sha256
  `262bfb5927a6d8f685c333615870f03e7aab8b381138d828eb5f8212e2f2fd56`, kept outside the repository.
- **How:** only the commands and templates written in the operating card, the SOP and REP-001; each step names its
  source line at 5ecd8b3c1 (`steps.md`). The prompt is the SOP §36.1 template filled in; the acceptance record is the
  REP-001 §6.1 example filled in; the effort baseline is the template filled in.
- **The lifecycle:** registry row and strict queue from the template → discovery → a decision for each finding → quality
  gate (prepare, dispatch) → `require` and `guard` → simulated implementation → acceptance records (accepted, conditional)
  → report without a baseline → approved baseline → a baseline changed after approval → open rework and the
  previous-report difference.
- **Test imitation:** the clone's local `origin/main` was moved once by hand, from the real value to the registry-row
  commit, to stand for "the registry row enters `main` with the owner's approval". It is not real force and not
  publication; nothing was pushed.

## Results — run 3, 29 of 29 as expected

| Phase | Passing examples | Blocking examples | Result |
|---|---|---|---|
| Dependencies | 1 | 0 | as expected |
| Task source (registry, strict queue) | 1 | 1 | as expected |
| Discovery | 2 | 1 | as expected |
| Quality gate (CT-PQG-01) | 3 | 2 | as expected |
| Queue control (`require`, `guard`) | 4 | 2 | as expected |
| Acceptance record (CT-OPS-03) | 2 | 2 | as expected |
| Report (CT-OPS-04) | 2 | 2 | as expected |
| Rework (Y1, Y4) | 3 | 0 | as expected |
| Close | 1 | 0 | as expected |

Reports: without a baseline, all four fields exactly "Taban yok — hesaplanamadı". With the approved baseline: Backend
%66.7 (8/12), Frontend %33.3 (2/6), Test %66.7 (4/6), overall %56.7 by the approved weights. P-02's conditional record
is not counted. A baseline number changed after approval suppresses the percentages, with its reason. With P-01-R1 open,
P-01 is not counted (0.0 in all four); the previous report verifies and the difference is shown.

## Discovery decisions — judgement, one by one

Query "Territory Management" (MOD-0151; its pack is `ready-for-dev` and names FU08 import/export in
`runtime_code_scope`). The work is a synthetic example of FU08's scope; nothing is claimed about the real FU08.

| Finding | Decision (P-01) |
|---|---|
| blueprint: MOD-0151 Territory Management | FU — inside MOD-0151's FU08 scope; no new module |
| sor: `territories` owned by MOD-0151 | parent — the work only exports; the owner stays MOD-0151 |
| registry: MOD-0151 | parent — the queue row's id is MOD-0151; FU08 has no id of its own in the registry |
| pack: MOD-0151 | FU — this work's approved pack; no separate pack |
| queue row P-01 | yeni — the work's own row (discovery found itself) |
| queue row P-02 | yeni — another FU08 slice; scopes do not overlap |

The queue-row decisions differ for each work package (P-02 and P-01-R1 have their own; `summary.json`). A finding without a
decision stops the driver. Codex confirmed the module decisions. The pilot shows that the gates require the decisions;
it does not show that a real Control Tower's decisions are right.

## Findings

| # | Kind | Finding | Status |
|---|---|---|---|
| 1 | documentation | REP-001 §7.1 stated the rule of `ledger_content_sha256` but gave no command to compute it | fixed in this commit: a copyable command in §7.1; copied from the document as written, it gives the tool's own result in 8 of 8 cases (`hash-command-check.txt`) |

Observations, not defects:
- Discovery lists the work's own `DISCOVERED` row and its sibling rows as findings, so each work package needs a decision
  for each of them.
- The first line of the §36.1 template offers an alternative after `# veya:`; the operator keeps one entry point. The
  pilot did so; the gate was not run on the line as printed.

Fixture runs, not tool findings (`fixture-runs.txt`): run 1 stopped on its own guard (a queue-row finding had no
decision); run 2 had four fixture errors — the level line was 7, not 6; log, list and record names `p01` and `p-01`; a
real API's `{id}` counted as a template placeholder; the invalid-baseline example missing. All four were fixed in the
driver before run 3.

## Files

| File | What it is |
|---|---|
| `steps.md` | Run 3, one row per step: phase, kind, source line, command, expected, actual, result |
| `raw.txt` | Run 3's full outputs, local paths replaced (`<klon>`, `__SCRATCH__`, `__HOME__`) |
| `summary.json` | Run 3: commits, the test imitation, tool hashes, discovery findings and decisions, findings |
| `fixture-runs.txt` | Runs 1 and 2 |
| `hash-command-check.txt` | The REP-001 §7.1 command, copied from the document, against the tool's function: 8 cases |
| `independent-review-codex.json` · `summary-codex.json` | Codex's independent re-run in its own clone and its direct checks, copied unchanged |

## Limits — NOT VERIFIED or not machine-checkable

- Synthetic data; no product code was written or run. Product quality and token savings are not measured.
- The real force of the registry row and publication are not verified (test imitation, local only).
- Not exercised: card §5 VERIFY (TRX and `known_failures.py`; no architecture run, no new code); a HIGH prompt's
  review record (only the blocking HIGH example); Y2 and Y3 (-R3 decision); BLOCKED, DEFERRED; cross-module work.
- Codex re-ran the same reviewed driver in its own clone and added nine direct checks; no separately written driver.
