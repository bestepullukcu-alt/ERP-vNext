# Evidence — step 2, new Control Tower starter kit and discovery (2026-10-15)

Owner-approved step 2 of the Control Tower work order (CT-STEP2-STARTER-KIT-OWNER-DECISION-01). Branch
`chore/ct/step2-starter-kit-01` on top of 0b392cb9e, local only. The ControlTower (Legacy) session gave a single-writer
agreement for the operating card and `.antigravity/scripts/verify_module_id.py` before they were written; that is not
an owner approval.

## Files

| File | What it is |
|---|---|
| `checks.txt` | Outputs of the checks below, as run on the final files. Local paths are replaced by `__SCRATCH__` / `__HOME__` |
| `verify-module-id-noregression.tsv` | Every active MOD id in the module registry: `--check-id` exit codes and output equality, `verify_module_id.py` of `origin/main` (3912d568b) against this package's; also `--check-all` and three `--candidate` cases |
| `run.sanitized.trx` | The architecture suite with the step-2 changes applied, sanitised |

The raw TRX is kept outside the repository, on the machine that ran it. Sanitisation: the step-1 tool and rules
(`../ct-pre-merge-fixes-01/README.md`, `sanitize_trx.py.txt`, sha256 `b015a9b4…`).

## What was built

| Item | Where |
|---|---|
| Opening checklist for a new Control Tower, discovery line format, report hash rule, "inceleme gerekli" rule, rework rules Y1–Y4, CT-PQG-01 timing | `docs/guides/operations/control-tower-operating-card.md` v1.6 §0 |
| Task source template | `execution/delivery/ct-queue-template.tsv` |
| `sop7` vocabulary: the 19 SOP §7 states (13 main + 6 exceptions); READY is the only dispatchable state | `execution/delivery/ct-queue-states.tsv` (19 rows added; the SCE rows unchanged) |
| Allowed transitions: 58 rows, one arrow each — 30 stated by SOP §7 / §19 (`sop`), 28 proposed (`öneri`); terminal states ACCEPTED, REWORK, FAILED, SUPERSEDED, CANCELLED | `execution/delivery/ct-sop7-transitions.tsv` |
| Vocabulary chosen per queue in the registry | `execution/delivery/ct-queue-registry.tsv`, new `vocabulary` column |
| Strict-queue checks: `module` column (MOD id, FU, `ortak:`; deprecated aliases and unknown ids refused; fail-closed without the Blueprint), exits from terminal states across the queue's git history, `-R<n>` linked to an existing original through `depends_on`, a `docs/records/decisions/` record for `-R3` and later (SOP §28.2) | `scripts/ct_queue.py` |
| `--discover` (Blueprint_Data, SoR_Map, module registry, module packs on every local ref, gateway resource → service, registered task sources) and `--check-report` | `.antigravity/scripts/verify_module_id.py`; the other modes unchanged |
| Tool dependency | `scripts/requirements-tools.txt` (`openpyxl==3.1.5`) |

## Checks and results

| Check | Result |
|---|---|
| `verify_module_id.py` existing modes, old against new | 172 active ids, 344 `--check-id` comparisons, `--check-all`, 3 `--candidate`: 0 differences. CLI: 5 commands, same output and exit code. Only the usage text differs (the four new options) |
| Discovery: "Position & Organization Assignment" + `position-assignments` | Exit 3, "inceleme gerekli": MOD-0288 and MOD-0299 (Blueprint and SoR_Map), MOD-0288 and MOD-0288-FU01 (registry), three packs (one only on `origin/hr-future`), the gateway resource on ports 5057 and 5063. The deprecated MOD-0299 alias is information only |
| Discovery: an unknown name | Exit 0, "incelenen kaynaklarda eşleşme bulunamadı", with the sources and the gaps |
| `--check-report` | Valid 0 · one line changed 2 · hash line removed 2 |
| Fail-closed | openpyxl missing, not a git repository, unreadable workbook, missing columns, malformed gateway file, no routes: all "tamamlanamadı" (exit 2 or `Incomplete`), never "bulunamadı" |
| Dependency file, clean venv, offline | Without openpyxl: exit 2 with the install command. Offline install: no distribution. **Installing from PyPI: NOT VERIFIED** |
| `ct_queue.py --self-test` | 46/46. With the new `-R3` path guard removed: 45/46, the named case fails |
| `ct_queue.py validate` | OK, 0 errors (the 62 legacy duplicate ids in SCE stay a warning) |
| A queue made from the template | 0 errors; the header equals the agreed columns |
| SCE / MVP6 | 53 refs, 26,528 id comparisons against the step-1 tool: 0 differences; `CT-QUEUE.tsv` unchanged |
| Card links | 11 relative links, 0 broken |
| Decision record quotes | Asserted byte-exact against the transcript by the renderer |
| Architecture suite | 131/138; the 7 failures are the 7 registered known failures: `known_failures.py check` 7 BİLİNEN, exit 0, raw and sanitised; fingerprints identical to step-1 run 3. Code pinned before the run, unchanged after. Raw sha256 `4e3dc37c…`, sanitised `323e6f2e…` (full values in `checks.txt`); R1 306 · R2 145 · R3 1 · R4 1, nothing left |

## NOT VERIFIED

- **Installing `scripts/requirements-tools.txt` from PyPI** — needs a download; none was made.
- **CI fingerprints** — as in step 1, equal fingerprints between a local run and CI are not verified.
- **Remote main** — only the local ref `origin/main` (3912d568b) was read; no fetch was made by this work.
- **What discovery cannot see** — branches that are not on this machine, unpushed work elsewhere, other machines. The
  report says so in its `EKSİK` lines; exit 0 does not prove that the work is new.
- **CT-PQG-01** — not started. Until it is ready, a new Control Tower cannot dispatch its first development work.
