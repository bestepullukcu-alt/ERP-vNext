# Transfer packet request — what to send the source project

    version : 1.0
    governs : derived from TP-001 v1.5 (sha256 3a60135eff9a84dc4fdef39c42b113c8c99f1c4c8df326aca4a817b30458ea65)
    purpose : TP-001 is OUR acceptance standard — 774 lines the source never needs to read.
              THIS is what we send them. It asks for the producer packet only.
    use     : fill the parameter block, paste the request, one send per MODULE.

## Who writes what — the reason this request is short

TP-001 §4a splits the artifact in two. The source writes **only** the producer half:

    producer packet   §A–L, §N, §O, §P        the source writes this. It is what we request.
    destination supp. §M, §Q, §R, §S, §T, §U  WE write this. Never ask the source for it.

So the request below never mentions conformance deltas, cutover gates, reconciliation queries or
rollback triggers. Those are ours, they depend on our rulebook, and they cannot exist while the
source is inspecting itself. Asking for them produces either guesses or a stalled lane.

**What their packet earns is acceptance level 1 only** — "the evidence is complete, honest and
citeable, so the module can now be scoped". It is not a statement that anything works. Tell them
so, in the request, so they do not over-claim.

---

## Parameter block — fill before sending

    MODULE ID            : MOD-____
    CANONICAL NAME       : ____________________  (Blueprint verbatim — run our gate first)
    GATE OUTPUT          : paste the OK line from
                           python3 .antigravity/scripts/verify_module_id.py . \
                             --check-id MOD-____ --name "____"
    EXISTS HERE?         : registry rows ___ · pack ___ · code refs ___
                           (if ANY is non-zero this is FORK RESOLUTION, not intake — stop and relabel)
    WE ALREADY HOLD      : contract path + version + sha256, or "nothing"
    THEIR LOCATION       : which of their services hold it (there may be more than one)

---

## The request — paste from here down

```
TRANSFER PACKET REQUEST — <MODULE ID> <CANONICAL NAME>

  requested by : ERP-vNext-recovery (destination)
  mode         : READ-ONLY INSPECTION. Change nothing, fix nothing, commit nothing.
  deliverable  : ONE versioned markdown file + ONE sha256 manifest. Nothing else.
  scope        : sections A–L, N, O, P below. Sections M, Q, R, S, T, U are ours and are
                 NOT requested — do not attempt them.

WHAT THIS EARNS
  A complete packet earns "packet acceptance": your evidence is complete, honest and
  citeable, so we can scope the module. It is NOT a claim that anything works, and we will
  not read it as one. Disclosing an absence costs you nothing; implying a guarantee you
  cannot evidence costs us a defect.

WHAT WE ALREADY KNOW — correct us if any of it is wrong
  module            : <MODULE ID>, canonical name "<CANONICAL NAME>"
  our gate result   : <GATE OUTPUT>
  present here      : <EXISTS HERE?>
  we hold           : <WE ALREADY HOLD>
  your location     : <THEIR LOCATION>

HOW TO ANSWER — these eight rules decide whether the packet is usable

  1  CITE OR OMIT. Every factual claim carries a repository-relative path AND the commit
     that last changed it, against one immutable snapshot you name up front. A claim you
     cannot cite goes in section N, not in the body.

  2  FOUR STATES, NEVER ONE WORD. For every operation, answer separately:
       DECLARED (in a pack or contract) · IMPLEMENTED (code exists) ·
       WIRED (routed/registered/composed) · RUNNING (observed live).
     "Ready", "done" and a percentage answer none of these.

  3  A STUB IS NOT AN IMPLEMENTATION. Where something is satisfied by a fail-closed client,
     a fixture, a mock, a no-op gateway or an unimplemented adapter, name the class and say
     so. We have already been caught by this once: a dependency reported present turned out
     to be a fail-closed client and a fixture, and the module depending on it was listed at
     ~80%.

  4  MEASURED OR ATTRIBUTED. Every number is either measured by you with the command shown,
     or attributed to its source and marked unverified. A number you did not produce
     yourself must not appear as if you had.

  5  NO SOURCE LABELS AS READINESS. "done", "FROZEN", "production-ready" and percentages are
     your assessment of yourself. Report them as such. We will not convert them into
     readiness here without our own evidence.

  6  DISCLOSE PACK-VERSUS-CODE DISAGREEMENT. Where your pack and your code disagree, show
     both and mark the disagreement. Do not pick silently — the disagreement is itself the
     finding. We have seen a pack reading "draft / frontend-only mockup" beside a working
     controller.

  7  NO SECRET VALUES, EVER. Reference credential-like material by truncated SHA-256 and
     length, never by value. Note that removing a secret from a tree does not revoke it.

  8  BLOCKED IS A GOOD ANSWER. If a section cannot be answered from evidence, write BLOCKED
     and why. An inference that fills a gap is worse than a gap.

SECTIONS — answer all sixteen

  A  IDENTITY
     Your pack path and status; your canonical name for it; the accountable HUMAN owner
     (if your records name an agent label rather than a person, say so); owning domain and
     service.

  B  CONTRACT AUTHORITY
     Path, version, x-status, SHA-256, and the FULL operationId list — or the single word
     ABSENT, which is a finding and not an omission. If we said we hold a copy, answer by
     digest whether yours is byte-identical, and list every incompatible difference.

  C  PER-OPERATION STATE
     One row per operation: the four states from rule 2, each with a locator. Include the
     route, the HTTP method and the controller or handler that serves it.

  D  DEPENDENCY GATES
     Every capability this module requires first. Per gate: its module ID, whether it is
     implemented on YOUR side with a file count and locator, and whether your module reaches
     it through a stub (rule 3). This section decides whether the module is buildable here,
     so it is the one we read first.

  E  DATA
     Entity and field model; validation rules; the state machine with its legal transitions;
     permission keys as literals; the error-code vocabulary. If any stable ID would change
     on transfer, give the mapping.

  F  IDENTIFIERS CROSSING THE BOUNDARY
     For every identifier a consumer holds: hard delete, soft delete or tombstone; whether
     any event or notification is emitted; whether a tombstone stays queryable; the
     referential retention commitment. "Not provided" is acceptable. Silence is not.

  G  SECURITY
     How trusted tenant context is resolved; where the authorization boundary sits; what a
     cross-tenant read returns; and whether LegalEntity eligibility is evaluated CURRENTLY or
     read from a stored snapshot — a snapshot is not eligibility. State whether actor
     binding, binding revision and revocation fencing exist. TTL caching is not a revocation
     guarantee.

  H  MUTATION GUARANTEES
     Concurrency control; idempotency scope; transaction boundaries; audit and outbox
     behaviour; and the ORDERING of side effects relative to the commit point. Where the
     ordering admits a partial effect — a side effect that lands before a later check fails —
     name it. A failed request must not be assumed to have produced nothing.

  I  UI SPECIFICATION
     Fields, columns, filters, row actions, states, empty and loading behaviour, and which
     languages exist. Send the SPECIFICATION, not your view files: our layouts, table
     contract and dialog standard differ from yours, so the views will be rebuilt here
     regardless.

  J  GOVERNANCE
     Decisions recorded; named approvers; open items; and specifically what is NOT approved.

  K  LIMITS
     Pagination, rate limits, idempotency, concurrency, availability expectations,
     versioning and deprecation policy.

  L  WHAT IT DOES NOT PROVIDE
     Explicit, specific, and not padded to look thorough. This is the section we weight
     most heavily. The last packet we received established through this section that a
     guarantee two lanes had been assuming existed in neither codebase.

  N  WHAT YOU COULD NOT VERIFY, AND WHY
     Your own limits — not ours. Anything you could not measure, with the reason. This
     section is mandatory and it stays in your packet permanently.

  O  PROVENANCE
     Source repository path; the exact commit you inspected; the DIRTY STATE of that
     checkout at measurement (which files were uncommitted, so we can tell committed bytes
     from working-tree bytes); the digest of every artifact you send; and WHO AUTHORISED the
     transfer out of your repository.

  P  CONSUMERS AFFECTED
     Anyone depending on a contract, operation set, identifier or lifecycle guarantee that
     this transfer would change. Per consumer: acknowledgment received, or the named party
     who still owes it. We will not release against an open acknowledgment, so an honest
     "owed by X" here is more useful than a blank.

DELIVERABLE
  1  <MODULE-ID>-transfer-packet-v1.0.md       sections A–L, N, O, P
  2  <MODULE-ID>-transfer-packet-v1.0.sha256   first line: "# verify from: this folder",
                                               then the digest of file 1 and of any contract
                                               you attach
  Put the version in the filename. Once issued, treat the packet as IMMUTABLE — we cite its
  digest downstream. A correction is a NEW version with a new digest, never an edit.

DO NOT
  commit, push, branch, stage or stash · modify any source, config, pack or contract ·
  fix a defect you find (name it with its locator instead) · print any secret value ·
  send code, a branch or an archive — this is a document request
```

---

## Worked example — MOD-0126, the only currently viable member

Parameters measured 2026-10-11 in `ERP-vNext-recovery` at HEAD `2acce233b`:

    MODULE ID            : MOD-0126
    CANONICAL NAME       : Payments & Approvals
    GATE OUTPUT          : OK  MOD-0126: proven against Blueprint/registry.
    EXISTS HERE?         : registry rows 0 · pack 0 · code refs 0
                           → clean intake, NOT a fork (contrast MOD-0140 and MOD-0220,
                             which are present here and must be relabelled)
    WE ALREADY HOLD      : nothing — no payments, bank, treasury or cash contract exists
                           in docs/analysis/contracts/
    THEIR LOCATION       : services/DitenErpTreasuryService (PaymentBatchesController)
                           services/Diten.Platform

**Why MOD-0126 and not the others.** Measured against the Blueprint's dependency-gate column,
MOD-0126 is the only member of the proposed five whose gates are met here: Workflow Designer
(MOD-0023, 199 files) and Audit Trail Service (MOD-0021, 113 files) are both implemented.
MOD-0119, MOD-0120, MOD-0124 and MOD-0289 are all gated on MOD-0003 Data Contract Registry, which
has **zero** implementation in either repository.

**Still blocking even for MOD-0126:** its third gate is MOD-0269 Banks/SWIFT/Payments Gateway,
which the Blueprint classes as *External Systems Register / External Provider*. That is a provider
and interface decision, not a build — and section D of the returned packet should confirm how their
MOD-0126 reaches it today.

## Sending this for the other modules

For MOD-0140 and MOD-0220, **do not send this request.** Both already exist here, so TP-001 T10
makes them fork resolution rather than intake. MOD-0140's comparison is written at
`docs/records/audits/2026-10/mod-0140-dual-implementation-01/`; MOD-0220's has not been done.

For MOD-0119, MOD-0120, MOD-0124 and MOD-0289, the request is premature until MOD-0003 exists —
section D would come back saying so, which we already know. Requesting it anyway buys a document
that restates a measured blocker.
