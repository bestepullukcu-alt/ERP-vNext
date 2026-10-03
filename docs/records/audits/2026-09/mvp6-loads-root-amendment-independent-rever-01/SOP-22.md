# MVP6-LOADS-ROOT-AMENDMENT-INDEPENDENT-REVER-01 — SOP §22

**Date:** 2026-09-24  
**Role:** independent contract re-verifier; candidate-02 authorı değil  
**Branch / HEAD:** `feature/mvp6-logistics` / `4a8d4d4b339528a88e6220fb8402e5a2c771136c`  
**Verdict:** **PASS — unpublished candidate-02 technical scope only**

## Exact result

| Artifact | Expected SHA-256 | Independently measured | Result |
|---|---|---|---|
| Candidate YAML | `aeb354e8cc5d06f36bc0ee0e4e43b28a8701a2c3bec44200d47cec9095dfc744` | same | PASS |
| Corrected annex | `e2107301e3c6cfae5e6768de77c19f5b4fa5ea7ff42f1a4023650bd3fb7750cd` | same | PASS |
| Canonical-baseline patch | `27cc3a8f6c5000b9d06b80b2b0037e84750cd7bc40611da4b5f6fa33582e11cc` | same | PASS |
| Candidate-01→02 delta | `79de2b2c59d085e7759fc356a454a697d35e6052a8f3a18049bf755a8c70a0b5` | same | PASS |

Canonical YAML preimage `5dfe7c1b…` and Loads v2 annex preimage `a2187c93…` match the controlling hashes. In a fresh `/private/tmp/mvp6-loads-root-rever-*` copy, `git apply --check` and `git apply` both exited `0`. The applied YAML and newly created v3.1.0-rc.1 annex are byte-identical to candidate-02; the v2 annex remains byte-identical to its preimage.

## Successor delta

The candidate YAML is byte-identical to candidate-01. The annex has the same line count and differs at exactly line 192:

```diff
-info.version2.0.0 is candidate metadata only; wire contractVersion v1 remains unchanged.
+info.version3.1.0-rc.1 is candidate metadata only; wire contractVersion v1 remains unchanged.
```

Reversing that one token produces candidate-01 annex bytes exactly. Therefore root authority, optional/nullable and fail-closed policy, existing status/error matrix, permissions, correlation/replay rules, nil handling, multi-Shipment exclusion, detail exclusion and searchable-lookup exclusion are byte-equivalent outside the correction. The annex title, opening candidate statement and normative release sentence now consistently identify `3.1.0-rc.1`; wire `contractVersion v1` and the warning that metadata supplies no negotiation, migration, cutover or rollback remain.

## Inherited OpenAPI evidence boundary

Full OpenAPI/ref/example validation was **not rerun**. Candidate-02 YAML SHA-256 is exactly the YAML independently validated in `mvp6-loads-root-amendment-independent-ver-01`. The following result is therefore classified **INHERITED**:

- full OpenAPI 3.1 packaged meta-schema: 0 errors;
- `OpenAPIV31SpecValidator`: 0 errors;
- 298 local refs and 236 schema-bound examples;
- missing/null/valid/nil fixtures pass and malformed UUID is rejected;
- six negative mutants are detected.

The inherited source is `raw/raw-results.json` SHA-256 `8255831b4510abdcc60a29ebc3d860df7a37e2d9f77305a1da00df180f82aa9c`. This re-VER does not claim a fresh validator execution.

## Compatibility and remaining gates

Optional response-field addition is not automatically compatible. Tolerant readers may ignore it; strict unknown-property parsers on the same route/wire `v1` may fail. Final version selection, actual consumer inventory and consent must bind the eventual final YAML and annex bytes.

Still separate and unopened: final version decision, exact-hash consumer consent, canonical publication authority/application, any guard binding, producer runtime uptake and independent runtime verification, pack promotion, UI/integration uptake, rollout and E5/G5.

## No-change boundary

Candidate-02, canonical contracts, runtime, packs, guard and Git state were not modified. This lane created only `docs/records/audits/2026-09/mvp6-loads-root-amendment-independent-rever-01/`. Pre-existing repository dirt is outside this verdict.

