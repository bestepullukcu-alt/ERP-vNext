---
id: DCP-004-ODR
name: DCP-004 Open Decision Records
type: Decision Record Set
parent: DCP-004
status: approved
owner: NY (ny@gmgroup.ch)
domain: pharmacovigilance
decided: 2026-08-09
---

# DCP-004 - Open Decision Records (OD-2, OD-4, OD-5, OD-6, OD-7)

> **Artifact type:** Owner decision records supporting DCP-004 §18. This file records decisions only. It is
> **not** a Delivery Capability Pack, **not** a module pack, and authorizes **no** runtime work by itself.
> Runtime authority still flows through DCP-004 status plus each member module pack's own gate.

**Approver:** NY (ny@gmgroup.ch), PVG system owner / Enterprise Architect
**Decision date:** 2026-08-09
**Effect:** closes the five decisions that blocked DCP-004 promotion from `draft`.

| Decision | Title | Verdict | Blocks released |
|---|---|---|---|
| OD-2 | W-3A0 foundation remediation scope and owner | **DECIDED - split into Lite / Full** | MOD-0230 `ready-for-dev` |
| OD-4 | MOD-0231 Signal Minimum Scope state model | **DECIDED - minimum lifecycle fixed** | MOD-0231 pack completion (pack stays `draft`) |
| OD-5 | MedDRA source, licence, versioning, import policy | **DECIDED - MSSO subscription required; procurement started** | Nothing yet; MOD-0232 stays `draft` until the licence is executed |
| OD-6 | MOD-0234 data product and semantic metric minimum gates | **DECIDED - deferred with explicit entry conditions** | Nothing; MOD-0234 stays contract-only |
| OD-7 | Build / buy / partner strategy and integration boundary | **DECIDED - hybrid, dedicated `Diten.PvgService`** | MOD-0230 `service` frontmatter |

---

## OD-2 - W-3A0 foundation remediation scope and owner

**Question (DCP-004 §14.3):** What exact scope belongs to W-3A0 foundation remediation, and which team owns
closing it?

### Decision

W-3A0 is split into two workstreams with different owners and different gates.

**W-3A0-Lite - PVG consumption ports.** Owner: **NY / PVG**. Delivered inside `Diten.PvgService`.
Scope: three consumption ports (`IPvgFieldSecurityPolicy`, `IPvgWorkflowTransitionGate`, `IPvgEvidenceLinkPort`),
each with a deny-by-default adapter registered as the DI default, plus one explicitly non-production adapter
per port that hard-refuses to activate in a Production environment, plus a conformance test suite.
**Gate released:** MOD-0230 `approved` / `ready-for-dev` - i.e. build and test authorization only.

**W-3A0-Full - real platform modules.** Owner: **`platform-shared-services`**. Scope: MOD-0019 Data Masking &
Row/Field Security, MOD-0023 Workflow Designer (Workflow/Inbox v1), MOD-0031 Evidence Linking Service, plus a
named retention / legal-hold owner. **Gate released:** MOD-0230 **operational runtime authorization** - i.e.
production, supplier qualification, and validation.

### Rationale

Five of the eight REG-PV-BASE legs are already merged, tested code in this repo: MOD-0018 authorization
(`Diten.Platform.Common/Authorization`), MOD-0021 audit (`Diten.Platform` audit feature, outbox, redaction,
export), MOD-0041 observability (`SensitiveDataRedactor`, `SensitiveDataLogEventEnricher`), correlation
(`ICorrelationContext`, `CorrelationIdMiddleware`), and tenancy (`ITenantContext`, `TenantResolutionMiddleware`).
Only masking, workflow, and evidence-link are genuinely absent.

Every behaviour MOD-0230 requires from those three absent dependencies is a **denial** behaviour, as written in
the pack's own Failure Path list. A deny-by-default adapter satisfies each requirement exactly. Therefore
MOD-0230 can be built and fully tested without them - it cannot be **operated** without them.

### Explicit non-waiver

This decision waives nothing. MOD-0019, MOD-0023, and MOD-0031 remain production blockers. The
`PVG-MOD0230-FieldSecurity-Contract v1`, `PVG-MOD0230-WorkflowTransitionGate-v1`, and
`PVG-MOD0230-EvidenceLink-v1` evidence rows remain **unapproved** and are marked as satisfied *for the build
gate only* by the fail-closed port design.

### Constraints on W-3A0-Lite

- Ports are interface + deny default only. They must not store policy data, host a workflow engine, or persist evidence.
- Non-production adapters must throw at startup when `ASPNETCORE_ENVIRONMENT=Production`. A conformance test asserts this.
- `PvgPendingEvidenceStore` may only record evidence as **pending**. It may never report evidence as satisfied and may never assemble an evidence pack.
- When a real module ships, its client replaces the default in one DI registration. No handler, entity, validator, or view may change as a result.

---

## OD-4 - MOD-0231 Signal Minimum Scope state model

**Question (DCP-004 §14.4):** For MOD-0231 Signal Minimum Scope, what minimum case lifecycle states must exist
before MOD-0234 can consume them?

### Decision

The minimum lifecycle is fixed at six linear states plus two terminal and one non-linear state. Anything beyond
this belongs to full W-4 Case Processing and remains out of scope.

| State | Owner | Enters from | Meaning |
|---|---|---|---|
| `Received` | MOD-0230 | intake create | Intake record exists; not yet triaged |
| `Triaged` | MOD-0230 | `Received` | Triage outcome and route target recorded |
| `InProcessing` | MOD-0231 | `Triaged` | Accepted into case processing from the MOD-0230 handoff |
| `AssessmentComplete` | MOD-0231 | `InProcessing` | Causality/seriousness assessment recorded |
| `ReadyForSignal` | MOD-0231 | `AssessmentComplete` | Signal handoff preconditions met |
| `Closed` | MOD-0231 | `ReadyForSignal`, `AssessmentComplete` | Case closed for the signal-minimum slice |
| `Rejected` | MOD-0230 / MOD-0231 | `Received`, `Triaged`, `InProcessing` | Terminal; not a valid safety case |
| `Duplicate` | MOD-0230 / MOD-0231 | `Received`, `Triaged` | Terminal; merged into an existing case reference |
| `OnHold` | MOD-0231 | any non-terminal | Non-linear; blocks forward transition, preserves prior state |

**Signal handoff precondition.** MOD-0234 may consume a case only when **all** hold:

1. State is `ReadyForSignal`.
2. MOD-0232 coded terms exist and are bound to an immutable dictionary version.
3. Evidence completeness is reported **satisfied** by `IPvgEvidenceLinkPort` - never by a pending record.
4. A TRACE-BUNDLE correlation chain exists from intake through triage, processing, and coding.

### Constraints

- These state names are the MOD-0231 **delivery-slice** state model. `Signal Minimum Scope` remains a delivery slice label and must never appear as the module `name`, which stays exactly `Case Processing`.
- The state set is owned by MOD-0231 but **enforced** through `IPvgWorkflowTransitionGate`. MOD-0231 must not embed its own workflow engine.
- MOD-0231 stays `status: draft`. This decision unblocks its pack completion, not its implementation.

---

## OD-5 - MedDRA source, licence, versioning, and import policy

**Question (DCP-004 §14.5):** What MedDRA source, licence, version-update cadence, and import/validation
approach will be accepted for MOD-0232?

### Decision

**Source and licence.** MedDRA is licensed exclusively through the MedDRA Maintenance and Support Services
Organization (MSSO). No other source is acceptable - not a third-party redistribution, not a vendor-bundled
copy, not an extract from a partner safety system. A subscription agreement executed in the organisation's own
name is a hard precondition for any MOD-0232 work beyond `draft`.

**Versioning.** MedDRA is published on a twice-yearly cycle, 1 March and 1 September. The current release is
**version 29.0, released 1 March 2026**; 29.1 is expected 1 September 2026. Confirm the tier, fee band, and
current cadence with the MSSO at subscription time.

**Import and storage policy - binding on MOD-0232 regardless of licence status:**

1. Dictionary data is imported into a **versioned, immutable snapshot** collection. A snapshot is never mutated in place.
2. Every coded-term assignment binds to an explicit `{dictionaryVersion, termCode}` pair. Version is never inferred or defaulted.
3. Recoding across versions is **append-only** and auditable. Prior assignments are never overwritten.
4. **No MedDRA term, code, hierarchy fragment, or sample value may appear in source files, UI fixtures, seed data, test data, or documentation** - including this repository's test fixtures. This holds even after the licence is executed, unless the executed agreement explicitly permits that exact use in writing.
5. Display, search, and export are restricted to tenants covered by the licence. Access outside the licensed envelope fails closed.
6. Import, access, display, and export events are logged with correlation IDs and without PHI.

### Procurement action - starts immediately

| Step | Owner | Timing |
|---|---|---|
| Open MSSO subscription enquiry; determine tier and fee band | NY | Day 1 |
| Legal review and execution of the subscription agreement | NY / Legal | Day 1-20 |
| Record licence ID, licensed version, and permitted-use envelope back into this decision record | NY | On execution |

**MOD-0232 remains `status: draft` until the executed licence is recorded here.** This is the longest external
lead time in the PVG programme and is the reason procurement starts on Day 1 rather than after MOD-0230 ships.

---

## OD-6 - MOD-0234 data product and semantic metric minimum gates

**Question (DCP-004 §14.6):** What data product contract and semantic metric IDs are the minimum viable gates
for MOD-0234?

### Decision

**MOD-0234 remains contract-only. This decision defers the gate rather than closing it, with explicit entry
conditions recorded so the deferral is not open-ended.**

MOD-0004 Metric & Semantic Registry and MOD-0063 Data Warehouse / Lakehouse are not merely unbuilt - as of
2026-08-09 **neither has a row in `execution/registries/module-id-registry.md`**, despite both being
Blueprint-canonical (MOD-0004 W-2, MOD-0063 W-3). A hard gate cannot be defined against an unregistered module.

### Entry conditions before OD-6 can be closed

1. MOD-0004 and MOD-0063 have registry rows and named owner domains.
2. MOD-0004 supplies approved semantic metric IDs, threshold definitions, observation-window semantics, and insufficient-data rules.
3. MOD-0063 supplies approved data-product contract IDs, cohort definitions, lineage, refresh/as-of semantics, quality status, and aggregate privacy rules.
4. MOD-0230, MOD-0231, and MOD-0232 consumption contracts are approved.

### Requirements PVG places on MOD-0004 / MOD-0063 (recorded now, owned there)

These are **requirements on the owning modules**, not PVG-owned metrics. PVG must not define or compute them.

- Case counts by suspect product and coded preferred term, over a declared observation window.
- An explicit insufficient-data rule that suppresses a metric rather than returning a misleading zero.
- A disproportionality measure slot, with the measure family and thresholds owned by MOD-0004.
- As-of semantics so a signal evaluation can be reproduced exactly at a later date.

### Standing constraints - unchanged

MOD-0234 keeps `shell: none` and `golden_reference: none`. No Signal Management UI shell, service shell, route,
menu entry, placeholder dashboard, placeholder endpoint, seed, or fake data may be created. Any future runtime
requires a revised and reapproved module pack with concrete `shell`, `golden_reference`, `entity_base`, and
`form_field_count`, as a separate planning event.

---

## OD-7 - Build / buy / partner strategy and integration boundary

**Question (DCP-004 §14.7):** Is PVG implementation intended as buy/partner integration, internal build, or
hybrid wrapper?

### Decision

**Hybrid, partner-aware internal control wrapper, in a dedicated `Diten.PvgService` on port 5011.**

The Blueprint records MOD-0230, MOD-0231, MOD-0232, and MOD-0234 as `Buy/Partner`. This decision does not
overturn that for the whole domain; it splits it by module.

| Module | Strategy | Rationale |
|---|---|---|
| MOD-0230 Case Intake & Triage | **Internal build** in `Diten.PvgService` | Thin, tenant-facing, and entirely dependent on Diten's own RBAC, audit, workflow, evidence, and tenancy contracts. Buying it would mean buying a foreign tenant model. Fastest path to demonstrable progress |
| MOD-0231 Case Processing (signal-minimum slice) | **Internal build**, slice only | Only the minimum lifecycle MOD-0234 needs. Full W-4 case processing stays a buy/partner candidate |
| MOD-0232 MedDRA Coding | **Buy / partner leaning** | A MedDRA workbench with dictionary versioning, browsing, and recoding is a mature commercial product; rebuilding it is not justified |
| MOD-0234 Signal Management | **Buy / partner** | Signal detection and disproportionality analytics depend on a data platform Diten does not have (MOD-0063 unregistered) |

### Integration boundary

`Diten.PvgService` is the **controlled wrapper**, not a bridge:

- It owns the Diten-controlled intake contract, tenant workflow boundary, RBAC/audit/evidence/correlation integration, and the partner adapter boundary.
- A partner adapter port (`IPvgSafetyPartnerAdapter`) is **declared but not implemented** in slice 1, so a bought PV safety system can be wrapped later without changing the tenant surface.
- The frontend must never call a partner system, the Gateway, or a service port directly. The profile is same-origin MVC proxy → Gateway → `Diten.PvgService`.
- `Diten.PvgService` must not become a standalone PV safety platform. Internal build scope is capped at the Diten-controlled contract, tenant UI boundary, foundation integration, and adapter layer.

### Runtime parameters released by this decision

| Parameter | Value | Note |
|---|---|---|
| Service | `Diten.PvgService` | Releases MOD-0230 frontmatter `service` from `TBD` |
| Port | **5011** | Verified free. 5056-5060 are taken (5059 MDM, 5060 HCM are live but undocumented). `.antigravity/rules/ports.md` is a protected path and needs explicit approval to update |
| Gateway upstream | `/api/pv-case-intake-triage` | Per NET-001; the pack's proposed `/api/v1/...` upstream is a convention violation |
| Gateway downstream | `/api/v1/pv-case-intake-triage` | `v1` belongs on the downstream template |
| Entity base | `EntityBase` | Tenant-owned; unchanged |

**Prerequisite before scaffolding:** the stale ignored `services/Diten.PvgService/bin` and `obj` folders must be
deleted first. They contain generated restore/build metadata with no tracked source and will collide with a
real scaffold.

---

## Reconciliation

- 2026-08-09: OD-2, OD-4, OD-5, OD-6, OD-7 decided by NY. DCP-004 §18 updated; DCP-004 promoted from `draft` to `approved`.
- 2026-08-09: OD-1 and OD-3 were already resolved 2026-08-04 and are unchanged by this record.
- Implementation-phase outcomes are recorded in DCP-004 §20 and in each member module pack, not here.
