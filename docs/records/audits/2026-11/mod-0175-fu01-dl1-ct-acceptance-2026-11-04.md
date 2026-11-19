# MOD-0175-FU01 DL-1 — Control Tower kabulü

- Tarih: 2026-11-04. CT: MVP-1 CT, oturum `ca7382cf`. Doğrulama: depo dışı detached ağaç `C:\Users\Ali\ct-verify-fu02-dl1` @ `47762de1f`.
- İncelenen: `47762de1f` (feat(sce): MOD-0175-FU01 DL-1 — quarantine hold / blocked record closure on source reversal, QUARANTINE 1.4.0),
  ebeveyn `25e06fe08`. MVP-1 hattında; push / PR yok. Geliştirme oturumu `local_161089a6`.
- Paket: `MOD-0175-FU01-quarantine-hold-source-reversal.md` @ sha256 `e070d6b6…5380` (r5, onaylı `79a6fa096`).
- Sonuç: **VERIFIED** — DL-1 tek başına kullanıcıya açık ortama çıkmaz (paket §22: DL-1 ve DL-2 birlikte açılır).

## Ölçülenler

| Kontrol | Sonuç |
|---|---|
| Kapsam (`25e06fe08..47762de1f`) | 47 dosya; yalnız `services/Diten.InventoryService` ve `docs/analysis/contracts` (QUARANTINE 1.4.0, donmuş 1.3.0 kopyası, README satırı) |
| XMC-001 | `InventoryCentralAuditScope.cs` sistem aktörü kipi (Q-03); `ExpiryQuarantineTypeExemptionTests` sabitlenen satır 111 → 127 (sahip izni, Beste 2026-11-04, MOD-0176) |
| Tam Inventory takımı (filtresiz, kendi replica set'i, 27017 yok) | 1715 / 1719, 4 atlandı, 0 kırmızı — geliştirici sayısıyla aynı (taban 1638 / 1642) |
| Geliştirici ölçümleri (CT tekrar koşmadı) | TenantArchitecture 221 / 6 kırmızı, tabanla aynı altı; derleme uyarısı 0; 61 sabotajın 57'si kırmızı, 4'ü ikinci savunma katmanıyla açıklandı (S08, S26, S43, S52) |

## CT sabotajı (uygulandı → sonuç → geri alındı)

| Sabotaj | Sonuç |
|---|---|
| `QuarantineRecordStates.IsOpen` `Voided`'ı da açık sayar | kırmızı: `SourceReversalRulesTests.Open_and_terminal_predicate`, `SourceReversalSweepTests.AC18_a_voided_hold_is_not_counted_as_open_anywhere`, `SourceReversalObserverTests.AC18_the_retest_queue_does_not_count_a_voided_hold`; `git checkout` ile geri alındı |

## Sapmalar (CT kabul etti)

- W6 testi (AC-39 a4) üç duraklatmalı kurguyla; V1 UUID dışı kimlik validator 400 (ayrı log yok); AC-18 CT-Q6 seri çakışma listesi
  kaynak taramasıyla; `TransactionRetryCounter` AC-12'de kaydedilmedi.

## Açık kalanlar

- MOD-0178'in iki açık-yüklem yeri `!= HoldStatus.Decided` ile Voided'ı açık sayıyor: `PutawayTasks/.../GetPutawayCandidateListHandler.cs:185`,
  `Warehouse/Services/WarehouseSerialReader.cs:36`. XMC-001 (sahip kararı bekleniyor; öneri: MOD-0175-FU01 DL-2'de düzelt).
- DL-2 (ekran) ve DL-3 (defter bekçisi; Q-10 MVP-6) başlamadı.

ACCEPTED, kod main'e girdiğinde yazılır (SOP §7).
