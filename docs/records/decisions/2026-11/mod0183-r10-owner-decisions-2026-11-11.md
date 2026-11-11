# MOD-0183 r10 — owner decisions (2026-11-11)

- Owner: Natig (gmg-10, K1, MOD-0183 owner), in the SCE / MVP-6 Control Tower conversation, 2026-11-11.
- Pack: `execution/domains/supply-chain-execution/module-packs/MOD-0183-shipment-tracking-pod.md` (r9 `0d0e64ef` on `main`
  at `3b42f0005`; r10 is the next revision).
- Context: MOD-0178 (MVP-5) asked for ONE MOD-0183 revision covering the warehouse intake mapping, `reservationId` and
  `skuLevel: Item`, started after Dilim B PR #400 merged (merged 2026-11-11, `3b42f0005`); target ≈ 2026-11-21, confirmed
  when the pack is approved.

Measured on `origin/main` `3b42f0005` before the questions: INVENTORY-BUNDLE 1.7.0 carries `MovementRequest.reservationId`,
`restoreReservation` and `SkuLevel` `Item`; MOD-0173-FU03 r5 approved (CT-APR-01, GMG-ERP, 2026-11-11); WMS-BUNDLE exists only
as `docs/analysis/contracts/drafts/wms-bundle-v1-proposal.md` (no `wms-bundle.openapi.yaml`); WAREHOUSE-OUTBOUND is 1.0.0
(`SkuLevel` without `Item`); `WarehouseIntakeCoordinator` has no production caller (§8.2 obligation 2).

| # | Question | Answer (owner's choice) | In the pack |
|---|---|---|---|
| R10-1 | Scope of the warehouse intake | **Open it fully:** §8.2 obligation 3 (whitelist, `shipTo` dropped, hash kept) + user-triggered sync (MOD-0178 Q-M6-4) + field mapping (`skuLevel`, `lotId`, `fromLocationId`, `fefoBypassReason`, `outboundNumber`, `legalEntityId`); intake is enabled in production by this revision | new section, §8.2, §24.5 |
| R10-2 | How `reservationId` travels | **Internal field:** stored from the warehouse line on the shipment line and sent as INVENTORY 1.7.0 `MovementRequest.reservationId` at dispatch; SHIPMENT-BUNDLE does not change. MOD-0178's wording "SHIPMENT 3.5.0 line field" is corrected on their side | §23.4, new section |
| R10-3 | K3 / `restoreReservation` | **In r10:** the five changes §24.6 lists (REVERSAL table, S23-17, "a reversed issue does not restore…", the compensation case, acceptance for both K3 branches) | §23.4, §24.6 |
| R10-4 | Basis while WMS-BUNDLE 1.1.0 is not on `main` | **Draft + code gate:** r10 is written against the draft's §2.3 members; code may start only when WMS-BUNDLE 1.1.0 is FROZEN on `main`, and the `Item` path only when WAREHOUSE-OUTBOUND 1.1.0 + WMS-BUNDLE 1.2.0 are FROZEN on `main` (MOD-0178's one CT step) | Teslim dilimleri, gates |
| R10-5 | OD-R10-1 — the intake operation | **SHIPMENT-BUNDLE 3.6.0** (3.5.0 → 3.6.0, additive only; single writer MVP-6 CT) carries `intakeWarehouseOutbound` `POST /shipments/warehouse-intake/{outboundId}` as proposed in §25.3; the Gateway route is written by the integration-agent lane; 3.6.0 lands with the DL-5 code. The pack lists the 3.6.0 items; no contract file is changed by the pack. R10-2 is unchanged: `reservationId` is not part of 3.6.0 | §25.3, §25.7, §25.9 |
| R10-6 | OD-R10-2 — customer on an intake shipment | **Separate state:** an intake shipment shows its own customer state "warehouse shipment — no customer" (new localized text, 7 languages), never "Old record"; a customer on the outbound is a later MOD-0178 topic | §25.3, §25.8 |
| R10-7 | OD-R10-3 — showing `outboundNumber` | **Add to 3.6.0:** optional, read-only `outboundNumber` on `ShipmentSummary` (so also `ShipmentDetail` and the list); shown as the source code on list and details; null for manual shipments | §25.1, §25.3, §25.8 |
| R10-8 | OD-R10-5 — drift hash over a body with an address | **Accepted residual risk:** only the SHA-256 is stored, the address is neither stored nor recoverable from it | §25.2, §25.9 |

Decision source: the owner's answers to the four questions in the MVP-6 CT conversation, 2026-11-11 ("Tam aç",
"İç alan", "r10'a girsin", "Taslak + kod kapısı"); R10-5…R10-8: the owner's answers of the same day ("SHIPMENT 3.6.0",
"Ayrı durum metni", "3.6.0'a ekle", "Kalan risk olarak kabul"). These are decisions on the revision's scope; they are not a pack approval,
and no code, commit to `main` or deployment follows from them.
