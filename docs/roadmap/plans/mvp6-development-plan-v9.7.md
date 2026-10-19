# MVP6 development plan v9.7 — 2026-10-18

Supersedes v9.6 (2026-10-17). Everything here is measured; where a number moved, the lane or commit that
moved it is named so the claim can be checked rather than believed. Full working of this session's audit:
`docs/roadmap/plans/mvp6-session-audit-resequence-2026-10-18/AUDIT.md`.

## 1. What changed since v9.6

A 23-commit CT session on `feature/mvp6-logistics` (`b2c383715…1c4a02982`). What it moved:

- **The owner-decision backlog is largely cleared.** Four rulings recorded 2026-10-18: BL-652(a) → Option 1
  (Admin baseline widen, expiry 2026-11-03); gateway routes → A explicit swap + B reserve 10 routes;
  l10n glossary → the **{Claims,Returns} camp** canon; Supplier **DC-01 approved** (0190/0192 placement).
- **Running code moved only in the l10n layer.** l10n Part A + A2 across the five shipment-bundle modules
  (`7211cf3cc`, `078d967c2`) + the six per-module reviews — Err* convergence, 0 plain-infra keys. VER F-1/F-2
  fixes. This is the only production-code change of the session.
- **The six UIs passed an independent VER** (`0aba476f8`): SOP-22 PASS, 0 blocker/high, both findings fixed.
- **0190/0192 reversed scope** (`ad4851e85`): the owner overrode the 2026-10-05 fixture-bound decision to a
  **full working UI**, opened the §22 composition gate (fixture-demand interim), and the wave G1–G6 is
  prepared. **This is a scope expansion — tracked here as Wave 6.1 (§4), additive to the core.**

Branch is **0 behind / 87 ahead** of `origin/main`. All 23 commits are pushed to `origin/feature/mvp6-logistics`;
**none are on `origin/main`** (no PR this session, owner's choice). The merge-to-main decision is still open
and still the owner's (GIT-002 §5).

## 2. Measured state — the honest line: prepared ≫ delivered

v9.6's gate numbers stand unchanged by this session because **the session delivered decisions, specs,
patches and hand-offs — not applied/integrated code**:

| | MET | NOT MET | NOT MEASURED | |
|---|---:|---:|---:|---|
| **published — `origin/main`** | **37** | 9 | 10 | **66.1%** (unchanged; this session's 87 commits are on the feature branch, not main) |
| local/branch, pushed | 43 | 4 | 9 | 76.8% |

**No integrated PASS exists.** Q151 (the Stage-2 gate — the five modules exercised through the composed,
gateway-fronted stack) has not run; only service-boundary HTTP evidence passes. That is the real
"MVP-6 integrated" bar and it is unmet. An authoritative gate recompute (the scorer over the current tree)
is a measurement the integration lane runs; this plan does not invent a new percentage.

**Prepared-but-unapplied, decaying:** the gateway + BL-652 patches are `git apply`-clean but unapplied; the
§22 `Program.cs` composition pins (`a2a216be…`, `50c48a2b…`) are already stale (Q484 moved the file to
`7513971f…`) and drift further the longer they wait. A large prepared backlog is a liability, not an asset,
until it is applied.

## 3. The re-sequence — four parallel lanes (the critical path is execution-bound now)

v9.6's §5 owner queue is closed and doubly stale; do not read from it (use the queue-reality readout +
this session's decision records). Organise the remaining work by what unblocks what:

- **Lane A — apply now, no new dependency.** Apply the gateway (ocelot + OD-7 guard) and BL-652 AdminModules
  patches; integration-agent runs G4 (compose 0190/0192 against fixtures) + G5 (activate the 10 reserved
  routes) + re-pin. Cheapest momentum; owner/integration-agent act.
- **Lane B — owner actions, small and high-leverage.** (1) **Q151 tenant sign-in** (or authorise the
  gateway-HTTP fallback) — the single unlock for the **integrated PASS**, the most valuable missing
  evidence. (2) Name the **SUPPLY-CONSTRAINTS producer owner** so G2 can freeze. (3) The merge-to-main PR
  decision. (4) Seed `demand.plans.consume`.
- **Lane C — cross-team long pole.** MOD-0188 freezes **DEMAND v2** → G3 real-demand swap. Decoupled by the
  fixture interim behind the `IDemandReader` abstraction; nothing in the core or Wave 6.1 interim waits on it.
- **Lane D — build, after Lane A composes.** Wave 6.1 G6 UI (resx/bridge skeleton landed `1c4a02982`), then
  its VER.

**Spine:** Lane A + Lane B.1 → first integrated PASS → **MVP-6 core done**. Wave 6.1 runs in parallel and
does not gate the core.

## 4. Wave 6.1 — MOD-0190 S&OP + MOD-0192 Capacity full UI (additive, not in the core finish line)

Gates, all CT-prepared under `mvp6-0190-0192-ui-wave-01/`: G0 override ✅ · G1 §22 open ✅ · G2
SUPPLY-CONSTRAINTS draft ✅ (needs a producer owner + freeze) · G3 DEMAND-v2 consumer-adapter design ✅ (needs
v2 freeze) · G4 composition spec ✅ (integration-agent applies) · G5 routes · G6 UI (skeleton ✅) · G7 perms ·
G8 VER. Estimates: 0190 52/86/148 h, 0192 58/98/168 h (owner-approved, option A). **Keep MVP-6-"done" = the
five shipment-bundle modules + DemandPlanning integrated & VER'd; Wave 6.1 ships after.**

## 5. Owner queue (what remains — short, because most closed this session)

| id | item | state |
|---|---|---|
| Q151 | tenant sign-in for the integrated run (or authorise HTTP-boundary fallback) | **OPEN — highest value** |
| — | apply the gateway + BL-652 patches (authorise the integration-agent) | OPEN (Lane A) |
| — | merge-to-main PR, or keep deferring | OPEN (GIT-002 §5) |
| G2 | name the SUPPLY-CONSTRAINTS producer owner, then freeze | OPEN (Wave 6.1) |
| Lane C | MOD-0188 freezes DEMAND v2 | OPEN (cross-team) |
| Supplier | DC-02/DC-03 build seams; DC-04/DC-05 deferred (no producer) | scheduled / deferred |

## 6. Discipline — what this session got right, and the standing risk

Right: every owner decision recorded before acting; CT reverted nothing it lacked authority for (Program.cs
stayed the integration-owner's; the BL-652 permission patch was prepared, not applied); both subagent results
(6-UI VER, G6 skeleton) were re-verified against the tree, not taken on the agent's word.

Risk: **preparation without application.** 23 commits, one production-code layer. The plan's next move must be
to apply the ready items (Lane A) and get the integrated PASS (Lane B.1), not to accumulate more prepared
artifacts.

## 7. Inputs a v9.8 should carry

The two durable memories this session produced — the {Claims,Returns} l10n canon and the Supplier DC owner
assignment — plus this audit and the Wave 6.1 gate tracker. Keep the queue-reality method: verify any plan
item against the decision records and the tree before acting on its label.

## 8. Measures

23 commits this session, all pushed to the feature branch, 0 on main. Gate to main unchanged at 37/56
(66.1%). l10n: five shipment-bundle modules converged to the Err* canon (0 plain-infra keys), 6-UI VER PASS.
Wave 6.1: 6 of 8 gates prepared, skeleton code landed. No integrated PASS yet — the one measurement that
would move §2 is Lane A + Q151.
