# MVP6 Loads root amendment — release review

## Recommendation

Recommend **SHIPMENT-BUNDLE `3.1.0` / wire `v1`** for owner selection. The published baseline is `3.0.0`; the observable schema delta adds only optional/nullable `LoadSummary.lifecycleCorrelationId` to the existing `queryLoads` response. Routes, operation IDs, request bodies, statuses, error codes, permissions, replay rules and correlation behavior are unchanged. The field remains absent from `required`.

This is a version recommendation, not an owner decision. A minor version number does not make same-route compatibility safe. Tolerant readers can ignore the property; strict readers that reject unknown response properties can fail. A metadata major would also not create route negotiation while wire `contractVersion` remains `v1`, so `3.1.0` plus explicit inventory, exact-hash consent and coordinated cutover is the smallest honest proposal.

## Exact final-proposed bytes

| Artifact | SHA-256 | State |
|---|---|---|
| `artifacts/shipment-bundle-v3.1.0-final-proposed.openapi.yaml` | `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2` | staged publishable byte proposal; embedded `x-status: FROZEN`; package remains unapproved |
| `artifacts/loads-semantics-v3.1.0.md` | `9d8a370664abb8221d5f8b23038f7c506e0621fadb2e5c83450ad93e63cf5034` | final-named normative annex proposal; explicitly NOT PUBLISHED |
| `publication.patch` | `dc0ad05bbb47b05c3c62dadf138faab847337ddc9f387bb9f764e84e02fdbed5` | unapplied two-file canonical patch |
| `candidate02-to-final-proposed.patch` | `05cf97898666c23651dc9dc6628bcee7b00f0d5a04c083f89d463339546661ce` | review delta |

Canonical preimages are YAML `5dfe7c1bba32551bd8d4b532243878684e69a6d9560e667a4183bfd516b9d21c` and Loads v2 annex `a2187c934cf9176cae4cb8b9c70dfd1298154e5124cdc197d3029103636c2be1`. The patch preserves the v2 annex and creates `loads-semantics-v3.1.0.md`.

## Candidate-02 to final proposal

The parsed YAML difference is limited to:

- `info.version`: `3.1.0-rc.1` → `3.1.0`;
- `info.x-status`: `CANDIDATE` → intended publication value `FROZEN`;
- three Loads operation annex pointers and their descriptive text: `loads-semantics-v3.1.0-rc.1.md` → `loads-semantics-v3.1.0.md`.

The annex changes only its title, opening release-status sentence and normative metadata sentence. Root authority, missing/null/malformed/valid/nil behavior, tenant/LE, permissions, errors/statuses, replay/correlation and exclusion boundaries remain identical. `raw/validation-results.json` proves parsed business-semantic equality after removing those metadata/pointer fields.

## Consumer conclusion

The repository contains an actual Loads producer that currently emits five LoadSummary fields and therefore needs a later uptake change. The Loads UI is a design consumer and remains HELD. Current gateway source contains no Loads route. Test/probe code is evidence tooling, not deployment consent. No repository search can prove that external applications, SDKs or integrations do not exist; the accountable inventory owner must explicitly declare the external set and bind consent to the two proposed artifact hashes.

## Separate gates

1. **Final version selection:** select `3.1.0 / wire v1` against the two artifact hashes above.
2. **Consumer inventory and consent:** declare the external inventory and record consent for each actual consumer against those hashes; strict-parser migration/cutover must be explicit.
3. **Canonical publication:** separately authorize exact `publication.patch` and the resulting two hashes. This preparation does not apply it.
4. **Runtime uptake:** separately authorize the Loads producer projection/tests and later UI/integration uptake after publication. Publication does not grant runtime work.

