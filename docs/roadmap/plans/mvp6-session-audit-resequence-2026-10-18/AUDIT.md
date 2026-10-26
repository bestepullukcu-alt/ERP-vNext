---
module: TOOLING
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
commit:
---

# MVP-6 — session audit & re-sequencing (2026-10-18)

    author    : CT (gmg-10) · measured from the 21 commits of this session + the live gate state
    purpose    : (1) audit what actually moved; (2) say how the plan/sequence should change. The v9.6 plan
                already trailed reality (see mvp6-queue-reality-readout-01); this session changed the
                landscape enough to warrant a v9.7 structure.

## 1. What moved this session (21 commits)

**Owner decisions RULED (were the biggest backlog):**
- BL-652(a) → Option 1, expiry 2026-11-03 · gateway routes → A swap + B reserve · l10n glossary →
  {Claims,Returns} camp · Supplier DC-01..05 assigned + **DC-01 approved** · 0190/0192 **override** (full
  working UI) + §22 gate **opened** (fixture-demand interim).

**Applied as running code (frontend only):**
- l10n Part A + A2 across the five shipment-bundle modules (32 + 26 files): key renames + value fixes +
  Err* convergence; 0 plain-infra keys. The six per-module l10n reviews (56 fixes). **These are the only
  production-code changes this session.**
- VER F-1 + F-2 fixes (l10n guards, DemandPlanning @Json.Serialize).

**Independent verification:**
- 6-UI VER: SOP-22 **PASS**, CT-reverified; both findings closed.

**Prepared, NOT yet applied/executed (the bulk):**
- Patches: gateway ocelot + OD-7 guard; BL-652 AdminModules. Specs/designs/hand-offs: Q151 integration,
  6-UI VER (done), DEMAND-v2 consumer-acceptance + G3 adapter design, Supplier DC-01 governance-diff,
  0190/0192 wave G1–G6, SUPPLY-CONSTRAINTS draft.

## 2. The honest state: prepared ≫ delivered

**Almost everything this session is PREPARED, not DELIVERED-live.** The branch's *running* surface moved
only by the l10n layer. Decisions, patches, specs and hand-offs are ready, but the execution steps that make
them real have not run, because they are gated on owner-applies, the integration-agent, a tenant sign-in, or
a cross-team freeze. Concretely:
- **No integrated PASS exists** — Q151 (the Stage-2 gate) has not run; only service-boundary HTTP evidence
  passes. This is the real "MVP-6 integrated" bar and it is unmet.
- The two ready patches (gateway, BL-652) are `git apply`-clean but unapplied; the §22 Program.cs pins are
  already stale (Q484 moved the file) and will drift further the longer they wait.
- 0190/0192 are still uncomposed (routes 404) — the override/§22/G4-spec authorise it but nothing is applied.

**Risk:** a large prepared backlog decays. Hashes drift, context is lost, and "prepared" silently becomes
"stale." The plan should push the ready items into execution soon, not accumulate more preparation.

## 3. The biggest plan change: 0190/0192 scope reversal

The 2026-10-05 decision had 0190/0192 **fixture-bound, deferred, no UI** — out of MVP-6's moving set. This
session the owner **reversed** that to a **full working UI**, which pulls in a multi-gate program:
composition (G1/G4), the SUPPLY-CONSTRAINTS contract that did not exist (G2), DEMAND v2 uptake (G3,
cross-team), the UI build (G6, est. 52–168 h each). **This materially expands MVP-6 scope.** The plan must
decide explicitly: is the 0190/0192 full UI *inside* MVP-6 (which pushes the MVP-6 finish line out
substantially), or a **named follow-on wave** after the five-module core ships? Recommendation: **follow-on
wave** — keep MVP-6 "done" = the five shipment-bundle modules + DemandPlanning integrated & VER'd; track
0190/0192 as Wave 6.1.

## 4. Recommended re-sequence (v9.7 structure) — four parallel lanes

The critical path is now **execution-bound, not decision-bound**. Organise by what unblocks what:

- **Lane A — ready to execute now (no new dependency):** apply BL-652 + gateway patches; run the
  integration-agent on G4 (compose 0190/0192 against fixtures) + G5 (activate reserved routes) + re-pin.
  Dispatch nothing new that needs an owner. *This is where momentum is cheapest.*
- **Lane B — owner actions (small, high-leverage):** (1) the **Q151 tenant sign-in** (or authorise the
  gateway-HTTP fallback) — this is the single unlock for the **integrated PASS**, the most important missing
  evidence; (2) **name the SUPPLY-CONSTRAINTS producer owner** (G2 freeze); (3) seed permissions
  (demand.plans.consume).
- **Lane C — cross-team long pole:** **MOD-0188 freezes DEMAND v2** → G3 real-demand swap. Everything
  "fully real" for 0190/0192 waits here. **Decouple it:** the fixture-demand interim lets the 0190/0192 UI
  ship visibly without it; v2 swaps in later behind the `IDemandReader` abstraction.
- **Lane D — build (after Lane A composes):** G6 UI for 0190/0192 (resx/bridge skeleton in flight), then its
  own VER.

**Dependency spine:** Lane A → Q151 integrated PASS (needs Lane B.1) → MVP-6 core done. 0190/0192: §22(done)
→ G4 compose (Lane A) → G5 routes (Lane A) → G6 UI (Lane D) → VER; G3 real demand (Lane C) swaps in whenever
it lands, independently.

## 5. The single most important next action

**Get the Q151 tenant sign-in (Lane B.1) and apply the two ready patches (Lane A).** Together they produce
the first integrated PASS and make the five-module core genuinely *done*, not just *prepared*. Everything
else (0190/0192 wave, DEMAND v2) is additive and can run in parallel without blocking the core.

## 6. What a v9.7 should drop / keep

- **Drop:** reading from v9.6's §5 queue — it is closed (readout) and now doubly stale after this session.
- **Keep & elevate:** the queue-reality-readout method (verify a plan item against decision records + the
  tree before acting). Add the two memories this session produced as plan inputs: the {Claims,Returns} l10n
  canon and the Supplier DC owner assignment.
- **Add:** the 0190/0192 Wave 6.1 as a separate tracked item with its G1–G8 gates, explicitly additive.

## Files

- `AUDIT.md` — this. Inputs: the 21 session commits, the wave plan (`mvp6-0190-0192-ui-wave-01/`), the VER
  report, the queue-reality readout, and the owner decisions recorded 2026-10-18.
