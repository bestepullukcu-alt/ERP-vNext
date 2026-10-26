# MOD-0126 placement — the decision that must precede the module pack

    created : 2026-10-11
    status  : DECISION-READY / UNAPPROVED
    for     : owner / CT
    why now : the source lane has begun the MOD-0126 producer packet. Its item 2 — a
              destination module pack — cannot be written until this is answered.

## The blocker, stated precisely

`.antigravity/rules/module-pack-standard.md` §2 makes **`domain` and `service` mandatory**, and
`domain` must equal *"the domain folder name, exactly"*. For MOD-0126:

    domain  : no finance/treasury domain exists here — 11 domains, none of them one
    service : no treasury service exists here — 17 services, none of them one

So a standard-conformant pack **cannot be written**, and writing one with `domain: TBD` would
violate the standard rather than defer it. (The MOD-0230 precedent of `service: TBD` does not
extend to `domain`: that pack sat inside a real `pharmacovigilance/` folder.)

**Placement is therefore a prerequisite, not a field.** Everything else for the pack is measured
and ready — §4 below.

## What is already settled

| | |
|---|---|
| identity | `MOD-0126` · `Payments & Approvals` · DCP-002 gate **OK, proven against Blueprint/registry** |
| Blueprint | Wave **W-3** · ERP Suite · capability group **Treasury & Cash** |
| exists here | registry rows **0** · pack **0** · code refs **0** → clean intake, not a fork |
| SoR | payment batches, payment instructions, approvals, release states |
| gates met here | **MOD-0023 Workflow 199 files · MOD-0021 Audit 122 files** |
| gate NOT met | **MOD-0269 Banks/SWIFT/Payments Gateway** — 0 files, and it is an *External Systems Register / External Provider* entry, so a provider decision rather than a build |

MOD-0126 is the only member of the proposed five whose Blueprint gates are otherwise met; the other
four are gated on MOD-0003, which has zero implementation in either repository.

## The placement options

### Option A — new `finance-treasury` domain + new `Diten.FinanceTreasuryService`, port 5066
Correct by Blueprint grouping: Treasury & Cash is its own capability group, and the four
MOD-0003-blocked members would eventually join it.
**Against:** AGENTS.md "Yol A" is the explicitly rare path, and this stands up a domain, a service
and a port for **one** module. If MOD-0003 is never funded, it stays a one-module domain.

### Option B — place it in `procurement` / `Diten.ProcurementService`, port 5065
Adjacent in practice: P2P ends in payment, MOD-0140 already lives there, and the service exists and
runs.
**Against, and this is measured:** the module ID registry already drew this line in the opposite
direction. Its MOD-0140–0145 reservation note reads *"Not in this reservation (deliberate):
MOD-0146 Payment Run Support (Not SoR; **Finance/Treasury; out of scope**)"*. The registry has
already ruled that payment-run capability is **not** procurement's. Placing MOD-0126 — a
Treasury & Cash module — into procurement would contradict a recorded deliberate exclusion, and
would do so silently unless the note is amended in the same decision.

### Option C — defer MOD-0126 until MOD-0003 is funded, then open the domain with five members
Makes Option A proportionate and avoids a one-module domain.
**Against:** MOD-0126 does not depend on MOD-0003. This parks a module whose gates are met behind
one whose absence does not block it.

### Option D — reserve the ID now, place later
Registry row only, no pack, no code. Keeps identity canonical and the producer packet useful while
placement is settled.
**Against:** does not unblock implementation; it only prevents the ID drifting.

## What the owner is deciding

1. **Placement** — A, B, C, D, or another. If B, whether the registry's MOD-0146 exclusion note is
   amended in the same decision, because leaving it contradicted is the silent-divergence case
   TP-001 T32 exists to prevent.
2. **MOD-0269** — the provider and interface decision. MOD-0126's third Blueprint gate. Not a build.
3. **Whether to proceed to a pack at all before 2 is answered.** A pack can be written and sit at
   `draft`; it cannot reach `ready-for-dev` with an unmet gate.

## §4 · Pack content already measured — drop-in once placement is chosen

Against the standard's 21 required sections, here is what is measured and what still needs the
producer packet:

| § | section | state |
|---|---|---|
| 1 | Module Summary | **ready** — Payments & Approvals; SoR per Blueprint; W-3; Treasury & Cash |
| 2 | Ownership and Boundaries | **ready** — SoR: payment batches, instructions, approvals, release states. Out: bank dispatch (MOD-0269, external), AP/AR invoice ownership (MOD-0119/0120) |
| 3 | Owned Objects | **needs producer §E** — their `PaymentBatchesController` is the anchor |
| 4 | Entity Fields | **needs producer §E** — field model + validation + index needs |
| 5 | Repo Scope | **blocked on placement** — depends on the chosen service folder |
| 6 | Protected Paths | **ready** — `.antigravity/**`, other domains' `services/`, Archive, `_Layout.cshtml`, `ocelot.json` (integration-agent only) |
| 7 | Dependencies | **ready, measured** — MOD-0023 ✓199 · MOD-0021 ✓122 · MOD-0269 ✗0 (external provider) · MOD-0018 ✓267 · MOD-0031 ✓71 (evidence pack, per soft pages) · **SoD/approver 12 files — thin, and approvals are this module's core; verify before relying on it** |
| 8 | Runtime Constraints | **blocked on placement** — port follows the service |
| 9 | Layout & Shell Contract | **ready** — `shell: tenant` ⇒ `Layout = "_LayoutTenantShell"`, views under `Views/<Area>/...`. Their views use `_LayoutBackbone`, which does not exist here |
| 10–11 | Backend / Frontend File Convention | **ready** — Golden Reference; `golden_reference` resolves once the producer gives the create/edit field count |
| 12 | Validation Rules | **needs producer §E** |
| 13 | Failure Path to Verify | **partly ready** — duplicate, missing, unauthorized, concurrency; the approval-specific paths need producer §H (side-effect ordering) |
| 14 | Authorization Convention | **needs producer §E** — their keys are `ap.*`-style; ours must be re-prefixed to the chosen domain |
| 15 | Gateway / API Routing Decision | **blocked on placement** — integration-agent task either way |
| 16 | Acceptance Criteria | **drafted after 3, 4, 12** |
| 17 | Test Expectations | **ready in shape** — unit, integration, frontend smoke, build, `verify_datatable_page.py`, RESX |
| 18 | Ready-for-dev Checklist | **ready in shape** — must include the MOD-0269 gate and the SoD verification |
| 19 | Implementation Notes | **ready** — this document, the intake plan, and the producer packet digest |
| 20 | Follow-up Items | **ready** — MOD-0003 for the other four members |
| 21 | **Audited Events** (AUD-001) | **needs producer §H** — mandatory for new packs: every write command → event name → path. A payments module with unaudited approval transitions would fail AUD-001 |

Three sections are blocked on placement; five need the producer packet; the rest are ready.

## Sequence from here

    1  owner decides placement (this document)          → unblocks §5, §8, §15
    2  producer packet arrives for MOD-0126             → unblocks §3, §4, §12, §14, §21
    3  destination module pack written, status `draft`
    4  MOD-0269 provider decision                        → required before `ready-for-dev`
    5  SoD/approver depth verified (12 files measured)   → required before `ready-for-dev`
    6  owner promotes pack to `ready-for-dev`            → only then may code be written

Steps 1 and 2 are independent and can run in parallel. **No code may be written before step 6**
(AGENTS.md §10), and nothing in this document authorises any.

## Measurement limits

Measured here: the standard's mandatory frontmatter fields and 21 required sections; our domain and
service inventory; MOD-0126's Blueprint row (name, wave, group, gates, SoR, soft pages); the DCP-002
gate result; its absence from our registry, packs and code; dependency-gate file counts; the
registry's MOD-0146 exclusion note.

Not measured: whether the source's MOD-0126 reaches its own workflow, audit, evidence and approver
dependencies through real implementations or fail-closed/fixture clients — the source lane has
flagged this and it is their packet's §D. Our 12-file SoD count is a destination measurement and
says nothing about theirs.
