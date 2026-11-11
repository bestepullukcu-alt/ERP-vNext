# MOD-0040-FU02 DL-1 — Control Tower kabulü

- Tarih: 2026-11-04. CT: MVP-1 CT, oturum `ca7382cf`. Doğrulama: depo dışı detached ağaç `C:\Users\Ali\ct-verify-fu02-dl1`
  (`c1fc98d15` tam koşu; `f6b541e4d` kayıt kodu + denetim koşusu).
- İncelenen: `db769471c` (ilk grup, AC-41) · `c1fc98d15` (DL-1'in kalanı, AC-1…24, AC-26…47) · `658945363` (A-10 defter izi).
  MVP-1 hattında; push / PR yok. Geliştirme oturumu `local_7e3ecfc9`.
- Paket: `MOD-0040-FU02-record-code-service.md` @ sha256 `62f352de…cc2d` (r11, onaylı `a931345f6`).
- Sözleşme: RECORD-CODE-ALLOCATE 1.0.0 **FROZEN** (`531bb00c1`, `docs/analysis/contracts/record-code-allocate.md`).
- Sonuç: **VERIFIED**.

## Ölçülenler

| Kontrol | Sonuç |
|---|---|
| Platform Application.Tests tam koşu (`c1fc98d15`, filtresiz, DITEN_TEST_MONGOD 9.0) | 9039 test, 22 kırmızı; kayıt kodu kırmızısı 0. 20'si taban / ortam kırmızısı (denetim kuyruğu taşıma, PPM saklama, pazar referans verisi, Mongo test altyapısı); 2 zamanlama testinden `MeetingPersonNamesTests` yeniden koşuda yeşil, `TenantSubscriptionEndpointsHttpMongoTests` eşzamanlılık testi kırmızı kaldı (geliştiricinin `658945363` sonrası tam koşusunda yeşil) |
| Geliştirici tam koşusu (`658945363`) | 9153 test, 19 kırmızı, hepsi önceki 21'in alt kümesi; yeni kırmızı 0; uyarı 161 = taban |
| Kayıt kodu testleri, donmuş sözleşmeye karşı (`f6b541e4d`, `~RecordCode`, AC-23 dahil) | **263 / 263 yeşil** |
| `AuditTrailStandardTests` (`25e06fe08`) | Platform satırı 0; iki kayıt kodu komutu platform-islem-ici izinde (A-10); kalan 19 kırmızı yalnız CRM |
| Geliştirici sabotajları | 92 sabotajın 91'i kırmızı; SB-3 yeşil (sürücü `Unspecified` zamanı doğru çeviriyor — kusur değil, ölçüm) |

## Kapılar (paket Bölüm 18.1)

| Kapı | Durum |
|---|---|
| Sözleşme 1.0.0 donduruldu (AC-23 donmuş sözleşmeye karşı) | kapandı — `531bb00c1` |
| A-10 defter | kapandı — `658945363`, karar kaydı `8648a68fb` (platform-islem-ici) |
| A-16 hız sınırı | kapandı — 600 / dakika / bölüm (sahip kararı) |
| A-11 / A-17 (idempotencyKey biçimi, prefixScope, izin adları, 200 zarfı) | kapandı — sözleşme §12 N-2, N-3, N-4, N-10 (Treasury CT + Ali) |
| A-12 kayıt satırı + "reserved" notu + domain-config | kapandı — bu kayıtla aynı commit |

## Sapmalar (CT kabul etti)

- Pack §4: `AllocatedAtUtc`, `AlignedAtUtc`, `LockedAtUtc` `DateTime` (UTC) — sahip kararı B (2026-11-02); UTC güvencesi testi var.
- A-10: karar metnindeki "platform-merkezi" yerine platform-islem-ici (çift kayıt olmaması için), karar kaydı düzeltildi.

## Açık kalanlar

- `PlatformTransactionExecutor` etiketsiz commit hatasını 500 döndürüyor (paylaşılan altyapı; ayrı takip).
- `BoundedDisplayNamesTests` titrek (ayrı takip).
- N-8: RCS-001 §3 CTR → AGR satırı Ali izni bekliyor (dondurmayı bekletmez).
- Sonraki dilimler (DL-2…) başlamadı.

ACCEPTED, kod main'e girdiğinde yazılır (SOP §7).
