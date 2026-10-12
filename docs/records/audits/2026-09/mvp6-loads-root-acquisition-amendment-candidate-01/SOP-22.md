# MVP6-LOADS-ROOT-ACQUISITION-AMENDMENT-CANDIDATE-01 — SOP §22

**Date:** 2026-09-24  
**Branch / HEAD:** `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
**Verdict:** **CANDIDATE READY FOR INDEPENDENT TECHNICAL VER — publication and runtime remain HELD**

## Authority

The user selected `D185-ROOT-ACQ-01` as the policy target for a versioned amendment candidate. The decision explicitly did not authorize canonical publication, a final version, consumer consent, or production implementation. This package uses that authority only to prepare candidate bytes.

## Exact candidate

| Artifact | SHA-256 |
|---|---|
| Canonical YAML baseline | `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c` |
| Canonical Loads annex baseline | `a2187c934cf9176cae4cb8b9c70dfd1298154e5124cdc197d3029103636c2be1` |
| Candidate YAML | `aeb354e8cc5d06f36bc0ee0e4e43b28a8701a2c3bec44200d47cec9095dfc744` |
| Candidate annex | `620038c8d53af260ef03238db9dddebe74fd368ccbed6e04b47d032a44a15ac1` |
| Applicable amendment patch | `6a87af9fb006a2583b0f734fe7a374ebdd383a15459c4bd76e0d7a58c2ac568a` |

`3.1.0-rc.1` is a proposed candidate identifier. It reflects an additive optional response field against canonical 3.0.0. It is not a final version selection and does not transfer any earlier consent.

## Exact semantic delta

- `LoadSummary.lifecycleCorrelationId` is optional, nullable, and UUID formatted.
- Its only authoritative source is persisted `LoadPlan.CorrelationRoot`.
- An upgraded producer emits the stored non-null value when present.
- Missing, null, malformed, or non-authoritative values make transition unavailable; no request is sent.
- A present stored nil UUID remains distinguishable from missing/null.
- No derivation from GET trace, IDs, Shipment roots, cache, audit data, or user input is allowed.
- No read-time write, backfill, migration, detail endpoint, lookup endpoint, publisher, or multi-Shipment root policy is introduced.
- Existing tenant/LE, permissions, root-before-fingerprint, replay, lifecycle, error, audit and Pending-outbox rules remain unchanged.

The YAML list example carries the new field, all three Loads operations point at the candidate annex, and the wire `contractVersion` remains `v1`.

## Compatibility

The request schemas, `LoadResponse`, lifecycle event schemas, shared Error, and all non-Loads operations/components are byte-equivalent at the parsed-document level after excluding the candidate metadata and approved Loads pointers. The response property is optional, so tolerant consumers can continue reading the list. Strict response deserializers that reject unknown properties may break; actual consumer inventory and exact consent therefore remain release gates. Producer always-emission is behavioral and cannot be proven by this static package.

## Validation

- Patch check and disposable apply: PASS.
- Applied YAML and annex are byte-identical to the candidate files: PASS.
- YAML parse: PASS.
- 298 local `$ref` values resolve: PASS.
- 69 focused structural/schema/invariance checks: 69 PASS.
- Missing root accepted by candidate schema: PASS.
- Explicit null accepted: PASS.
- UUID accepted: PASS.
- Malformed UUID rejected: PASS.
- Non-Loads paths and all schemas except `LoadSummary` preserved: PASS.
- Load request, success response and event schemas preserved: PASS.

The complete `openapi-spec-validator` / OpenAPI 3.1 meta-schema toolchain was not available in this environment, so full document validation remains mandatory in independent VER. Static PASS is E1/E2 only and is not publication or runtime evidence.

## ASSUMPTION register

- **ASSUMPTION-185-ROOT-CAND-01:** `3.1.0-rc.1` is an appropriate review identifier for an additive optional response property; the release owner still selects the final version.
- **ASSUMPTION-185-ROOT-CAND-02:** Consumers may include strict unknown-property parsers; no compatibility consent is inferred from schema optionality.
- **ASSUMPTION-185-ROOT-CAND-03:** Existing operational data may contain missing, null, malformed or nil roots. This candidate authorizes no inventory, migration or backfill.
- **ASSUMPTION-185-ROOT-CAND-04:** The Load root remains independent from every referenced Shipment root until a separate multi-Shipment policy is approved.

## Remaining gates

1. Independent VER on these exact hashes, including full OpenAPI 3.1 validation.
2. Release-owner final version selection.
3. Exact consumer inventory and separate consent for final YAML+annex hashes.
4. Exact publication authority and canonical application.
5. Producer implementation/VER, then UI/integration uptake/VER.
6. Separate multi-Shipment policy, rollout, E5/G5 and full-module gates.

## No-change statement

Canonical YAML and annex hashes remain their recorded baselines. No runtime, module pack, Gateway, guard, UI, migration, backfill, commit, push, or stash action occurred.

