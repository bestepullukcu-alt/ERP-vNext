# MVP6-LOADS-ROOT-AMENDMENT-INDEPENDENT-VER-01 — SOP §22

**Date:** 2026-09-24  
**Role:** independent contract verifier  
**Branch / HEAD:** `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
**Verdict:** **REWORK**

## Verdict basis

The exact candidate is mechanically reproducible and its OpenAPI document is technically valid:

- YAML `aeb354e8cc5d06f36bc0ee0e4e43b28a8701a2c3bec44200d47cec9095dfc744`: MATCH.
- Annex `620038c8d53af260ef03238db9dddebe74fd368ccbed6e04b47d032a44a15ac1`: MATCH.
- Patch `6a87af9fb006a2583b0f734fe7a374ebdd383a15459c4bd76e0d7a58c2ac568a`: MATCH.
- Patch check/apply against the two exact canonical preimages: PASS.
- Applied YAML and annex are byte-identical to the candidate targets: PASS.
- Full packaged OpenAPI 3.1 meta-schema: 0 errors.
- `OpenAPIV31SpecValidator`: 0 errors.
- 298 local references resolve and 236 schema-bound examples validate.
- Missing/null/valid/nil fixtures pass; malformed UUID fails.
- Six deliberately wrong mutants are detected.

The package nevertheless needs rework because the proposed versioned annex contains a contradictory candidate-version statement:

`loads-semantics-v3.1.0-rc.1.md:192` says `info.version2.0.0 is candidate metadata only`, while the candidate YAML and annex title identify `3.1.0-rc.1`. This is a normative release-boundary section, so the stale statement can misidentify the reviewed artifact during final version/cutover assessment. The verifier did not edit it.

## Scope and semantic result

The YAML delta is limited to ten expected parsed-document locations: candidate metadata; three Loads annex pointers/descriptions; the `queryLoads` example; and `LoadSummary.lifecycleCorrelationId`. All non-Loads paths and every schema other than `LoadSummary` are unchanged. `LoadSummary` adds only an optional, nullable UUID property.

The D185 root policy itself is represented consistently outside the stale version sentence:

- authority is persisted `LoadPlan.CorrelationRoot`;
- upgraded producer emission is required when the stored non-null value is present;
- no GET-trace, ID, Shipment-root, cache, audit or user-input derivation;
- no read-time write or backfill;
- stored nil UUID remains present, while missing/null/malformed values keep transition unavailable;
- read and transition permissions remain separate;
- status/error, root-before-fingerprint, replay, lifecycle, audit and Pending-outbox behavior remains unchanged;
- no Shipment-root merge, detail operation or searchable lookup is introduced.

These are static E1/E2 findings. They do not prove producer emission, persistence provenance, runtime fail-closed handling, consumer uptake, HTTP/JWT/Mongo behavior or rollout.

## Compatibility disposition

Optionality alone does not establish compatibility. A tolerant response reader can ignore the new field, but a strict unknown-property parser on the existing route/wire `v1` can fail. The repository-focused scan found the Loads producer/controller and module-pack references, but no exact live frontend/SDK Loads response parser; it does not establish the absence of external consumers.

The release owner must select the final version only after corrected final bytes exist. Consumer inventory and consent must bind the corrected final YAML and annex hashes. Metadata `3.1.0-rc.1` neither supplies route negotiation nor transfers consent.

## Minimal rework and successor verification

Create a successor candidate; preserve this package. Correct the stale version statement at annex line 192 so it accurately describes the successor candidate metadata while retaining wire `contractVersion: v1` and the existing migration/cutover warning. Regenerate the annex and patch hashes. The YAML need not change unless the candidate author intentionally changes final metadata.

Independent successor VER must repeat hash/preimage/apply/byte-equality, annex consistency and full OAS checks against the new exact bytes. No other business semantics should be reopened.

## Authority boundary

No candidate or canonical file was corrected or published. No consumer consent, final-version approval, producer/runtime work, pack promotion, rollout, E5/G5 or DEV GO is granted.
