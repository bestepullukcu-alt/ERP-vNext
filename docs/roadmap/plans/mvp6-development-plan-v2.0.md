# MVP-6 development plan v2.0

Date: 2026-09-16. Planning WP: MVP6-CT-REPLAN-02.
Owner: local Control Tower; central owners retain publication and shared-governance authority.
Branch: feature/mvp6-logistics. Inspected HEAD: 4a8d4d4b339528a88e6220fb8402e5a2c771136c.
This is the current local continuation plan. It supersedes current-state/next-work statements in
mvp6-logistics-development-plan.md and mvp6-logistics-continuation-2026-09-16.md; it does not
supersede frozen contracts, central decisions, or immutable HELD DEV/VER v1.0 prompts.

## 1. Authority and current state

Apply AGENTS.md authority order, approved pack/domain boundaries and user scope decisions.
CT SOP v2.4 and operating card govern the requested workflow; the SOP still declares
Proposed canonical. This plan does not change its adoption status or elevate it above AGENTS.md.

- MOD-0183 bounded slice is centrally ACCEPTED at the inspected commit, E4. Full module/E5/G5
  are not accepted. See [central acceptance](../../records/audits/2026-09/mvp6-mod0183-ct-review-01-2026-09-16.md).
- MOD-0184 preparation is complete and the Shipment prerequisite is satisfied for preparation.
  Pack remains draft, Phase 1.5 HELD, DEV/VER v1.0 HELD. No runtime dispatch is READY.
- Lifecycle, exact case-sensitive scoped code reservation, composition design, atomic persistence
  and bounded direct-service integration design were approved. Errors/replay decisions exist but
  GAP-184-04/05 require authorized contract publication and uptake.
- Proposed SHIPMENT-BUNDLE 1.1.0 is NOT PUBLISHED. Canonical remains 1.0.0, wire v1.
- Consumer technical evidence is complete enough for disposition; it is not consumer consent:
  [three-lane results](../../records/audits/2026-09/mod-0184-consumer-technical-evidence/README.md).
- CARRIER-FIXTURE-01 is a specific remaining technical finding: four inherited create422 examples
  include foreign lifecycle codes and error.details, inconsistent with bounded Carrier output policy.
- Architecture 15 PASS / 3 deferred external FAIL is historical verified evidence, not a fresh rerun.
  Repository gate remains BLOCKED. Platform DB-010 and HCM/Talent JWT remain outside this lane.

## 2. Critical path — stop repeating general preparation

Disposition CARRIER-FIXTURE-01 → if needed, narrowly revised candidate and hashes → independent
technical recheck → owner/security and actual-consumer dispositions → authorized single publication
→ contract uptake → Phase 1.5/pack approval → new DEV prompt → Carrier implementation → independent
VER → central bounded acceptance → downstream preparation/development gates.

Do not request new generic readiness reports for already settled decisions. An owner must select the
specific example policy; technical agents must not invent a new create business rule or 422 code.
If the exact patch changes, existing evidence remains historical and affected checks rerun against
new hashes. Shared Unprocessable components used by Shipment must remain unchanged.

## 3. Work and lane dashboard

All future IDs below are proposed WP identities, not new MOD identities or dispatched work.
One runtime writer maximum. No delivery dates or blocker ages are invented; first record dates are
2026-09-16 and elapsed age should be measured at dispatch. No repeated runtime rework is currently active.

| Seq | WP | Module / build lane | Agent lane / target | Depends on / exit | Risk | Readiness / status |
|---|---|---|---|---|---|---|
| 0 | MVP6-MOD0183-CT-REVIEW-01 | 0183 / Shipment | CT | E4 and content-bound VER chain | HIGH | ACCEPTED, bounded only |
| 1 | MVP6-184-FIXTURE-DISPOSITION-01 | 0184 / contract | AL-MVP6-184-INS / read-only-auditor; accountable contract owner decides | CARRIER-FIXTURE-01 exact locations/options → recorded owner disposition | HIGH | Review input ready; owner decision OPEN |
| 2 | MVP6-184-CANDIDATE-R1 | 0184 / contract | AL-MVP6-184-CONTRACT / designated contract writer | Step1 approved policy → new candidate-only patch/hash; no canonical writes | HIGH | CONDITIONAL, only if content change selected |
| 3 | MVP6-184-CANDIDATE-VER-01 | 0184 / contract | AL-MVP6-184-VER / independent read-only-auditor | Final exact candidate → schema/fixture/negative-test/non-Carrier comparison | HIGH | HELD until final candidate |
| 4 | MVP6-184-PUBLISH-01 | shared bundle | Single authorized publisher + security owner | Required owner dispositions/consumer inventory + step3 → canonical two-file publication | HIGH | BLOCKED, no release authority inferred |
| 5 | MVP6-184-UPTAKE-01 | 0184 / consumption | AL-MVP6-184-UPTAKE / testing-agent, independent check after | Published exact hashes → actual mock/test/tooling version consumption demonstrated | HIGH | HELD; not dependent on finished Carrier service |
| 6 | MVP6-184-RELEASE-DEV-01 | 0184 / preparation | module-pack-author + central CT | Uptake, GAP closure, explicit composition/Phase1.5 approval → ready-for-dev pack + DEV v2.0 | HIGH | HELD |
| 7 | MVP6-184-DEV-02 | 0184 / Carrier | AL-MVP6-184-DEV / orchestrator + add-module | Approved pack and new exact prompt → implementation + E4 handoff | HIGH | BLOCKED |
| 8 | MVP6-184-VER-02 | 0184 / Carrier | AL-MVP6-184-RUNTIME-VER / read-only-auditor | Completed frozen DEV handoff → independent E4 and Shipment regression | HIGH | HELD |
| 9 | MVP6-184-CT-ACCEPT-01 | 0184 / Carrier | Central CT | Independent verdict + exact scope/records → bounded decision | HIGH | HELD |
| 10 | Subsequent per-module WPs | 0185/0186/0187 → 0190/0192 → 0147/0148 | New bounded lanes after pack approval | Own contracts, ownership, DoR and sequence gates | HIGH | All draft; runtime CLOSED |

Integration status for steps1–6: specification/governance only. Step7–9 target bounded direct-service
E4, not integrated E5. Agent and verification verdicts remain separate from central CT status.
No planned row grants authority to publish, promote, merge, commit or push.

## 4. Consumer dispositions without circular gates

- MOD-0183: unchanged paths/components and no Carrier client found; obtain accountable design/compatibility
  acknowledgment using existing evidence. Publication is not a Shipment runtime deployment.
- MOD-0184: synthetic fixture/parser checks exist; actual Carrier SDK/service is absent. Resolve the four
  example mismatch cases. Do not require a finished service to permit its own development.
- MOD-0185: draft planned Active Carrier dependency; obtain dependency/design disposition.
- MOD-0187: draft optional Carrier relation; obtain design disposition.
- MOD-0186: draft, no demonstrated direct Carrier dependency; owner records applicable/N/A status.
- Other/external consumers: publisher completes inventory and names actual consumers or records an
  accountable no-additional-consumers statement. Repository absence is not external absence.
- MOD-0140: N/A on current measured scope; do not invent an extra approval dependency.

Prepublication compatibility evidence and postpublication uptake are different. Uptake can measure
that the approved mock/test consumer loads the canonical YAML+annex hashes and executes affected
contract tests; it cannot claim nonexistent SDK or service runtime conformance. Central owner must
accept that concrete uptake criterion before closing the existing GAP records.

## 5. Carrier DEV minimum contract after GO

Use the final approved pack and published contract, not this plan as a substitute.
Five layers; backend-only, shell none, golden_reference none, form_field_count 0; port5061.
Only queryCarriers/createCarrier/changeCarrierStatus. Scoped Carrier + success receipt + audit in one
Mongo transaction, exact code reservation and immutable receipt; no stock or Supplier master ownership.

Composition must isolate Carrier routes from broad ShipmentContextMiddleware, shared model-error
mapping and Shipment-specific replay policy. Program.cs edits require the exact approved allowlist;
no global changes to accepted Shipment semantics. Shared seams have one writer.

Golden flow: authorized scoped create Active → list/reload → permitted lifecycle change → reload;
Retired terminal; replay preserves original result/audit while response uses current correlation.
Acceptance includes denial/header precedence/nil UUID/parser-valid keys, invalid schema, uniqueness,
cross-tenant/LE, all lifecycle pairs, replay conflicts, concurrency, unknown commit recovery,
rollback/restart, historical receipts and sanitized errors. Demonstrate CARRIER-FIXTURE-01 regression
fails before correction and passes afterward; never mark an evidence script's exit0 as full PASS if
its result records findings.

E4 requires HTTP + Mongo persisted state + audit/replay + authorization/isolation + failure/restart.
Verify fresh process against fresh binary. Re-run accepted Shipment regression after composition edits.
Architecture must retain exactly known external failures, with no new slice failures; no waiver invented.
Gateway, shared permission registration, live ingress and E5 use separate authorized integration WPs.

## 6. Parallel work and dispatch discipline

No new agents are dispatched by this plan update. Technical comparisons are already complete.
Next immediate work is a decision, not another three-lane blanket inspection.

SOP §§3.2/16 requires separate worktrees plus disjoint scopes for parallel writers. Earlier evidence
agents wrote distinct directories in one checkout: file preservation passed, but that topology does not
satisfy the stated separate-worktree rule. Do not repeat it or retroactively label it compliant.
Future parallel read-only reviewers may inspect frozen inputs without writing repo files; their outputs
stay outside the repository and CT records them after review. If writing evidence/scripts, classify as
bounded artifact work, not strict Profile C/VER. Only CT/documentation writer persists final reports.

For any writer: explicit branch/base/worktree, dirty-file/hash manifest, allowed paths and protected
paths; one writer per shared composition/contract. For any VER: separate implementer identity and
no source/fixture repair; return finding to a new rework WP. At two consecutive failed reworks with
the same cause, reassess architecture per §28.2; do not count owner waiting as failed runtime rework.

DEV v1.0 and VER v1.0 remain immutable HELD. Material contract/acceptance changes require a new
v2.0 prompt or superseding WP. Every released prompt needs SOP §17 metadata and one NE/NEDEN/NASIL/
YAPMA/DOĞRULA block. No placeholder branch/worktree values may be dispatched.

## 7. Safe next inspection prompt — optional owner assistance, not publication

```text
Target: read-only-auditor / read-only-audit
WP MVP6-184-FIXTURE-DISPOSITION-01; prompt v1.0; lane AL-MVP6-184-INS, type INS.
Profile C; risk HIGH; evidence E1/E2; persistence no writes; consistency N/A.
Repository/worktree /Users/natig/Projects/ERP-vNext-recovery
Branch feature/mvp6-logistics
Expected HEAD 4a8d4d4b339528a88e6220fb8402e5a2c771136c
Dirty baseline: existing modified MOD0184 pack and untracked preparation/CT/evidence/plan
records; enumerate at start and preserve. No repository writes allowed.
Authority: AGENTS.md; CT SOP; Supply Chain domain and draft MOD0184 pack;
mod-0184-publication-final-decision-v1.0.md and consumer-technical-evidence/README.md
under docs/records/audits/2026-09; exact existing publication package.
Dependencies: completed technical evidence. Parallel-safe: no concurrent writer to inputs.
NE: Give the contract owner a narrow disposition recommendation for CARRIER-FIXTURE-01.
NEDEN: Generic preparation is complete; four inherited examples conflict with bounded policy.
NASIL: Read exact Carrier create422 reference/examples and annex. Compare options:
Carrier-local examples/content with shared response preserved, or explicit inherited-example
applicability clarification. Do not invent a new create business rule or status.
YAPMA: No edits, publication, owner signature, pack promotion, runtime, commit/push/stash.
DOĞRULA: Identify exact pointers, acceptance effect and consumer mock selection risk; report
which option needs changed artifacts/hashes. Technical recommendation is not owner approval.
Output SOP §22 with path:line evidence in response; CT persists it separately if needed.
Stop only affected work for hash/branch conflict or missing authority. No routine questions.
```

## 8. Records, deferrals and closure

The old plan/pack/README contain historical drift. This current plan records reality without
rewriting accepted evidence or historical reports. Central DCP/registry/board/backlog reconciliation
is a pending owner action, not silently completed here. CT-0183-05 stays central-owned.
The original172-protected baseline now differs at the previously revised Carrier draft pack;
report that known difference and fresh preservation separately, never claim172/172 old equality.

MOD0183 GAP01/02/03/04 and live ingress stay deferred; they are not automatic prerequisites for
Carrier scope that does not consume them. Three external architecture failures remain user-deferred,
not formal time-unlimited waivers. Owners/review dates must be recorded by central governance before
any waiver-based gate; do not fabricate expiry or require unrelated fixes for bounded Carrier work.

Next review trigger: fixture disposition, changed candidate hashes, publication/uptake, or DEV handoff.
Full MVP6 completion still requires shipment→carrier/load→POD→return/claim, S&OP/capacity reproducible
snapshots, supplier feedback and actual integrated E5/G5; mock PASS alone cannot close it.
