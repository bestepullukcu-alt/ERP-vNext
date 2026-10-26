---
decision_id: MOD0183-DOCS-PATH-SHIPMENT-BUNDLE-320-REPIN-01
status: approved
decided_at_local: 2026-10-19
decided_by: Ali (owner), relayed in the SCE / MVP-6 Control Tower conversation
decision_source: owner decision 4a relayed in the MVP-6 Control Tower conversation, 2026-10-19: approve the docs-path pin only for main's approved shipment-bundle 3.2.0; 3.3.0 is pinned with the PR that carries it
payload_sha256: 4bf6fd42345842469bbf1d520e9163088456a5f7523f4b7e5416850609d90858
approvedBy_value: current-role-user-message-2026-10-19
supersedes_binding: docs/records/decisions/2026-09/mvp6-loads-publication-guard-owner-decision-01.json (that record is unchanged; the authority now binds this one)
---

# Docs-path authority — shipment-bundle 3.2.0 re-pin (owner decision, human record)

The machine-readable record is `mod0183-docs-path-shipment-bundle-320-repin-owner-decision-01.json` (same folder). `docs/reference/architecture/docs-path-authority.json` binds it by
path and SHA-256 `3e68e1c0854e8bb7f0ab0c13ec22ef26b0a9d6dc8c03b0bb0263c4befdf84c5e`.

## What is approved

1. The canonical target `docs/analysis/contracts/shipment-bundle.openapi.yaml` re-pinned from `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2` to `fb84a72aaf1d5727e607a47d0eccd7bf964dc9337bdbb10fe1c24c4de72f70ff` — contract version **3.2.0**.
2. Nothing else: the carrier target and every sealed input are byte-identical, so the payload
   (`canonicalTargets` raw text + "\n" + `sealedInputs` raw text) is SHA-256 `4bf6fd42345842469bbf1d520e9163088456a5f7523f4b7e5416850609d90858`.

## Limits

- If the YAML target, any seal or the payload changes, this approval does not carry over to the new hash.
- No policy, schema, reader or test change; no other target or seal.
