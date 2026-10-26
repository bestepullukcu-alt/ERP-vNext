---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E3
decision: conditional
commit:
---

# `scripts/status_report.py` cannot see where lane records live — a one-glob defect under the "rule that cannot be obeyed"

    proposal  : mvp6-status-report-glob-fix-01
    author    : CT (gmg-10)
    measured  : 2026-10-17, on the working tree at HEAD (37 commits ahead of origin/main)
    scope     : scripts/status_report.py — the REP-001 §7 instrument that is supposed to PRODUCE §2's
                mandatory status table. Not applied here: scripts/ is a protected path (Edit/Write denied),
                so this is handed over as a patch the owner applies, exactly as the CI row
                (mvp6-supplychain-ci-row-01) was.
    verdict   : DEFECT, with a correct fix. The script's --records glob reaches only two path levels in the
                working tree, so every lane record (three levels) is invisible; and its two read modes
                (working tree vs --ref) use different glob engines that silently disagree.

## The defect

`docs/roadmap/plans/mvp6-development-plan-v9.6.md` §6 names this as the consequential discipline finding:
"§2's table has never been producible by its own instrument." Measured here, the mechanism is two bugs in
one function:

1. **Depth.** `--records` defaults to `docs/records/audits/*/*.md`. In working-tree mode the script calls
   `Path.glob`, whose `*` does **not** cross `/`, so `*/*.md` matches exactly `audits/<month>/<file>.md` —
   **309 files**. But lane records live one level deeper, at `audits/<month>/<record-dir>/<file>.md` —
   **768 files** at three levels, plus 33 at four. They are simply never read.

2. **Two engines disagree.** In `--ref` mode the script instead uses `fnmatch`, whose `*` **does** cross
   `/`. So the very same pattern that under-reaches in the working tree *over*-reaches against a git ref
   (it would also match `execution/domains/a/b/module-packs/x.md` for the packs glob). The two modes are
   not comparable, which defeats the point of a `--ref` run.

Net effect: against the working tree the records section is empty for every module, so the table is all
`kayıt yok`, so every status report in this programme — CT's included — has been hand-written, which
REP-001 §2's first line forbids.

## Evidence (this tree, HEAD, supply-chain packs)

One record in the whole repository carries the REP-001 §6 künye today:
`docs/records/audits/2026-10/mvp6-gateway-idempotency-replay-01/REPORT.md`
(`module: MOD-0183`, `evidence_level: E4`, `decision: conditional`, `commit: 7b168c4fa`). It sits at three
levels, so the current glob cannot see it.

| | MOD-0183 row | footer |
|---|---|---|
| **before** (`before-working-tree.txt`) | `… \| kayıt yok \| kayıt yok \| kayıt yok \|` | `CT karar kaydı olan 0` |
| **after** (`after-working-tree.txt`) | `… \| E4 \| …REPORT.md@7b168c4fa \| conditional \|` | `CT karar kaydı olan 1` |

The other 13 modules still read `kayıt yok` **after** the fix — correctly. Those records genuinely do not
carry the künye (see Defect 2 below); the script now reports the one that does and stays silent on the
rest, instead of being blind to all of them.

The fix also makes `--ref` honest. Run against `origin/feature/mvp6-logistics` the MOD-0183 row is
`kayıt yok` and the count is 0 — because that record is **unpushed**. That is REP-001 §3 working as
intended (a local-only record is E0 / not evidence); the old script showed 0 everywhere regardless of
depth, which hid the distinction rather than making it.

## The fix

`status_report-glob-fix.patch` — 29 insertions, 3 deletions, one file. `git apply --check` passes clean
against the current `scripts/status_report.py`. Three changes:

- a single `glob_to_regex()` helper gives **both** read modes one set of semantics (`*`/`?` do not cross
  `/`, `**` matches any run of directories), replacing `fnmatch` in the `--ref` branch;
- the `--records` default becomes `docs/records/audits/**/*.md`, which reaches the depth lane records
  actually live at (1131 files, from 309);
- the now-unused `import fnmatch` is dropped.

It invents nothing. It only lets the instrument read the files it was always meant to read, and makes a
ref-run comparable to a tree-run. No module pack, no record, and no other script is touched.

### To apply (owner, in a registered checkout)

```
git apply docs/roadmap/plans/mvp6-status-report-glob-fix-01/status_report-glob-fix.patch
chmod 755 scripts/status_report.py   # unchanged; the patch carries no mode change
python3 scripts/status_report.py --packs "execution/domains/supply-chain-execution/module-packs/*.md"
```

## What this does NOT fix — Defect 2, the künye absence (owner's call)

Making the glob correct does not make the table full. `collect_decisions` counts only records carrying the
REP-001 §6 front matter, and **1130 of 1131 records do not carry it**. Retrofitting it collides with K4
(records are never corrected) — written up, with a `KUNYE.yaml` side-car proposal, in
`docs/records/audits/2026-10/ct-rule-collision-rep001-k4-01/`. That remains an owner decision and is out
of scope here. This patch is the half that is a plain defect with a correct answer; the künye half is a
rule collision that needs a ruling.

## Files

- `status_report-glob-fix.patch` — the change, `git apply`-ready from repo root.
- `before-working-tree.txt` / `after-working-tree.txt` — the full table, current script vs patched.
