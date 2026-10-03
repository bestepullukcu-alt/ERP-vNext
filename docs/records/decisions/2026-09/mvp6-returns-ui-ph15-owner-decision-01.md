# Owner decision — MOD-0186 Returns UI Phase 1.5 (PH15-UI-186) — 2026-09-26 (late record)

Recorded by the Q93 v2 lane (Claude Code on the Mac; single ledger writer) at 2026-09-26T21:15+03:00 on CT instruction (CT writes no files).

**This is a late record.** The owner approved PH15-UI-186 on 2026-09-26 in the CT session; the exact minute was **not recorded**.
The owner confirmed the approval at ~20:20 +03:00 (CT conversation). No earlier time is claimed here.

## Decision

The owner approved the Phase 1.5 architecture table in `docs/roadmap/plans/mvp6-decision-prep-01/PH15-UI-186.md`
(SHA-256 `08bc9d8608132ebf5c257b11625495634f4e9a90d7660b1ec9eae0c185bcd3ef`, measured 21:14) for the MOD-0186 Returns UI —
the "Exact text" in that file applies as written, with the one amendment below.

## Amendment — error-code set: 21 codes, not 22

PH15-UI-186 binds 22 codes including `SHIPMENT_ROOT_INVALID`. The **21 published codes** are accepted instead:
`SHIPMENT_ROOT_INVALID` is inbound-only (the Shipment's own code) and the Returns reader maps it to `RETURN_SHIPMENT_ROOT_INVALID`.
Source: Q91 finding F2 in `docs/records/audits/2026-09/mvp6-returns-ui-draft-01/README.md` (package SHA256SUMS
`a80047b50696c3a88a4512b1405ee0c0d9e3458ef09870d4f15c3862ef755905`), accepted by CT in
`docs/records/audits/2026-09/mvp6-ct-verdicts-q86-q87-q90-q91-q94-q96-2026-09-26.md`.

## Scope

As in PH15-UI-186: the 21 owned UI paths of pack §32.10 in an isolated environment, shared changes only as a hash-bound overlay.
**Not approved:** edits to shared files, gateway, permissions catalogue, contracts, packs or backend; `done` status; commit or push;
closure before Phase 4.5 in the integrated target.

## Effect

- Q65 (Returns UI DEV) → READY for Q65b (Mac) with the Q91 recipe.
- 0186-1 (a) becomes creditable (09a README line 82); the credit goes to queue row Q99.
