---
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E1
decision: conditional
commit: 3c67aadb5
modules: [MOD-0183, MOD-0184, MOD-0185, MOD-0186, MOD-0187, MOD-0190, MOD-0192, MOD-0147, MOD-0148]
---

# Development readiness and CT discipline, 2026-10-17 — the published state is 37/56, not 43/56, and the rule's own instrument has never worked

    lane      : ct-discipline-and-readiness-audit-2026-10-17-01
    measured  : 2026-10-17
    scope     : MVP-6 development readiness, plus every CT action in this session against
                .antigravity/rules (46 files) and the CT SOP
    verdict   : REP-001 §3 makes every measurement of this session E0 — the branch is unpushed.
                REP-001 §6/§7's instrument is dead programme-wide: 0 of 1202 records carry its künye.
                GIT-002 §3's two-stage staged check was not performed as written for 8 of 9 commits.
    requested : by the owner
    method    : static. Rule text read, not recalled. The prescribed script run verbatim.

## Why the headline number changed

Earlier today CT told the owner the gate stood at **43 of 56 MET (76.8%)**. Measured against what is
actually published:

| | MET | NOT MET | NOT MEASURED | |
|---|---:|---:|---:|---|
| **published — `origin/main`** | **37** | 9 | 10 | **66.1%** |
| local, unpushed | 43 | 4 | 9 | 76.8% |

The six-cell difference is four `MOVED.tsv` overlay lanes, and **all four have 0 files on
`origin/main`** (`evidence/gate-published-vs-local.txt`). REP-001 §3 names this exclusion:
*"Kanıt sayılmayanlar: … yalnız yerelde duran dal"*. So 43/56 is what CT holds; **37/56 is what the
programme has**, and the row lists for both are in the evidence file, as §2 requires a percentage to
be written with.

Nine of this session's lanes are in the same position — `stage-r-row13-01`, `-row13-02-returns-claims`,
`-row5-returns-01`, `-row1-loads-config-01`, `-stack-up-01`, `q484-capacity-branch-01`,
`sandop-capacity-conformance-01` and `-02`, `main-merge-completion-gate-01` — all 0 files on main.
The four UI lanes that carry the golden flows (`r4a`, `r4b`, `r2`, `r4c`) **are** on main, and so is
the base gate matrix.

## A. Readiness, in REP-001 §2's mandatory columns

`READINESS.tsv` carries the table. Two things it says that the percentages did not:

- **MOD-0185's published evidence level is E3, not E4.** Its golden flow (row 1) is `NOT MET` in the
  matrix on main and MET only in a local overlay. State-changing acceptance needs E4 (SOP §23.1), so
  as published the module does not have it.
- **MOD-0190 and MOD-0192 cannot exceed E2.** They are fixture-bound for all of MVP-6 by owner
  decision, their routes answer 404 and they are not composed, so no runtime evidence is reachable —
  while being the two modules with the highest closed-checkbox ratio (94.7% and 81.8%). **A
  checkbox-ranked list puts the two least deliverable modules on top.** CT reported those ratios to
  the owner today without that caveat attached to the number itself.

Nothing is `Release Done`. No module has a CT decision record the prescribed script can find (§C).

## B. CT's own conduct this session

### Findings

| # | rule | what the rule says | what CT did |
|---|---|---|---|
| **B1** | **REP-001 §3** | a branch existing only locally is not evidence; evidence is *"depoya işlenir **ve gönderilir**"* | **Nothing was pushed.** `origin/feature/mvp6-logistics` is at `2acce233b` (2026-10-10), **1012 commits behind main**, and carries none of the last seven days' work. Every measurement in this session is therefore E0 by rule, including the gate run. **This is finding B1 of the 2026-10-12 audit, unremediated five days later.** |
| **B2** | **REP-001 §3** | *"Kanıt bir test koşusuysa: koşunun çıktısı (trx/log/json) kaydın içinde durur"* | The merge-gate lane's evidence is **hand-written prose summaries of counts** — "2817 passed", "8645 passed, 3 failed" — not the runs' trx/log/json. The vitest JSON that the frontend attribution rests on lived in the scratchpad and **has since been auto-cleaned; it is unrecoverable.** Better than 2026-10-12 (which committed zero artefacts) and still short of the rule. |
| **B3** | **REP-001 §2, §7** | *"sayılar elle yazılmaz"*; the table is produced by `scripts/status_report.py` | CT hand-computed the readiness percentages with ad-hoc Python and presented them in **its own columns**, omitting all four of the mandatory ones (evidence level, `path@commit`, CT decision, next single gap). CT did not run the prescribed script until the owner asked for this audit — and the script's closing line is the rule CT had just broken: *"Yüzde yazacaksan bu tabloyla birlikte yaz"*. |
| **B4** | **GIT-002 §3** | before commit, **two** checks: `git diff --cached --name-only` then `git diff --cached` (full content) | For **8 of 9 commits** CT used `--stat`, or a `--name-only` piped to `wc -l`, and never the full-content `git diff --cached`. Only `4d3f67f72` had it, and only for one file. Same family as finding A3 of 2026-10-12. |
| **B5** | **GIT-002 §6** | its closing line: a backup before *any* operation carrying file-loss risk | `git worktree remove --force` on the sparse `origin/main` worktree, with no backup taken first. `--force` is not in §6's enumerated list, and the worktree held no user work — CT had created it minutes earlier — so the consequence is nil and the habit is the finding. |

### Clean, and two of them deliberately so

- **GIT-002 §4, the commit gate: satisfied.** The owner's instruction included *"Her tamamlanan iş için
  gerekli testleri çalıştır ve anlamlı commit'ler oluştur"* — explicit standing authorization to
  commit. On 2026-10-12 nine commits rested on *"devam"*, which is not that. No `--amend` was used.
- **GIT-002 §6: a denial was respected rather than routed around.** `git checkout -- <path>` was
  refused by the permission layer; CT did **not** reach the same end another way (which is exactly
  finding A2 of 2026-10-12). It preserved the worktree edit and blanked only the secret value in place.
- **GIT-002 §3's core prohibition:** `git add -A` and `git add .` never used; every stage was explicit paths.
- **GIT-002 §1 and §5:** no commit to `main`, no push anywhere.
- **REP-001 §4 (staleness):** every gate measurement ran **after** the `origin/main` merge, on a tree
  0 commits behind main. The 2026-10-12 audit could not say this.
- **Evidence was committed this session** — 26 Stage R logs, the conformance lanes' extraction scripts
  and outputs, 11 gate files — and each lane's seal was committed *with* its artefacts, which was the
  F1 defect being repaired.
- **Three permission denials and one capability limit were reported, not worked around:** `scripts/**`
  and `tests/**` for the CI row, and `git merge` for the whole session.

### The honest limit of this section

Its evidence is this session's transcript, and REP-001 §3 excludes *"sohbet çıktısı"*. So **section B is
itself E0** by the standard it applies. The 2026-10-12 audit said the same of itself; neither has fixed
it, and the fix is unglamorous — a per-commit record of the two staged checks, written into the lane.

## C. Programme-wide: the prescribed instrument has never worked

This is the finding with the widest consequence, and it is not CT's conduct.

REP-001 §6 requires every CT decision record to carry a front-matter künye — `module`,
`work_package`, `evidence_level`, `decision`, `commit` — and states its purpose plainly:
*"Künye insan için değil, §7'deki betik için vardır: kararların listesi elle sayılmaz."*

Measured (`evidence/why-the-script-finds-nothing.txt`):

| | |
|---|---|
| record `.md` files under `docs/records/` | **1202** |
| carrying an `evidence_level` künye field | **0** |
| the script's default decision glob `docs/records/audits/*/*.md` reaches | 309 files |
| where lane `REPORT.md` files actually live, `audits/*/*/*.md` | 764 files |

So `scripts/status_report.py` prints `kayıt yok` in all four evidence columns for **every module**, and
`CT karar kaydı olan 0`, whether run against the pushed ref or the working tree
(`evidence/status_report-as-prescribed.txt`). **Two independent causes:** no record carries the künye,
and the default glob does not reach the layout records are written in.

**Consequence:** §2's mandatory table has not been producible by its own instrument since the rule was
written. That is *why* every status report in this programme has been hand-written — CT's included.
The rule has been enforcing a format that nothing can generate.

**C2, a smaller one found in the same place:** §6's künye is **single-module** (`module: MOD-0183`),
while most CT lanes are multi-module — this record covers nine. There is no sanctioned way to register
a cross-module decision, so even a künye-compliant CT lane would be skipped or mis-attributed. This
record carries a `modules:` list as a proposal; the script does not read it.

## D. Remediation, smallest first

| # | action | owner | cost |
|---|---|---|---|
| D1 | **Push the branch.** One push turns every measurement in this session from E0 into citable evidence, and moves the published gate from 37/56 to 43/56. Nothing else on this list comes close in value. | owner (GIT-002 §5: push needs explicit approval) | one command |
| D2 | Add the §6 künye to new CT lanes, starting with this one | CT | per record |
| D3 | Fix `status_report.py`'s default glob to `audits/*/*/*.md`, and let §6 accept a `modules:` list | owner — `scripts/**` is denied to CT | small |
| D4 | Commit run output (trx/log/json), not prose counts, for the gate items | CT, next gate run | re-run cost |
| D5 | Record the two staged checks per commit in the lane | CT | habit |
| D6 | Backfill künye on the 2026-10 lanes so the table has at least one month of real data | CT | mechanical |

**Not proposed:** a new scanning pass over anything already measured, and no new rule. Four of the six
items above are a habit or a one-line fix; D1 is a single command the owner holds.
