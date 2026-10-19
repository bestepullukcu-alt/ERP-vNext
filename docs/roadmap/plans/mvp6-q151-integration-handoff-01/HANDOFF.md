---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# Q151 — integration execution hand-off (to the integration-agent)

    prepared  : 2026-10-18 by CT (gmg-10), measured at HEAD of feature/mvp6-logistics
    for       : @integration-agent (one registered checkout; shared files in sequence; gateway/** and
                tests/** are Protected Paths)
    authority : OD-Q15 **A** (owner, 2026-09-27) — "Final integration in a new registered checkout built
                from the declared base stack (Q143)." Q151 is **READY**, its dependency **Q143 (BASE-STACK
                record v1) is DONE/CT-ACCEPTED**. This is execution, not an owner decision and not a CT lane
                task. Refs: mvp6-q15-status-01/STATUS.md, mvp6-owner-decisions-q19-q15-q03b-q03c-q21-2026-09-27.md.
    goal      : pass the still-unpassed **Stage-2 gate** — the integrated checkout + the five golden-flow
                vertical slices — and record the first **integrated PASS** (superseding Q435's
                VALID_PRE_MERGE / INTEGRATION_STALE classification; no integrated PASS exists yet).

## Current integration state (measured 2026-10-18 — newer than Q435's 2026-10-05 snapshot)

- **Branch vs main: 0 behind, 76 ahead** of `origin/main`. origin/main is an ancestor of HEAD, so the
  origin/main merge that Q435 skipped is effectively a **no-op now** — the branch already contains main.
  (Confirm again at execution time; if main has moved, merge it first — in an app worktree via the host
  `sync_with_base_branch`, not a manual `git pull`.)
- **All five modules compose** in `services/Diten.SupplyChainService/src/.../Api/Program.cs:102-111`
  (ShipmentTrackingPod 0183, ReverseLogistics 0186, Carrier 0184, RoutingLoadPlanning 0185, Claims 0187
  manifest providers + the hosted registration).
- **All 33 tenant UIs are now TRACKED** (Shipments 10, Carriers 6, Loads 5, Returns 6, Claims 6). This
  **supersedes** the stale DCP-009 note that Carriers/Loads/Claims were "working-tree only" — the
  untracked-UI risk Q435 flagged is gone.
- **S&OP (0190) / Capacity (0192)** are NOT composed and have no UI — out of this integration by owner
  decision (fixture-bound, `mvp6-0190-0192-fixture-bound-owner-decision-01`). Do not compose them.

## Prerequisite patches to apply in the integrated checkout (Protected Paths — integration-agent applies)

These are prepared, `git apply --check`-clean, and are the step-2 items that belong in the integrated run:

1. **Gateway explicit routes** — `docs/roadmap/plans/mvp6-gateway-routes-conversion-01/patches/ocelot-explicit-routes.patch`
   (12 shipment-bundle + 10 reserved supply-chain routes on 5066) **and**
   `.../patches/od7-guard-new-test.patch` (the OD-7 route-count guard). Apply **both together**; the guard
   fails against the pre-swap catch-all. Owner ruling: `mvp6-gateway-explicit-routes-owner-decision-01.md`.
2. **BL-652(a) AdminModules** — `docs/roadmap/plans/mvp6-bl652-coupling-decision-01/patches/option1-adminmodules-widen.patch`
   (adds the four logistics ModuleCodes so a tenant Admin holds `mdm.legal-entities.read`, else create 503s).
   Owner ruling: `mvp6-bl652-admin-baseline-coupling-owner-decision-01.md` (temporary, expiry 2026-11-03).
   This is the fix for the legal-entity-validation coupling that would otherwise fail the create steps of
   the golden flows. Run the AuthService `Roles/` suite after.

The Q484 Capacity test-host env-gate and the Q498 version bumps are already in the branch (no action).

## Execution steps

1. Build the registered checkout from the Q143 base stack (BASE `a8a236de` → Q117 → Q121) / current branch
   tip; confirm 0-behind-main or merge main via the host first.
2. Apply the two prerequisite patch sets above; build `Diten.SupplyChainService`, `Diten.ApiGateway`,
   `Diten.AuthService`; run the AuthService `Roles/` and gateway `OcelotConfigurationTests` suites.
3. Run the **five golden-flow vertical slices** through the composed, gateway-fronted stack
   (Shipment/POD, Carrier, Load, Return, Claim), tenant-scoped, with a real sign-in.
4. Record the **integrated PASS/FAIL per flow** and supersede Q435's INTEGRATION_STALE classification.

## Acceptance criteria

- All five golden flows **PASS through the integrated checkout** (gateway → service → persistence),
  tenant-isolated, create steps succeeding (i.e. BL-652(a) applied), undeclared gateway paths 404.
- The gateway guard asserts exactly 12 + 10 routes; the AuthService role tests pass with the widened
  AdminModules.
- Baseline for comparison: the **service-boundary HTTP evidence already PASSES**
  (`docs/records/audits/2026-10/mvp6-golden-flows-http-01/REPORT.md`). Q151 is the higher bar — the same
  flows through the integrated, gateway-fronted, composed stack.

## Known blocker the integration-agent will hit (and the options)

**Driving the golden flows through the UI locally is blocked** — not a code defect
(`memory: lane-local-walls`). Only the `PLATFORM` tenant exists in Platform's registry and
`tenant_login_settings` has 0 rows, so a freshly activated tenant 404s at login-settings and
`set-password` 500s; the one ready SuperAdmin's password reset only arrives by email (capturing that token
was correctly refused as credential exploration). **Options for the integration-agent:**
(a) the owner provisions a working tenant sign-in / seeds `tenant_login_settings`; or
(b) run the integrated slice at the **service/gateway HTTP boundary** (JWT minted for the test tenant),
which is the same bar the HTTP evidence already meets, now re-run through the gateway with the explicit
routes — and record that as the integrated PASS, noting the UI-driven run still needs (a).
CT recommends raising (a) with the owner, since it is the only thing that lets the UI-level slice run.

## What this does NOT include

No CT code or contract change (none is needed — composition and UIs are in the branch). Pack promotion,
permission seeds beyond BL-652(a), and the S&OP/Capacity composition remain out (owner-barred). DC-02…05 and
the l10n Parts B/C are unrelated to this gate.

## Files

- `HANDOFF.md` — this. Entry points it references: the two gateway patches, the BL-652 patch, the Q15 status,
  the BASE-STACK/Q143 record, and the golden-flows HTTP baseline.
