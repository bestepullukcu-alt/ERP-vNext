---
decision_id: MOD0183-DOCS-PATH-SHIPMENT-BUNDLE-330-REPIN-01
status: approved
decided_at_local: 2026-10-19
decided_by: Ali (owner), relayed in the SCE / MVP-6 Control Tower conversation
decision_source: owner decision 4a relayed in the MVP-6 Control Tower conversation, 2026-10-19: pin shipment-bundle 3.3.0 (Q498, 7bdf0110f) in the MVP-6 PR that carries it; 3.2.0 is pinned on main by its own small PR
payload_sha256: 52f9dd12386093a681dffcce2bf959b9abda976075f9b1f45676c59d16455f2a
approvedBy_value: current-role-user-message-2026-10-19
supersedes_binding: docs/records/decisions/2026-09/mvp6-loads-publication-guard-owner-decision-01.json (that record is unchanged; the authority now binds this one)
---

# Docs-path authority — shipment-bundle 3.3.0 re-pin (owner decision, human record)

The machine-readable record is `mod0183-docs-path-shipment-bundle-330-repin-owner-decision-01.json` (same folder). `docs/reference/architecture/docs-path-authority.json` binds it by
path and SHA-256 `74f15a1699b88a193afc42baae1bfa6e2e9805ee57898490157d157f5a931168`.

## What is approved

1. The canonical target `docs/analysis/contracts/shipment-bundle.openapi.yaml` re-pinned from `6dc1dd486375130dc4225d59f08bc4ff05e62d148ac72aeeb2034731e7796aa2` to `c59bb4803a2d7e55eff0ce1e5aeb126df7b4eb3fbffb251e999bc0c069d43ef2` — contract version **3.3.0**.
2. Nothing else: the carrier target and every sealed input are byte-identical, so the payload
   (`canonicalTargets` raw text + "\n" + `sealedInputs` raw text) is SHA-256 `52f9dd12386093a681dffcce2bf959b9abda976075f9b1f45676c59d16455f2a`.

## Limits

- If the YAML target, any seal or the payload changes, this approval does not carry over to the new hash.
- No policy, schema, reader or test change; no other target or seal.
