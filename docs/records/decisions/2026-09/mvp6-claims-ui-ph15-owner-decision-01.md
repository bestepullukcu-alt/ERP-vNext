---
decision_id: MVP6-CLAIMS-UI-PH15-OWNER-DECISION-01
status: approved (PH15-UI-187)
decided_at_local: 2026-09-26 (Europe/Istanbul), in the MVP6 Control Tower session; the exact minute was not recorded (CT verdict Q64a F1)
decided_by: Natig Yusubov, Chief Executive Officer (repository owner)
decision_source: owner approval in the MVP6 Control Tower conversation, 2026-09-26; recorded late on CT instruction (CT verdict on Q64a, finding F1, recorded in docs/records/audits/2026-09/mvp6-ct-verdicts-q64a-q77a-q78-2026-09-26.md)
approvedBy: current-role-user-message-2026-09-26
bound_to: docs/roadmap/plans/mvp6-decision-prep-01/PH15-UI-187.md sha256 41ffefb8ef9baecf78efab6a932970fbd57dace53d1ddd3669b2df32c417406c
recorded_by: AL-MVP6-Q79-REC-01 (Q79, chat lane on the Linux VM bridge) at 2026-09-26T17:30:43+0300 on CT instruction; CT writes no files
---

# MOD-0187 Claims UI — Phase 1.5 table PH15-UI-187 approved

Owner answer: **Yes — approved.** The approval was given in the CT session on 2026-09-26 and used by the Claims UI draft
lane (Q64a), but it was not recorded at the time. The exact minute is not known; this record says so rather than
inventing one.

Exact decision text (source `docs/roadmap/plans/mvp6-decision-prep-01/PH15-UI-187.md` sha256 `41ffefb8ef9baecf78efab6a932970fbd57dace53d1ddd3669b2df32c417406c`, section "Exact decision text"):

> I approve the Phase 1.5 architecture table in `docs/roadmap/plans/mvp6-decision-prep-01/PH15-UI-187.md` for the MOD-0187 Claims UI defined in pack §32 (pack sha256 `31cb35c38fd97c91172884156a32b69ec219e3cf4f96ad5f9124dc3c8ad06626`). Under my decisions of 2026-09-26 (modules first; SR-D4 overlay), one writer may build the 21 owned UI paths of §32.10 in an isolated environment (HEAD `4a8d4d4b` archive + accepted overlays), starting with vertical slice CU-VS1, and deliver shared changes and the self-registration provider/tests/nav keys only as a hash-bound overlay package. This does not approve edits to shared files, gateway, permissions catalogue, contracts, packs or backend; `done` status; commit or push; or closure before Phase 4.5 in the integrated target. G-MODAL, G-DATETIME and G-ICONMAP are resolved in the vertical slice; any need for a new owned path or scope change comes back to me.

## Notes

- The quoted text binds the MOD-0187 pack at sha256 `31cb35c38fd97c91172884156a32b69ec219e3cf4f96ad5f9124dc3c8ad06626`.
  The pack is now `8ed42fad66b7739b8c56778a57e0ead84cc8ce3678ea4868306263ac501a94a2`; the only change since is the Q75 heading-label correction (owner decision
  `mvp6-pack-heading-labels-owner-decision-01.md`, independently verified in Q76), which changed headings and approval
  lines only.
- CT verdict Q64a F2: the owner decisions "modules first" and "draft overlays" (~16:17) take precedence over the pack
  §32.14 wording "UI code not authorized"; that wording is corrected in the next MOD-0187 pack revision.
- CT verdict Q64a F3: the Q64b isolated-environment recipe is HEAD archive + A12 360 overlay + the Claims draft archive.

The quoted text is copied byte-for-byte from the source file (including its `>` marker); the source still carries its
"NOT APPROVED — prepared text only" heading, which this record supersedes. The source file is not edited (K4).
