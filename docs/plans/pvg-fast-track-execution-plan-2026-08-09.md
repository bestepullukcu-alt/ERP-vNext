# PVG Fast-Track Execution Plan - 2026-08-09

> **Supersedes the planning posture of** [`docs/audits/pvg-development-replanning-audit-2026-08-09.md`](../audits/pvg-development-replanning-audit-2026-08-09.md).
> It does **not** contradict that audit's findings. Every blocker it recorded is still real. This plan changes
> **how those blockers are closed**, not whether they exist.
>
> **Owner:** NY (ny@gmgroup.ch) - PVG system owner / Enterprise Architect
> **Date:** 2026-08-09
> **Governance basis:** DCP-004 (approved 2026-08-09), OD-2/OD-4/OD-5/OD-6/OD-7 decision records
> ([`DCP-004-open-decision-records-2026-08-09.md`](../../execution/portfolio/delivery-capability-packs/DCP-004-open-decision-records-2026-08-09.md))

---

## 1. Why the audit said "stop" and why development can start anyway

The audit concluded: *"Do not begin PVG implementation now."* That conclusion is correct **under one implicit
assumption** - that "implementation" is a single gate covering both writing code and running it in production.

MOD-0230's own module pack does not make that assumption. Its owner-evidence table already separates:

| Gate | What it authorizes | Caveat already written in the pack |
|---|---|---|
| MOD-0230 `approved` / `ready-for-dev` | Backend, tests, UI may be built | - |
| MOD-0230 **operational runtime authorization** | Production/validated operation | *"Local non-operational scaffold only; not operational runtime, not production use, not supplier qualification, not validation approval."* |

**This plan splits those two gates and drives them at different speeds.** The first gate is closeable this week.
The second stays closed until the real foundation modules land. Nothing regulated is waived.

---

## 2. Verified blocker map - paper status vs actual runtime

The audit listed nine REG-PV-BASE dependencies as blockers without distinguishing which already exist as
merged, tested code in this repo. That distinction is the entire fast-track.

| Dependency | Blueprint | Registry row | Runtime code present in repo | Real blocker for MOD-0230 build? |
|---|---|---|---|---|
| MOD-0018 RBAC / ABAC | W-1 | `ready-for-dev`; FU10a, FU10b, FU12 `implemented` | **Yes** - `Diten.Platform.Common/Authorization/`: `IEntitlementChecker`, `ITenantAuthorizationContext`, `IDataScopeResolver`, `EntitlementCheckResult`, `EntitlementDenyReason`, `IEntitlementAuditSink`, `RequiresFeatureAttribute`, `RequiresModuleAttribute` | **No** - consumable today |
| MOD-0021 Audit Trail | W-1 | `ready-for-dev / implemented evidence` | **Yes** - `Diten.Platform`: `AuditEvent`, `IAuditEventRepository`, `AuditBehaviorOptions`, audit outbox worker, actor redaction, export serializer, `Faz1AuditCoverageTests` | **No** - consumable today |
| MOD-0041 Logging & Monitoring | W-1 | `approved` | **Yes** - `Diten.Platform.Common/Observability/`: `SensitiveDataRedactor`, `SensitiveDataLogEventEnricher`, `ObservabilityOptions`, health checks | **No** - consumable today |
| MOD-0040 Canonical ID & Correlation Standard | W-2 | ⚠ registry row is a **deprecated alias** pointing to MOD-0288 | **Partly** - `ICorrelationContext`, `CorrelationContext`, `CorrelationIdMiddleware` exist | **No for code**, but the registry entry is wrong (see §7, Finding F-A) |
| Tenant isolation | - | - | **Yes** - `ITenantContext`, `TenantResolutionMiddleware`, `BaseEntity`/`TenantScopedEntity` | **No** |
| **MOD-0019 Data Masking & Row/Field Security** | W-3, Build, `SEC-DATA-BUNDLE` | ✗ **no registry row at all** | **No** | **YES** |
| **MOD-0023 Workflow Designer** | W-1 | `review / planned` | **No** | **YES** |
| **MOD-0031 Evidence Linking Service** | W-4, Build, `EVIDENCE-LINK` | `review / planned` | **No** | **YES** |
| MOD-0004 Metric & Semantic Registry | W-2 | ✗ **no registry row** | No | Only MOD-0234 |
| MOD-0063 Data Warehouse / Lakehouse | W-3 | ✗ **no registry row** | No | Only MOD-0234 |

**Result: five of the eight REG-PV-BASE legs are already merged code. Three are genuinely missing.**
The fast-track is entirely about how those three are handled.

---

## 3. The unlock - REG-PV-BASE consumption ports with fail-closed defaults

### 3.1 The problem with waiting

MOD-0019, MOD-0023, and MOD-0031 are separate platform modules owned by `platform-shared-services`. Two of
them are not even registered. Waiting for all three to be delivered as production modules before MOD-0230 can
be *written* puts PVG behind three platform deliveries it does not control. That is the reason the audit's
sequential plan has no credible date.

### 3.2 What MOD-0230 actually requires

Read the pack's own failure-path list. Every required behaviour for these three dependencies is a
**denial** behaviour:

- *"Missing MOD-0019 policy for a sensitive field → field omitted/masked or operation denied; no permissive fallback."*
- *"Workflow/Inbox unavailable → triage/routing transition blocked; no untraceable routing."*
- *"Evidence-link unavailable → fail-closed or explicitly degraded behavior; no fake evidence pack."*

A deny-by-default adapter **literally satisfies** every one of these. MOD-0230 does not need MOD-0019/0023/0031
to exist in order to be built and fully tested. It needs a **port** it can call and a default implementation
that denies.

### 3.3 The decision

MOD-0230 defines three **consumption ports inside its own boundary** (`Diten.PvgService`), each with a
deny-by-default adapter registered as the DI default:

| Port (PVG-owned) | Backed later by | Default adapter (fail closed) |
|---|---|---|
| `IPvgFieldSecurityPolicy` | MOD-0019 `SEC-DATA-BUNDLE` client | `DenyAllFieldSecurityPolicy` |
| `IPvgWorkflowTransitionGate` | MOD-0023 Workflow/Inbox v1 client | `DenyAllWorkflowTransitionGate` |
| `IPvgEvidenceLinkPort` | MOD-0031 `EVIDENCE-LINK` client | `DenyAllEvidenceLinkPort` |

This is an **anti-corruption layer, not a reimplementation**. The ports own no policy data, no workflow
engine, and no evidence storage. They own only the shape of the call and the fail-closed default. When the real
modules land, a client adapter replaces the default in one DI registration line; no handler, entity, validator,
or view changes.

To make the module demonstrable before the real modules exist, each port also gets **one explicitly
non-production adapter**, config-gated and hard-refusing to activate when `ASPNETCORE_ENVIRONMENT=Production`:

| Non-production adapter | What it does | Why it is honest |
|---|---|---|
| `PvgStaticFieldPolicy` | Applies the 16-field sensitivity matrix already written in the MOD-0230 pack | The matrix is PVG's own input to MOD-0019, not a masking engine |
| `PvgStaticTransitionGate` | Applies the triage state set defined in the MOD-0230 pack | Enforces PVG's own state rules; owns no queue, assignment, or SLA |
| `PvgPendingEvidenceStore` | Records evidence requirements as **pending**, never as satisfied | Cannot fabricate an evidence pack; blocks handoff exactly as required |

Full specification: [`docs/specs/pvg-reg-pv-base-port-contracts-v1.md`](../specs/pvg-reg-pv-base-port-contracts-v1.md).

### 3.4 What this does not buy

The non-production adapters **do not** close the operational runtime gate. MOD-0230 cannot go to production,
supplier qualification, or validation until MOD-0019, MOD-0023, and MOD-0031 ship as real modules and their
owners sign the corresponding evidence rows. That gate is unchanged from the audit.

---

## 4. Scope surgery - what leaves slice 1

Two of the eight MOD-0230 owner-evidence approvals block on owners who do not exist yet and are **not needed
for the intake baseline**. Removing their surfaces from slice 1 removes them from the critical path entirely.

| Surface | Slice 1 decision | Reason |
|---|---|---|
| Create, Read, List, Detail, Update | **In** | Core intake baseline |
| Triage, Route | **In** | Core triage baseline; gated by `IPvgWorkflowTransitionGate` |
| **Archive / Void** | **Out of slice 1** | Requires `PVG-MOD0230-RetentionLegalHoldArchiveVoid-v1`; no compliance/legal-hold owner assigned. Pack already states archive/void is optional until retention approval |
| **Export (incl. masked export)** | **Out of slice 1** | Requires an approved MOD-0019 masking policy; masked-only export cannot be proven without a real masking owner |
| Delete / bulk delete | **Never** | Already locked out by the pack |
| Any AI behaviour | **Out** | Governed-AI gates absent |

Net effect on the 8-row evidence table:

- **4 rows closeable now** against merged platform code: RBAC, AuditEvent, TraceBundle, ObservabilityErrorModel.
- **3 rows satisfied by fail-closed ports** for the build gate: FieldSecurity, WorkflowTransitionGate, EvidenceLink. These stay **open** for the operational runtime gate.
- **1 row removed from slice 1 scope**: RetentionLegalHoldArchiveVoid.

---

## 5. Critical path

Working days from the approval date. Lane A is the critical path; lanes B and C run fully in parallel and must
start on Day 1 because they have long external lead times.

### Lane A - MOD-0230 to running code

| Day | Work | Exit condition |
|---|---|---|
| **0** (done today) | Approve DCP-004. Sign OD-2, OD-4, OD-5, OD-6, OD-7. Reconcile module ID registry (add MOD-0019, MOD-0230, MOD-0231, MOD-0232, MOD-0234, MOD-0004, MOD-0063 rows). | DCP-004 `approved`; registry rows exist |
| **1** | Resolve MOD-0230 frontmatter: `service: Diten.PvgService`, `owner: NY`, `target`. Fill the 4 closeable evidence rows against merged platform code, with file-level citations. Record slice-1 scope surgery in the pack. Promote pack to `ready-for-dev` with an explicit **non-production build authorization** section. | MOD-0230 `ready-for-dev`; operational runtime still `[ ]` |
| **2-3** | Build the REG-PV-BASE port package: 3 interfaces, 3 deny-by-default adapters, 3 non-production adapters, DI extension, production-environment refusal guard, conformance test suite. **No business logic.** | Port conformance tests green; deny-by-default proven |
| **4-6** | MOD-0230 backend. `Diten.PvgService` scaffold (port **5011**), `Features/CaseIntakeTriage/` CQRS per Golden Reference, 16-field entity on `EntityBase`, Mongo repository + indexes, validators, tenant isolation, audit wiring, correlation propagation. Commands: `Create`, `Update`, `Triage`, `Route`. Queries: `GetList`, `GetById`. **No `Delete`, no `BulkDelete`, no `Archive`, no `Export`.** | Backend builds; unit tests green |
| **7-8** | Failure-path suite - all 12 paths in the pack, plus cross-tenant, missing-policy, raw-PHI-leak scans over logs/traces/metrics/audit payloads/validation errors. | Every failure path proven to fail closed |
| **9-10** | Gateway route (integration-agent task) + tenant UI Compact set under `Views/Pharmacovigilance/CaseIntakeTriage/`, `_LayoutTenantShell`, same-origin MVC proxy, l10n resources. DataTable verifier run. | UI renders; DataTable verifier passes |
| **11** | Reconcile DCP-004 §20, module registry status, platform delivery board. | MOD-0230 `in-progress` → `done` for slice 1 |

### Lane B - MedDRA licensing (starts Day 1, long lead)

MOD-0232 is gated on a commercial licence, not on code. MedDRA is currently at **version 29.0, released
1 March 2026**, on a twice-yearly cycle (1 March / 1 September), administered by the MSSO. Subscription is
tiered by organisation type and revenue and requires an executed agreement before any term data may be
imported, stored, displayed, or exported.

| Day | Work |
|---|---|
| 1 | Open MSSO subscription enquiry; identify subscription tier and fee band for the organisation |
| 1-20 | Legal review and execution of the subscription agreement |
| on execution | Record licence ID, version, and permitted-use envelope in OD-5; only then may MOD-0232 leave `draft` |

**This is the true long pole for MOD-0232.** Starting it on Day 1 costs nothing and can save weeks.

### Lane C - real MOD-0019 / MOD-0023 / MOD-0031 (starts Day 1, unblocks production only)

| Day | Work |
|---|---|
| 1 | Raise MOD-0019 with `platform-shared-services`: it has no registry row despite being Blueprint W-3 / Build / `SEC-DATA-BUNDLE`. Add the row; request a minimal masking module pack |
| 1 | Hand the MOD-0230 16-field sensitivity matrix to the MOD-0019 owner as the first concrete consumer requirement |
| 1 | Hand the MOD-0230 triage state set and route-target requirement to the MOD-0023 owner |
| 1 | Hand the MOD-0230 evidence-completeness requirement to the MOD-0031 owner |
| ongoing | Each real module replaces one deny-by-default adapter and closes one evidence row |

---

## 6. Gate model after this plan

```text
DCP-004 approved  ────────────────┐
                                  │
MOD-0230 ready-for-dev  ──────────┼──► BUILD + TEST AUTHORIZED (non-production)
  = 4 real evidence rows closed   │      · Diten.PvgService, port 5011
  + 3 fail-closed ports in place  │      · backend, tests, gateway, tenant UI
  + retention scope removed       │      · local/dev/CI only
                                  │
MOD-0019 real ────┐               │
MOD-0023 real ────┼───────────────┴──► OPERATIONAL RUNTIME AUTHORIZED
MOD-0031 real ────┘                      · still BLOCKED, unchanged from the audit
Retention/legal-hold owner ───────┘      · required before production/validation
```

---

## 7. Findings this plan adds to the audit

| # | Finding | Impact | Action |
|---|---|---|---|
| **F-A** | **MOD-0019 has no row in `execution/registries/module-id-registry.md`** despite being Blueprint-canonical (W-3, Build, `SEC-DATA-BUNDLE`). Four PVG packs and DCP-004 name it as the masking owner. The identity gate passes only because the Blueprint workbook carries it. | `PVG-MOD0230-FieldSecurity-Contract v1` can never be signed - there is no registered owner module to sign it | Add the registry row on Day 0; raise ownership with `platform-shared-services` |
| **F-B** | **MOD-0230, MOD-0231, MOD-0232, MOD-0234 have no registry rows and no master-plan rows.** | Execution cannot be tracked; DCP-002 reconciliation is incomplete | Add registry rows on Day 0 |
| **F-C** | **MOD-0004 and MOD-0063 have no registry rows** but are cited as *hard* MOD-0234 runtime gates. | MOD-0234 cannot even reach contract-ready; the gate references unregistered modules | Add registry rows; keep MOD-0234 contract-only (unchanged) |
| **F-D** | **`MOD-0040` in the registry is a deprecated alias to MOD-0288** (Organization/Person/Position), but all four PVG packs use "Blueprint MOD-0040 / TRACE-BUNDLE" for canonical ID and correlation. Blueprint MOD-0040 is *Canonical ID & Correlation Standard*. | Two different MOD-0040 meanings in one repo. MOD-0230's pack already warns about this, but the registry row still contradicts the Blueprint | Add a registry note distinguishing Blueprint MOD-0040 (correlation standard) from the deprecated repo alias |
| **F-E** | **Proposed MOD-0230 gateway route violates NET-001.** The pack proposes upstream `/api/v1/pharmacovigilance/case-intake-triage`; NET-001 requires upstream `/api/{resource}` with the `v1` prefix on the **downstream** template only. | Route would not match repo convention | Upstream `/api/pv-case-intake-triage`; downstream `/api/v1/pv-case-intake-triage` |
| **F-F** | **Port band is nearly exhausted and `.antigravity/rules/ports.md` is stale.** Doc lists up to 5058; 5059 (MDM) and 5060 (HCM) are live in `launchSettings.json` and `ocelot.json` but undocumented. | Next service picks a colliding port | Assign `Diten.PvgService` = **5011** (verified free). `ports.md` is a protected path - it needs explicit approval to edit |
| **F-G** | `services/Diten.PvgService/` exists as **ignored `bin`/`obj` output only** with no tracked source. | Confirmed by the audit; becomes a real collision the moment the service is scaffolded | Delete the stale generated folder immediately before Day 4 scaffolding |

---

## 8. What stays blocked - unchanged from the audit

- **MOD-0230 operational runtime / production / validation.** Gated on real MOD-0019, MOD-0023, MOD-0031 and a retention/legal-hold owner.
- **MOD-0230 archive, void, and export surfaces.** Out of slice 1 by decision; require their own owner evidence.
- **MOD-0231 implementation.** Pack stays `draft` until MOD-0230 slice 1 handoff contract is built and tested.
- **MOD-0232 implementation.** Pack stays `draft` until the MedDRA licence is executed. No MedDRA terms in source, fixtures, seed, or test data under any circumstances.
- **MOD-0234 runtime.** Contract-only. `shell: none`, `golden_reference: none` unchanged. MOD-0004 and MOD-0063 are not merely unbuilt - they are unregistered.
- **All AI behaviour.** Governed-AI gates absent.

---

## 9. Risks introduced by fast-tracking

| Risk | Mitigation baked into this plan |
|---|---|
| Non-production adapters leak into production | Every non-production adapter throws at startup when `ASPNETCORE_ENVIRONMENT=Production`; a conformance test asserts this |
| Ports drift into reimplementing MOD-0019/0023/0031 | Ports are interface + deny default only. Adapter line count is capped and reviewed; no policy storage, no workflow engine, no evidence store |
| "Built" is mistaken for "authorized" | The operational runtime evidence row stays `[ ]` and is called out in the pack, DCP-004 §20, and this plan |
| MOD-0019 never gets an owner | Raised on Day 1 as a registry defect (F-A), not as a PVG request |
| Slice-1 scope creeps into archive/export | Forbidden command and route names are enumerated in the pack; verifier checks route families |

---

## 10. Immediate next actions

1. **Delete** the stale ignored `services/Diten.PvgService/bin` and `obj` folders (F-G).
2. **Confirm** port 5011 and request approval to update the protected `.antigravity/rules/ports.md` (F-F).
3. **Start the MedDRA MSSO subscription enquiry today** - Lane B is the longest external dependency.
4. **Raise MOD-0019's missing registry row and ownership** with `platform-shared-services` (F-A).
5. **Begin the REG-PV-BASE port package** - it is the only thing between here and MOD-0230 handler code.

---

## Sources

- MedDRA release cadence and current version: [English MedDRA Version 29.0 is now available for download](https://www.meddra.org/news-and-events/news/english-meddra-version-290-now-available-download), [What's New with MedDRA Version 29.0 and the MSSO](https://files.meddra.org/www/Training%20Materials/2026/Materials/001363_Whats_New_with_MedDRA_V29.0.pdf)
- MedDRA subscription tiers and process: [Subscription Types](https://www.meddra.org/subscription/subscription-type), [Process](https://www.meddra.org/subscription/process)
