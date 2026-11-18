---
decision_id: MOD0183-DOCS-PATH-SHIPMENT-BUNDLE-350-REPIN-01
status: approved
decided_at_local: 2026-11-11
decided_by: Natig (gmg-10, K1, MOD-0183 owner), in the SCE / MVP-6 Control Tower conversation
decision_source: owner answer "Onaylıyorum" to "Sözleşme 3.5.0 parmak izinin yeniden sabitlenmesini onaylıyor musunuz?" in the MVP-6 Control Tower conversation, 2026-11-11; the 3.5.0 contract ships with Dilim B (MOD-0183 + MOD-0186 DL-3/DL-4), PR #400
payload_sha256: a8fe6af820f6e9f08d2d18e0bde0024fac165dde2be03e61399541670d8f910c
approvedBy_value: current-role-user-message-2026-11-11
supersedes_binding: docs/records/decisions/2026-10/mod0183-docs-path-shipment-bundle-340-repin-owner-decision-01.json (that record is unchanged; the authority now binds this one)
---

# Docs-path authority — shipment-bundle 3.5.0 re-pin (owner decision, human record)

The machine-readable record is `mod0183-docs-path-shipment-bundle-350-repin-owner-decision-01.json` (same folder). `docs/reference/architecture/docs-path-authority.json` binds it by
path and SHA-256 `29de855dff1a15710958b34d12501bd9fa9ec9c19e03e3f7999e003d6e39750b`.

## What is approved

1. The canonical target `docs/analysis/contracts/shipment-bundle.openapi.yaml` re-pinned from `84d804f6bbcb0ebc14c2046d44a72585c482e014a4ca9e295e15757648f59562` to `42ccf361a08c13f8c080a0c95dfcd98a91bc9f5d7da4295041b9e9c17d25f8b7` — contract version **3.5.0** (additive: `customerRef`, `queryShipments` search and ordering, `getReturn`).
2. Nothing else: the carrier target and every sealed input are byte-identical, so the payload
   (`canonicalTargets` raw text + "\n" + `sealedInputs` raw text) is SHA-256 `a8fe6af820f6e9f08d2d18e0bde0024fac165dde2be03e61399541670d8f910c`.

## Limits

- If the YAML target, any seal or the payload changes, this approval does not carry over to the new hash.
- No policy, schema, reader or test change; no other target or seal.
