# Module transfer packet — rules (TP-001 v1.1)

    version   : 1.1 — supersedes the v1.0 draft (TRANSFER-PACKET-RULES.md) in this folder
    status    : PROPOSAL / UNAPPROVED. Written to standard shape.
    created   : 2026-10-11
    scope     : applies whenever a module developed in ANOTHER repository is proposed for
                intake here. One packet per MODULE — not per service, not per domain.
    placement : `.antigravity/rules/` is the functional home and is a PROTECTED PATH
                (AGENTS.md §4). Promoting this file there is an owner decision and must
                carry a §6.1 rule-map line in the SAME commit — AGENTS.md states that a
                rule absent from the map is orphaned.

v1.1 merges four rules contributed from the migration-governance ruleset (T19–T22) and adds
T4, the destination-standards rule, after a real ruleset was measured asserting source
standards as destination requirements.

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
inspection step** — step 1 of the five-step sequence in §6. It establishes what exists, what is
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

#### T3 · Separate owned objects from consumed services
**POLICY.** The packet distinguishes what the module is the system of record for from what it
merely consumes. Dependencies migrate only when explicitly included; otherwise the packet names
the adapter and the destination availability gate that must exist first.

#### T4 · Destination standards are read from the DESTINATION's AGENTS.md
**MEASURED CASE.** A migration ruleset was measured on 2026-10-11 asserting that ERP-vNext
"requires `_LayoutBackbone.cshtml`" and "seven languages". Both came from the **source** repo's
`AGENTS.md`. Measured here: `_LayoutBackbone.cshtml` **does not exist** in the destination — its
layouts are `_LayoutPlatformAdmin` (admin), `_LayoutTenantShell` (tenant) and a frozen `_Layout` —
and localisation is **Platform 2 languages (en, tr) · Tenant 7**, not a flat seven. The source
mandates a flat seven and `_LayoutBackbone`.

Had that ruleset governed the move, treasury views would have been ported onto a layout that is
not here — the precise defect the conformance audit found in all 7 of the producer's views.

Every destination requirement in a packet cites the **destination's** AGENTS.md by section. A
requirement traceable only to the source's rulebook is a source fact, and belongs in the
conformance delta (T15) rather than in the requirements.

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
**POLICY** (docs-organization K5, K6). The filename carries its version. The packet ships with a
manifest whose first line declares the verification directory. On arrival it is placed under
`docs/vendor/<package>/` **unedited**, with provenance recorded; derived working files go where the
five questions put them, never into `vendor/`.

#### T27 · BLOCKED is a valid and preferred outcome
**MEASURED CASE.** Two lanes returned BLOCKED on measurement rather than proceeding: a merge gate
stopped because eight overlapping files were uncommitted, and this intake's membership fell from
five to one when MOD-0003 was verified. Each cost two commands and saved an approval cycle spent on
an absent foundation.

A packet that cannot answer a mandatory section says BLOCKED on that section and why. An author who
fills a gap with an inference has produced a worse artifact than one who stops.

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

## 5 · Refusal conditions — the packet must return BLOCKED

- The DCP-002 gate does not pass for the ID and canonical name (**T1**).
- The module already exists in the destination (**T10**) — relabel as fork resolution.
- A mandatory section cannot be answered from evidence (**T27**).
- Credential-like material cannot be classified (**T20**) — quarantine and escalate.
- Answering would require changing the source repository (**T9**).
- A destination requirement can be traced only to the source's rulebook (**T4**).

## 6 · Where the packet sits in the migration sequence

| step | owner | the packet's role |
|---|---|---|
| 1 · inspect the source | source side | **this is the packet.** Disclose what exists, what is missing, what cannot be verified |
| 2 · approve destination scope | destination owner / CT | module pack, ownership, dependency contracts, ID mappings — authored FROM the packet |
| 3 · transfer and adapt | destination | preserve provenance; document intentional differences (T21, T22) |
| 4 · verify behaviour | destination | builds, tests, runtime flows, security checks — in the destination (T24) |
| 5 · cut over | destination owner | reconcile data, confirm consumer compatibility, execute with rollback available (T24) |

Preserve ownership, behaviour, security and contract compatibility. **Copying files alone does not
establish a successful migration.**

## 7 · What these rules are not

They do not authorise intake, reserve any ID, approve any capability pack, or permit code to move.
They govern the shape and the honesty of the evidence a module brings with it. Approval remains
CAP-001 and the owner's.
