# PR: feature/mvp6-logistics → main

**Compare / open:** https://github.com/bestepullukcu-alt/ERP-vNext/compare/main...feature/mvp6-logistics?expand=1

**Title:** MVP-6 Logistics — shipment bundle, S&OP + Capacity UI, R-2 alignment, gateway correlation

**Body:**

## Summary
- **Shipment bundle (MOD-0183…0187)** — Shipments, Carriers, Loads, Returns, Claims: services + tenant UIs (7 languages),
  gateway routes; Q151 integrated run PASS; G5 end-to-end chain (shipment → carrier/load → POD → return/claim) PASS and
  reconciled; 6-UI independent VER PASS.
- **Wave 6.1 — MOD-0190 S&OP + MOD-0192 Capacity** — composed only where a demand source exists (non-production seed;
  production stays uncomposed, honest 404); full tenant UIs; §24 manifests; G8 independent VER PASS (incl. delta).
- **R-2 alignment** — S&OP/Capacity gates take the legal entity from `X-Legal-Entity-Id`, validated by MDM (they still
  required the `legal_entity_id` claim AuthService stopped issuing — every real user would have got 403).
- **Gateway** — signed correlation (lifecycle roots survive the edge); signing key required outside Development.
- **Roles** — G7: Admin holds the 8 S&OP/Capacity keys, Viewer the 2 reads (temporary, expires 2026-11-03 with Q358).
- **MOD-0188 DemandPlanning** — UI + fixture-proven slice; production authorities are post-MVP-6 (owner decision).

## Tests (measured on the branch)
- Diten.Web.Tests 2621/2621 · SupplyChain 584/585 (the one is the recorded `ClaimReplayTests…DurableRecovery`,
  restart-mode only) · Auth Roles 848/0/7.
- Not in CI today: SupplyChain, Diten.Web and PlanningService suites (`service-tests.yml` covers Auth/MDM/Platform).

## Deploy requirements
- Gateway: `Observability:Correlation:SigningKey` (secret, never committed).
- S&OP/Capacity compose only with `DemandFixtures__*` set (non-production).
- Owner-applied grants still pending: BL-652 v2 patch (`docs/roadmap/plans/mvp6-bl652-coupling-decision-01/patches/option1-adminmodules-widen-v2-after-g7.patch`).

## Merge notes
Branch is 110 ahead / 77 behind main; `git merge-tree` is clean; main's commits don't touch this code.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
