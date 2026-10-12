---
decision_id: MVP6-LOADS-UPTAKE-PH15-OWNER-DECISION-01
status: approved (PH15-185-UPTAKE) + F-1: fix together with the uptake
decided_at_local: 2026-09-26, ~14:36–14:48 +03:00 (approximate window given by CT; the minute of each single answer was not given)
decided_by: Natig Yusubov, Chief Executive Officer (repository owner)
decision_source: owner answer in the MVP6 Control Tower conversation (in-app question tool), 2026-09-26
approvedBy: current-role-user-message-2026-09-26
bound_to: docs/roadmap/plans/mvp6-loads-uptake-prep-01/PH15-185-UPTAKE.md sha256 aa2fa589d91dd724ac68a414f303440a7b475d471d63ff545f198f8c06a45c2d; docs/roadmap/plans/mvp6-loads-uptake-prep-01/README.md sha256 ce59aecd76c8253023a6b88b78c7953f8b8f0d142c34b03060ef8fada00e5c5e; docs/roadmap/plans/mvp6-loads-uptake-prep-01/OWNED-PATHS.md sha256 37e519324d47a39e4810f291e67f3499edea8f1a5611d5b2ce5aaefd41c09f7d; docs/roadmap/plans/mvp6-loads-uptake-prep-01/SHA256SUMS sha256 1518468c724e14b6a5558227bf929fd54bfdd51eaa2afe46bd74d00e633c0337
recorded_by: AL-MVP6-REC-PACK-01 (Q71, chat lane) at 2026-09-26T14:45:53+0300 on CT instruction; CT writes no files
---

# MOD-0185 Loads producer uptake — Phase 1.5 table approved; finding F-1 fixed together with the uptake

## Decision 1 — Phase 1.5 table PH15-185-UPTAKE: approved

Owner answer: **Yes — approved.** An invalid or missing stored root is emitted as `null` in `queryLoads`; the list never fails. No new endpoint, no backfill.

Exact decision text (source `docs/roadmap/plans/mvp6-loads-uptake-prep-01/PH15-185-UPTAKE.md` sha256 `aa2fa589d91dd724ac68a414f303440a7b475d471d63ff545f198f8c06a45c2d`):

> I approve the Phase 1.5 table in `docs/roadmap/plans/mvp6-loads-uptake-prep-01/PH15-185-UPTAKE.md` for the MOD-0185 producer uptake granted as decision C / option A: `queryLoads` emits the persisted `LoadPlan.CorrelationRoot` as `LoadSummary.lifecycleCorrelationId` using a presence-aware read (stored UUID incl. nil → value; missing, null or invalid → null; the list is not failed), with no derivation, no backfill, no new endpoint and no change to create or transition. One writer may change only the 11 paths in OWNED-PATHS.md in the isolated environment of ISOLATED-ENV.md, followed by independent runtime verification on the Mac. This does not approve UI, Gateway, Program.cs, contracts, packs, the transition-path finding F-1, commit or push.

## Decision 2 — finding F-1: fix together with the uptake

Owner answer: **fix F-1 together with the uptake.** A transition on a Load without a stored root is rejected safely. No contract change and no endpoint change. The estimate rows and test rows for the F-1 fix are added in the DEV prompt.

Finding as written in the source (source `docs/roadmap/plans/mvp6-loads-uptake-prep-01/README.md` sha256 `ce59aecd76c8253023a6b88b78c7953f8b8f0d142c34b03060ef8fada00e5c5e`, "Top risks"):

> 2. **Finding F-1 (outside decision C):** the transition path (`LoadRepository.cs:43-44`) also reads typed `LoadPlan`; a legacy document without a stored root becomes `Guid.Empty`, which matches a valid nil inbound correlation (annex line 178). Needs a separate CT/owner decision; this package does not touch it.

## How the two decisions fit

The Decision 1 text keeps its own words, including "This does not approve … the transition-path finding F-1". Decision 2, given in the same CT session, is the separate owner decision on F-1 that the text leaves open; it adds the F-1 fix to the same Mac writer package. The Phase 1.5 table's design choice (b) ("F-1 … is **not** fixed in this package") is therefore superseded by Decision 2; the table file is not edited (K4).

Open for the DEV prompt (not decided here): the owned-path list (`OWNED-PATHS.md`, 37e519324d47…) limits `LoadRepository.cs` to "QueryAsync only"; the F-1 fix touches the transition read (`LoadRepository.cs:43-44`), so the DEV prompt must name the extra owned scope, the tests and the estimate. Runtime work runs only on the local Mac.

The Decision 1 quote is copied byte-for-byte from its source file (including its `>` quote markers); the F-1 quote is the source line byte-for-byte with a `> ` marker added; the source file itself still carries its "NOT APPROVED — prepared text only" heading, which this record supersedes for the option chosen. Source files are not edited (K4).
