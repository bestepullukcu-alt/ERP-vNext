---
decision_id: MOD0183-DOCS-PATH-SHIPMENT-BUNDLE-340-REPIN-01
status: approved
decided_at_local: 2026-10-29
decided_by: Natig (gmg-10, K1, MOD-0183 owner), in the SCE / MVP-6 Control Tower conversation
decision_source: owner answer "Onaylıyorum" to "Sözleşme 3.4.0 parmak izinin yeniden sabitlenmesini onaylıyor musunuz?" in the MVP-6 Control Tower conversation, 2026-10-29; the 3.4.0 contract ships with MOD-0183 DL-1 (Q500, pack r7 22fe550d, CT-APR-01 #287)
payload_sha256: 04539ed56d7000964875ffd694d38a3fee1effdbf6874889737df767011a0eba
approvedBy_value: current-role-user-message-2026-10-29
supersedes_binding: docs/records/decisions/2026-10/mod0183-docs-path-shipment-bundle-330-repin-owner-decision-01.json (that record is unchanged; the authority now binds this one)
---

# Docs-path authority — shipment-bundle 3.4.0 re-pin (owner decision, human record)

The machine-readable record is `mod0183-docs-path-shipment-bundle-340-repin-owner-decision-01.json` (same folder). `docs/reference/architecture/docs-path-authority.json` binds it by
path and SHA-256 `54f8e59fb57c85b4ba1bfd40647873ae36d6d40fd26d77f5f12b5652ab517833`.

## What is approved

1. The canonical target `docs/analysis/contracts/shipment-bundle.openapi.yaml` re-pinned from `c59bb4803a2d7e55eff0ce1e5aeb126df7b4eb3fbffb251e999bc0c069d43ef2` to `84d804f6bbcb0ebc14c2046d44a72585c482e014a4ca9e295e15757648f59562` — contract version **3.4.0** (additive; patch `docs/roadmap/plans/mvp6-integration-wp-p1-01/SHIPMENT-BUNDLE-3.4.0.patch`).
2. Nothing else: the carrier target and every sealed input are byte-identical, so the payload
   (`canonicalTargets` raw text + "\n" + `sealedInputs` raw text) is SHA-256 `04539ed56d7000964875ffd694d38a3fee1effdbf6874889737df767011a0eba`.

## Limits

- If the YAML target, any seal or the payload changes, this approval does not carry over to the new hash.
- No policy, schema, reader or test change; no other target or seal.
