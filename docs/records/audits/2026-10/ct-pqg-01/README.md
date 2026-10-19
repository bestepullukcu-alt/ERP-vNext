# Evidence — CT-PQG-01, Prompt Quality Gate (2026-10-15)

Authority: the ten owner decisions in `CT-OPERATING-MODEL-OWNER-DECISIONS-01` §3, and
`CT-COMPLETION-DELEGATION-OWNER-DECISION-01` (finishing the remaining local work by joint decision of Codex and the AI
session; the owner's words are relayed by Codex). Branch `chore/ct/pqg-01` on 2481004b1, local only. The ControlTower
(Legacy) session gave a single-writer agreement for the SOP and the operating card before they were written; it notes
that it did not verify the relayed owner authority, and that its agreement does not replace it.

## Files

| File | What it is |
|---|---|
| `checks.txt` | Outputs of the checks below, as run on the final files. Local paths are replaced by `__SCRATCH__` / `__HOME__` |
| `run.sanitized.trx` | The architecture suite with all CT-PQG-01 files applied, sanitised (step-1 tool and rules) |
| `dependency-check-codex.txt` | Codex's clean-venv install of `scripts/requirements-tools.txt` from PyPI, copied unchanged; verified read-only by the AI session (`checks.txt`) |

## What was built

| Item | Where |
|---|---|
| The mechanical gate: `check` (exit 0 / 1 ENGEL / 2 tamamlanamadı), `pack` (the source pack and its digest), `--self-test` | `scripts/prompt_gate.py` |
| The gate's rules, the §9.1 signal names, the HIGH review record format, the `neden:` convention for `-R` rows | `docs/guides/operations/control-tower-sop.md` new §20.2 |
| The new prompt fields: Risk, §9.1 sinyalleri, Görev sınıfı, İzinli / Korunan yollar, Sözleşme yüzeyleri, Keşif | SOP §36.1 template |
| The gate in the opening checklist and the dispatch preflight | operating card v1.7 §0, §3 |
| The joint-decision authority, with its provenance | `docs/records/decisions/2026-10/ct-completion-delegation-owner-decision-01.md` |

## The ten decisions, and how each is met

| # | Decision (CT-OPERATING-MODEL-OWNER-DECISIONS-01 §3) | Met by |
|---|---|---|
| 1 | A quality gate before a prompt is sent | SOP §20.2; card §0 and §3. Mandatory for new (strict-queue) Control Towers |
| 2 | Pack, API, permissions, tests, sabotage verified against their real sources; not reinterpreted | `«text» (path:line)` quotes checked verbatim at Expected HEAD; text without a quote is refused; the pack's status and module are checked |
| 3 | A cheap mechanical check on every implementation prompt | `prompt_gate.py check` (seconds, read-only) |
| 4 | HIGH prompts reviewed independently before sending | a committed review record is required for HIGH; author ≠ reviewer |
| 5 | Judged by open critical / blocking findings and source match, not a score | the gate recounts open findings from the record's lines; any open critical or blocking finding blocks |
| 6 | After a critical fix the whole contract surface is re-verified | the review is bound to the prompt's sha256 and the source pack digest; any change makes it stale; SOP §20.2 requires the whole surface |
| 7 | A bounded but verifiable source pack for the reviewer | `prompt_gate.py pack` |
| 8 | The mechanical check does not replace the review | stated in SOP §20.2; HIGH needs both |
| 9 | Measure prompt-caused rework and contradictions | the `neden: prompt\|kod\|ortam\|kapsam` convention for `-R` rows; **counting is CT-OPS-04, not done here** |
| 10 | Into the repository's Control Tower process | SOP §20.2, §36.1, card |

## Choices agreed with Codex

- **Dispatch and preparation:** the default command is the dispatch check: the queue must be registered in the local
  `origin/main` (SHA printed). `--prepare` checks preparation only and says "DAĞITIM İZNİ DEĞİL". The chain is not on
  `main`, so only preparation and the pilot are possible now.
- **Hash cycle:** the HIGH review is a separate committed record given with `--review`. The prompt stays byte-identical
  to what was reviewed; the record names the prompt's sha256 and the source pack digest.
- **Producer identity of a discovery report:** the tool's sha256 in the report must be that of
  `verify_module_id.py` at the commit the report names (not today's HEAD); an unreadable commit is "tamamlanamadı". Later
  unrelated commits do not stale a valid report or review.
- **Dirty files:** only those touching the allowed paths, the protected paths, a source file or an "Önce oku" pattern
  block; the rest is an inventory.
- **Task class:** `ürün` needs a ready-for-dev or approved pack of the row's module; `yönetişim` needs an approved scope
  record under `docs/records/decisions/` that names the WP or is the queue row's record. No pack is invented.

## Checks and results

| Check | Result |
|---|---|
| `prompt_gate.py --self-test` | 70/70 |
| Codex's first review: three false passes (scope record not checked for approval or binding; "Önce oku" patterns outside the source pack; a hash-valid but malformed discovery report) | tests written first: red (51/62); after the fix green (63/63) |
| Codex's second review: three more (a new untracked file under a pattern; an empty or repeated `NE:`; a prompt for another repository checked with this repository's queue) | tests written first: red (66/70); after the fix green (70/70) |
| The AI session's own finds | a pack that belongs to another module; a report saying "no match" that carries a BULGU; an "inceleme gerekli" report without a BULGU; one guard that had no test of its own until then |
| Sabotage: each of 25 guards removed in turn | every removal turns at least one named test red |
| End to end, real git, scratch clone (13 cases) | as expected: see `checks.txt`, including a dispatch pass with the registry on `origin/main`, a stale review, an old report made by an uncommitted tool copy, and the same prompt refused by another repository's gate |
| Retro dry run on WP-SCMM-05-S1's prompt (accepted on 2026-09-07; information only) | as written: format ENGELs (the new fields are missing). After a format-only conversion: 12 ENGELs — a Repository path from another machine, a Worktree that is not a path, two shortened source paths, contract surfaces missing or paraphrased, no quote in a HIGH prompt, no approved pack, no queue row, no review record |
| `ct_queue.py --self-test` · `validate` | 57/57 · OK (unchanged) |
| SOP §36.2 to the end of the file | byte-identical before and after |
| Architecture suite (one run, all files pinned) | 131/138; the 7 failures are the 7 registered known failures (`known_failures.py check` 7 BİLİNEN, exit 0, raw and sanitised); fingerprints identical to step 2's last run |

## Limits — NOT VERIFIED or not machine-checkable

- **A review's quality.** The gate checks the record mechanically; it cannot judge the review. Author and reviewer
  identities are declared, not authenticated.
- **Dispatch mode.** It is effective only once the registry and the gate are on `main`. Until then `--prepare` only.
- **Discovery after the queue row exists.** Discovery then finds the work's own queue row as a BULGU; the Control Tower
  records a decision for it ("yeni — this is the work package's own row"), or runs discovery before adding the row.
- **§9.1 from paths.** Only migrations, the gateway and authorization paths are recognised; other signals rely on the
  author and the review.
- **Pattern semantics.** For the dirty check `dir/*` also covers nested paths (conservative).
- **Item 9.** Counting prompt-caused rework is CT-OPS-04.
- **CI and remote `main`.** Not run / not read; no fetch was made.
- **Dependency.** Installing `scripts/requirements-tools.txt` from PyPI is now verified on this machine (Codex,
  2026-10-15; checked read-only by the AI session). Step 2's evidence keeps its historical "NOT VERIFIED" line
  unchanged. The successful retry's install log was not kept; other operating systems and CI are not claimed.
