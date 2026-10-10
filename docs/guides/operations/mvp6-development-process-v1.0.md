# MVP6 development process v1.0 — pilot

Status: **in use for MVP6 from 2026-09-25** at the owner's request ("use these recommendations and improve the development process").
Source: *MVP6 Geliştirme Önerileri Rev2* (24 Sep 2026) and *Individual Developer Performance Rules Rev2* (24 Sep 2026).
Applies beneath `AGENTS.md` and the [Control Tower SOP v2.4](control-tower-sop.md). Where this guide is silent or
conflicts, the SOP and AGENTS.md win. It changes no existing approval, acceptance or exact-hash boundary.

The routine-fix authority clause in §7 is in force for new work packages from 2026-09-25 22:17 (owner decision `MVP6-ROUTINE-FIX-AUTHORITY-OWNER-DECISION-01`).

Working records: [`docs/roadmap/plans/mvp6-process-pilot-01/`](../../roadmap/plans/mvp6-process-pilot-01/README.md).

## 1. Why

Observed in MVP6 (CT audit 2026-09-25): 0 of 8 accepted work packages passed first review; 3 were returned only for
evidence quality; missing sources and wrong routes appeared only when verified parts were combined; browser checks came
late; HELD packages were re-dispatched and produced the same HELD result; 0/9 modules run in one integrated checkout.

The goal is **more independently accepted delivery with less rework** — not more prompts, files or reports.

## 2. Queue states (CT)

Every item in `CT-QUEUE.tsv` has exactly one state:

| State | Meaning | May be dispatched? |
|---|---|---|
| READY | Scope, authority, source baseline, acceptance rows and environment are known | Yes |
| IN-PROGRESS | One lane owns it | — |
| DECISION-REQUIRED | Waiting for an exact owner decision; the decision text is prepared once | **No** |
| HELD | Waiting for another item, input hash or environment capability | **No** |
| BLOCKED | A defect or capability gap stops it | No |
| REWORK | A security defect or affected acceptance evidence reopened it | Yes, as a new version |
| DONE | CT disposition recorded | — |

**Stop rule:** a DECISION-REQUIRED or HELD item is re-dispatched only when the owner decision, an input hash or the
environment capability has changed. The trigger is written in the queue row.

## 3. Module sequence (every work package)

1. **Preflight** — confirm scope, authority, source baseline (HEAD archive + exact overlays), acceptance matrix rows and environment.
2. **Early vertical slice** — run one complete operation through real Auth → Gateway → UI → backend → DB before the rest
   is built. This is where DI, route, view, localization and root defects must surface (SOP §18.0).
3. **Build** the remaining behaviour inside the owned paths only.
4. **Writer self-check** — main flow, security, error and replay boundaries; complete the evidence checklist (§5).
5. **Freeze** the final source (manifest + archive hash).
6. **Independent VER** on the frozen source. The verifier reviews the test plan *during* DEV and adds its own negative
   cases; acceptance runs only on the frozen source. For a critical security or contract finding, tests are not limited
   to the changed files.
7. **One CT disposition** records accepted scope, open rows and effort treatment (credit only with an O/M/P split).

## 4. Single acceptance matrix

Before DEV starts, every criterion has one row: expected **HTTP** result, **browser** result, **DB** result, owner,
evidence type, dependency and READY/BLOCKED. Generic checks (e.g. full-CRUD DataTable conventions) are marked in-scope
or out-of-scope for the bounded package **at the start**, through a change record — never by silent waiver.

## 5. Evidence checklist (writer before hand-off; verifier before verdict)

- Exact source manifest and archive SHA-256; base HEAD; overlay list.
- Native .NET 8 SDK/runtime versions; effective configuration captured before start; no connection to 27017.
- Source → binary → process → browser binding (binary hashes, ports, PIDs).
- DB before/after for every mutation and every negative case.
- Redacted raw evidence (no bearer tokens, cookies or reusable credentials).
- Cleanup record: processes, ports, secrets, Mongo data removed.
- **Incomplete archive?** Apply the HEAD-archive + overlay method before reporting "not runnable".
- Environment evidence is produced with the MVP6 evidence kit (guide: mvp6-evidence-kit-v1.0.md in this folder; scripts: scripts/evidence-kit/).

## 6. Verification by impact

Evidence may be **inherited** only when the change has no impact on its source, contract, configuration, dependency
or fixture. Every reuse and every rerun is recorded in `EVIDENCE-REUSE.tsv` with the reason.
Example (first application): the A12 successor changes `load()` in `Shipments/details.js`, which is the refresh path
used by A08/A09 — so A08/A09 get a targeted browser regression instead of being inherited.

## 7. Authority model

Exact approval boundaries stay. For a **new** work package the owner may add this clause to its approval:

> "Within the stated owned paths and acceptance criteria, defect fixes and their regressions that create no new wire
> behaviour and no shared change are within this work package's authority."

**In force for new work packages approved from 2026-09-25** — [owner decision](../../records/decisions/2026-09/mvp6-routine-fix-authority-owner-decision-01.md). It never covers new contract behaviour, shared security, migration, scope
expansion or an explicit exact-hash constraint, and it does not reinterpret existing approvals.

## 8. Ownership and parallelism

| Role | Output | Success condition |
|---|---|---|
| Control Tower | Acceptance matrix, queue, dispositions | Every open criterion has owner, evidence type and dependency |
| Integration owner | Versioned source + environment manifest | Composition build and route smoke pass; no secrets |
| Module writer | Main flow and negative checks | Real-Auth chain runs early; error paths visible |
| Independent verifier | Acceptance report on frozen source | Fresh vs inherited evidence separated; no silent closure |
| Environment owner | PNG and fault-test environment | Supported method, or an explicit blocker; no restriction bypassed |

Pilot limit: **at most two independent product lanes and one shared-environment lane at a time.**
Auth, Gateway, navigation, localization and `Program.cs` changes go through the single integration owner, in sequence.
Other lanes consume those sources as immutable inputs.

## 9. Reporting and measurement

- Update effort **only** at three events: writer complete, independent VER, CT decision. No small repeat reports.
- Show implementation index and accepted-milestone effort separately; keep the frozen baseline and current forecast apart;
  list unestimated scope.
- Record every milestone event with a timestamp in `MILESTONE-EVENTS.tsv` (task start, writer hand-off, VER, CT decision).
  **No backfill** of past durations with estimates.
- Record active human time (reported or observed) in `TIME-INTERVALS.tsv`; agent run time, environment waits and owner
  waits in their own columns/rows — never added to human hours.
- Weekly indicators: acceptance cycle time (with waits shown separately), first-VER pass rate (numerator/denominator),
  rework share, open items with blocker age and owner, evidence reuse, effort progress.
- Small-sample results are observations; no personal ranking. CT message activity (38 h 50 min to 24 Sep) is not human work time.

## 10. Pilot and review

Pilot scope: Shipment UI closure (A12, A08/A09 regression, remaining rows) and Carrier PNG/CT closure, then use those
flows as the reference for Loads, Returns and Claims UI. Compare like-for-like work only. If cycle time and rework fall
without lowering the acceptance standard, extend the process to the other modules; CT reviews this after the pilot.

## 11. `.antigravity` gates every lane follows (added 2026-09-26)

Source: `AGENTS.md`, `.antigravity/agents/orchestrator.md`, `.antigravity/workflows/add-module.md`, `.antigravity/rules/GEMINI.md`;
check record `docs/records/audits/2026-09/mvp6-antigravity-compliance-check-2026-09-26.md`.

1. **Role line first.** Each lane opens with the agent it applies, e.g. `🤖 Applying knowledge of @frontend-ui-ux` (GEMINI.md), and reads that agent file and the rules its task needs (AGENTS.md §6.1 map).
2. **Pack gate.** No code from a missing or `draft` pack (orchestrator rule 2).
3. **Phase 1.5 before code.** The 9-row architecture table is filled and approved by the owner through the question tool before Phase 2.
4. **Self-registration.** Every tenant-assignable module ships a `ModuleManifestProvider` mirroring the real UI pages and actions, RoutePath-derived scope, completeness tests in both directions, a reconcile-state test, and `Nav.Domain/Module/Page.*` keys in 7 languages. No module closes without it.
5. **UI rules.** Golden reference templates copied exactly; `verify_datatable_page.py --reference slim|compact` and `quality-gate-datatable.md` before delivery; bounded OUT rows only through recorded owner decisions.
6. **Gateway.** `ocelot.json` changes only by the integration owner (integration-agent); a missing route is a blocker, not a local fix.
7. **Phase 4.5 runtime smoke** through Channel A (MCP browser), B (Playwright) or C (owner check); never claimed without evidence.
8. **Phase 6 closure.** API narrative, illustrated user manual, per-module audit report, backlog closure record (⚠️ partial until verified live) and the pack's acceptance boxes — with file paths in the report.
9. **Backlog gate.** Anything deliberately deferred is added to `docs/roadmap/backlog/product-backlog.md` before the work counts as done.
10. **Records are never edited.** Corrections and later dispositions are new dated records (docs-organization K4).
11. **Git.** GIT-002 applies to every lane: no staging, commit or push without explicit owner approval; never `git add -A` or `git add .`.

## Revision history

- 2026-09-25 22:14 — v1.0 pilot issued.
- 2026-09-25 22:17 — §7 routine-fix clause marked in force (owner decision).
- 2026-09-26 02:02 — §11 added: `.antigravity` gates, at the owner's request "check antigravity rules and continue them".
