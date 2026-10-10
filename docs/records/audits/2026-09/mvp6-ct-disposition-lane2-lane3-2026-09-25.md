# MVP6 CT disposition — Lane-2 (Q05) and Lane-3 (Q06), 2026-09-25

Role: MVP6 Control Tower (Claude). Recorded 2026-09-25 23:25 Istanbul. Process: `docs/guides/operations/mvp6-development-process-v1.0.md`.
No build, test or runtime was run by CT. No acceptance, waiver or effort credit is created by this record.

## Q05 — Shipment UI acceptance matrix (Lane-2, CT background agent, 22:29–22:37)

Package: `docs/records/audits/2026-09/mvp6-shipment-acceptance-reconcile-01/` — `ARTIFACTS.sha256` verified 4/4 by CT.
**Disposition: RECORD ACCEPTED as the single Shipment UI acceptance matrix** (reconciliation, not acceptance of any criterion).
25 rows: 3 CLOSED_EXACT, 11 ACCEPTED_BOUNDED, 1 STATIC_ONLY, 4 PARTIAL, 1 UNAUTHORIZED, 3 POLICY_OPEN, 2 OPEN; 18 READY / 7 BLOCKED.
Generic DataTable 49 PASS / 35 FAIL retained as historical FAIL; row-level split 23 out-of-scope / 8 evidence gaps / 4 policy conflicts confirmed.

CT decisions on the lane's MISSING-DECISIONS items:
- **MD-02 (evidence reuse):** A01, A02, A04–A07, A15 and the A03 missing-read branch were accepted on the older 354-source manifest;
  later shared-UI and A12 changes touch their inputs (controller, `Program.cs`, `dt-defaults.js`, shared resx, `details.js`).
  Decision: **RERUN** in the final Shipment UI VER on the frozen final source; not inherited. Recorded in `EVIDENCE-REUSE.tsv`.
- **MD-01 (A07):** stays PARTIAL; no separate acceptance on the 24 Sep policy/error VER. Covered by the MD-02 rerun.
- **MD-04 (A09 whole):** A09 is closed when its three stale-case rows remain CLOSED_EXACT after the Lane-1 targeted regression
  and A09-SUCCESS passes the MD-02 rerun. Until then A09 as a whole is not stated as closed.
- **MD-03 (owner):** when DN-02 is put to the owner, its text must name generic FAIL rows 07 (`Unknown`), 22 (`ShowAll`), 14 and 34 (delete variants). Queued with Q11.

## Q06 — Evidence kit proposal (duplicate lanes)

Two lanes wrote the same folder `docs/roadmap/plans/mvp6-evidence-kit-proposal-01/`: the CT background agent (22:29–22:49) and the
owner's LANE 3 chat (started 22:23, last write 23:20). The LANE 3 chat rewrote `README.md`, `KIT-SPEC.md`, `REUSED-SCRIPTS.tsv`,
`ADOPTION-DECISION.md` and `SHA256SUMS`; its `SHA256SUMS` verifies. The CT agent's 18 draft files under `proposed/` are no longer
listed in `SHA256SUMS` (orphans). The owner stopped the LANE 3 chat at about 23:23.
**Disposition: the LANE 3 chat package is the single current proposal**; the orphaned CT-agent drafts are superseded (move pending owner approval).
DocsPathGuard check by CT: the bare `.py/.sh/.js` kit files reference only `docs/records/`, one of the five permitted folders — no violation found by inspection.
Process note: the duplicate came from dispatching the same queue item through two channels; from now on each queue item has exactly one dispatch channel recorded in `CT-QUEUE.tsv`.
Decision A (A1 pilot adoption) is reported by the LANE 3 chat as chosen by the owner; CT records it only after owner confirmation in the CT conversation.

## Addendum 2026-09-25 23:28 — Q23

Owner approved moving the 18 superseded CT-agent draft files into `docs/roadmap/plans/mvp6-evidence-kit-proposal-01/proposed/_superseded-ct-agent-draft/` (move only, nothing deleted). The approved package and its `SHA256SUMS` are unchanged and still verify. Decision A1 is recorded in `docs/records/decisions/2026-09/mvp6-evidence-kit-adoption-owner-decision-01.md`.

## Addendum 2026-09-25 23:40 — Q04 and Q07

- **Q04 A12 runtime VER:** `mvp6-shipment-a12-runtime-independent-ver-01/` (ARTIFACTS 3/3 OK). Result STOPPED AT PREFLIGHT: input hashes 5/5 PASS; runtime NOT RUN because the lane ran in a Linux VM shell without native .NET 8, mongod or /private/tmp. **CT disposition: BLOCKED (executor)**, no acceptance, no failure of A12. Re-dispatch trigger: a native macOS executor (Claude Code running locally on the Mac). P1 hashes may be reused if unchanged. The lane removed an empty `.git/index.lock` with owner permission; no other deletion.
- **Q07 Loads decision A:** `docs/records/decisions/2026-09/mvp6-loads-root-amendment-owner-decision-a-01.md` verified by CT: bound pack `OWNER-DECISION-PACK.md` 51f7be10… matches; release-prep SHA256SUMS 21/21 OK; canonical preimages unchanged (YAML 5dfe7c1b…, v2 annex a2187c93…); v3.1.0 annex not published. **CT disposition: DONE.** Q08 (B) now DECISION-REQUIRED; Q09 stays HELD.
