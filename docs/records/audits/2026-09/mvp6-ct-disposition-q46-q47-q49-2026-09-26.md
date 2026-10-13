# CT disposition — Q46/Q47 pack applies, Q49 independent VER (2026-09-26)

Recorded by: Control Tower, 2026-09-26T09:36+0300. New record (K4). Base 4a8d4d4b; no commit/push/stash.
Flow followed: writer (Lane-2) → independent VER (Lane-3, not the writer) → this CT decision (SOP; owner decision Q49).

| Item | Evidence | CT check | Result |
|---|---|---|---|
| Q46 Returns pack (alignment → UI revision) | `mvp6-pack-apply-q46-q47-01/` | ARTIFACTS 13/13 OK (repo root); MOD-0186 = `6c8fbe28…a5a0` | **ACCEPTED** |
| Q47 self-registration patches 01,02,03,04,06 | same | DCP-009 `e346043d`, MOD-0183 `8e269efa`, MOD-0184 `346288ab`, MOD-0185 `45b5dd33`, MOD-0187 `31cb35c3` = signed after-hashes | **ACCEPTED** |
| Patch 05 rebase | `mvp6-self-registration-patch05-rebase-01/` | SHA256SUMS 3/3; CT `git apply --check` exit 0 on live MOD-0186 | Ready → Q48 sign-off |
| Q49 independent VER | `mvp6-pack-apply-independent-ver-01/SOP-22-VER.md` | ARTIFACTS verified; verdict PASS 6/6 incl. byte-exact reproduction | **ACCEPTED** (also covers Q38 Claims apply retroactively) |

## Verifier observations — CT disposition

- **O1** Claims apply checksum line `762ab533…` is superseded by signed patch 06 (`762ab533 → 31cb35c3`). Chain recorded here; no record edited.
- **O2** Four packs had pre-existing uncommitted edits before today's sign-offs; those earlier edits are outside this VER. Carried to the commit-strategy decision (Q03).
- **O3** `product-backlog.md` change at 08:59 is the Q41 backlog apply (`mvp6-backlog-entries-apply-01/`, CT disposition `mvp6-ct-disposition-q40-q41-2026-09-26.md`). Explained; closed.
- **O4** Applied sections keep the heading suffix "PATCH PROPOSAL — NOT APPROVED" (signed bytes). Correction needs its own patch and sign-off → queue Q50.

Effort: no product credit (pack text). AG-01 remains OPEN until code lands via the integration owner.
Tracked diff now 16 files (+DCP-009, +MOD-0183).
