# MVP6 Loads guard-binding preparation (Q26) — documents only, NOT APPROVED

Prepared 2026-09-26, 00:20–00:33 +03:00 (Europe/Istanbul), by the MVP6 guard-binding preparation lane. Baseline `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`. Nothing in the repository other than this folder was written. `docs-path-authority.json`, the test, `.antigravity`, contracts and decision records are unchanged. No build or test was run.

## Result in one paragraph

Publishing the 3.1.0 YAML changes exactly one guard input: the canonical-target pin for `shipment-bundle.openapi.yaml` (`5dfe7c1b…d21c` → `6dc1dd48…96aa2`). The new annex must **not** become a target, because no active tool uses it and the reader would reject it as unused. No sealed input changes. The emulated guard is **already red** on today's tree: 19 hits in 8 Loads amendment evidence files from 24 Sep. At the owner's direction (scope question, this session), the candidate therefore also seals those 8 files as historical evidence. Candidate payload SHA-256: **`a77538b4784e1749b7ebe2e7d38d2e8400803c63ec8e6f49d3382929d7d611e0`**. In emulation, candidate + publication passes, and every partial combination fails. Publication and binding must land together.

## Files

| File | Purpose |
|---|---|
| `ANALYSIS.md` | What the guard checks (with test line numbers), what changes, the pre-existing red and emulation results |
| `PAYLOAD-HASH.md` | Payload algorithm, calibration against the current approved payload, candidate computation |
| `OWNER-DECISION-TEXT.md` | Copyable owner decision MVP6-LOADS-PUBLICATION-GUARD-01 — NOT APPROVED |
| `EXECUTION-ORDER.md` | Single-writer combined step, .NET 8 runs, independent VER, failure/rollback rules |
| `docs-path-authority.candidate.json.txt` | Full candidate authority (UNAPPROVED, decision null) |
| `owner-decision.candidate.json.txt` | Candidate decision record (UNAPPROVED, PENDING-OWNER) |
| `PROVENANCE.md.txt` | Exact bytes of the provenance file to be written at `docs/records/audits/2026-09/mvp6-loads-publication-guard-01/PROVENANCE.md` |
| `authority-payload.txt` | Exact payload bytes (hashes to the payload SHA-256) |
| `authority-candidate.diff.txt` | Unified diff of the current authority vs the candidate |
| `guard-emulator.py.txt`, `build-candidate.py.txt`, `run-*-emulation.py.txt` | Python emulation and build scripts (stored as .txt so no guard scans them) |
| `EMULATION-CURRENT.txt`, `EMULATION-RESULTS.txt` | Raw emulation output |
| `SHA256SUMS` | Hashes of every file above (repo-root-relative) |

## Open for CT / owner

1. The owner decides MVP6-LOADS-PUBLICATION-GUARD-01 (text in OWNER-DECISION-TEXT.md), recorded under `docs/records/decisions/2026-09/`.
2. CT issues a publication DISPATCH v1.1 that follows EXECUTION-ORDER.md (v1.0's evidence `raw/` and separate sequencing would turn the guard red).
