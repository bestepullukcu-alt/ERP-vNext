# MVP6 development plan v9.8 — 2026-10-19

Supersedes v9.7 (2026-10-18). Every number below is measured on `feature/mvp6-logistics`; the commit or record that
moved it is named.

## 1. What changed since v9.7

v9.7's risk was "preparation without application". This window applied, ran and verified:

- **Integrated PASS exists.** Q151 run 2 (`1ace4f30e`) — five golden flows through the gateway; then the **G5 chain**
  (`mvp6-g5-e2e-chain-01`): shipment → carrier/load → **POD** → return/claim, reconciled, every event on the root.
- **Wave 6.1 delivered**, not just prepared: G4 composition (`078cf2964`, seeded non-prod only) · G6 S&OP + Capacity UIs
  (`e8894c828`, `fcf42be05`) · G7 role grants (owner-applied, `40eac559c`) · G8 independent VER **PASS** after rework
  (`8fd22ddaa`, `85b685c4f`).
- **Two silent production defects found and fixed:** S&OP/Capacity gates required the `legal_entity_id` claim
  AuthService no longer issues (every real user 403; `2302c677c`, owner decision); Shipments Details never sent the
  legal-entity scope (`4b9dcd57c`).
- **§16 acceptance turned from paperwork into evidence:** independent audit + 7 new guard facts + G5 → shipment bundle
  19/32 boxes MET with cited evidence (was 0/32) (`4cf762069`).
- **Owner decisions 2026-10-19:** MOD-0188 ships **fixture-proven**; **one PR** to main
  (`mvp6-finish-line-0188-and-pr-owner-decision-01`).

## 2. Measured state

| | value |
|---|---|
| Web tests | 2621/2621 |
| SupplyChain tests | 590/591 (recorded `ClaimReplay` restart-mode only) |
| Auth Roles | 848 / 0 / 7 skipped |
| Independent VERs | 6-UI PASS · G8 PASS (+ delta PASS) |
| Pack §16 (shipment bundle) | 0183 8/11 · 0184 3/5 · 0185 4/5 · 0186 3/5 · 0187 5/6 — in scope 8/11 · 3/4 · 4/4 · 3/4 · 5/5 |
| 0190 / 0192 §16 | 8/9 · 8/10 |
| Branch vs main | 110 ahead / 77 behind; merge-tree clean |

## 3. Finish line (owner-ruled) and what is left

**MVP-6 done = shipment bundle + S&OP/Capacity + DemandPlanning (UI, fixture-proven), merged to main.**

| # | item | owner | state |
|---|---|---|---|
| 1 | Open and merge the PR (`docs/roadmap/plans/mvp6-pr-to-main-01/PR.md`) | owner | **ready** |
| 2 | Apply BL-652 v2 (Admin baseline for Carrier/Load/Return/Claim) | owner | ready (`…-v2-after-g7.patch`) |
| 3 | Gateway `SigningKey` in dev/live config | owner | open |
| 4 | OpenAPI response/example/event validation (0183 #10, 0184 #2, 0185 #3, 0187 #2) | CT | **done** `e5b01cc0c` |
| 5 | SupplyChain + Web (+ Planning) suites into CI | CT / CI owner | open — today every green number is local |
| 7 | Free disk on the dev machine (lane runs hit 183 MB free; MongoDB needs ≥ 500 MB) | owner | 1.7 GiB after freeing 3 GB of scratch copies |
| 6 | Temporary Admin widenings expire **2026-11-03** (Q358, BL-652, G7) | owner | re-approve or replace by entitlements before then |

## 4. Post-MVP-6 (named, not in the finish line)

DemandPlanning production authorities (S1, AC-D01…D29); DEMAND v2 (G3) to replace the S&OP/Capacity fixture seed;
SUPPLY-CONSTRAINTS producer MOD-0193 (G2); BL-652 Option 2 (SupplyChain → MDM service identity); the parked process
audit (`memory: mvp6-process-audit-2026-10-19`: CI coverage, executable rules, adapter consolidation, secret hygiene).

## 5. Measures

This window: ~35 commits, all pushed. Code-bearing: R-2 alignment, G6 UIs, G8 rework, 7 §16 guard facts, Shipments fix,
G7 grant. Records: G4, G6×2, G8 + delta, G5, §16 audit, 3 owner decisions.
