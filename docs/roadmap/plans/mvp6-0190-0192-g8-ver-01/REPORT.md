---
module: MOD-0190
work_package: WP-LANE-MAIN-SYNC-05
evidence_level: E4
decision: conditional
target: AL-MVP6-0190-0192-G8-VER, VER, HIGH
role: read-only-auditor (Phase A), persisted by CT (Phase B)
mode: strict repository-read-only
modules: [MOD-0190, MOD-0192]
---

# Independent VER — MOD-0190 S&OP + MOD-0192 Capacity tenant UIs — REPORT

    auditor      : Diten read-only-auditor (strict repository-read-only), dispatched 2026-10-18 by CT
    persisted by : CT (gmg-10) — the auditor cannot write; CT re-verified M1–M3 on the tree (below)
    audited      : fcf42be056aff4411c5875197481da488f865acc, from a `git archive` copy in the scratchpad.
                   `git diff --name-only fcf42be05 HEAD -- frontend services` was empty at both start and end
                   (the only later commits are docs plus the owner-applied G7 AuthService change, which is out of scope).

## SOP §22 OVERALL VERDICT: CONDITIONAL

There are 0 Blocker and 0 High findings, plus 3 Medium and 7 Low. The bound surface, tenant/legal-entity isolation,
composition rule, error mapping and l10n are sound. The Medium findings need DEV rework or an owner waiver before acceptance.

## NE results (auditor)

| NE | result | headline evidence |
|---|---|---|
| NE1 surface = scope | PASS (L2) | Six adapter ops each (`SupplyChainSandopPlansController.cs:68-99`, `SupplyChainCapacityPlansController.cs:69-102`). View routes = manifest RoutePaths. Identity/pages/actions = §24. The runtime resolves the **normalized** nav key (`NavNameLocalizer.cs:50,66-78`), so the code's `SANDOPPLANS`/`CAPACITYPLANS` is the form that works. |
| NE2 l10n | PASS | 7/7 parity (S&OP 75, Capacity 93 keys). No empty values, no echo. fr same-words (`Source`, `Version`) are justified. Bridge = `requiredKeys` (93/111, same order). 0 unbridged `t()`/map keys. Nav keys ×7. |
| NE3 error mapping | PASS (L3) | Every service/gate/adapter code is in `PublishedCodes` and `ERROR_KEY`. Unknown codes fall back to `ErrInternalError`. No raw server text is shown. |
| NE4 UAS-001 | PASS server-side; client → M2 | No `.read` → `_AccessDenied` before any surface, with no script section. Actions appear only for held keys. A 403 on load shows the denied card with no redirect. |
| NE5 isolation | PASS (test gap M3) | Tenant comes only from the token. LE comes from `?legalEntityId=` → `X-Legal-Entity-Id`, validated by MDM fail-closed in both gates. No dependence on the `legal_entity_id` claim remains. A missing scope sends nothing. No scope query goes downstream. |
| NE6 contract fidelity | PASS (L5/L6) | Bodies are forwarded byte-exact. One Idempotency-Key per opened form. Decimals stay strings. No polling, no `innerHTML`. Sign-off asks for confirmation. |
| NE7 composition | PASS | Both providers are registered only inside `if (demandSeed is not null)` (`Program.cs:142-146`). Production gets no seed. |
| NE8 secrets/PII | PASS | Only test-only signing literals and a test sentinel. |
| NE9 build/tests | PASS | `node --check` 6/6. Web build 0 errors. Web S&OP/Capacity tests 88/88, l10n guards 29/29, full web suite 2614/2614. Service tests (lane rs on 35190) 139/139. |

## Findings

| id | sev | where | finding | rework |
|---|---|---|---|---|
| M1 | Medium | `SandopPlans/Details.cshtml:66,79`; `CapacityPlans/_EvaluationPanel.cshtml:28` | Pack §23.5 (both packs) requires the workspace tables (Snapshots, Sign-offs, Bottlenecks) on `data-dt-standard="v2"` + `DtDefaults.create()`, client-side. The build uses plain tables, and the G6 records don't disclose this. | Implement the bounded DT v2 profile, or get an owner amendment of §23.5. |
| M2 | Medium | `SandopPlans/details.js:159-165,186-200`; `CapacityPlans/details.js:161-167,194-203` | The `#planName` header stays "Loading…" on 403-on-load, network failure and non-404 failures, above the denied card or alert. This breaks UAS-001 §1 and the three-states rule; the Shipments precedent replaces the title. | Replace or hide the title on every non-success path, and add a test. |
| M3 | Medium | `CapacityContextMiddleware.cs:61-65` | The Capacity gate's MDM outcomes (NotReferenceable → 404, Unavailable → 503) are untested, because the HTTP host always stubs `Valid`. S&OP has the equivalent facts. | Add the gate facts, with a sabotage proof. |
| L1 | Low | `SupplyChainSandopPlansController.cs:18` | Header comment still says "checks it against the token" (stale after R-2). | Fix the comment. |
| L2 | Low | packs §24 nav tables | The packs name `Nav.Page.SANDOP_PLANS`/`CAPACITY_PLANS`, which never resolve. | Correct to the normalized keys (docs). |
| L3 | Low | `SandopRepository.cs:91`; adapter `:176-183` | The service's enveloped 500 `INTERNAL_ERROR` is not a published code, so the adapter remaps it to 503 "retry". | Relay `INTERNAL_ERROR` with its status. |
| L4 | Low | `SandopContextMiddleware.cs:30` | The S&OP gate never fills `RequestContext`, so its logs carry empty correlation/tenant/LE. | Set the logging context. |
| L5 | Low | `CapacityPlans/details.js`, `SandopPlans/details.js` | §23.4/§23.7/§23.8 behaviours missing: (a) Evaluate is not hidden while an evaluation is open; (b) a state conflict does not reload the plan; (c) an evaluation 404 leaves stale content; (d) a bad id in the URL is dropped silently; (e) an S&OP mutation 404/403 does not close the action. The server refuses in each case. | Implement, or get an owner waiver. |
| L6 | Low | `_SignOffOffcanvas.cshtml:49`; Capacity `readRows` | Client mirrors from §23.6 missing: comment ≤ 2000 and the decimal pattern. | Add the mirrors (no tightening). |
| L7 | Low | Views/Models file set | The file set differs from the §23.10 owned paths (single `*PlansIndex` marker, `_CreateEditOffcanvas`, `*Permissions.cs`, …). | Amend §23.10, or rename. |

AUD-001: all 6 write commands write a module-local audit record in-transaction. The missing SupplyChain ledger is
repo-wide and pre-existing (architecture test: 17 commands, no ledger), not a G6 defect.

## CT re-verification (independent, 2026-10-18)

- **M1 confirmed:** MOD-0190 §23.5 states the DT v2 profile verbatim; the views have plain `<table class="table">`.
- **M2 confirmed:** `renderDenied` and the network/non-404 branches never set `planName`.
- **M3 confirmed:** no file under `tests/…/CapacityPlans/` drives `NotReferenceable`/`Unavailable`.
- **Baseline move explained:** the auditor saw `DefaultRolePermissionTemplate.cs` modified mid-run. That was the owner
  applying the G7 package (`git apply`), committed by CT as 40eac559c. It is outside the audited scope.

## Files

`VER-HANDOFF.md` · this `REPORT.md` · `SHA256SUMS` (the audited files at fcf42be05 plus this report).
