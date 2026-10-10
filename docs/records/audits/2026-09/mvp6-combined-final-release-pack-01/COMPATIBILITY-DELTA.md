# Candidate → final proposal

Status: proposal only; business design is already approved and is not reopened.

|Area|Verified candidate|Proposed publication bytes|Compatibility effect|
|---|---|---|---|
|info.version|2.0.0|3.0.0|Metadata-major can break version-pinned consumers/tools; no route negotiation or migration is created|
|info.x-status|CANDIDATE|FROZEN|Intended post-approval classification inside staged bytes only; does not assert actual publication|
|info.x-candidate-revision|R2|removed; x-shipment-root-semantics points to new annex|Metadata/link only|
|Returns descriptions|semantics-candidate.md / Unpublished R2 label|returns-semantics-v3.0.0.md / versioned normative label|Filename/release label only|
|Claims descriptions|claims-semantics-r2.md / R2 label|claims-semantics-v3.0.0.md / versioned normative label|Filename/release label only|
|Root/Returns/Claims annexes|R2/candidate headings and composition metadata|Versioned headings, packaging/link metadata|All normative behavior preserved by exact reversible text-edit proof|
|Root binding|Returns root-binding.md packaging reference|Same bundle root schema + shipment-root-semantics-v3.0.0.md|Does not imply producer publication/implementation/uptake|
|Everything else|Validated candidate|Unchanged|Components, webhooks, schemas, required/null, grants, decimals, lifecycle, root/errors/replay preserved|

Machine-readable allowlist and counts: evidence metadata-path-transformations.json. Full review diff: candidate-to-final.patch. Reversing ONLY declared edits recovers all four original candidate files byte-exact. All operation ASTs match after removing declared description label/path edits. No root-chain redesign or new business policy.

Recommended3.0.0 is conservative because Returns/Claims eligibility, error/replay/grant obligations are stronger than canonical2.0.0; optional producer root by itself would be additive, but it is not the whole release. wirev1 and routes remain unchanged. This package proposes a version; it does not select one on the owner's behalf.

Shipment producer must still implement/verify field emission and persisted-root provenance; schema compatibility is not runtime readiness. Loads paths/models are unchanged; multiple Shipment roots remain an open uptake disposition, not silently rejected or merged. Returns/Claims are draft consumer designs; model PASS does not establish running consumers. The recorded external-consumer-none statement is preserved; no renewed attestation sought and no fresh external inventory claimed.

Version/hash-sensitive historical tools must not be treated as new uptake: e.g. mod-0184-published-uptake/check_uptake.py:11 asserts its own historical expected hashes. Six active-tool seals remain unchanged; target-pin hashing is not a successful execution of their historical scripts against3.0.0. No old script/output is overwritten or old consent transferred. Approved design policies stay intact; release consent and future runtime rollout remain separate.
