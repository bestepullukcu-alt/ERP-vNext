# MOD-0040-FU02 — sahip kararları 01 (2026-11-03)

Paket: `execution/domains/platform-shared-services/module-packs/MOD-0040-FU02-record-code-service.md` r11 (sha256 `62f352de…cc2d`,
onay satırı `a931345f6`). Sahip: Beste (K2). Kararlar paket metnini değiştirmez; DL-1 kabul kapısını (Bölüm 18.1) kapatır.

| # | Konu | Karar | Sahip | Tarih |
|---|---|---|---|---|
| A-10 | DL-1'in eklediği iki komut (`AllocateRecordCodeCommand`, `AlignRecordCodeGroupPrefixCommand`) denetim defterinde | `tests/architecture/audit-ledger/Diten.Platform.md` defterine **platform-merkezi** izi altında yazılır; `AuditTrailStandardTests` bu iki komut için kırmızı kalmaz. Defter satırı DL-1 düzeltme commit'iyle girer | Beste | 2026-11-03 |
| A-16 | Hız sınırı değeri (sözleşme N-9) | 600 istek / dakika / bölüm (tüketici servis + kiracı) kalır; eski sırrın geçerlilik penceresi en çok 24 saat | Beste | 2026-11-03 |
| N-7 | Şirket öneki biçimi (paket K-6) | K1'den 2026-11-02'ye kadar karar gelmedi; RCS-001 §1 ile kapandı | MVP-1 CT | 2026-11-03 |

Açık kalan dondurma kapıları (sözleşme §12 "Bekletir: evet"): N-2 (yanıt zarfı), N-3 (`idempotencyKey` biçimi), N-4 (`prefixScope`),
N-10 (izin adları) — Treasury CT ve MDM sahibinin teyidi bekleniyor.
