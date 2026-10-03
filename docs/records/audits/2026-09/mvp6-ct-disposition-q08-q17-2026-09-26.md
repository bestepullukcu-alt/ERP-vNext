# MVP6 CT disposition — Q08 (Loads decision B), Q17 (pack alignment), status of Q04 and Q24

Role: MVP6 Control Tower (Claude). Recorded 2026-09-26 00:05 Istanbul. No build, test or runtime was run by CT.
Baseline unchanged: `feature/mvp6-logistics` @ `4a8d4d4b…1136c`; tracked-file diff unchanged (13 files, +5314/−215).

## Q08 — Loads decision B (Lane-4)

Record `docs/records/decisions/2026-09/mvp6-loads-root-amendment-owner-decision-b-01.md` verified: bound pack `OWNER-DECISION-PACK.md` 51f7be10… matches;
canonical YAML still 5dfe7c1b…, v2 annex a2187c93…, v3.1.0 annex absent (nothing published). **CT disposition: DONE (decision recorded).**
Dispatch draft `docs/roadmap/plans/mvp6-loads-publication-dispatch-01/DISPATCH-v1.0.md` reviewed.

**CT finding (sequencing):** `docs/reference/architecture/docs-path-authority.json` (APPROVED) pins `canonicalTargets`
`docs/analysis/contracts/shipment-bundle.openapi.yaml` to 5dfe7c1b…. Publishing the 3.1.0 YAML (6dc1dd48…) changes that hash, so the
production DocsPathGuard would fail until the binding is re-approved. Decision B explicitly excludes guard/docs-path binding. The dispatch
treats this as an open item *after* publication. **CT decision: publication execution (Q25) is HELD until a guard-binding update for the
3.1.0 hash is prepared and decided by the owner (Q26)**, so that publication and binding land together and the repository guard is never left red
(same pattern as the Capacity v3 publication, 22–23 Sep).

## Q17 — pack alignment deltas (Lane-2 chat)

Package `docs/roadmap/plans/mvp6-pack-alignment-01/` — `SHA256SUMS` 19/19 OK. **CT disposition: DONE (prepared, not approved).**
- MOD-0190 and MOD-0192: new deltas align the shared packs to the packs the owner promoted to `ready-for-dev` on 2026-09-22 and bind the CT acceptances.
  Promotion decisions queued as Q27 (0190) and Q28 (0192).
- MOD-0187 Claims: existing delta `mod-0187-final-pack-delta-01/` is intact but **stale** (prepared before the 22 Sep CT acceptance; pins the pre-publication YAML).
  CT decision: refresh it as a successor delta (Q29, prep) before any promotion decision; the old delta stays unchanged.
- Noted, not changed: MOD-0190 bound to SANDOP-CAPACITY 2.0.0 while canonical is 3.0.0 (re-pin open); preserved NON_PASS S&OP T03 271/276 and T04 Loads 0/1.

## Q04 and Q24 — not run

No output folder exists for `mvp6-shipment-a12-runtime-independent-ver-02` or `mvp6-evidence-kit-validation-01`; `scripts/evidence-kit/` absent.
Both remain READY and need a local macOS Claude Code session.

## Addendum 2026-09-26 00:40 — Q26 and Q29

- **Q26 guard-binding prep:** `docs/roadmap/plans/mvp6-loads-guard-binding-prep-01/` — SHA256SUMS 16/16 OK. **DONE (prepared, NOT APPROVED).** Publication changes one guard input (YAML pin 5dfe7c1b → 6dc1dd48); new annex not a target; candidate payload a77538b4…11e0. Finding: emulated guard is **already red** on the current tree (19 hits in 8 untracked Loads amendment evidence files from 24 Sep); candidate adds 8 historical seals. Emulation only (Python) — the production .NET DocsPathGuard has not been run; it must run on the Mac in the combined step. Owner decision MVP6-LOADS-PUBLICATION-GUARD-01 queued as Q30; publication Q25 stays HELD until Q30, then a DISPATCH v1.1 per EXECUTION-ORDER.md.
- **Q29 Claims delta refresh:** `docs/roadmap/plans/mvp6-pack-alignment-02-claims/` — SHA256SUMS 5/5 OK. **DONE (prepared, NOT APPROVED).** Target f0e4d3bd… ready-for-dev; no Claims contract drift; gaps listed in its SOP-22 (R14 hash typo in an existing record, one un-retraced composition hop, 44-vs-47 path count, Loads 3.1.0 forward drift). Promotion decision queued as Q31.
- Q04 and Q24 still not run (no output folders).

## Addendum 2026-09-26 01:28 — Q33 UI pack drafts

- `docs/roadmap/plans/mvp6-ui-pack-drafts-01/` — SHA256SUMS (b568c94f…) 13/13 OK. **DONE (drafts, NOT APPROVED).** Returns and Claims UI as in-pack revisions (DCP-002 check exit 0 for both; no new IDs). Slim golden reference (6 create fields each); list, create and transition screens; no detail page (no single-record read operation published). Transition uses the Shipment lifecycle ID read server-side via `getShipment`, so users also need `supplychain.shipments.read`. Contract gap recorded: Claims list cannot show approved amount after reload. Estimates (O/M/P h): Returns 58/100/172, Claims 56/98/168. Not testable until the accepted backends (43 + 44 files) are in an integrated target (Q14/Q15).
- Owner preferences given in the lane chat (full scope; extra Shipment read grant) are noted as preferences; the approval texts remain NOT APPROVED until the owner decides in CT (Q34).
- Terminal lanes Q04 / Q24 / Q25: no output yet.

## Addendum 2026-09-26 01:36 — Q35 UI pack revisions

- `docs/roadmap/plans/mvp6-ui-pack-revisions-01/` — SHA256SUMS (6b7d770b…) 6/6 OK. **Claims: DONE (patch prepared, NOT APPROVED).** Order enforced: shared pack a342054c → alignment-02 00dee2b1 → f0e4d3bd → UI revision 5644479a → 762ab533 (status stays ready-for-dev). Gaps recorded: approved amount absent from list data; transition inputs vs `window.showConfirm` modal rule; field icons need a shared test file; `occurredAt` needs date-time with offset; generic DataTable verifier vs approved OUT rows; Claims alignment still unapproved.
- **Returns: BLOCKED.** The Returns alignment patch in `mvp6-final-pack-delta-01/proposed-pack.patch` (731622d2…) is a pseudo-patch (no headers/hunks, no target hash); git apply and patch reject it; its recorded replacement leaves the pack draft. CT decision: prepare a real Returns alignment patch built like the Claims one (Q36) before any Returns promotion (Q16) or Returns UI revision.
- CT correction: the Q34 decision record time was 01:24, not 01:30 (administrative fix r1).

## Addendum 2026-09-26 01:42 — Q38 applied, Q36 prepared

- **Q38 Claims pack apply:** `docs/records/audits/2026-09/mvp6-claims-pack-apply-01/` ARTIFACTS 8/8 OK. CT re-hashed the pack: MOD-0187 = 762ab5337af85b96… `status: ready-for-dev`; tracked diff still 13 files (only MOD-0187 content changed). **DONE.**
- **Q36 Returns alignment + UI revision:** `docs/roadmap/plans/mvp6-pack-alignment-03-returns/` SHA256SUMS 5/5 OK. **DONE (prepared, NOT APPROVED).** Shared pack 07a8a015… → alignment f4396e8a… → UI revision 6c8fbe28…; order proven; replaces the unusable pseudo-patch as the Returns promotion path. Owner sign-off queued as Q39 (supersedes Q16).
- Effort: no numeric credit recorded. Candidate for the next effort successor: pack/design remaining rows 0187-1 (6 h ML) and, after sign-off, 0186-1 — only with an explicit O/M/P allocation.
