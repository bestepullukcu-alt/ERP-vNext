# WP-MOD0188-V2-OUTBOX-PRODUCER

Status: CT assigned; producer audit and Ali acceptance pending. Module:
MOD-0188 Demand Planning. Writer lane: existing
`feature/sce/mod-0188-demand-planning` checkout only.

## Goal

Verify and, only where evidence shows a gap, correct the MOD-0188 Demand v2
outbox producer against Module Pack AC-D25 and the owned DRAFT event schema.
This work does not authorize event transport, central freeze or AC closure.

## Scope

- Read the current publish/invalidation Mongo transactions, outbox model and
  indexes, DRAFT event schema, AC-D19/D21/D25/D27 and existing evidence.
  Produce a field-by-field matrix before modifying code. Do not duplicate
  already verified coverage.
- Verify eventId, revisionId, Tenant/LegalEntity/series scope, cycle, new
  state, monotonic stateVersion, integrity summary and no demand rows in
  producer payloads. Test replay, conflicting replay, concurrent requests
  and transaction rollback on audit/outbox failure in a real isolated Mongo
  replica set. Do not treat an outbox intent as delivered event.
- For publication replacing an old baseline, identify whether the old
  Superseded transition has an independently verifiable notification/status
  path. The choice of a distinct Superseded event versus approved equivalent
  status change remains a central decision: document the gap; do not invent
  a frozen event or consumer behavior.
- Check producer event schema conformance with positive and negative cases.
  If a mismatch is within MOD-0188 owned code/DRAFT schema and the business
  rule is already approved, make the narrow fix and focused tests. If it
  needs a central/shared decision, stop that portion and report it.

## Boundaries

No live cross-service transport, Gateway/ocelot, MOD-0189, central contract
freeze, Demand v1 changes or edits to AGENTS.md, DCP, domain config,
registry, `.antigravity`, frozen contracts or other services. No AC closed.
Preserve prior staged/unstaged and central user changes. Same checkout and
branch only; no new worktree, branch, staging, commit, push or PR.

## Verification

Use only an owned temporary loopback Mongo replica set on an allowed port,
never operational 27017. Run focused real-Mongo tests and the full MOD-0188
suite with skips reported explicitly; API build and DRAFT JSON schema parse.
Show one relevant red-then-green sensitivity check only if a safe bounded
mutation is possible. Record whether production code changed. Clean only
owned PID/port/temp folders and measure cleanup. Report independently what
is producer-verified, what requires central v2 approval, and what requires
event delivery or MOD-0189 integration.

Stop for frozen-contract break, data-loss risk, protected/shared edit or
unresolved business/security rule. Finish safe independent work first.

## Agent prompt

Use `@[.antigravity/agents/orchestrator.md]`, Module Pack, domain config,
AGENTS.md, `/add-module` and relevant backend, data, integration, security,
testing and code-quality role/rule files. Reading role files in one chat is
not running subagents; report the distinction honestly.
