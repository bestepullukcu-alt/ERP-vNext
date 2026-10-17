# Evidence — İ4 S2, a new pack's acceptance criteria traced to acceptance records (2026-10-16)

Authority: `CT-I4-S2-JOINT-DECISION-01` (joint decision of Codex and the AI session; the owner left the choice and the
local implementation to them — the owner's answer was relayed by Codex). Implements `CT-I4-AC-TRACEABILITY-PROPOSED-01`.
Branch `chore/ct/pqg-01` on 96b500788, local only. The ControlTower (Legacy) session's line-level single-writer
agreement for the four shared documents was relayed by Codex (its answer did not reach the AI session); the AI session
told Legacy before writing.

## What changed — six source files

| File | Change |
|---|---|
| `scripts/acceptance_record.py` | the optional künye field `module_pack` (well formed when present, once; its absence breaks nothing); `parse_pack()` — one scanner: a closed künye, fenced code tracked by its mark and length, headings only outside fences, one exact `## Acceptance Criteria`, every item `AC-<n> (zorunlu\|isteğe bağlı) — <criterion>`, at least one mandatory; `module_coverage()`; `run_module_check()` and the `module-check` command; the self-test |
| `scripts/status_report_v2.py` | two pure helpers moved out, behaviour unchanged: `current_revision()` (from `count_works`) and `read_strict_queue()` (from `build`) — shared with `module-check` |
| `.antigravity/rules/module-pack-standard.md` | §7: the AC form for the new packs of new Control Towers (no new load on older ones); the old packs are not converted; the standard's example pack shows the form |
| `.antigravity/workflows/prepare-module-pack.md` | item 16: one reference to §7, for the new packs of new Control Towers |
| `.antigravity/rules/status-reporting-and-evidence.md` | REP-001 §6.1: the optional `module_pack`; §6.2: `AC-<n>` as an `ID`, the command and what its exits mean |
| `docs/guides/operations/control-tower-sop.md` | §29.2: one paragraph — the first condition is measured by `module-check` for new Control Towers; exit 0 is not Module Done. SOP from §36.2 onward unchanged |

`module-check`: exit 0 means only that every mandatory `AC-<n>` of the named, approved pack version (`path@40-hex`) is
`geçti` or a reasoned `N/A` in an authoritative, accepted record of the module whose current revision is ACCEPTED and
whose künye binds exactly that version. **It is never Module Done.** 1 = a mandatory criterion open; 2 = not measurable.

## Checks and results

| Check | Result |
|---|---|
| `acceptance_record.py --self-test` | 100/100 |
| `status_report_v2.py` after the two helpers moved out | its self-test output byte-identical (75/75); CT-OPS-04's real-git end to end 18/18 |
| Other self-tests on the final files | ct_queue 69/69 · prompt_gate 71/71 · hook 21/21 · known_failures 24/24; `ct_queue validate` OK |
| Tests first | the new cases red against stubs (the five `module_pack` cases tightened to fail for their real reason, not as an unknown field); Codex's boundary cases red before the scanner; the empty-criteria cases red before the guard; the standard's example drift test red on HEAD's example |
| Sabotage | 23 guards of `acceptance_record.py` and 5 moved guards of `status_report_v2.py` removed in turn: each turns a named test red; the 4 wires of `run_module_check` removed in turn in the real-git end to end: each turns its expectation red |
| Real-git end to end (driver `e2e_mc.py.txt`, sha256 `afd2ecbf…`), 17 expectations | all pass: full coverage 0 (not Module Done); v1 records do not cover v2; a draft pack, an old-format pack, an old real pack, a short SHA, a non-strict queue, an unknown `--ref` (no SHA claimed), another module's pack id: 2; a record bound to another version, a record without `module_pack`, an open `-R1`, a conditional record, another module's record: not counted; an invalid queue: 2 |
| Backward compatibility of `check` | six records without `module_pack` (accepted; conditional; rejected; superseded; the superseding head; the wrong module): old and new give the same exit and output. HEAD holds no v2 record, so this comparison runs on real-git fixtures |
| The 163 module packs at 96b500788 | measured with `parse_pack`: 0 measurable — 134 without the exact `## Acceptance Criteria` heading, 25 in the old format, 4 without a künye; none changed |
| Codex, independently | 12 boundary cases, 8 `check` parity fixtures, 256 revision-chain samples, the 163 packs, the same driver replayed in its own clone (17/17) and two `--ref` checks (`independent-review-codex.json`) — the same driver, not a separately written one |
| Architecture suite (one run, the files pinned) | 131/138; the 7 failures are the 7 registered known failures (`known_failures.py check` 7 BİLİNEN, exit 0, raw and sanitised); fingerprints as in the 6188349c8 run |

## Files

| File | What it is |
|---|---|
| `checks.txt` | The outputs above; local paths replaced (`__SCRATCH__`, `__HOME__`, `__TMP__`) |
| `e2e_mc.py.txt` | The real-git end-to-end driver |
| `run.sanitized.trx` | The architecture suite with the change, sanitised |
| `independent-review-codex.json` | Codex's independent, path-free summary, copied unchanged |

## Limits — NOT VERIFIED or not machine-checkable

- No new Control Tower uses `module-check` yet; the fixtures are synthetic.
- Exit 0 checks AC coverage only; the other conditions of SOP §29.2 are not checked.
- Whether "Teslimatlar" and "the delivery check" in the İ4 list meant this is NOT VERIFIED; the record-code lock stays
  open.
- `.antigravity` changes need the owner's confirmation before any merge; publication is the owner's decision.
