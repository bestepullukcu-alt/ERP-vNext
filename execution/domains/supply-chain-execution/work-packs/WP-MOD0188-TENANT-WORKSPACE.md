# WP-MOD0188-TENANT-WORKSPACE

Status: CT assigned; implementation and Ali acceptance pending. Module:
MOD-0188 Demand Planning. Writer lane: existing
`feature/sce/mod-0188-demand-planning` checkout only.

## Goal

Build the first honest tenant Demand Planning workspace for manual review.
Prioritize AC-D28 and the already implemented manual Draft and revision-read
flows. This is not live-source integration or acceptance of any AC.

## Scope

- Inspect the Module Pack, current PlanningService API, owned Demand v2 DRAFT,
  and existing tenant MVC, localization, unauthorized-surface and frontend
  conventions before editing. Use `_LayoutTenantShell`, not the frozen layout.
- Provide a compact, usable workspace with one LegalEntity selector, cycle and
  revision navigation, 13 visible weeks with access to all 52, explicit
  zero/Missing/Unknown states, Draft edit/reason, review decisions and revision
  status/history where the owned API actually supports them. Do not fabricate
  missing forecast, accepted history or external-source behavior.
- Keep Demand permissions distinct. An unauthorized visitor sees only the
  prescribed explanation, not a workspace skeleton. A missing or unverifiable
  company scope reveals no company content. A stale selection never grants
  access; server-side checks remain authoritative.
- Use all seven tenant RESX languages (en, tr, fr, es, zh, ar, ru), including
  errors and Invalidated historical warnings. Verify key parity. Keep client
  messages localized through the repository's localization bridge.
- Normal frontend traffic must use Gateway 5000. The Gateway route and live
  Platform company-list contract are not approved here: never call port 5068
  directly or embed fake production data. If a real route/source is unavailable,
  render a clear unavailable state; use explicitly isolated fixtures only in
  tests and preview. Record the exact integration dependency for CT.

## Boundaries

No Gateway/ocelot edits, live cross-service calls, MOD-0189, Demand v1 changes,
central contract freeze or changes to AGENTS.md, DCP, domain config, registry,
`.antigravity`, frozen contracts or other services. Do not mark ACs complete.
Do not make a DataTable the primary workspace; an added DataTable would require
its own Golden Reference decision and verifier.

Preserve prior staged/unstaged and central user changes. Same existing checkout
and branch only. No new worktree, branch, staging, commit, push or PR.

## Verification and gates

Build frontend and affected PlanningService projects. Test permission/no-scope
states, one-company selection, 13/52-week navigation, long labels and mobile
widths, zero/Missing/Unknown distinctions, Draft reason/validation and the
Invalidated warning with fixtures. Test direct service-port absence and Gateway
boundary. Check seven-language RESX parity and localization. Run browser
smoke at desktop and mobile sizes using an isolated test fixture; report what
cannot be smoke-tested through the real Gateway. Run relevant existing tests
and report pass/fail/skip separately; preserve prior Mongo evidence. Include
screenshots only as test evidence, not as proof of live integration.

Stop for a protected/shared central edit, frozen-contract break, data-loss
risk, or unresolved business/security rule. Complete safe independent work
first and report the exact blocker. CT K13 and Ali E4 are separate gates.

## Agent prompt

Use `@[.antigravity/agents/orchestrator.md]`, the Module Pack, domain config,
AGENTS.md, `/add-module` and relevant frontend, integration, security,
localization, testing and code-quality role/rule files. Reading role files in
one chat is not running subagents; report the distinction honestly.
