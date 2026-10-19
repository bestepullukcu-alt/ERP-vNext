# PR: feature/mvp6-logistics → main

**Compare / open:** https://github.com/bestepullukcu-alt/ERP-vNext/compare/main...feature/mvp6-logistics?expand=1

**Title:** MVP-6 Logistics — shipment bundle, S&OP + Capacity UI, R-2 alignment, gateway correlation, SupplyChain CI row

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
- **Roles** — G7: Admin holds the 8 S&OP/Capacity keys, Viewer the 2 reads (temporary, expires with Q358).
- **MOD-0188 DemandPlanning** — UI + fixture-proven slice; production authorities are post-MVP-6 (owner decision).
- **CI** — new `supplychain-tests` row in `service-tests.yml` (owned single-member replica set from
  `scripts/test-env/supplychain-ci-mongo.sh`, `enableTestCommands=1`; `CiServiceTestsGuardTests` updated). This PR is its
  **first Linux run**. Web already runs in `phase1-gates.yml`, Planning in `planning-tests`.
- **ClaimReplay restart test** — no longer red by design: the child is a conditional fact, and a new ordinary test runs it
  write → read in two `dotnet test` processes (sabotage-proven), so durable replay is proven in every run, CI included.
- **Docs-path authority** — shipment-bundle **3.3.0** pinned (decision `MOD0183-DOCS-PATH-SHIPMENT-BUNDLE-330-REPIN-01`,
  owner decision 4a; main's 3.2.0 pin came with #157).

## Tests — full gate on the merged tree (`1cded220a`, main #151–#157 in; every suite `known_failures.py check --suite`)
| suite | result | registry |
|---|---|---|
| SupplyChain | 591 / 0 / 1 skipped (the allow-listed Claims restart child) | clean |
| Auth Application | 2795 / 0 / 44 skipped | clean |
| Gateway | 259 / 0 | clean |
| Platform CompositionGuard | 12 / 0 | clean |
| Planning Cycles (0188) | 350 / 0 | clean |
| ArchitectureTests | 141 / 3 | 3 known (BL-615) |
| Platform Application | 8683 / 2 | **from main**: `LoginOnlyEndpointGuardTests` ×2, MOD-0022 (MVP-1) — owner: known, not MVP-6's |
| Web | 2689 / 2 | **from main**: `NavManifestL10nGuardTests` ×2, MOD-0191 SafetyStock (#155) — owner: known, routed to MOD-0191 |

No new red from this branch. Contract fidelity: all 247 published examples, live success+error answers of every bound
operation, and every Load/Return/Claim event envelope validate against the frozen `shipment-bundle.openapi.yaml`.
Pack §16 (in scope): 0183 8/11 · 0184 3/4 · 0185 4/4 · 0186 3/4 · 0187 5/5 · 0190 8/9 · 0192 8/10.

## Merge notes
- 121 ahead / 0 behind main. **Merge with a merge commit** (CT-APR-01: squash / fast-forward hides the human step).
- `docs/roadmap/backlog/product-backlog.md`: main numbered BL-646…649 first, so this branch's BL-646…649 became
  **BL-658…661** (each with an "Eski numara" note; `docs/records/` keep the old numbers). BL-650…653 unchanged.
- `tests/known-failures.tsv`: the `NoCodeFilePointsIntoDocsOutsideTheFiveFolders` row re-fingerprinted — this branch adds
  4 references into `docs/analysis/contracts/`, the registered K2 exception (#156).
- After merge, CT-APR-01: MOD-0183, 0184, 0185, 0186, 0187, 0190 and 0192 changed after POLICY-START, so the **next
  dispatch** from them needs a version-bound approval row (`scripts/pack_approval.py`). Not a merge blocker.

## Deploy requirements
- Gateway: `Observability:Correlation:SigningKey` (secret, never committed).
- S&OP/Capacity compose only with `DemandFixtures__*` set (non-production).
- Owner-applied grants still pending: BL-652 v2 patch (`docs/roadmap/plans/mvp6-bl652-coupling-decision-01/patches/option1-adminmodules-widen-v2-after-g7.patch`).
- Temporary Admin Shipment grant (Q358) is removed at expiry, as its own small change (plan v9.9 row 6).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
