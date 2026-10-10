# SOP §22 — Independent Loads candidate VER

**Candidate technical validation PASS; candidate packaging REWORK; publication NO-GO.** No canonical/runtime authorization inferred.

Exact original patch5092e7823d0c8a714569f68353558e826fc0714e0d88b84a514451813f4af632 and all13manifest entries independently hash-match. Patch applies to disposable baseline and produces exact candidate YAML/annex bytes. User design approval compared against full attached D185 proposal; annex contains proposal verbatim apart from trailing newline, prefixed with explicit candidate/gate scope. Approved decisions are not missing from this artifact.

## Fresh independent reproduction
Copied frozen candidate before executing scripts; author originals unchanged. OpenAPI3.1/schema/format validators pass:60 assertions,259local references,133inline examples.68fixture records are8executed static reference-profile cases +49lifecycle/8eligibility/3business-rejection declared oracles. They are not68 behavioral tests. Ten negative controls reject:4operation declaration mutations,3normative-text mutations,3invalid Error bodies. These exercise static consistency, not authorization/replay/concurrency implementation. String-removal controls cannot prove semantically equivalent normative prose or actual runtime enforcement; report does not claim either.

Original shared components and all non-Loads paths structurally unchanged. **Correct count is10non-Loads path keys containing11operations**, not11paths (12total path keys,2Loads keys). Candidate changes3Loads operations plus version and annex only. Producer ShipmentProjection emits required Shipment decision fields and Web serializer does not configure null-ignore; source evidence supports explicit null profile. Current Delivered/assigned canonical example is profile-shape evidence only, not successful create eligibility. No runtime mocks or Carrier/Shipment service regression rerun.

## Rework requirements
1. Correct all package reports/assertion labels claiming11nonLoads paths to10paths/11operations; regenerate hashes.
2. Replace proposed normative publication annex's embedded historical decision report with a concise standalone Loads-only normative annex preserving every approved business/security/reference/replay/transaction rule. Current annex carries old1.1.0/transition409-missing statements, historical unapproved labels, local absolute paths and Phase1.5 planning text. Preamble disambiguates it enough for review but publication consumers should not need to interpret a historical proposal as a contract. Preserve original proposal separately and supply semantic crosswalk to new sections. Do not invent new defaults or authority.
3. Keep remaining lexical/profile questions explicitly outside finalized normative promises until resolved. Regenerate exact patch/manifest and static checks after editorial repair. No new owner approval of already-approved business semantics is required merely for faithful editing.

## Three claimed interpretation gaps
- Optional Carrier decision-field absence: proposal expressly defines four-field Shipment profile; Carrier missing decision fields are not enumerated. However general approved matrix already assigns schema-valid insufficient reference503REFERENCE_STATE_UNAVAILABLE. A Carrier-specific clarification should align with that rule, not reopen general503or choose404without basis. Exact fields still need explicit profile clarity.
- Query scope-override predicate: aliases/casing/key set absent in approved text; genuine runtime precision gap. General unknown-query tolerance already approved and must remain.
- Trusted claim lexical syntax: UUID/nil rule explicitly covers correlation, not exhaustive tenant/LE/sub lexical forms or parser duplicates. Genuine precision gap; do not import Carrier-specific strict syntax implicitly. Parser401versus authenticated-unusable403 remains approved priority.

## Release constraints
Minor1.2.0 compatibility unproven: duplicateShipment/stop/eligibility rules strengthen schema-valid accepted inputs. NonLoads structural sameness is not Loads consumer behavioral conformance or release consent. Future canonical YAML hash necessarily changes DocsPathGuard pin; requires reviewed authority payload update while17historical seals remain intact. No current guard change by candidate.

Real repository14,417non-generated files and gitstatus/HEAD unchanged; no repo writes, commit/push/stash. Full nochange evidence repository-no-change.json. Static reproduced artifacts in candidate/. No accepted runtime scope, Phase1.5 promotion or publication readiness.
