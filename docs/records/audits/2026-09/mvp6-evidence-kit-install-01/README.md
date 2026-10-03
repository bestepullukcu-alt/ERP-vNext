# MVP6-WP-EVIDENCE-KIT-INSTALL-01 (Q24a) — kit v1.2 **installed, not validated — Q24b pending**

- Lane AL-MVP6-KIT-INSTALL-01 (chat lane, device bridge to the linked Mac folder). Repo `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched).
- Start 2026-09-26T13:43:29+03:00; writer done 2026-09-26T13:45+03:00 (Europe/Istanbul).
- Authority: [AUTHORITY.md](AUTHORITY.md).

## Result

| Step | Result |
|---|---|
| Proposal-03 `SHA256SUMS` (`e8bec4d7…caad`) | 33/33 OK |
| ADOPTION-DECISION-v1.2.md §3 hashes | 28/28 OK |
| Step A: V1 decision record written; V1 quote byte-identical to §2 | PASS |
| Step A: ledger edits | as listed in [LEDGER-EDITS.md](LEDGER-EDITS.md) |
| Step B: install per PLACEMENT.tsv | 27/27 files `match` ([INSTALL.tsv](INSTALL.tsv)); no target existed beforehand; kit code not edited |
| Step B: A1 §5 guide line | inserted once ([GUIDE-EDIT.txt](GUIDE-EDIT.txt)) |
| Validation (K00–K11, final seal) | **NOT RUN** — Q24b, local Mac |

The kit is **not yet required** for runtime lanes (A1 and the ~13:43 split): it becomes required only after Q24b passes and CT reviews it.

## ASSUMPTIONs

1. **Missing queue rows:** CT-QUEUE had no rows for Q59, Q60, Q61 and Q63, whose states the prompt sets. They were appended in the existing layout, with item text and record taken from their CT records (`mvp6-ct-verdicts-q59-srd4-2026-09-26.md`, `mvp6-decision-prep-01/`, `mvp6-shipment-iso-final-ver-lnx-01/`, `mvp6-ct-verdict-q63-kit-v2-2026-09-26.md`). Owner/depends columns for appended rows are this lane's best reading, not CT text.
2. **28 vs 27:** §3 binds 28 hashes; `proposed/PLACEMENT.tsv` itself has no install path, so 27 files are installed. PLACEMENT.tsv's hash was verified in place.
3. **File modes:** mode 755/644 was requested per PLACEMENT.tsv, but the device-bridge mount strips group/other bits, so the files show 700/600 here. The owner execute bit is correct. Q24b should confirm the modes on the Mac filesystem and set 755/644 if needed; content hashes are unaffected.
4. **Approximate times:** owner-decision times given as "~" in the prompt keep a "~" prefix in MILESTONE-EVENTS.
5. **Overlap with Q64 Step A:** the Q64 prompt (Mac) also contains ledger Step A edits (Q59/Q60/Q61/Q63/Q09 states; append Q62/Q64/Q65). Those rows now exist. The Q64 session should skip them rather than duplicate them (CT to adjust Q64).

## Repository state

Uncommitted; validation pending (Q24b). No git writes; no `.git/index.lock`.
