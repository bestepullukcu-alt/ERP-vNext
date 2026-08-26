---
id: DCP-004
slug: work-aggregation-task-center
name: Work Aggregation / Task Center (Görev Merkezi)
candidate_id: CAND-CAP-0006
product_name: "Görev Merkezi / Task Center"
type: Delivery Capability Pack
standard: CAP-001
status: approved
owner_domain: platform-shared-services
owner: enterprise-architect / platform-team
created: 2026-07-24
approved: 2026-07-24
approved_by: enterprise-architect
canonical_source: "docs/System Capability & Implementation Blueprint - master 7.xlsx#Blueprint_Data (NO matching MOD row — verified)"
executable_authority: "frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/fixture-contract.js"
intent_reference: "docs/workcenter-rebuild-spec.md (v2) — intent only, NOT authority"
identity_gate: "python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0006 --name \"Work Aggregation / Task Center (Görev Merkezi)\" → exit 0 (2026-07-24)"
---

# DCP-004 — Work Aggregation / Task Center (Görev Merkezi) — Delivery Capability Pack

> **Artifact type:** This is a **Delivery Capability Pack** (CAP-001 governance / orchestration contract).
> It is **NOT** a runtime entity, **NOT** a module pack, **NOT** a MOD-0014 runtime Capability Group, and
> **NOT** a business-capability-matrix row. It references member modules **by ID only** and never replaces
> their module packs.
>
> **What this pack is:** the governance **charter** for the cross-module personal work-aggregation surface —
> its ownership boundary, its single executable authority, the current-state-vs-target gap, and the
> provider-integration law that every source module must obey to push work into it. It **consolidates**
> existing decisions that today live scattered across the spec, MOD-0023, MOD-0024, and the backlog; it never
> copies them.
>
> **Status guard:** `approved` (EA, 2026-07-24). Approval satisfies **only condition 1** of the CAP-001 §7
> two-condition gate; it authorizes **no** code by itself. No WC slice may start until its **own** module pack
> is separately `approved`/`ready-for-dev` (condition 2). This pack still triggers **no** production
> implementation, **no** service/frontend/gateway change, and mints **no** Blueprint `MOD-xxxx`. The candidate
> identity `CAND-CAP-0006` stays `candidate / pending-EA` — charter approval does **not** change it.

---

## 1. Identity and status

| Field | Value |
|-------|-------|
| Governance ID | **DCP-004** |
| Candidate capability ID | **CAND-CAP-0006** (temporary; DCP-002 candidate namespace) |
| Canonical (governance) name | Work Aggregation / Task Center (Görev Merkezi) |
| User-facing product name | **Görev Merkezi / Task Center** (SAP Task Center line) |
| Code name (unchanged) | `WorkCenterNext` |
| Slug | work-aggregation-task-center |
| Type | Delivery Capability Pack (CAP-001) |
| Status | `approved` (EA, 2026-07-24) — CAP-001 two-condition gate, condition 1 met; still requires each member slice's own module pack `ready-for-dev` before code |
| Owner domain | platform-shared-services |
| Executable authority | `frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/fixture-contract.js` |
| Intent reference (non-authority) | `docs/workcenter-rebuild-spec.md` (v2) |

### Identity decision (DCP-002 — fail-closed)

- The Blueprint (`Blueprint_Data`) contains **no** MOD row for this capability — **verified**. No `MOD-xxxx`,
  `PSS-*`, or `NEW-*` ID is invented for it.
- The next free candidate slot in the DCP-002 candidate namespace (`CAND-CAP-0001…0005` occupied) is
  **`CAND-CAP-0006`**, reserved as a temporary governance identity (lifecycle: candidate → pending-EA →
  future EA `MOD-xxxx`). It is **never** written into runtime literals.
- Identity gate (2026-07-24):
  `python3 .antigravity/scripts/verify_module_id.py . --candidate CAND-CAP-0006 --name "Work Aggregation / Task Center (Görev Merkezi)"`
  → **exit 0** (`OK  candidate CAND-CAP-0006: temporary governance identity, pending EA, not Blueprint-backed, not in runtime.`).

### EA follow-up (NOT done by this pack)

Adding a **canonical Blueprint `MOD-xxxx` row** for Work Aggregation / Task Center is a **separate Enterprise
Architect (user) decision**. This pack does **not** perform it and does **not** propose a specific number.
Until that EA allocation exists, `CAND-CAP-0006` remains the only governance identity, recorded with a
reservation row in [`module-id-registry.md`](../../registries/module-id-registry.md) and in the
[reconciliation ledger](../blueprint-master-plan-reconciliation.md) (the candidate gate requires the ledger
entry). **Marked: EA follow-up.**

## 2. Business outcome

One cross-module **personal** action surface where every module's approval + task + review + issue + exception
reaches the person who must act, with simple/frequent actions completed in place and complex work deep-linked
to the source. The Task Center owns a **thin personal overlay** only; it never becomes a second system of
record for status, time, dependency, or workflow. The outcome is measured by: (a) every participating module
pushes work through **one** contract (WC-1), and (b) no module integration requires bespoke Task Center code.

## 3. Problem statement

The frontend surface (`/WorkCenterNext`) and its canonical fixture contract exist and are internally
consistent, but the **aggregation reality behind them does not yet exist**:

- The workflow engine (MOD-0023) ships real `ApprovalTask` data and a `GetMyWorkflowTasks` query, but returns
  a **raw** `WorkflowTaskDto` — there is **no projection layer** (WC-1) that turns it into the canonical
  work-item the surface consumes (see §C).
- **Enterprise Strategy pushes zero work today.** Its objective/demand review is a free-text `ApprovalStatus`
  string field, not a queue; nothing converts those fields into actionable work items (see §C).
- Without a governed provider-integration law, each future module would re-invent its own Task Center wiring —
  the exact "WC-1 done wrong → every integration rewritten" risk the backlog already flags as the most
  critical seam.

This charter fixes the boundary and the integration law on paper **before** any WC-1..WC-5 backend is built.

## 4. Capability boundary

### 4.A Ownership table (hard boundaries)

| Concern | Owner | Owns | Explicitly does NOT own |
|---|---|---|---|
| **Görev Merkezi / Task Center** (this capability, `CAND-CAP-0006`) | Task Center aggregation layer (`WorkCenterNext`) | Thin personal overlay (pin / snooze / seen / personal planned-date / reminder / **note**); the **aggregation projection**; the **render of effective actions**; deterministic block selection | **Any** other semantics — no workflow decisions, no native lifecycle, no status/time/dependency truth, no permission/eligibility computation |
| **MOD-0023 — Workflow Designer (Approvals/SLAs/Escalations)** | platform-shared-services | `ApprovalTask` + approve / reject / delegate / request-info / cancel semantics; SLA timing & escalation; the lifecycle **transition gate** | Operational task execution; the Task Center overlay; the presentation projection |
| **MOD-0024 — Task & Checklist Engine** | platform-shared-services | Generic task/checklist primitives; **self-task generation** (a **provider**); native lifecycle only when MOD-0024 is the declared source | Approval semantics; other modules' native task lifecycle; the aggregation surface's authority |
| **Source business modules** (Finance, HCM, PPM/MOD-0117, MDM, Enterprise Strategy, …) | their own domains | Native lifecycle / status / time / dependency of their own objects; the authoritative `nativeStatus` | The normalized status, the effective `actions[]`, and the personal overlay (those are produced downstream) |

> **One-line rule:** *the Task Center renders work; it decides nothing.* Every semantic — workflow decision,
> native lifecycle, permission, SLA — is owned upstream and only **projected** here.

### 4.B Authority declaration (executable contract wins)

- **`fixture-contract.js` is the single executable authority.** Its enums, invariants, and validators
  (`validateWorkItem`, `validateTrigger`, `validateCatalog`, `WorkCenterNextContract`) are the contract of
  record for what a work item / trigger is and what is valid.
- **`docs/workcenter-rebuild-spec.md` (v2) is intent/rationale only.** Where the spec's prose and the contract
  disagree, **the contract wins.**
- **Alignment obligation (documentation reconciliation, not code):** the spec's older `capabilities[]` prose
  must be read as the contract's `workItemCapabilities[]`
  (`planning | execution | timeTracking | checklist | subtasks | dependencies | attachments | evidence |
  activity | processStages | businessContext | relatedRecords`). `informationRequest`/`reviewFlow` are **not**
  capabilities in the contract; the spec text that implies otherwise is superseded by the contract.

## 5. Member modules and follow-ups (by ID only)

| Member | ID | Role in this capability |
|---|---|---|
| Workflow Designer | **MOD-0023** | Approval-work provider + decision/SLA/escalation semantics; source of `ApprovalTask` |
| Task & Checklist Engine | **MOD-0024** | Frontend fixture/resolver contract owner (current draft slice); future self-task provider |
| Source business modules | Finance / HCM / **MOD-0117** PPM / MDM / Enterprise Strategy (candidate provider) / … | Native providers behind WC-1 |
| RBAC/ABAC | **MOD-0018** | Effective permission / eligibility feeding `actions[]` (never computed in browser) |
| Audit Trail | **MOD-0021** | Activity/audit projection; decision stamps |
| Notification Service | **MOD-0027** | WC-4 notification seam |

**Delivery seams (backend prerequisites, sequenced as DCP slices — see §8):** WC-1 unified work-item provider
contract · WC-2 working-time/calendar seam · WC-3 assignee resolver · WC-4 notification seam · WC-5 provider
registry. Source: [`docs/product-backlog.md`](../../../docs/product-backlog.md) "WorkCenter ön-koşulları".

## 6. Ownership map

- **Enterprise Architect (user):** owns the future canonical Blueprint `MOD-xxxx` allocation for this
  capability (§1 EA follow-up) and approval of each DCP slice.
- **platform-team:** authors member module packs (MOD-0023 batches, a future MOD-0024 backend slice, the WC-1
  projection slice) once each is separately approved.
- **MOD-0023:** authoritative for approval semantics; **must not** be re-implemented by the Task Center or by
  MOD-0024.
- **MOD-0024:** authoritative for the frontend fixture/resolver contract in its current draft; future generic
  self-task provider.
- **Task Center overlay (`WorkCenterNext`):** authoritative **only** for the personal overlay fields.
- **Identity SoR pair (DCP-002):** [`module-id-registry.md`](../../registries/module-id-registry.md) +
  [`blueprint-master-plan-reconciliation.md`](../blueprint-master-plan-reconciliation.md) hold the
  `CAND-CAP-0006` reservation.

## 7. Dependency graph

```text
fixture-contract.js (executable authority, EXISTS)
        │  (design input, not implementation)
        ▼
WC-1 unified work-item provider contract  ──────────────┐
   • projection layer over provider raw DTOs            │
   • MOD-0023 raw WorkflowTaskDto → canonical work item │
        ▼                                               │
WC-5 provider registry (how a module declares itself a provider)
        ▼
WC-3 assignee resolver (user today → position-based later, BL-008)
        ▼
WC-2 working-time / calendar seam (naive 24/7 now → real calendar later)
        ▼
WC-4 notification seam (bell/email projection)
        ▼
Task Center aggregation API (renders effective actions[] + overlay)
```

MOD-0023 (approval provider) and MOD-0024 (self-task provider) are **providers behind WC-1/WC-5**, not layers
of the Task Center. The Task Center depends on WC-1 first; everything else is additive on top of it.

## 8. Ordered delivery sequence (sequential DCP slices — NOT one module pack)

Each slice is **separately approved** and gets its **own** module pack (single-module) or child DCP slice.
This charter authorizes **none** of them to code.

| Order | Slice | Scope headline | Gate before start |
|---|---|---|---|
| 1 | **WC-1** unified work-item provider contract + projection | Canonical work-item projection over provider raw DTOs; first proof = MOD-0023 `WorkflowTaskDto` → canonical item (title/actions[]/source/deep-link/normalized status/concurrency) | This charter `approved` + WC-1 module pack `ready-for-dev` |
| 1b | **WC-1b** frontend wiring + **tenant module manifest / catalog self-registration** | Wire `/WorkCenterNext` from mock → real WC-1 API; add a `WorkAggregation` **manifest provider** (parallels the 6 existing `*ManifestProvider`s) declaring the tenant page + nav (tenant shell) + the `platform.work-aggregation.inbox.view` permission + 7-lang l10n. **Newly recorded seam ([BL-022](../../../docs/product-backlog.md))** — without it WorkCenter is invisible in tenant nav and returns 403. **CORRECTION (2026-07-25, code-verified):** the earlier claim that this permission is "auto-seeded via catalog→auth sync" is **wrong** — the sync creates the KEY automatically, but the GRANT to a tenant user is not automatic (tenant-Admin baseline is a curated allow-list). Access is delivered by **entitlement** (EA 2026-07-25: `IsTenantAssignable: true`, non-baseline), so no protected `Diten.AuthService` edit is needed; the module stays invisible until an operator entitles it. **⚠ Hazard B2:** the A1 auto-registration worker syncs `moduleCode/scope = null`, and scope can never be downgraded from `PlatformAdmin` to `Tenant` (`InternalPermissionsController.cs:146-151`) — since WC-1 already shipped the attribute, WC-1b must verify/repair the stored `Module`/`Scope`. Additive otherwise; safe **after** WC-1 because the stable identifiers (ModuleCode/permission/shell) are already locked. | WC-1 shipped |
| 2 | **WC-5** provider registry | How a non-workflow module declares itself a Task Center provider (parallels `WorkflowManifestProvider`) | WC-1 shipped |
| 3 | **WC-3** assignee resolver seam | `assignee resolver` indirection so position-based assignment (BL-008) drops in without rewrite | WC-1 shipped |
| 4 | **WC-2** working-time/calendar seam | SLA/deadline behind a working-time interface (naive 24/7 now) | WC-1 shipped |
| 5 | **WC-4** notification seam | Task notification (bell/email) behind an interface | WC-1 shipped |

Rationale for ordering: WC-1 is the highest-risk seam ("wrong once → every integration rewritten"); WC-2/3/4/5
are additive indirections designed so deferred features attach **without regression**.

## 9. Prerequisites

- This charter `approved` / `ready-for-execution` **and** the specific slice's module pack `approved`/
  `ready-for-dev` (CAP-001 §7 two-condition gate).
- `fixture-contract.js` remains the frozen executable authority for the work-item shape.
- MOD-0023 runtime present (it is — §C); MOD-0024 frontend contract slice present (it is — `ready-for-dev`).
- DCP-002 identity gate green for `CAND-CAP-0006` (it is — §1).

## 10. Architecture decisions — Provider-Integration Law (the charter's core)

> This is the law every source provider obeys to push work into the Task Center. It is normative for the WC-1
> projection slice. It changes **no** code here.

### 10.1 Status normalization map

Provider `nativeStatus` is normalized to the contract's five-value `NORMALIZED_STATUSES`
(`Pending · InProgress · Waiting · Done · Cancelled`). Raw provider status text is **never** parsed to infer
lifecycle, waiting, eligibility, or actions (MOD-0024 rule). Canonical mapping for the MOD-0023 provider:

| Provider (native) state | `normalizedStatus` | Extra signal / field |
|---|---|---|
| `WaitingApproval` | `Pending` | decision surface |
| `WaitingEvidence` | `Waiting` | **`waitingContext`** (required pair with `Waiting`) |
| `Escalated` | `Pending` | **escalation signal** (chip/notice, not a status) |
| `Approved` / `Rejected` | `Done` | terminal; no enabled inline state-changing action |
| `Cancelled` | `Cancelled` | terminal |
| `Delegated` | *(hidden from this actor)* | not the current actor's active work; a disposition/activity event |
| `TimedOut` | **`Cancelled` (terminal)** | resolved OD-WC-01 (EA 2026-07-24); `Escalated` is already a separate active-state signal |

`normalizedStatus: Waiting` and `waitingContext` are a **bidirectional canonical pair** (contract invariant
`WAITING_CONTEXT_BIDIRECTIONAL`). Personal snooze **never** produces `Waiting`/`waitingContext`
(`SNOOZE_MUST_NOT_CREATE_WAITING`).

### 10.2 `actions[]` projection rule

The browser receives **one** authoritative `actions[]` array, resolved upstream from:

```text
native provider rules
  + effective permission (MOD-0018)
  + assignment / delegation / separation-of-duties
  + blocker / system-safety
        ↓
   one authoritative actions[]   (each: code, label, enabled, source, disabledReason(Code), safety flags)
```

The browser **never invents** `start`, `complete`, `approve`, `signoff`, or any business action, and never
derives eligibility from capability/status (contract + MOD-0024 §8/§13). Source navigation, audit links,
document/related-record links, and recovery deep-links are **not** commands and stay **out** of `actions[]`.
Concurrency is one projection-level token; per-action token copies are rejected
(`ACTION_CONCURRENCY_DUPLICATE`, `CONCURRENCY_REQUIRED_FOR_ENABLED_INLINE_ACTION`).

### 10.3 `source` vs `lifecycleOwner`

- `source` = the **work module** the object lives in (`providerCode`, `providerContractVersion`, `objectType`,
  `objectId`, optional `deepLink`).
- `lifecycleOwner` = the module owning the item's **lifecycle/decision** — required **when it differs** from
  `source` (e.g. a Finance invoice whose approval lifecycle is owned by MOD-0023 workflow). This keeps
  "where the object lives" and "who runs its workflow" as two distinct, honest facts.

### 10.4 Provider-binding rule (A / B) — the routing law

When a module has work to surface, it binds one of two ways:

- **DEFAULT — Binding A (via MOD-0023):** any work that needs **approval, SLA, escalation, or
  separation-of-duties** is handed to the MOD-0023 engine and surfaces as an `ApprovalTask`-derived work item.
  MOD-0023 is **reused, never re-implemented** by the module or by the Task Center.
- **Binding B (direct provider):** a module that only exposes a **simple native status** (no approval/SLA/SoD)
  registers as a **direct** WC-1/WC-5 provider and projects its own item.

> **Rationale (industry pattern):** this mirrors **SAP Task Center** — a central inbox fed by *Flexible
> Workflow* (Binding A) **and** app-native task providers (Binding B) — and **Oracle Worklist**. The Task
> Center is a *federation* of providers, not a workflow engine and not a bespoke-per-module surface.

**Applied to Enterprise Strategy:** objective/demand approvals that need a real decision queue belong on
**Binding A** (MOD-0023), replacing today's free-text `ApprovalStatus` field. A simple read-only strategy
status could be **Binding B**. This is a design directive for the WC-1 slice, not a change authorized here.

## 11. Scope

- Governance/charter only: ownership table, authority declaration, current-vs-target gap, provider-integration
  law, and the ordered WC-1..WC-5 slice plan.
- Reserve `CAND-CAP-0006`; record it in the identity SoR pair (registry + reconciliation ledger).
- Document the verified current-state findings (§C / §20) so the WC-1 slice starts from truth, not from the
  stale "no backend exists" assumption.

## 12. Explicit exclusions

- **No** runtime/service/frontend/gateway code; **no** Blueprint `.xlsx` edit; **no** member module pack edit
  (MOD-0023/MOD-0024 packs are **not** modified by this charter — their staleness is recorded as a follow-up,
  §20).
- **No** new Blueprint `MOD-xxxx` (EA follow-up, §1/§19).
- **No** WC-1..WC-5 implementation; **no** aggregation API; **no** provider certification.
- **No** change to the legacy `/WorkCenter` surface (frozen; §15).
- **No** Split/Kanban/Calendar (**BL-015**), no outbox/creator-scope (**BL-016**), no segment/chip visual work
  (**BL-017**) — all remain backlog-owned.

## 13. Governance drift risks

- **Spec-as-authority drift** — someone treats `workcenter-rebuild-spec.md` prose as binding over
  `fixture-contract.js`. Mitigation: §4.B authority declaration.
- **Semantic leakage** — the Task Center starts owning workflow/status/time. Mitigation: §4.A ownership table +
  MOD-0024/MOD-0023 non-merge rule.
- **Per-module bespoke wiring** — WC-1 skipped; each module writes its own Task Center adapter. Mitigation:
  §8 WC-1-first ordering + §10.4 binding law + WC-5 registry.
- **Stale pack drift** — MOD-0023 pack reads "no code produced" while runtime already shipped `ApprovalTask`.
  Mitigation: §20 records the finding + a pack-reconciliation follow-up (not edited here).
- **Fixture-truth drift** — Enterprise Strategy fixtures assert workflow deep-links that reality does not have.
  Mitigation: §20 fixture-truth debt logged to QA/backlog.
- **Identity drift** — a real `MOD-xxxx` invented before EA allocation. Mitigation: DCP-002 candidate gate;
  `CAND-CAP-0006` never enters runtime.

## 14. Review questions

1. Is `fixture-contract.js` accepted as the sole executable authority over the v2 spec? (§4.B)
   — **Answered (EA 2026-07-24): Yes.** `fixture-contract.js` is the sole executable authority;
   `workcenter-rebuild-spec.md` (v2) is intent-only; on conflict the contract wins (§4.B).
2. Is the A/B provider-binding law (default-A-to-MOD-0023) accepted as the routing rule? (§10.4)
   — **Answered (EA 2026-07-24): Yes.** A/B provider-binding law accepted; default = Binding A
   (governance-heavy work routed through MOD-0023) (§10.4).
3. Is `TimedOut` → `Waiting`+escalation **or** `Cancelled`? (OD-WC-01, §18)
   — **Answered (EA 2026-07-24): `Cancelled` (terminal).** Applied to §10.1.
4. Should Enterprise Strategy approvals move onto MOD-0023 (Binding A) as the first real provider, or stay
   representational until a later wave? (§18 OD-WC-02)
   — **Answered (EA 2026-07-24): deferred to the wave after WC-1**; WC-1's first provider is MOD-0023, not ES
   ([BL-018](../../../docs/product-backlog.md)).
5. Does the EA intend to mint a canonical Blueprint `MOD-xxxx` now, or keep `CAND-CAP-0006` through the WC-1
   slice? (§19)
   — **Answered (EA 2026-07-24): keep `CAND-CAP-0006` through WC-1**; Blueprint `MOD-xxxx` afterward
   ([BL-019](../../../docs/product-backlog.md)).

## 15. Gate criteria (re-used, not re-written — links)

Every downstream WC slice module pack must satisfy the standing project gates; this charter does **not**
restate their content:

- **7-language tenant l10n** — all of `en, tr, fr, es, zh, ar, ru` (tenant surface). See the localization gate
  in MOD-0024 §16/§17 and the project l10n rule. No tenant string ships in fewer than 7 languages.
- **No inline CSS (FG-003)** — style only via CSS classes in `backbone-custom.css`, scoped to `.wcn-*`; never
  `style=""` or `element.style`. See MOD-0024 §5 authorized-scope note.
- **Branch/commit policy** — one branch per module; never commit to `main`; commit only when the module slice
  is fully done ([GIT-002 git-safety.md](../../../.antigravity/rules/git-safety.md)).
- **Legacy `/WorkCenter` untouched** — byte-for-byte frozen; new work stays under `/WorkCenterNext`
  (MOD-0024 §6 protected paths).
- **DCP-002 identity gate** — any future canonical ID passes `verify_module_id.py`; `CAND-CAP-0006` stays out
  of runtime literals.

## 16. Acceptance criteria (this charter)

- [ ] `CAND-CAP-0006` reserved in the registry and reconciliation ledger; candidate gate exit 0.
- [x] Ownership table (§4.A) + authority declaration (§4.B) present and unambiguous.
- [x] Current-state-vs-target gap (§C / §20) present, grounded in runtime evidence.
- [x] Provider-integration law (§10) present: status-normalize map + `actions[]` projection rule +
  `source`/`lifecycleOwner` split + A/B binding rule.
- [x] Identity/name decision recorded; "Blueprint MOD row = EA follow-up" written and **not** performed here.
- [x] WC-1..WC-5 planned as sequential, separately-approved DCP slices (§8), not one module pack.
- [ ] Gates (§15) linked, not re-written.
- [ ] No runtime/service/frontend/gateway file and no Blueprint `.xlsx` changed; `status: draft`.

## 17. Downstream business-module impacts

None at runtime today. When WC-1 ships, every module that surfaces work adopts either **Binding A** (route
through MOD-0023) or **Binding B** (register as a direct provider). Enterprise Strategy is the first candidate
to convert its free-text `ApprovalStatus` into a real Binding-A queue. No business module is changed by this
charter.

## 18. Open decisions

- **OD-WC-01 — `TimedOut` normalization. RESOLVED (EA 2026-07-24).** `TimedOut → Cancelled` (terminal);
  `Escalated` is already a separate active-state signal, so a timeout is disposition-terminal, not another
  active state. Applied to the §10.1 normalize map.
- **OD-WC-02 — Enterprise Strategy provider timing. RESOLVED (EA 2026-07-24).** ES is deferred to the wave
  **after** WC-1; WC-1's first provider is MOD-0023 (its own approvals), not ES. Backlog:
  [BL-018](../../../docs/product-backlog.md).
- **OD-WC-03 — First canonical Blueprint `MOD-xxxx`. RESOLVED (EA 2026-07-24).** `CAND-CAP-0006` stays through
  the WC-1 slice; the Blueprint `MOD-xxxx` allocation comes afterward. Backlog:
  [BL-019](../../../docs/product-backlog.md).
- **OD-WC-04 — WC-1 provider-contract versioning. OPEN.** How `providerContractVersion` is governed across
  providers (certification). Owner: **platform-team at WC-1 pack authoring** — resolved inside the WC-1 module
  pack, not a backlog item.

## 19. Future follow-ups

1. **EA follow-up (identity):** allocate a canonical Blueprint `MOD-xxxx` row for Work Aggregation / Task
   Center and record the `CAND-CAP-0006 → MOD-xxxx` deprecated-alias chain (DCP-002). **Not done here.**
   Backlog: [BL-019](../../../docs/product-backlog.md) (triggers after WC-1; OD-WC-03).
2. **WC-1 module pack** (unified work-item provider contract + projection) — first executable slice.
3. **WC-5 provider registry**, **WC-3 assignee resolver**, **WC-2 working-time seam**, **WC-4 notification
   seam** — each its own approved slice (§8).
4. **MOD-0023 pack reconciliation** — update its "no code produced / Batch 01 unchecked" framing to match
   shipped runtime (§20 F1). Separate governance edit; **not** performed by this charter. Backlog:
   [BL-020](../../../docs/product-backlog.md).
5. **Enterprise Strategy fixture-truth cleanup** — reconcile representational workflow deep-links with reality
   (§20 F4); QA item, does not change the executable contract. Backlog:
   [BL-021](../../../docs/product-backlog.md).
6. **Enterprise Strategy as a real WC provider (Binding A / MOD-0023)** — convert the free-text `ApprovalStatus`
   into a real queue (§10.4, §17, OD-WC-02). Backlog: [BL-018](../../../docs/product-backlog.md) (after WC-1).
7. **BL-015 / BL-016 / BL-017** remain backlog-owned (views / outbox / segment-chip visuals).

## 20. Audit and reconciliation notes — Reconcile Step 0 (verified current state)

Findings verified against runtime on 2026-07-24 (read-only), recorded so the WC-1 slice starts from truth:

**F1 — MOD-0023 backend is real (its pack framing is STALE).** `ApprovalTask` entity
(`services/Diten.Platform/src/Diten.Platform.Domain/Entities/Workflow/ApprovalTask.cs`) and
`GetMyWorkflowTasksQuery` + `GetMyWorkflowTasksHandler`
(`services/Diten.Platform/src/Diten.Platform.Application/Features/Workflow/...`) exist and ship a
"WorkCenter inbox foundation." The MOD-0023 pack's "No code is produced by this pack" line and its unchecked
Batch 01 boxes are **stale** relative to this. **Correction is recorded here; the pack is a follow-up edit
(§19.4), not modified by this charter.**

**F2 — WC-1 projection layer is MISSING.** `GetMyWorkflowTasks` returns a **raw** `WorkflowTaskDto`
(`Id, WorkflowInstanceId, StageCode, StepCode, Status (raw string), AssignmentSnapshotId, AssigneeRef,
CommentRequired, EvidenceRequired, DueAt, CompletedAt, ActionedBy, ActionReasonCode`). Versus the canonical
work item it must become, the gap is:

| Canonical need | In raw `WorkflowTaskDto`? |
|---|---|
| `title` | ❌ missing |
| `actions[]` (effective, authoritative) | ❌ missing |
| `source` + deep-link (join to the business object) | ❌ missing |
| `normalizedStatus` / `nativeStatus` normalization | ❌ raw string only |
| concurrency (exposed to projection) | ❌ not exposed |
| assignee / candidate | ✅ `AssigneeRef` / `AssignmentSnapshotId` |
| `DueAt` | ✅ present |
| Evidence / Comment required | ✅ `EvidenceRequired` / `CommentRequired` |

**This gap is exactly the WC-1 slice's job** (§8 order 1).

**F3 — Enterprise Strategy pushes ZERO work today.** There is no workflow integration; approval is a free-text
`ApprovalStatus` field, not a queue. The fields exist (`ApprovalStatus`, `NextReviewDate`,
`DemandIdea.Submitted` + `ReviewDueDate`) but **no mechanism turns them into work items**. Directive: route
through Binding A (MOD-0023) at WC-1 (§10.4, OD-WC-02).

**F4 — Fixture-truth debt.** The Enterprise Strategy fixtures' `processInstanceId` and `lifecycleOwner:
workflow` are **representational**, and their 3/3 deep-link routes do **not** match a real workflow route.
This is a **fixture-accuracy debt** logged to **QA/backlog** (§19.5) — it does not block this charter and does
not change the executable contract.

**Reconciliation posture:** documentation/governance only. No runtime path is touched; the No-Change proof is
the unchanged runtime/service/frontend/gateway/Blueprint tree (verified via `git status`).

### Changelog

- **2026-07-24** — `draft → approved` (EA). Satisfies condition 1 of the CAP-001 §7 two-condition gate;
  authorizes **no** code. Open decisions OD-WC-01/02/03 resolved (EA 2026-07-24); **OD-WC-04 remains open**
  (resolved by platform-team in the WC-1 module pack). Candidate identity `CAND-CAP-0006` stays
  `candidate / pending-EA` — unchanged by this approval.
