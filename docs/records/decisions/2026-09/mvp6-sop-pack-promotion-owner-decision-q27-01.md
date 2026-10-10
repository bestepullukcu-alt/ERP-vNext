---
decision_id: MVP6-SOP-PACK-PROMOTION-OWNER-DECISION-Q27-01
status: approved (option A)
decided_at_local: 2026-09-26, ~14:36–14:48 +03:00 (approximate window given by CT; the minute of each single answer was not given)
decided_by: Natig Yusubov, Chief Executive Officer (repository owner)
decision_source: owner answer in the MVP6 Control Tower conversation (in-app question tool), 2026-09-26
approvedBy: current-role-user-message-2026-09-26
bound_to: docs/roadmap/plans/mvp6-decision-prep-02/Q27.md sha256 ed009b7d9692e372c47cf8b5aa4e4e213a2e765d9b10f10aa9c4429b185be26c; docs/roadmap/plans/mvp6-decision-prep-02/SHA256SUMS sha256 7a22f9e414f482d7c46e45c5f936d50c1ba127ab9fe33a3baf9c7c32175e235d; docs/roadmap/plans/mvp6-pack-alignment-01/mod-0190-sop/PROMOTION-DECISION.md sha256 8a01dad7da1f62343b1e6a086fc5e12ae5af42c2690ff4ee929f38e7c72a6c1e; docs/roadmap/plans/mvp6-pack-alignment-01/mod-0190-sop/proposed-pack.patch sha256 116b7c47e31fca6cda17d4632cb9f9730ed0e3b46122f82a2f1f5c5f8cd0923e; target execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md preimage sha256 637690f32c1fa03e3039a4542bd53f7bb9b12f6b18d856a7d0e1f23d740c6877 → result sha256 8403d8f46059c99ef34d3fe08b3cacdbbbefb321f30f921b620eceda9180ea40
recorded_by: AL-MVP6-REC-PACK-01 (Q71, chat lane) at 2026-09-26T14:45:53+0300 on CT instruction; CT writes no files
---

# MOD-0190 S&OP shared pack promotion (alignment delta) — Q27 option A

## Decision

Owner answer to Q27: **A — approve the exact text below.**

## Exact decision text (source `docs/roadmap/plans/mvp6-decision-prep-02/Q27.md` sha256 `ed009b7d9692e372c47cf8b5aa4e4e213a2e765d9b10f10aa9c4429b185be26c`; unchanged from `docs/roadmap/plans/mvp6-pack-alignment-01/mod-0190-sop/PROMOTION-DECISION.md` sha256 `8a01dad7da1f62343b1e6a086fc5e12ae5af42c2690ff4ee929f38e7c72a6c1e`)

> I approve applying `docs/roadmap/plans/mvp6-pack-alignment-01/mod-0190-sop/proposed-pack.patch` to `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md` only if the preimage SHA-256 is `637690f32c1fa03e3039a4542bd53f7bb9b12f6b18d856a7d0e1f23d740c6877` and the result SHA-256 is `8403d8f46059c99ef34d3fe08b3cacdbbbefb321f30f921b620eceda9180ea40`.
>
> The result aligns the shared pack with the isolated pack I promoted on 2026-09-22 (`6a57769ced4396d2bc4228749a7e24b0daf36ce279930bb77c5dfdfe19fd0983`, status `ready-for-dev`) and records the Control Tower acceptance `MVP6-MOD0190-401-DISPOSITION-CT-02` in a new §22. It changes no business rule, acceptance criterion or owned-path list.
>
> This decision does not approve: `done` status; common-checkout source uptake or any `Program.cs`, shared DI, permission or gateway change; re-pinning MOD-0190 to SANDOP-CAPACITY 3.0.0; live DEMAND, Workflow or Event Bus integration; UI; E5/G5; rollout; commit, push or stash. One named writer applies the patch after rechecking both hashes; any mismatch stops the application.

## Application

- Single named writer: AL-MVP6-REC-PACK-01 (Q71), working tree only, no index, no commit (see `mvp6-commit-strategy-owner-decision-q03a-01.md`).
- Patch `docs/roadmap/plans/mvp6-pack-alignment-01/mod-0190-sop/proposed-pack.patch` sha256 `116b7c47e31fca6cda17d4632cb9f9730ed0e3b46122f82a2f1f5c5f8cd0923e` on `execution/domains/supply-chain-execution/module-packs/MOD-0190-sop-workflow-signoffs.md`: preimage `637690f32c1fa03e3039a4542bd53f7bb9b12f6b18d856a7d0e1f23d740c6877` → result `8403d8f46059c99ef34d3fe08b3cacdbbbefb321f30f921b620eceda9180ea40`, both rechecked before and after `git apply`.
- Evidence: `docs/records/audits/2026-09/mvp6-pack-apply-q27-q28-01/` (HASHES.tsv, COMMANDS.tsv). Independent VER: queue Q72.
- Known follow-up, not part of this decision: the heading labels that still read as proposals inside the applied pack go to queue Q73 (heading-label patch, extends Q50).

The quoted text is copied byte-for-byte from the source file named above it (including its `>` quote markers); the source file itself still carries its "NOT APPROVED — prepared text only" heading, which this record supersedes for the option chosen. Source files are not edited (K4).
