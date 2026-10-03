# MVP6-PACK-ALIGNMENT-01 — shared pack alignment deltas (CT queue Q17, CT audit F-03)

**PREPARED PROPOSAL — NOT APPROVED.** Documents only. No pack, contract, product code, `.antigravity`, gateway or existing record was changed. No commit, push or stash.

- Lane: Agent Lane-2 chat, module-pack alignment preparation.
- Repo / branch / HEAD: `/Users/natig/Projects/ERP-vNext-recovery`, `feature/mvp6-logistics`, `4a8d4d4b339528a88e6220fb8402e5a2c771136c` (matched at start and end). The working tree was already dirty (349 status rows at start). Other lanes wrote during this task; they are not attributed to this lane.
- Start: 2026-09-25T23:46:57+03:00 (Europe/Istanbul). End: see `SHA256SUMS` time in the final report.
- Pattern: `docs/roadmap/plans/mvp6-final-pack-delta-01/` (Returns). This package adds a byte-exact, `git apply`-checked unified patch and a hash-bound owned-path table.

## Result per module

| Module | Existing delta | Current? | Action in this package |
|---|---|---|---|
| MOD-0187 Claims | `mod-0187-final-pack-delta-01/` (20 Sep) | **Intact but STALE** (see below) | Verified and reported only; **not duplicated**, as instructed |
| MOD-0190 S&OP | `mvp6-mod0190-pack-phase15-close-01/` (22 Sep) | No; pre-approval draft delta (`status: draft`, target `04f2e36f…`) | New delta in [mod-0190-sop/](mod-0190-sop/) |
| MOD-0192 Capacity | `mvp6-mod0192-pack-phase15-close-01/` (22 Sep) | No; pre-approval draft delta (`status: draft`, 2.0.0, target `b5b948ee…`) | New delta in [mod-0192-capacity/](mod-0192-capacity/) |

Both new deltas follow the same rule. The shared pack becomes, byte for byte, the isolated pack the **owner already promoted to `ready-for-dev` on 2026-09-22**; that is the pack the accepted work was built under. A new §22 then binds it to the CT acceptance and evidence hashes. `status` becomes the owner-approved `ready-for-dev`, never `done`. No business rule is changed.

| Module | Shared pack preimage | Owner-promoted pack | Proposed target | Controlling CT acceptance |
|---|---|---|---|---|
| MOD-0190 | `637690f3…6877` | `6a57769c…0983` | `8403d8f4…ea40` | `mvp6-mod0190-401-disposition-ct-02` (ACCEPTED) |
| MOD-0192 | `edd550b8…69f7` | `d01bf7a0…e6c0` | `de81a0e2…946c` | `mvp6-bc-successor-ct-handoff-01` (BOUNDED ACCEPTED) + `mvp6-mod0192-hosted-acceptance-consolidate-01` (NARROW CLOSE) |

Both patches pass `git apply --check` on copies of the current packs, individually and together, and reproduce the target bytes exactly.

## MOD-0187 Claims — verification of the existing delta

| Check | Result |
|---|---|
| `mod-0187-final-pack-delta-01/SHA256SUMS` | 13/13 OK |
| `proposed-pack.patch` against current shared pack `a342054c…1c1f` | applies, `--fuzz=0` |
| `inputs.sha256` | 13/14 match. **Drift:** `docs/analysis/contracts/shipment-bundle.openapi.yaml` `93c696e2…3571` → `5dfe7c1b…d21c`, i.e. the final YAML the delta pinned as a proposal is now the published canonical file |
| Relation to acceptance | Delta prepared 20 Sep. Claims CT acceptance `mvp6-mod0187-ct-accept-01/SOP-22.md` (`30c9233e…7942`, 22 Sep) came later and is **not referenced**. The delta's patch still says "final release unapproved" and `draft`. Its start conditions 1 (publication/consent) and the `Program.cs` grant are now recorded as met in that CT record |

**Status: PRESENT, INTACT, STALE.** It does not bind the pack to the accepted Claims work package. This package does not duplicate or edit it. Whether to refresh it is a CT decision (see final report).

## Common boundaries

- `Program.cs` is owned by no module pack. Each `owned-paths.md` records the accepted composition hash and the single integration-owner slot (process v1.0 §8). The common checkout today has `7fdb5ef0…`, which is neither accepted composition.
- None of the 38 + 43 owned paths exists in the common checkout. Source uptake is Q14/Q15, not pack promotion.
- Preserved NON_PASS: S&OP T03 271/276 and T04 Loads 0/1; no waiver.
- MOD-0190 is bound to SANDOP-CAPACITY 2.0.0 as accepted. The canonical file is now 3.0.0 (S&OP operations unedited); the re-pin is named as open, not performed.
- No new effort credit.

## Files

| Path | Purpose |
|---|---|
| `mod-0190-sop/SOP-22-PACK-DELTA.md`, `mod-0192-capacity/SOP-22-PACK-DELTA.md` | Lineage, Phase 1.5 delta, checks |
| `*/proposed-pack.patch` | Unified patch, preimage → target (header lines start with `#`) |
| `*/owned-paths.md` | Exact owned paths with accepted SHA-256, protected paths, `Program.cs` slot |
| `*/PROMOTION-DECISION.md` | Exact owner decision text — NOT APPROVED — with options |
| `SHA256SUMS` | Repo-root-relative checksums of every file above |
