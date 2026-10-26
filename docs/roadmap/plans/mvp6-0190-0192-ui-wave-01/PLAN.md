---
module: MOD-0190
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# MOD-0190 S&OP + MOD-0192 Capacity — full working UI: sequenced gate plan

    opened    : 2026-10-18 on the owner override
                (docs/records/decisions/2026-10/mvp6-0190-0192-ui-override-owner-decision-01.md) · author: CT
    status    : PLAN. The UI build is the LAST gate; it is blocked behind composition + data gates that are
                not CT-ownable alone (one needs the MOD-0188 owner, one a new frozen contract, one the Q292
                owner gate). Nothing is built here. Each gate names what it needs and who owns it.
    honest topline : "finish the UI" is really "stand up the composition + data these modules have never
                had, then build the already-designed UI." The UI itself is the smallest part.

## Critical path (each gate blocks the next)

| # | gate | what it needs | owner | CT can prepare? |
|---|---|---|---|---|
| **G0** | Owner override | DONE 2026-10-18 (this plan's basis) | owner | — |
| **G1** | **Q292 §22 composition gate OPEN** | owner authorises composing 0190/0192 in production (lifts the "gated target" status on the accepted-composition hashes) | owner | CT drafts the gate-open request |
| **G2** | **SUPPLY-CONSTRAINTS contract** (0192 precondition) | a new contract for capacity constraints, authored + **frozen**; it does not exist today | CT Supplier/seam contract writer + owner freeze | CT can draft the contract + options |
| **G3** | **DEMAND v2 uptake** (real demand, not fixtures) | MOD-0188 owner finalises the v2 draft; consumers re-model identity → `(revisionId, planningPeriodKey, contentVersion, 64-hex digest)`, adopt the canonical checksum, hold `demand.plans.consume` (the three conditions from `mvp6-demand-v2-consumer-acceptance-01`) | MOD-0188 owner + CT (consumer side) | CT can prep the consumer re-model once v2 is frozen; **blocked on MOD-0188** |
| **G4** | **Compose in `Program.cs`** | register S&OP + Capacity; swap `DemandFixtureReader` → a real DEMAND v2 client; un-env-gate the Capacity middleware (reverse Q484 test-host-only); Capacity also needs G2's SUPPLY-CONSTRAINTS client | CT / integration-agent | yes, after G1–G3 |
| **G5** | **Activate the 10 reserved gateway routes** | flip the `/api/supply-chain/*` routes from reserved-404 to live (they are already in the ocelot patch as reserved per the gateway ruling); update the OD-7 guard count | integration-agent (`gateway/**` Protected) | CT preps the patch delta |
| **G6** | **Build the UI** | the §23 UI scope + §24 manifests are already approved (`mvp6-sop-capacity-ui-scope`/`-pack-signoff`, UI-revision patches at `mvp6-ui-pack-patch-190-192-01/`). Build views/JS/resx under `frontend/.../SupplyChain/{Sandop,Capacity}`, 7-language l10n following the **{Claims,Returns} camp** canon ([[supplychain-l10n-canonical]]) | CT / l10n-agent | yes, after G4 (UI needs a live, composed backend to be "working") |
| **G7** | **Permissions** | `sop-workflow-signoffs` (0190) + `capacity-planning` (0192) are already manifest ModuleCodes; add the new `demand.plans.consume` grant (same shape as the BL-652 cross-domain coupling) | security/permission writer + owner | CT preps the grant |
| **G8** | **Integration + VER** | Q151-style integrated golden-flow slice for S&OP + Capacity; independent VER | integration-agent + read-only-auditor | CT preps the hand-offs |

## What CT can start NOW (unblocked by G1–G3)

- **G1 request** — draft the Q292 §22 gate-open package for the owner (the composition the gate guards is
  already hash-accepted as a *target*; this asks to lift the gate).
- **G2 draft** — draft the SUPPLY-CONSTRAINTS contract + options for owner freeze (builds on
  `mvp6-q281-demand-seam-01/OPTIONS.md`).
- **G3 consumer-side prep** — the DEMAND v2 consumer re-model is already specified by this session's
  acceptance review; CT can write the consumer adapter design, but it cannot be finalised until the MOD-0188
  owner freezes v2. **This is the long pole and it is cross-team.**

## Honest scope note

G3 (DEMAND v2) is a **future-MVP, cross-team** dependency — my consumer-acceptance review this session
concluded v2 is not frozen and needs consumer re-modeling. A **fully working** UI with real demand cannot
land until MOD-0188 moves. **Interim option** (if the owner wants visible progress sooner): compose 0190/0192
against the existing fixture demand (G4 minus the real DEMAND client) and build the UI on that — a
*composed-but-fixture-demand* UI that is real end-to-end except the demand source. Say the word and I'll
split the plan to deliver that interim first, with the real-demand swap deferred to when v2 freezes.

## Files

- `PLAN.md` — this. Override record: `mvp6-0190-0192-ui-override-owner-decision-01.md`. Inputs: the §23/§24
  UI decisions, the DEMAND-v2 acceptance review, the gateway ruling (reserved routes), Q484, Q281 demand seam.
