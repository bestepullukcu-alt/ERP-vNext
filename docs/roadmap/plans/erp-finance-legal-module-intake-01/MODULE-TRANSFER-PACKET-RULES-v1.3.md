# Module transfer packet — rules (TP-001 v1.3)

    version    : 1.3
    status     : PROPOSAL / UNAPPROVED — no operative policy change until approval.
    supersedes : the v1.0, v1.1 and v1.2 PROPOSALS ONLY. Neither was ever operative policy, so
                 nothing is withdrawn and no behaviour changes on adoption of this text.
                 v1.0  /Users/natig/Projects/ERP-vNext-recovery/docs/roadmap/plans/
                       erp-finance-legal-module-intake-01/TRANSFER-PACKET-RULES.md
                       sha256 b6074163df930e43efd5aab7279e19a0e8d6c4fb9d2771f8d9e6e2ef36c59b11
                 v1.1  same folder, MODULE-TRANSFER-PACKET-RULES-v1.1.md
                       sha256 77d5fda3ca5ff453c8a4aa2b3ab631cb411ba5a1b920b3b077f301400e908ff7
                 v1.2  same folder, MODULE-TRANSFER-PACKET-RULES-v1.2.md
                       sha256 5e8029bea206700c0762641f9a76a769b9385bad8009236aab7a10e3f9723e84
    created    : 2026-10-11
    scope      : applies whenever a module developed in ANOTHER repository is proposed for
                 intake into ERP-vNext-recovery. One packet per MODULE — not per service,
                 not per domain.

    GOVERNING REPOSITORY — read this before any citation below
      This document governs intake INTO the destination repository
      `/Users/natig/Projects/ERP-vNext-recovery`. Every unqualified "AGENTS.md" and every
      section number in this file refers to THAT repository's AGENTS.md. The source
      repository's AGENTS.md is a different document with different section contents, and
      citations are not interchangeable between them. See T4.

    PROMOTION — accountable approver and required record
      `.antigravity/**` is a PROTECTED PATH (destination AGENTS.md §4, measured at line 78),
      so promoting this file to `.antigravity/rules/module-transfer-packet-standard.md` is a
      change the user/owner must approve explicitly; no agent may self-promote it.
      The promotion commit must ALSO add this rule's line to the rule map at destination
      AGENTS.md §6.1 (line 192, "Kural Haritası"), because §6.1 states verbatim at line 247:
        "Yeni bir kural dosyası eklendiğinde **aynı commit'te** buraya satırı yazılır;
         yazılmayan kural yetimdir."
        — a rule file added without a same-commit rule-map line is orphaned.
      Required approval record: an owner decision under
      `docs/records/decisions/<yyyy-mm>/`, naming the approver, the approved digest of this
      file, and whether any source-repo deviation is granted (T32).

v1.1 merged four rules from the migration-governance ruleset and added T4, the
destination-standards rule. v1.2 answers an external clause review: it qualifies supersession,
names the accountable approver and approval record, replaces the relative "this folder" with
absolute predecessor paths and digests, extends T4 to reviewers, and adds T28–T32 plus the
three-level acceptance separation in §5 — the control the review correctly identified as missing.

Every rule carries its evidence class, per the SOP convention:
**MEASURED CASE** (a real incident measured on 2026-10-11) · **OBSERVED** (measured in repo) ·
**DERIVED CONTROL** (preventive, no incident yet) · **POLICY** (governance requirement).

---

## 1 · Why one packet per module

**MEASURED CASE.** The ten modules proposed for intake live across **three** producer services —
`DitenErpTreasuryService`, `Diten.Platform` and `DitenMdmService`. A service-shaped request would
have produced one packet covering six modules and silently omitted four. A module-shaped request
cannot.

## 2 · What a packet is for

Copying files does not establish a migration. A packet is the **evidence contract for the
inspection step** — step 1 of the five-step sequence in §7. It establishes what exists, what is
missing, and what cannot be verified, before any scope is approved or any code moves.

---

## 3 · The rules

### Group A — Authority and identity

#### T1 · The canonical name is the Blueprint's, verbatim — never the display name
**MEASURED CASE.** Of ten modules supplied by display name, **eight failed the DCP-002 gate on
canonical-name mismatch alone**: `Accounts Payable` vs `Accounts Payable (AP)`; `Legal Entities`
vs `Corporate Secretarial / Entity Management`; `Contract Lifecycle` vs `Contract Lifecycle
Management (CLM)`; `Payment Proposals / Approvals` vs `Payments & Approvals`. Every ID was valid;
eight names were not. A packet headed by a display name cannot be used as a work order.

The packet carries the ID, the canonical name character-for-character, and the pasted output of
`python3 .antigravity/scripts/verify_module_id.py . --check-id <ID> --name "<canonical>"`.
A packet without a passing gate line is **not accepted for reading**.

#### T2 · Preserve module identity and accountable ownership; record deliberate changes
**POLICY.** The packet names the canonical ID, the canonical name, the source pack path, the
source history reference, and an **accountable human owner**. Where the source names an agent
label rather than a person — measured: MOD-0140's producer pack names `ai-orchestrator`, which its
own disclosure flags as "an agent label, not an identified human accountable owner" — the packet
says so. Any intended change of owning domain or service is recorded as a deliberate decision,
never as a side effect of the move.

#### T3 · Separate owned objects from consumed services, with an explicit exclusion list
**POLICY.** The packet distinguishes what the module is the system of record for from what it
merely consumes. Dependencies migrate only when explicitly included; otherwise the packet names
the adapter and the destination availability gate that must exist first.

**Two independent axes, not one list.** v1.2 required three "disjoint" lists and that was a category
error: a consumed dependency's source code can simultaneously be consumed AND excluded from
transfer, so the lists cannot be semantically disjoint.

  Axis 1 — RELATIONSHIP, per object: `owned` (this module is SoR) · `consumed` (another module is
  SoR) · `shared` (no single SoR; requires an owner decision before anything moves).

  Axis 2 — TRANSFER DISPOSITION, per artifact: `transfers` · `re-expressed in destination` ·
  `excluded, consumed via contract` · `excluded, destination already has it` · `excluded, blocked`.

Every object carries one value on each axis. The pairing is what matters: `consumed` +
`excluded, consumed via contract` is the normal case; `shared` + `transfers` must never appear
without an owner decision, because shared database objects cannot be un-migrated after a write.

Where the packet lists a consumed service, it records the **dependency owner's acknowledgment**
that the destination will consume it, or states that the acknowledgment is absent. Measured: the
MOD-0140 consumption authority (DC-02) has been AWAITING-CT since 2026-09-27, so a packet claiming
that dependency as available would have been claiming an authority nobody granted.

#### T4 · Destination standards are read from the DESTINATION's AGENTS.md
**MEASURED CASE.** A migration ruleset was measured on 2026-10-11 asserting that ERP-vNext
"requires `_LayoutBackbone.cshtml`" and "seven languages". Both came from the **source** repo's
`AGENTS.md`. Measured here: `_LayoutBackbone.cshtml` **does not exist** in the destination — its
layouts are `_LayoutPlatformAdmin` (admin), `_LayoutTenantShell` (tenant) and a frozen `_Layout` —
and localisation is **Platform 2 languages (en, tr) · Tenant 7**, not a flat seven. The source
mandates a flat seven and `_LayoutBackbone`.

Had that ruleset governed the move, treasury views would have been ported onto a layout that is
not here — the precise defect the conformance audit found in all 7 of the producer's views.

Every destination requirement in a packet cites the **destination's** AGENTS.md by section and by
repository path. A requirement traceable only to the source's rulebook is a source fact, and
belongs in the conformance delta (T18) rather than in the requirements.

**T4 binds reviewers as well as authors.** Measured three times in two days, same root cause: a
migration ruleset asserted `_LayoutBackbone.cshtml` and a flat seven languages as destination
requirements, both source-only; a reviewer reported this document's two predecessors "not found in
this checkout" while reviewing from the source repository; and the same review reported that
"AGENTS.md ... does not contain a §6.1 rule-map or an 'orphaned rule' requirement", having read the
source's AGENTS.md — which indeed has none, while the destination's carries §6.1 at line 192 and
the orphaned-rule sentence at line 247. The citation was correct; the repository was not.

A review of an intake artifact states which repository it was conducted in, and resolves every
cited path in that repository before asserting absence. Absence measured in the wrong repository is
not evidence.

### Group B — Evidence discipline

#### T5 · Four states, never one word: DECLARED · IMPLEMENTED · WIRED · RUNNING
**MEASURED CASE.** Three conflations surfaced in one day. MOD-0003 is *declared* in a pack with
**zero** implementation. MOD-0119's pack reads `status: draft` / *"frontend-only mockup"* while a
working `AccountsPayableController` exists — *implemented but not declared*. The whole
`DitenErpTreasuryService` is implemented but **absent from its own gateway's `ocelot.json`**, which
declares ports 5004, 5050, 5056, 5057 and 5058 and none for treasury — *implemented, not wired*.
No port probe succeeded, so nothing was shown *running*.

The packet states all four, per operation, as separate columns. "Ready" and a percentage answer
none of them.

#### T6 · A fail-closed stub, a fixture and an adapter are not an implementation
**MEASURED CASE.** MOD-0003's entire presence on the producer side is
`FailClosedSourceContractValidationClient` and `FixtureSourceContractValidationClient`, whose own
throw message reads *"once MOD-0003 ships a stable data-contract registry route"*. A packet
reporting "MOD-0003 present" would have been true by file count and false in every way that
mattered — and the module depending on it was listed at ~80% readiness.

Where a dependency is satisfied by a stub, fixture, mock, fail-closed client or disabled adapter,
the packet names the class and says so. **Producer implementation, or nothing.**

#### T7 · A number you did not produce yourself does not appear as if you had
**POLICY.** The readiness percentages that opened this intake were corroborated by neither the
producer's packs nor any measurement. They were reproduced for context, attributed, and never used
to size anything.

Every figure is either measured by its author with the command shown, or attributed to its source
and marked unverified.

#### T8 · Cite a path and a commit, or move the claim to the limits section
**MEASURED CASE.** The MOD-0140 disclosure pinned every claim to a repository-relative path, its
last-changing commit and an immutable inspected snapshot, and byte-compared each cited file before
writing. That is why it was usable despite being a self-report from a repository the receiving side
never inspected.

Unciteable claims are not deleted — they move to **"what I could not verify and why"**, a mandatory
section.

#### T9 · State the inspected snapshot, and that nothing changed
**POLICY.** A packet is a read-only act. It names the commit it was taken at and confirms that no
source, config, pack or contract was modified and nothing was staged, committed, pushed or
stashed. A packet whose own tree moved while it was written describes a repository that no longer
exists.

#### T10 · The receiving repo must already know whether the module exists here — measured
**MEASURED CASE.** MOD-0140 was assumed absent, then found present, then found to be a **confirmed
fork**: different domain, service, port, route and API shape, with **zero shared history** — none
of the producer's four cited commits exist in the destination and `Mod0140` has no occurrences
there. MOD-0220 is a probable second fork on the same pattern.

Before a packet is requested, the receiving side records registry row, pack and code presence
here. A packet requested for a module that already exists here is a **fork-resolution** task, not
an intake, and must be relabelled before any work proceeds.

### Group C — Contract and dependencies

#### T11 · Contract authority: digest, version, operation list — or the word ABSENT
**MEASURED CASE.** Twice in one session. MOD-0140's producer has **no** SUPPLIER OpenAPI at all,
after scanning 134 candidate contract/openapi files. And the destination's own copy moved
`1.0.0 → 1.2.0`, **three operations to nine**, through a merge nobody reading the packs would have
noticed — two documents were corrected on 2026-10-05 to describe a surface that was already stale.

The packet carries contract path, version, `x-status`, SHA-256 and the full `operationId` list — or
states that no contract exists, which is a finding and not an omission. Where the receiving side
holds a copy, the packet answers **by digest** whether they are byte-identical, and documents every
incompatible change.

#### T12 · Name every dependency gate AND measure each one's implementation state
**MEASURED CASE.** The Blueprint's `Dependency Gate (must exist first)` column named ten
capabilities for this intake. Seven were *reserved* in the destination registry and that was
briefly read as "present". Measured: MOD-0004 and MOD-0063 have **zero** source files; MOD-0003 has
zero — an earlier count of three was a **false positive**, three HcmService employee-validator files
containing the substring `DataContract`; MOD-0005 and MOD-0030 are not reserved at all; and one
named gate, "Document Management", resolves to no Blueprint module by that name.

Verifying one gate collapsed the proposed membership from **five modules to one**.

Reserved is not built. The packet states, per gate: Blueprint ID, reserved here yes/no, implemented
here with a file count and a locator, and whether the producer satisfies it through a stub (T6).

#### T13 · Lifecycle and delete semantics for every reference crossing the boundary
**MEASURED CASE / DERIVED CONTROL.** Only an opaque `supplierId` crosses the MOD-0140 boundary, and
`deleteSupplier` plus `bulkDeleteSuppliers` appeared in the contract between two readings. A
consumer holding an opaque id and receiving no deletion event simply starts failing.

Per identifier the consumer will hold, the packet states: hard delete, soft delete or tombstone;
whether any event or notification is emitted; whether a tombstone remains queryable; and the
referential retention commitment. **"Not provided" is an acceptable answer. Silence is not.**
Consumers are notified of any change that could invalidate their references.

### Group D — Behaviour preservation

#### T14 · Tenant and LegalEntity isolation: current eligibility, not a stored snapshot
**MEASURED CASE.** MOD-0140's supplier GET returns identity, status and a stored relationship
snapshot; it does not revalidate current LE eligibility and its repository filter carries no LE
predicate. The producer states plainly that the affirmative Tenant+LE eligibility seam **does not
exist**, and the destination has no such endpoint either — a gap confirmed independently from two
codebases.

The packet states how trusted context is resolved, where the authorization boundary sits, that
cross-tenant access denies (404/empty with no metadata leak), and whether eligibility is
**current** or **snapshotted**. A snapshot is not eligibility.

#### T15 · Authentication, authorization, binding and revocation
**POLICY.** The packet maps issuer, audience, subject, permission keys, actor types and service
identities, and states whether binding identity/revision and revocation fencing exist. Measured:
for MOD-0140 none of binding key, binding revision, one-active-binding cardinality, commit-time
revocation fence or no-write/no-receipt/no-event guarantee exists on either side. TTL-only caching
is not a revocation guarantee and must not be reported as one.

#### T16 · Mutation guarantees — a failed request must not be assumed side-effect-free
**MEASURED CASE.** The producer disclosed that
`ExecuteSupplierWorkflowActionCommandHandler.Handle` performs the workflow transition, publication
and supplier update **before** the final `UpdateCaseAsync(… ExpectedVersion)` check, and that
`UpdateSupplierAsync`'s boolean result is ignored — so a later concurrency failure cannot be read
as "nothing happened". For contrast, measured in the destination:
`SubmitOnboardingCaseHandler` has exactly two awaits, a read and a single compare-and-swap write,
and acts on the CAS result.

The packet states concurrency control, idempotency scope, transaction boundaries, audit and outbox
behaviour, and the ordering of side effects relative to the commit point. Where ordering admits a
partial effect, it is named.

#### T17 · Data integrity and explicit ID mapping
**POLICY.** The packet preserves stable IDs, opaque references, relationships, versions, uniqueness
constraints, lifecycle states and audit provenance. **Where an ID changes, the packet carries the
explicit mapping.** An unmapped ID change silently breaks every consumer holding the old value.

### Group E — Destination conformance

#### T18 · The conformance delta is measured against the destination's mandatory decisions, by the receiving side
**MEASURED CASE.** The producer's treasury code was audited against the destination's AGENTS.md §6
on 2026-10-11. Backend largely conformed — five layers, MediatR, `Response<T>`, Mongo, TenantId
across 137 files. Frontend largely did not: **all 7** views on a `_LayoutBackbone` absent from the
destination; **1 of 9** `.cshtml` carrying DataTable v2; **zero** SweetAlert2; **zero** pipeline
behaviours and no `IPipelineBehavior` anywhere; **zero** manifest providers; `DeletedAt` in 3 files
against `IsDeleted` in 22.

One row per §6 decision, with a measured verdict: **conforms · partial · rewrite · add.** The
receiving side writes this section — it is the destination's standard, not the source's.

#### T19 · Express remediation as files and clusters. No time estimates
**POLICY.** A packet sizes work as paths and defect clusters. It does not estimate duration, and a
percentage is not a size.

#### T20 · No secret values. Digests and lengths only
**MEASURED CASE.** A merge gate measured 45 `JwtSettings.Secret` values across 46 tracked config
files, reported entirely as truncated SHA-256 digests and counts, and found **ten further copies**
of one digest in committed build output that the gate's own wording had not scoped. Not one value
was printed.

A packet references credential-like material by truncated digest and length, never by value — and
records that **repository sanitation is not rotation**: removing a live secret from a tree revokes
nothing.

### Group F — Honesty, process and refusal

#### T21 · Never promote source labels into destination readiness
**MEASURED CASE.** MOD-0140's producer pack reads `status: done` with no contract behind it, and
`supplier.openapi.yaml` carries `x-status: FROZEN` while moving 1.0.0 → 1.2.0 and three operations
to nine. `done`, `FROZEN`, `production-ready` and a percentage are **source labels**. They describe
the source's own assessment of itself.

No source label becomes destination readiness without destination evidence. Missing guarantees
remain explicit gaps and are never silently invented during the move.

#### T22 · Disclose pack-versus-code disagreement; never reconcile it silently
**MEASURED CASE.** MOD-0119's pack says `draft` and *"frontend-only mockup"*; the controller exists
and works. The destination's own MOD-0147/0148 packs described three contract operations while nine
were live. Both repositories had governance artifacts disagreeing with their own code.

The packet shows both and marks the disagreement. The reader decides which is authoritative. An
author who picks silently has destroyed the evidence that the two drifted.

#### T23 · "What it does NOT provide" is mandatory, and it is not padded
**MEASURED CASE.** The MOD-0140 disclosure's exclusions section was its most decision-relevant
part: it established that the DC-02/DC-03 producer seam — actor binding, binding revision,
revocation fencing, affirmative Tenant+LE eligibility — exists in **neither** codebase. That
converged with independent measurement and settled a question two lanes had been circling.

The section is required and specific. Padding it to look thorough defeats it.

#### T24 · Evidence and cutover: prove the flow, prepare the rollback
**POLICY.** Before cutover the receiving side proves a golden flow end to end, a controlled failure,
persistence after reload, and security isolation — in the **destination**. Data reconciliation,
consumer compatibility and a rollback path are prepared before the transition, not after it.

#### T25 · The packet is a document. It is never a code drop
**POLICY.** Arriving code must clear the destination's gates: DCP-002 identity, an
`approved`/`ready-for-dev` module pack, domain ownership, the §6 decisions, self-registration,
permission keys and the port scheme. A packet arriving as a branch or an archive bypasses all of
them.

The portable unit is the **contract, field model, test vectors, state machine, permission keys,
error vocabulary and decision records**. Handlers are re-expressed against the destination pipeline.

#### T26 · Versioned filename, own manifest, vendored unedited
**POLICY.** Attribution corrected in v1.3, and the correction is larger than a renumbering.

Unedited vendoring IS destination policy: `.antigravity/rules/docs-organization.md` **K5** —
*"`vendor/` içeriği düzenlenmez"* — an inbound document keeps the name it arrived with. That part
stands. **K6 is about illustrated user manuals under `guides/<module>/` and has nothing to do with
manifests;** v1.2 cited it wrongly.

The manifest requirement itself — a first line declaring the verification directory, and a separate
non-verifiable manifest for source paths — has **no home in the destination rulebook today**.
Measured: it was committed in `ff5777afe` (2026-10-04) and **removed in `63fa9cd59`**
(2026-10-06, *"revert(mvp6): R-1 .antigravity/ and AGENTS.md return to origin/main"*), which cut
120 lines from `docs-organization.md`, taking it from 247 to 151. Searching the current rulebook for
`Manifest hangi dizinden` or `verify from` returns zero.

So the manifest requirement is **TP-001's own**, asserted here and nowhere else. It is retained
because 66 record manifests in `docs/records/audits/2026-10/` already carry that first line and
would otherwise be unverifiable by any stated rule — but a convention 66 records depend on, with no
clause behind it, is a governance gap this document can only flag. **Re-adopting it into
`.antigravity/` is a separate owner decision and is not bundled into TP-001's promotion.**

#### T27 · BLOCKED is a valid and preferred outcome
**MEASURED CASE.** Two lanes returned BLOCKED on measurement rather than proceeding: a merge gate
stopped because eight overlapping files were uncommitted, and this intake's membership fell from
five to one when MOD-0003 was verified. Each cost two commands and saved an approval cycle spent on
an absent foundation.

A packet that cannot answer a mandatory section says BLOCKED on that section and why. An author who
fills a gap with an inference has produced a worse artifact than one who stops.

#### T28 · Immutable provenance, dirty-state disclosure, and permission to transfer
**POLICY.** The packet records the source repository path, the exact commit it was taken at, and
the **dirty state of that checkout at measurement** — which files were uncommitted, so a reader can
tell measured committed bytes from measured working-tree bytes. It records the digest of every
artifact it offers, and it records **who authorised the transfer out of the source**. A packet with
no stated transfer permission is an unsanctioned copy regardless of its content quality.

#### T29 · Consumer acknowledgment for every contract or reference change
**MEASURED CASE / DERIVED CONTROL.** The destination's own SUPPLIER copy moved 1.0.0 → 1.2.0 and
three operations to nine through a merge, and two consumer packs continued describing the old
surface for six days. Nobody was told, because nothing required telling.

Where intake changes a contract, an operation set, an identifier or a lifecycle guarantee that a
named consumer depends on, the packet lists those consumers and records each one's acknowledgment —
or states that it is outstanding and names who owes it.

#### T30 · Data migration is a repeatable, reconciled procedure — not a copy
**POLICY.** Where records move, the packet states: ID and reference mappings (T17), index and
uniqueness-constraint equivalence, lifecycle and deletion-state translation, the reconciliation
query that proves source and destination agree, and whether the procedure is **re-runnable without
duplicating**. A one-shot migration with no reconciliation query cannot be shown to have worked.

#### T31 · Cutover gates and rollback live in the DESTINATION SUPPLEMENT, not the producer packet
**POLICY**, scope corrected in v1.3 after review: a cutover plan cannot exist during source
inspection, so requiring it of the producer packet was unsatisfiable. See §4a for the split.

The **destination supplement** states: the entry gate permitting cutover to begin; the exit gate
declaring it complete; named rollback triggers and who may pull them; what happens to **writes
arriving during the transition** — refused, queued, or dual-written; and the post-cutover
monitoring window with its owner. "Prepare a rollback" without a trigger and an owner is not a
rollback plan.

**Partial and irreversible effects must be enumerated, because restoring data does not undo a
migration.** The supplement states, for each: writes queued during transition and how they drain or
are discarded; **events already published** to consumers, which cannot be recalled; writes made
only in the destination after cutover, which have no source counterpart to roll back to; and any
effect classified **irreversible** — with its recovery or reconciliation procedure rather than a
rollback. An irreversible effect with no named reconciliation step is a cutover that cannot be
aborted after that point, and the supplement must say where that point is.

#### T32 · Source/destination rule conflicts resolve to the destination; deviations are approved and expire
**POLICY**, resolving an ambiguity the external review raised. Where a source rule and a
destination rule conflict, the **destination rule governs** (T4). A deviation from a destination
mandatory decision is permissible only as an explicit, written exception that names: the decision
deviated from, the reason, the accountable owner, and an **expiry or review date**. An exception
without an owner and an expiry is a permanent silent divergence.

Per the destination's authority order (AGENTS.md §1), a module pack or domain config may carry such
an exception; a transfer packet may not grant itself one.

#### T33 · Evidence integrity: measured results are distinguished from inherited claims
**MEASURED CASE.** Reiterating T7 and T21 as an integrity control because the external review
flagged it independently: every assertion in a packet is labelled measured-by-author or
inherited-from-source. Secrets are excluded entirely (T20). An inherited claim promoted to a
measured one is the failure mode that makes a packet dangerous rather than merely incomplete.


---

## 4 · Required packet shape

Fixed section letters, so packets for different modules are comparable and diffable.

| § | content | rules |
|---|---|---|
| A | identity: ID, canonical name, gate output, pack path + status, owning domain, accountable owner | T1, T2, T22 |
| B | contract: path, version, `x-status`, SHA-256, full `operationId` list — or ABSENT; digest parity with the destination's copy; incompatible changes | T11 |
| C | per-operation state: declared · implemented · wired · running, with locators | T5, T8 |
| D | dependency gates: Blueprint ID, reserved here, implemented here with file count, stub-satisfied yes/no | T12, T6, T3 |
| E | data: entity/field model, validation rules, state machine, permission keys, error-code vocabulary, ID mappings | T17, T8 |
| F | identifiers crossing the boundary: delete/lifecycle semantics, events, tombstones, retention | T13 |
| G | security: trusted context resolution, cross-tenant denial, LE eligibility (current vs snapshot), binding and revocation | T14, T15 |
| H | mutation guarantees: concurrency, idempotency, transaction boundaries, audit/outbox, side-effect ordering | T16 |
| I | UI specification — fields, columns, filters, row actions, states, required languages. **Not their view files** | T18, T25 |
| J | governance: decisions recorded, named approvers, open items, what is NOT approved | T21, T22 |
| K | limits: pagination, rate limits, idempotency, concurrency, availability | T8 |
| L | **what it does NOT provide** — specific, unpadded | T23 |
| M | conformance delta against destination §6 — written by the RECEIVING side | T18, T4, T19 |
| N | **what I could not verify, and why** | T8, T27 |

A packet missing §L or §N is incomplete regardless of the other twelve.

## 4a · Two documents, two authors — the producer packet and the destination supplement

**Scope correction in v1.3.** v1.2 asked one artifact to carry conformance deltas and cutover
plans, which only the destination can write and which cannot exist during source inspection. The
packet is therefore split, and each half has its own completion gate.

| | **producer packet** (immutable once issued) | **destination supplement** |
|---|---|---|
| author | source side | receiving side |
| sections | A–L, O, P | M, N, Q, R, S |
| when | step 1, source inspection | steps 2–5, after packet acceptance |
| mutability | **immutable**; corrections are a new version with a new digest | revised freely until release authorization |
| gates | completeness of A–L, O, P ⇒ **acceptance level 1** | M, Q ⇒ level 2 · R, S ⇒ level 3 |

The producer packet is immutable because its digest is cited by the destination supplement, by the
module pack authored from it, and by the acceptance records. A producer who edits an issued packet
invalidates every downstream citation; a new version with a new digest does not.

### Explicit fields for T28–T33 — added in v1.3 because A–N permitted their omission

| § | document | field | rule |
|---|---|---|---|
| O | producer | source repo path, exact commit, **dirty-state disclosure** (which files were uncommitted at measurement), artifact digests, **named transfer authoriser** | T28 |
| P | producer | consumers affected by any contract/operation/identifier/lifecycle change, each with acknowledgment **or** named owing party | T29 |
| Q | destination | ID and reference mappings, index and uniqueness equivalence, lifecycle/deletion-state translation, **the reconciliation query**, re-runnable without duplicating yes/no | T30, T17 |
| R | destination | cutover entry gate, exit gate, rollback triggers + who may pull them, writes-in-transition rule, post-cutover monitoring window + owner, **enumerated partial and irreversible effects with recovery steps** | T31 |
| S | destination | each deviation from a destination mandatory decision: decision, reason, accountable owner, **expiry or review date** | T32 |

**Acceptance checklist.** No acceptance level may be granted while any field of its gating sections
is blank. A blank field is not an absence disclosure — §L discloses absent *capability*; a blank
field is missing *evidence*, and the two are different (§6).

## 5 · Three acceptances, never one — and a packet grants only the first

**This is the control the external review correctly identified as missing, and its stated main risk
is precisely what the separation prevents: approving a packet must not accidentally approve
functionality or guarantees the source never provided.**

| level | what it means | what it does NOT mean | who decides |
|---|---|---|---|
| **1 · Packet acceptance** | the evidence is complete, honest and citeable; the module can now be *scoped* | that anything works, that any guarantee exists, or that intake is approved | receiving lane / CT |
| **2 · Implementation acceptance** | the module is built in the DESTINATION and passes destination builds, tests, runtime flows and security checks (T24) | that it may be released or consumed by anyone | destination module owner |
| **3 · Release authorization** | consumers may depend on it; cutover may proceed under T31 | — | owner, with CAP-001 and the destination's gates |

Each level has its own record. **Level 1 is explicitly not a statement about software.** A packet
whose §L ("what it does NOT provide") lists an absent guarantee has been *correctly* accepted at
level 1 while that guarantee remains absent — accepting the packet accepts the disclosure, not the
capability.

Measured illustration: the MOD-0140 disclosure was a high-quality packet by every measure in this
document, and the correct conclusion from accepting it was that the actor-binding, revocation-
fencing and affirmative Tenant+LE eligibility guarantees **do not exist in either codebase**. Level
1 acceptance of that packet must never be readable as level 2 or 3 for those guarantees.


## 6 · Three different negatives — and only one of them is BLOCKED

**The sharpest finding of the external review: v1.2's refusal conditions said "cannot answer from
evidence ⇒ BLOCKED" while §5 permitted accepting a packet that honestly discloses absence. Those
contradicted. They are three distinct states.**

| state | means | where it goes | blocks which acceptance |
|---|---|---|---|
| **absent capability** | the source genuinely does not provide it, and says so | **§L**, specifically | **blocks nothing at level 1.** Blocks level 2/3 only if a destination requirement depends on it |
| **unverified claim** | it may exist; this author could not measure it, and says why | **§N**, with the reason | blocks **level 2** for the dependent behaviour; never level 1 |
| **blocked migration** | the packet cannot be *written* — see the conditions below | the packet returns BLOCKED on that section | blocks **level 1**, and therefore everything |

So: "MOD-0140 provides no actor binding" is an **absent capability** — disclosed, packet
acceptable, and the correct reading is that the guarantee does not exist. "I could not reach the
service to confirm the error envelope" is an **unverified claim** — §N, level 1 fine. "The ID fails
DCP-002" is a **blocked migration** — nothing proceeds.

An author who files an absent capability as a blocker has stalled honest work; one who files a
blocker as an absent capability has smuggled a defect past level 1.

### Conditions that make it a blocked migration

- The DCP-002 gate does not pass for the ID and canonical name (**T1**).
- The module already exists in the destination (**T10**) — relabel as fork resolution.
- A mandatory *field* is blank with no disclosure — as distinct from a disclosed absence (**T27**, §6).
- Credential-like material cannot be classified (**T20**) — quarantine and escalate.
- Answering would require changing the source repository (**T9**).
- A destination requirement can be traced only to the source's rulebook (**T4**).
- Transfer permission out of the source is unstated (**T28**).
- A deviation from a destination mandatory decision is proposed without owner and expiry (**T32**).

## 7 · Where the packet sits in the migration sequence

| step | owner | the packet's role |
|---|---|---|
| 1 · inspect the source | source side | **this is the packet.** Disclose what exists, what is missing, what cannot be verified |
| 2 · approve destination scope | destination owner / CT | module pack, ownership, dependency contracts, ID mappings — authored FROM the packet |
| 3 · transfer and adapt | destination | preserve provenance; document intentional differences (T21, T22) |
| 4 · verify behaviour | destination | builds, tests, runtime flows, security checks — in the destination (T24) |
| 5 · cut over | destination owner | reconcile data, confirm consumer compatibility, execute with rollback available (T24) |

Preserve ownership, behaviour, security and contract compatibility. **Copying files alone does not
establish a successful migration.**

## 8 · What these rules are not

They do not authorise intake, reserve any ID, approve any capability pack, or permit code to move.
They govern the shape and the honesty of the evidence a module brings with it. Approval remains
CAP-001 and the owner's.

---

## 9 · Traceability — the eight highest-risk controls, mapped

The external review listed eight controls whose presence it marked **unverified, not necessarily
missing**. Mapped to clauses so the next review can be clause-by-clause rather than exploratory:

| control | covered by | status in v1.2 |
|---|---|---|
| Immutable provenance — repo, commit, dirty state, digests, permission to transfer | **T9, T26, T28** | dirty-state disclosure and transfer permission were absent in v1.1; **added as T28** |
| Contract verification — operation-by-operation parity, versions, compatibility, consumer acknowledgment | **T5, T11, T29** | per-operation parity and digests were present; consumer acknowledgment was absent; **added as T29** |
| Security equivalence — trusted tenant/LE context, bindings, authorization, revocation fencing, outage behaviour | **T14, T15, §G** | present; outage behaviour is carried in §K and T15 |
| Data migration — ID/reference mappings, indexes, lifecycle and deletion semantics, reconciliation, repeatability | **T13, T17, T30** | mappings and lifecycle were present; indexes, reconciliation and repeatability were absent; **added as T30** |
| Side-effect integrity — concurrency, idempotency, audit/event ordering, transactions or compensation | **T16, §H** | present |
| Runtime proof — golden flow, controlled failure, reload persistence, explicit mock/fixture exclusion | **T6, T24** | present |
| Cutover and rollback — entry/exit gates, rollback triggers, writes during transition, post-cutover monitoring | **T24, T31** | T24 required the proofs but named no gates or triggers; **added as T31** |
| Evidence integrity — secrets excluded, measured vs inherited, exceptions with owners and expiry | **T7, T20, T21, T32, T33** | secrets and measured-vs-inherited were present; exception owner and expiry were absent; **added as T32** |

## 10 · What v1.2 changed, and what it did not

**Accepted from the review and fixed**

- supersession qualified — v1.0 and v1.1 were proposals; nothing operative is withdrawn
- accountable approver named, with a required owner-decision record under `docs/records/decisions/`
- relative "this folder" replaced by absolute predecessor paths and SHA-256 digests
- module boundary tightened — three disjoint lists and dependency-owner acknowledgment (**T3**)
- packet acceptance separated from implementation acceptance and release authorization (**§5**)
- source/destination conflict resolution and deviation approval with expiry defined (**T32**)
- six controls added: **T28–T33**

**Corrected in the review, with evidence**

The review stated that "AGENTS.md ... does not contain a §6.1 rule-map or an 'orphaned rule'
requirement", citing `/Users/natig/Projects/ERP-vNext/AGENTS.md`. Measured:

    destination  /Users/natig/Projects/ERP-vNext-recovery/AGENTS.md
                 line 192  "## 6.1 Kural Haritası — hangi işte hangi kural"
                 line 247  "Yeni bir kural dosyası eklendiğinde **aynı commit'te** buraya
                            satırı yazılır; yazılmayan kural yetimdir."
    source       /Users/natig/Projects/ERP-vNext/AGENTS.md
                 has "## 6. Runtime Kararları"; zero matches for 6.1 / Kural Haritası / yetim

The citation was accurate for the repository this document governs. The review read the source's
rulebook — the same root cause as its own earlier `_LayoutBackbone` and seven-language assertions,
and as its inability to locate this file. **T4 is extended in v1.2 to bind reviewers**, and the
header now states the governing repository before any citation appears. The reviewer's underlying
objection — that the citation was ambiguous about which repository — is accepted and fixed.

**Not changed**

Every identity, contract, dependency-gate, conformance and behaviour finding. The rule numbering of
T1–T27 is stable; v1.2 only appends T28–T33 and renumbers sections 5 onward.

---

## 11 · What v1.3 changed — the second external review, accepted in full

All six substantive findings and both editorial fixes are accepted. No finding was disputed.

| finding | fix |
|---|---|
| §4 shape omitted T28–T32 fields | **§4a** adds explicit sections **O–S** with per-field requirements and an acceptance checklist; a blank field blocks its gating acceptance level |
| Refusal conditions conflicted with honest incomplete disclosure | **§6** rewritten as three distinct negatives — *absent capability* (§L, blocks nothing at level 1), *unverified claim* (§N, blocks level 2 for the dependent behaviour), *blocked migration* (blocks level 1) — each mapped to the level it blocks |
| T31 required destination-owned work during source inspection | **§4a** splits the artifact: an **immutable producer packet** (A–L, O, P) and a **destination supplement** (M, N, Q, R, S), with separate completion gates |
| T3's three "disjoint" lists were a category error | **T3** replaced with two independent axes — *relationship* (owned/consumed/shared) and *transfer disposition* (five values) — every object carrying one value on each |
| T26 cited the wrong clause | **T26** corrected, and the correction is larger than expected — see below |
| Rollback underspecified for partial effects | **T31** now enumerates queued writes, **already-published events**, destination-only post-cutover writes, and irreversible effects with recovery or reconciliation steps rather than rollback |
| §2 pointed to §6 instead of §7 | fixed |
| §10 said "five controls" for six | fixed |

### The T26 finding was correct, and tracing it found something larger

The review was right that docs-organization **K5** supports unedited vendoring while **K6 concerns
illustrated user manuals**, not manifests. Tracing the correct home for the manifest requirement
found it has none:

    committed  ff5777afe  2026-10-04  fix(supply-chain): MOD-0183 UX, l10n, timestamps, ...
    REVERTED   63fa9cd59  2026-10-06  revert(mvp6): R-1 .antigravity/ and AGENTS.md return
                                      to origin/main   — cut 120 lines from
                                      docs-organization.md, 247 -> 151

The current rulebook returns **zero** matches for `Manifest hangi dizinden` or `verify from`. The
manifest convention was destination policy for two days and was then deliberately reverted.

Meanwhile **66 record manifests** under `docs/records/audits/2026-10/` still open with that first
line, including several written on 2026-10-11. They are therefore following a convention with no
clause behind it.

T26 now asserts the manifest requirement as **TP-001's own**, and flags the gap. Re-adopting it into
`.antigravity/` is a separate owner decision and is deliberately **not** bundled into TP-001's
promotion — bundling an unrelated rulebook restoration into this approval would be exactly the kind
of silent scope expansion T32 exists to prevent.

### Rule and section stability

T1–T33 numbering is unchanged; T3, T26 and T31 are rewritten in place. Sections 1–3 are unchanged,
**§4a is new**, §5–§10 keep their numbers, **§11 is this changelog**. The producer/destination split
reassigns existing sections between two documents without renaming any of them.
