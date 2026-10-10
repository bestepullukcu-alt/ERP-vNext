---
decision_id: MVP6-EVIDENCE-KIT-V1-2-ADOPTION-OWNER-DECISION-01
status: approved
decided_at_local: 2026-09-26T13:22+03:00 (approximate, "~13:22")
decided_by: Natig Yusubov, Chief Executive Officer (repository owner)
decision_source: owner choice of option V1 in the MVP6 Control Tower conversation (in-app question tool), 2026-09-26
approvedBy: current-role-user-message-2026-09-26
bound_to: docs/roadmap/plans/mvp6-evidence-kit-proposal-03/ADOPTION-DECISION-v1.2.md sha256 3a5ea48f8254f6346e500447733f78c664e0abc08e11ccc344478402b19d59d2; docs/roadmap/plans/mvp6-evidence-kit-proposal-03/SHA256SUMS sha256 e8bec4d7821b5f3f4d50ae3310a35ec9a6dbc105704bf97c3577d1ee40a3caad
precondition_met: independent re-check docs/records/audits/2026-09/mvp6-evidence-kit-v1-2-recheck-01/SOP-22-VER.md (PASS, recommendation V1)
recorded_by: AL-MVP6-KIT-INSTALL-01 (Q24a, chat lane) at 2026-09-26T13:44+0300 on CT instruction
---

# MVP6 evidence kit v1.2 — decision V1 (install under A1's terms)

## Decision text (verbatim from ADOPTION-DECISION-v1.2.md §2)

> "The owner approves the MVP6 evidence kit v1.2 for installation. The environment owner installs the files listed in
> `docs/roadmap/plans/mvp6-evidence-kit-proposal-03/proposed/PLACEMENT.tsv` at the listed paths, byte-identical to the
> SHA-256 values in §3 of `docs/roadmap/plans/mvp6-evidence-kit-proposal-03/ADOPTION-DECISION-v1.2.md`. These bytes
> supersede, for installation, the v1.0 bytes listed in decision A1 (`mvp6-evidence-kit-adoption-owner-decision-01.md`)
> and the v1.1 bytes of `mvp6-evidence-kit-proposal-02`; neither older set is installed. All other terms of A1 stay in
> force unchanged: pilot status; the one line added to §5 of `docs/guides/operations/mvp6-development-process-v1.0.md`
> exactly as quoted in A1 (the guide keeps the file name `mvp6-evidence-kit-v1.0.md`); the identity method (inside a
> lane's own isolated Auth database only, never port 27017, the password hash of each named seeded user is replaced with a
> bcrypt(12) hash of a locally generated password, which is destroyed with the database at cleanup — in v1.2 that
> password exists only in the memory of the lane supervisor and of the kit tasks it starts, and is shown only on the
> requesting user's own terminal); no change to product code, `.antigravity`, gateway configuration, contracts, packs,
> guards or existing evidence; no reinterpretation of any existing approval, acceptance or exact-hash boundary; each lane
> still sets its own security switches in its work package; and durable PNG remains a separate decision. The kit becomes
> the required environment method for new MVP6 runtime lanes only after one validation run on the Mac (Q24) has executed
> K00, K01, K02, K04, K05, K06, K07, K09, K11 and the final seal on the v1.2 bytes, meeting the pass conditions in §5 of
> this file, and CT has reviewed that evidence."

The file hashes bound by this decision are those in §3 of `ADOPTION-DECISION-v1.2.md` (28 files).

## Execution split (owner decision ~13:43)

Install (Q24a) in a chat lane without commit; validation run + commit (Q24b) in the first local Mac session. Until Q24b passes and CT reviews it, the kit is installed but not validated and not yet required.

## CT verdicts recorded here

Q66 writer output verified (33/33); Q67 independent re-check CT ACCEPTED (PASS, V1); Q51 effort update 08 CT ACCEPTED — delivered 1,464 h (51.5 %), CT-accepted 1,140 h (40.1 %) on 2,842 h; forecast total 2,954 h; A2 and A3 confirmed by CT; A7 overlap carried to the next update.

## Not decided here

Which seeded users and legal entities a lane uses (the work package decides), durable PNG (decision B), and any item outside F1–F14 of `CHANGES.tsv`. No commit, push, product, gateway, `.antigravity`, contract, pack or guard change is authorized.
