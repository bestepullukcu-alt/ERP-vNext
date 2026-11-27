---
decision_id: MOD0183-DOCS-PATH-SHIPMENT-BUNDLE-351-REPIN-01
status: approved
decided_at_local: 2026-11-18
decided_by: Beste (bestepullukcu-alt, K2; MVP-6 / MOD-0183 owner after the 2026-11-17 handover), in the MVP-1 / MVP-5 Control Tower conversation
decision_source: owner choice "Ayrı CT commit'i (Önerilen)" to "MOD-0183 DL-8: SHIPMENT-BUNDLE 3.5.1'e geçince DocsPathGuard mimari testi sözleşmeyi eski özetinde tuttuğu için kırmızıya döner. Ne yapalım?" in the Control Tower conversation, 2026-11-18; recorded in WP-0183-DL8 §2 item 1 (c51042197). The 3.5.1 contract ships with MOD-0183 DL-8 (20aaffc78).
payload_sha256: bb025a8a72c78dd66e367179b4a5a76e3595ab951e387c4d1733c256cf4435e3
approvedBy_value: current-role-user-message-2026-11-18
supersedes_binding: docs/records/decisions/2026-11/mod0183-docs-path-shipment-bundle-350-repin-owner-decision-01.json (that record is unchanged; the authority now binds this one)
---

# Docs-path authority — shipment-bundle 3.5.1 re-pin (owner decision, human record)

The machine-readable record is `mod0183-docs-path-shipment-bundle-351-repin-owner-decision-01.json` (same folder). `docs/reference/architecture/docs-path-authority.json` binds it by
path and SHA-256 `6195b8156a3035c2d6d2447f2dc7ee9a36e84fba562d51d89d8399bd6a873242`.

## What is approved

1. The canonical target `docs/analysis/contracts/shipment-bundle.openapi.yaml` re-pinned from `42ccf361a08c13f8c080a0c95dfcd98a91bc9f5d7da4295041b9e9c17d25f8b7` to `8f7564b30af0fdc05bd3b8cae1b66306c6bf1ec682955801c32766a86083e241` — contract version **3.5.1** (descriptions only; MOD-0183 r11 §26 DL-8 exception, applied by `20aaffc78`).
2. Nothing else: the carrier target and every sealed input are byte-identical, so the payload
   (`canonicalTargets` raw text + "\n" + `sealedInputs` raw text) is SHA-256 `bb025a8a72c78dd66e367179b4a5a76e3595ab951e387c4d1733c256cf4435e3`.

## Limits

- If the YAML target, any seal or the payload changes, this approval does not carry over to the new hash.
- No policy, schema, reader or test change; no other target or seal. A CRLF working copy still reads different bytes (DocsPathGuard hashes raw bytes); measure in an LF checkout.
