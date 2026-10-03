# MVP6 BASE-STACK — v1

| Field | Value |
|---|---|
| Record | BASE-STACK v1 · WP Q143 · CT-QUEUE row `Q143` (line 169) · owner decision OD-R9 (`docs/records/audits/2026-09/mvp6-ct-verdicts-q138-q139-owner-recs-2026-09-27.md` line 45, sha256 `90cf50f0f1061d48e8a73ded69cae58b2048bf6edd007c6cbb4cb636a5ed30eb`) |
| Written by | LANE 4 (Cowork, Linux VM), integration-agent + read-only-auditor, 2026-09-27 (+03:00) |
| Repo | `feature/mvp6-logistics` @ `4a8d4d4b339528a88e6220fb8402e5a2c771136c`; `git status --porcelain` only; no git write |
| Versioning (K4) | This file is never edited. A changed stack is a new file `BASE-STACK-v2.md` (new folder `mvp6-base-stack-v2/`) with its own SHA256SUMS |
| Machine-readable | `LAYERS.tsv` in this folder |

## 1. The stack (order is fixed)

```text
BASE a8a236de…  →  Q117 83e6322c…  →  Q121 93bf1c07…  →  module draft (per build)
```

| # | Layer | Artifact (full sha256) | Files | Effect | Evidence folder (SHA256SUMS, checked by Q143) | Proof / VER | CT verdict record (sha256; lines) |
|---|---|---|---:|---|---|---|---|
| 0 | **BASE** | `mvp6-q103-accepted-base-01/BASE-MANIFEST.tsv` = BASE-HASH `a8a236de82a9f2b5d4c0f3560aa13894c3740c6e187cb0140f5da1b08109c661` | 14,566 | Defines the tree: L0 `git archive HEAD` `96ae026a…` + L1 BC-SOURCE `ebd5d80c…` + L2 A12-360 `7b6a0d1a…` + L3 Auth-22 `f50350b8…` (Q103 README §1) | `mvp6-q103-accepted-base-01/` (`231563ae…b518`) **14/14 OK** | — | `mvp6-ct-verdicts-q115-q64c-q103-2026-09-26.md` (`f16fe1d0…099e`; §3, lines 37–43) — CT ACCEPTED |
| 1 | **Q117** | `mvp6-q117-test-guard-fixes-01/q117-fixes-overlay.tar.gz` `83e6322c1d4dd46c80b0b4de0a2b60493883a7b2d923c7685da689de702f800d` | 11 | 10 modified + 1 added (`TestAssemblyBsonSetup.cs`) → tree 14,567 | `mvp6-q117-test-guard-fixes-01/` (`4c11829b…d14d`) **12/12 OK** | `mvp6-q119-ver-q117-01/` (`3e4b9be9…8524`) **15/15 OK**, VER PASS 9/9 | `mvp6-ct-verdicts-q120-q119-q117-q64e-2026-09-27.md` (`c6dd1ce5…2b04`; lines 11–19) — CT ACCEPTED, "stack it after BASE in all later builds" |
| 2 | **Q121** | `mvp6-q121-mongo-guard-fixes-01/q121-mongo-guard-fixes-overlay.tar.gz` `93bf1c07249c43853870cd74150a1f0813bff1d74a8661259f8e14112258ea90` | 2 | 2 modified (Platform tests) → tree 14,567 | `mvp6-q121-mongo-guard-fixes-01/` (`0208a4fc…d937`) **3/3 OK** | `mvp6-q121b-build-test-01/` (`a7832bac…aca1`) **20/20 OK**; `mvp6-q121c-supplychain-rerun-01/` (`2a86c732…57c6`) **18/18 OK** | `mvp6-ct-verdicts-q129-q101b-q121-2026-09-27.md` (`1ebacf02…5d85573`; D-3 line 48, D-5 line 50) — CT ACCEPTED; draft acceptance `mvp6-ct-verdicts-q123-q114-q121a-2026-09-27.md` (`229e326e…7964`) |
| 3 | **Module draft** | the module's own CT-accepted draft overlay, named with its full sha256 in the build prompt | — | module files only | its own folder | its own VER | its own CT verdict — **not fixed by this record** |

All folders are under `docs/records/audits/2026-09/`. Full hashes are in `LAYERS.tsv`.

### Consistency checks run by Q143 (repo only, archives extracted to `/tmp` only)

| Check | Result |
|---|---|
| sha256(`BASE-MANIFEST.tsv`) = `BASE-HASH` file = `a8a236de…` | PASS; 14,566 rows |
| Q117 archive: 11 members = the 11 `OVERLAY-MANIFEST.tsv` rows; member sha256 = `overlay_sha256` | PASS 11/11 |
| Q117 `base_sha256` = the BASE-MANIFEST row for the same path (the added file absent from BASE) | PASS 11/11 |
| Q121 archive: 2 members (0644, 0/0) = the 2 paths of its README; member sha256 = README postimages | PASS 2/2 |
| Q121 preimages = the BASE-MANIFEST rows (Q117 does not touch them) | PASS 2/2 |
| Q117 ∩ Q121 paths | 0 → the order Q117 → Q121 has no overwrite conflict |

### Composed trees on the Mac

`~/mvp6-env/base/src`, `~/mvp6-env/q117/`, `~/mvp6-env/q121b/` and every other composed tree: **NOT-VERIFIABLE-HERE (~/mvp6-env)**. The
Linux VM reaches only the repo. Earlier lanes recorded the tree checks: BASE 14,566/14,566, 0 writable (Q121b `P1`/`P5`, Q121c `V-base.txt`).

## 2. The 10 UC-01 files are NOT part of any layer

The 10 Platform test files changed in the working tree without a WP (UC-01, `docs/records/decisions/2026-09/mvp6-owner-decisions-q19-q15-q03b-q03c-q21-2026-09-27.md`,
sha256 `50a62aec0766530ce0a741d262157e334d3b17094fffb27760d4a86603f4d3b5`, CT-2) are **not in BASE, Q117 or Q121**:

- Q143 measured the 10 ` M` paths from `git status --porcelain` (not opened for content).
- 0 of them are in the Q117 or Q121 archive.
- BASE carries its own versions of all 10 (from L0 `git archive HEAD`). The working-tree copies differ from those BASE rows in 10/10 cases.
- **Never copy them from the working tree into a stacked tree.** Their disposition is Q152.

The same holds for every other working-tree edit: the stack is defined by the manifests and archives above, not by the checkout.

## 3. Compose recipe (cp only, fail-closed, never deletes)

Run on the Mac in a new, empty folder under `~/mvp6-env/<lane>/`. Never in the repo, and never inside an existing tree.

1. **Verify before use.** For each layer: `sha256sum -c SHA256SUMS` in its evidence folder, then check the archive's sha256 = the value in §1.
   BASE: sha256(`BASE-MANIFEST.tsv`) = `a8a236de…`, and the source tree matches the manifest 14,566/14,566 with 0 extra. Any mismatch → **stop**.
2. **BASE.** `cp -Rc` (APFS clone), or `cp -Rp`, of the verified BASE tree into `<lane>/src`. Never modify the BASE tree itself (it stays read-only).
3. **Each overlay, in order Q117 → Q121 → module draft:**
   - extract the archive into a new empty staging folder `<lane>/stage/<layer>/`;
   - check that the member list equals the layer's declared list (Q117 `OVERLAY-MANIFEST.tsv`; Q121 README) and every member's sha256 matches;
   - skip AppleDouble `._*` members and report them;
   - copy with `cp -p` from stage into `src/` (overwrite or add);
   - never `rm`, never `--delete`, never `rsync --delete`, never `git apply` in the tree.
   - Any unexpected member, missing member or hash mismatch → **stop** and keep everything as it is.
4. **Verify after.**
   - Total file count: BASE + Q117 + Q121 = 14,567, plus the module draft's added files.
   - Every overlay path in `src/` has the overlay's sha256.
   - Every other path still has its BASE-MANIFEST sha256.
   - Record the result in the lane's evidence folder.
5. **Clean-up.** Staging folders and failed attempts are kept (no deletion), as in Q103, which kept `attempts/`. Build in a clone of `src/`, never in `src/` itself.

**Known exposure:** BASE still holds the old F02 value in the 3 Loads probe `.py` files. Q117 replaces those files. A tree without Q117 must be treated as exposed (CT record Q117, line 23).

## 4. How to cite this stack

A prompt or record that builds on the stack writes:

```text
Base stack: BASE-STACK v1 — docs/records/audits/2026-09/mvp6-base-stack-v1/BASE-STACK-v1.md sha256 <sha256 of this file, from SHA256SUMS>
            (BASE a8a236de → Q117 83e6322c → Q121 93bf1c07 → <module draft name + full sha256>)
```

- The lane checks `SHA256SUMS` in this folder and the quoted hash before composing.
- A build that deviates writes the deviation in its report. An example is Q129: BASE → Q117 → v4 without Q121, accepted under D-3 because Q121 touches only Platform test files.
- A later stack is cited only by its own new version file.
