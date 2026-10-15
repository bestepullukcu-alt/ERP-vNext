# WP-MOD0188-FORECAST-METHODS

Status: partial technical K13 verified (Naive and Seasonal Naive only); five methods and AC-D09/D11 remain open. Module: MOD-0188 Demand Planning. Owner lane: existing
`feature/sce/mod-0188-demand-planning` checkout only.

## Goal

Implement an isolated, deterministic forecasting-method engine behind a .NET
interface for the Module Pack's seven candidates: Naive, Moving Average,
Simple Exponential Smoothing, Holt trend, Seasonal Naive, Holt-Winters and
Bias-corrected Croston. Feed the existing eligibility and rolling-origin
components with verified synthetic weekly fixtures. This is an AC-D09/D11
core slice, not a live ForecastRun or acceptance completion.

## Scope

- First inspect current PlanningService code and package references. Select
  maintained, suitable forecasting/numerical libraries using primary package
  documentation and tests; record license, version and algorithm coverage.
  Prefer a proven implementation for method logic. Do not invent a library's
  capability or silently hand-roll an unsupported forecasting algorithm.
- Check official SAP/Oracle planning documentation for relevant transparency,
  override and method-comparison practices. Record only directly supported
  design implications; do not claim functional parity.
- Use the pack's 26/104-week and intermittent-event gates. Missing, Unknown or
  unresolved stockout is not a zero. Keep the historical as-of boundary and
  52-week horizon. All candidate results remain side-by-side; no automatic
  ranking, winner, fallback, confidence score or publication.
- Preserve the existing WAPE/MAE/bias/MASE and horizon-specific semantics.
  Document method parameters and policy version in the fixture output.
- Cover deterministic reference series, seasonal and intermittent boundaries,
  zero demand, insufficient history and future-data leakage with focused
  tests. Verify critical test sensitivity without leaving a weakened source.

## Boundaries

PlanningService DemandPlanning application and tests only, plus narrowly needed
package references and this owned WP evidence. No frontend, Gateway, central
contracts/registry, frozen Demand v1, `.antigravity`, other services or MOD-0189.
No live cross-service reader, accepted-history claim, Draft method selection
persistency, API or automatic publish in this WP. If a suitable library cannot
cover a method, report that method and continue safe independent methods; do
not mark AC-D09/D11 complete.

Only the existing worktree and branch. Preserve staged/unstaged and central
user changes. Local commit only if isolated MOD-0188 scope is individually
verified; no push or PR.

## Verification and gates

Build PlanningService API, run focused forecast tests and full MOD-0188 suite.
Real Mongo tests must use only an owned temporary loopback replica set on a
free permitted port, with test-named DB; clean only the owned PID/folder.
Report package evidence, exact method coverage, test totals, skipped tests,
changed files, source checkpoint and remaining live-data gaps. CT K13 and Ali
manual acceptance remain separate. Stop for frozen-contract break, data-loss
risk or required edit of protected/shared central files.

## Agent prompt

Use `@[.antigravity/agents/orchestrator.md]` with this WP, the Module Pack,
domain config, AGENTS.md, add-module workflow and relevant data, backend,
integration, testing and code-quality agent/rule files. Reading role files in
one chat is not running subagents; report the distinction honestly.

## CT independent checkpoint

- Current test project and API dependencies rebuilt: 0 warnings, 0 errors.
- Full suite on a CT-owned temporary loopback replica set: 203 passed, 0 failed,
  0 skipped. Owned PID 36612, temporary folder and port 31994 were verified
  absent after cleanup; operational Mongo ports were untouched.
- The fixture engine registers only Naive and Seasonal Naive. The five other
  candidate requests throw explicitly. This is partial method coverage, not
  complete AC-D09/D11 or live ForecastRun acceptance.
- Follow-up candidate to inspect: official NuGet SignalSharp 0.1.10 targets
  .NET 8 and documents Moving Average, Simple Exponential Smoothing and Holt
  trend. Its small adoption and older .NET 8 release require API, numerical,
  license, dependency and maintenance checks before any package adoption.
  Newer 0.1.12 targets .NET 10 and cannot be presumed compatible here.
- Ali approved only a temporary, official SignalSharp 0.1.10 download outside
  the repository for SES/Holt numerical evaluation. This does not authorize
  adding the package to the project or stopping any existing process. Moving
  Average is excluded from this spike: its documented API smooths history,
  not a 52-week forward forecast.
- The isolated SignalSharp spike found SES reference agreement but Holt's
  fixed-parameter first forecast differed from its documentation by about
  0.26. No package was added. Ali subsequently approved a controlled exception
  for in-house .NET implementations of the five remaining methods in MOD-0188.
  Every method must use explicit, versioned parameters, published definitions,
  independent numerical reference cases and focused boundary tests. This does
  not authorize hidden defaults, live accepted-history claims, automatic
  winner selection, new worktrees, push or PR. Ambiguous Croston correction or
  Holt-Winters seasonal variant must be reported, not silently guessed.
- Ali approved the initial variants for the remaining fixture methods:
  additive Holt-Winters with a 52-week seasonal period, and SBA
  (Syntetos-Boylan approximation) for bias-corrected Croston. Multiplicative
  Holt-Winters and SBJ are not approved as interchangeable fallbacks. Exact
  initialization and coefficient rules must be explicit, versioned and tested
  against independent numerical references before either method is enabled.
  The developer reports Moving Average, SES and Holt trend implemented and a
  212/212 suite; this is not yet an independent CT K13 verification. AC-D09
  and AC-D11 remain open, as do accepted-history and live integration gates.
