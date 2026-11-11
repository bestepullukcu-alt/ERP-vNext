# Module transfer packet — rules (TP-001)

    status   : PROPOSAL / UNAPPROVED. Written to standard shape.
    placement: `.antigravity/rules/` is the functional home and is a PROTECTED PATH
               (AGENTS.md §4). Promoting this file there is an owner decision and must
               carry a §6.1 rule-map line in the SAME commit — a rule absent from the map
               is orphaned and therefore invisible.
    created  : 2026-10-11
    scope    : applies whenever a module developed in another repository is proposed for
               intake here. One packet per MODULE. Not per service, not per domain.

Every rule carries its evidence class, per the SOP convention:
**MEASURED CASE** (a real incident measured this session) · **OBSERVED** (measured in repo) ·
**DERIVED CONTROL** (preventive, no incident yet) · **POLICY** (governance requirement).

---

## Why one packet per module, and not per service

**MEASURED CASE.** The ten modules proposed for intake on 2026-10-11 live across **three**
producer services — `DitenErpTreasuryService`, `Diten.Platform` and `DitenMdmService`. A
service-shaped request would have produced one packet covering six modules and silently omitted
four. A module-shaped request cannot.

---

## The rules

### T1 · The canonical name is the Blueprint's, verbatim — never the display name
**MEASURED CASE.** Of ten modules supplied by display name, **eight failed the DCP-002 gate on
canonical-name mismatch alone**: `Accounts Payable` vs `Accounts Payable (AP)`, `Legal Entities`
vs `Corporate Secretarial / Entity Management`, `Contract Lifecycle` vs `Contract Lifecycle
Management (CLM)`. Every ID was valid; every name was not. A packet headed by a display name
cannot be used as a work order.

The packet carries the ID, the Blueprint canonical name character-for-character, and the pasted
output of
`python3 .antigravity/scripts/verify_module_id.py . --check-id <ID> --name "<canonical>"`.
A packet without a passing gate line is **not accepted for reading**.

### T2 · Four states, never one word: DECLARED · IMPLEMENTED · WIRED · RUNNING
**MEASURED CASE.** Three separate conflations surfaced in one day. MOD-0003 is *declared* in a
pack with **zero** implementation. MOD-0119's pack says `status: draft` / *"frontend-only mockup"*
while a working `AccountsPayableController` exists — *implemented but not declared*. The entire
`DitenErpTreasuryService` is implemented but **absent from its own gateway's `ocelot.json`** —
*implemented, not wired*. And no port probe succeeded, so nothing was shown *running*.

The packet states all four, per operation, as separate columns. "Ready" and a percentage are not
answers to any of them.

### T3 · A fail-closed stub, a fixture and an adapter are not an implementation
**MEASURED CASE.** MOD-0003's entire presence on the producer side is
`FailClosedSourceContractValidationClient` and `FixtureSourceContractValidationClient`, whose own
throw message reads *"once MOD-0003 ships a stable data-contract registry route"*. A packet that
reported "MOD-0003 present" would have been true by file count and false in every way that
matters.

Where a dependency is satisfied by a stub, fixture, mock, fail-closed client or adapter, the
packet names the class and says so. **Producer implementation or nothing.**

### T4 · A number you did not produce yourself does not appear as if you had
**POLICY**, and the phrasing is Control Tower's. The readiness percentages that opened this intake
were corroborated by neither the producer's packs nor any measurement. They were reproduced for
context and attributed; they were never used to size anything.

Every figure in a packet is either measured by its author with the command shown, or attributed to
its source and marked unverified.

### T5 · Cite a path and a commit, or move the claim to the limits section
**MEASURED CASE.** The MOD-0140 disclosure of 2026-10-11 pinned every claim to a
repository-relative path plus its last-changing commit plus an immutable inspected snapshot, and
byte-compared each cited file before writing. That is why it was usable despite being a
self-report from a repository this side never inspected.

Unciteable claims are not deleted — they move to **"what I could not verify and why"**, which is a
mandatory section.

### T6 · State the inspected snapshot, and that nothing was changed
**POLICY.** A packet is a read-only act. It names the commit it was taken at, confirms no source,
config, pack or contract was modified, and confirms nothing was staged, committed, pushed or
stashed. A packet whose own tree moved while it was written describes a repository that no longer
exists.

### T7 · The receiving repo must already know whether the module exists here — measured
**MEASURED CASE.** MOD-0140 was assumed absent, then found present, then found to be a **confirmed
fork** with a different domain, service, port, route and API shape. MOD-0220 is a probable second
fork on the same pattern. Both were discovered only because someone looked.

Before a packet is requested, the receiving side records: registry row, pack, and code presence
here. A packet requested for a module that already exists here is a **fork-resolution** task, not
an intake, and must be relabelled before any work proceeds.

### T8 · Contract authority: digest, version, operation list — or the word ABSENT
**MEASURED CASE.** Twice in one session. MOD-0140's producer has **no** SUPPLIER OpenAPI at all,
after scanning 134 candidate files. And our own copy moved `1.0.0 → 1.2.0`, **three operations to
nine**, through a merge that nobody reading the packs would have noticed; two documents were
corrected on 2026-10-05 to describe a surface that was already stale.

The packet carries the contract path, version, `x-status`, SHA-256 and the full `operationId`
list — or states that no contract exists, which is a finding and not an omission. Where the
receiving side holds a copy, the packet answers **by digest** whether they are byte-identical.

### T9 · Name every dependency gate AND measure each one's implementation state
**MEASURED CASE.** The Blueprint's `Dependency Gate (must exist first)` column named ten
capabilities for this intake. Seven were *reserved* in our registry and that was briefly read as
"present". Measured: MOD-0004 and MOD-0063 have **zero** source files, MOD-0003 has zero
(an earlier count of three was a false positive — three HcmService employee-validator files
containing the substring `DataContract`), and MOD-0005 and MOD-0030 are not reserved at all. One
named gate, "Document Management", resolves to no Blueprint module by that name.

Reserved is not built. The packet states, per gate: Blueprint ID, reserved here yes/no,
implemented here with a file count and a locator, and whether the producer depends on it through a
stub (see T3).

### T10 · Lifecycle and delete semantics for every reference that crosses the boundary
**MEASURED CASE / DERIVED CONTROL.** Only an opaque `supplierId` crosses the MOD-0140 boundary, and
`deleteSupplier` plus `bulkDeleteSuppliers` appeared in the contract between two readings. A
consumer holding an opaque id and receiving no deletion event simply starts failing.

For each identifier the consumer will hold, the packet states: hard delete, soft delete or
tombstone; whether any event or notification is emitted; whether a tombstone remains queryable;
and the referential retention commitment. **"Not provided" is an acceptable answer. Silence is not.**

### T11 · The conformance delta is measured against the receiving repo's mandatory decisions
**MEASURED CASE.** The producer's treasury code was audited against AGENTS.md §6 on 2026-10-11.
Backend largely conformed — five layers, MediatR, `Response<T>`, Mongo, TenantId. Frontend largely
did not: **all 7** views on a `_LayoutBackbone` that does not exist here, **1 of 9** `.cshtml` with
DataTable v2, **zero** SweetAlert2, **zero** pipeline behaviours, **zero** manifest providers,
`DeletedAt` in 3 files against `IsDeleted` in 22.

The packet carries a row per §6 decision with a measured verdict: conforms · partial · rewrite ·
add. The receiving side writes this half, not the producer — it is our standard, not theirs.

### T12 · Express remediation as files and clusters. No time estimates
**POLICY**, carried from the Control Tower lane contracts. A packet sizes work as paths and
defect clusters. It does not estimate duration, and a percentage is not a size.

### T13 · Disclose pack-versus-code disagreement; never reconcile it silently
**MEASURED CASE.** MOD-0119's pack says `draft` and *"frontend-only mockup"*; the controller
exists and works. Our own MOD-0147/0148 packs described three contract operations when nine were
live. Both repositories had governance artifacts that disagreed with their own code.

Where the producer's pack and the producer's code disagree, the packet shows both and marks the
disagreement. The reader decides which is authoritative. An author who picks silently has
destroyed the evidence that the two had drifted.

### T14 · No secret values. Digests and lengths only
**MEASURED CASE.** The 2026-10-05 merge gate measured 45 `JwtSettings.Secret` values across 46
tracked config files, reported entirely as truncated SHA-256 digests and counts, and found ten
further copies of one digest in committed build output that the gate's own wording had not
scoped. Not one value was printed.

A packet references credential-like material by truncated digest and length, never by value — and
notes that **repository sanitation is not rotation.**

### T15 · "What it does NOT provide" is mandatory, and it is not padded
**MEASURED CASE.** The MOD-0140 disclosure's exclusions section was the single most decision-
relevant part: it established that the DC-02/DC-03 producer seam — actor binding, binding revision,
revocation fencing, affirmative Tenant+LE eligibility — exists in **neither** codebase. That
converged with our own measurement and settled a question two lanes had been circling.

The section is required, it is specific, and padding it to look thorough defeats it.

### T16 · The packet is a document. It is never a code drop
**POLICY.** Arriving code must clear the receiving repo's gates — DCP-002 identity, an
`approved`/`ready-for-dev` module pack, domain ownership, the twelve §6 decisions, self-
registration, permission keys and the port scheme. A packet that arrives as a branch or an archive
bypasses all of them.

The portable unit is the **contract, the field model, the test vectors, the state machine, the
permission keys, the error vocabulary and the decision records**. Handlers are re-expressed against
our pipeline.

### T17 · Versioned filename, own manifest, vendored unedited
**POLICY** (docs-organization K5, K6). The packet filename carries its version. It ships with a
manifest whose first line declares the verification directory. On arrival it is placed under
`docs/vendor/<package>/` **unedited**, with provenance recorded; derived working files go where the
five questions put them, never into `vendor/`.

### T18 · BLOCKED is a valid and preferred outcome
**MEASURED CASE.** Two lanes this session returned BLOCKED on measurement rather than proceeding:
the merge gate stopped because eight overlap files were uncommitted, and this intake's membership
fell from five to one when MOD-0003 was verified. Both outcomes cost two commands and saved an
approval cycle spent on an absent foundation.

A packet that cannot answer a mandatory section says BLOCKED on that section and why. An author who
fills a gap with an inference has produced a worse artifact than one who stops.

---

## Required packet shape

Fixed section letters, so packets for different modules are comparable and diffable.

| § | content | governing rules |
|---|---|---|
| A | identity: ID, Blueprint canonical name, gate output, pack path + status, owning domain, named human owner | T1, T13 |
| B | contract: path, version, `x-status`, SHA-256, full `operationId` list — or ABSENT; digest parity with the receiving side's copy | T8 |
| C | per-operation state table: declared · implemented · wired · running, with locators | T2, T5 |
| D | dependency gates: Blueprint ID, reserved here, implemented here with file count, stub-satisfied yes/no | T9, T3 |
| E | data: entity/field model, validation rules, state machine, permission keys, error-code vocabulary | T5 |
| F | identifiers crossing the boundary: delete/lifecycle semantics, events, tombstones, retention | T10 |
| G | UI specification — fields, columns, filters, row actions, states. **Not their view files** | T11, T16 |
| H | governance: decisions recorded, named approvers, open items, what is NOT approved | T13, T15 |
| I | limits: pagination, rate limits, idempotency, concurrency, availability | T5 |
| J | **what it does NOT provide** — specific, unpadded | T15 |
| K | conformance delta against §6, written by the RECEIVING side | T11, T12 |
| L | **what I could not verify, and why** | T5, T18 |

A packet missing §J or §L is incomplete regardless of the other ten.

## Refusal conditions — the packet must return BLOCKED

- The DCP-002 gate does not pass for the ID and canonical name (**T1**).
- The module already exists in the receiving repository (**T7**) — relabel as fork resolution.
- A mandatory section cannot be answered from evidence (**T18**).
- Credential-like material cannot be classified (**T14**) — quarantine and escalate.
- The packet would require changing the producer repository to answer (**T6**).

## What these rules are not

They do not authorise intake, reserve any ID, approve any capability pack or permit any code to
move. They govern the shape and honesty of the evidence a module brings with it. Approval remains
CAP-001 and the owner's.
