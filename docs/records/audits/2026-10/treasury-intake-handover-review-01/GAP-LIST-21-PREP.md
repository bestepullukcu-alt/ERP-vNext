---
record_id: TREASURY-GAP-LIST-21
kind: sahip denetimi (CT-APR-02 değildir) — bulgular ve kapanışlar
received_at_local: 2026-10-26
received_from: depo sahibi (K1), sohbette yapıştırılan denetim ("CONDITIONAL PASS — D33 güvenlik kararları r8'e işlenmiş; hazırlık belgeleri henüz geliştirme izni vermiyor")
reviewed_input: ZIP treasury-hazirlik-ve-MOD0289-r8.zip (manifest 7/7) — MOD-0289 r8 @ bebe8fa73, beş hazırlık kaydı @ 4c59827ec, karar kaydı @ a181e933b
fixed_in: bu kaydın commit'i (dal docs/ct/treasury-packs-rev-02) — beş pack'in hash'i değişti; yeniden inceleme gerekir
previous: GAP-LIST-20-PRE-REVIEW.md
---

# Gap listesi 21 ve kapanışları

Denetimin doğruladıkları: ZIP sağlam (7/7); MOD-0289 `slices()` DL-1…DL-4; güvenlik onayı, anahtar kaynağı, döndürme,
`ApprovalRef` ve AC-50 eşiği yazılmış; gerçek Mongo / yük testleri DL-2 kabul kapısında, geçmiş sayılmamış; MOD-0289 için
ayrı kod alma izninin bulunmadığı korunmuş. Sahibin önceliği: yeni WP yazmadan önce kapsam / dilim düzeltmeleri hedef
pack'lere işlenir ve değişen hash'lerle yeniden incelenir; hazırlık notu pack düzeltmesinin yerine geçmez.

| # | Gap | Kapanış |
|---|---|---|
| 1 | MOD-0289:599 — AC-27 yanlış dilime bağlı (hesap + maskesiz okuma kanıtı DL-3'te) | Bağımlılık satırı: AC-27 banka kısmı DL-1, hesap / aktivasyon / maskesiz okuma kısmı ve tam kapanış DL-3; AC-27 metnine dilim ayrımı; DL-1 Bitiş'e "27 (banka kısmı)" |
| 2 | MOD-0289 Gateway listesinde `bank-account-purposes` yok | Gateway kararına `/api/mdm/bank-account-purposes` ve `/{everything}` eklendi (uçlar DL-2, ekran DL-4; rota DL-2 teslimiyle istenir) |
| 3 | Hazırlık kayıtlarındaki pack tutarsızlıkları pack'lere işlenmeli (özellikle Cash Positioning AC-29, indeks hizmeti sahibi, MOD-0126 sabit sürümü; AR test yolları) | Dört Treasury pack'i ve MOD-0289 düzeltildi (aşağıda) |

## Pack bazında kapanışlar (dal `docs/ct/treasury-packs-rev-02`)

| Pack | Yeni SHA-256 | Kapananlar |
|---|---|---|
| MOD-0126 | `65a619148d704eb681ab240e87d3ac5882b89e23618c14f93f399bcf195eaed8` | `IPaymentMethodReferencePort` (422 / 503, Ordinal) portlar, Repo Scope, DL-1 yolları, AC-41; `ux_payment_batch_code` (silinmiş dahil) + test / sabotaj; D35 (iz b, borç 0, `Finance` kategorisi DL-1 öncesi, checklist); D34 (onay `ready-for-dev` sürümüne, DCP önce numara); MOD-0023 §17 rev 2 (`803fbaa5…`, AC-W1…W17, D32/1); D30 hizalama; listede olmayan para birimi 400 `PAYMENT_CURRENCY_CODE_INVALID`; D22/2 cümlesi D25/1 ile güncellendi |
| MOD-0119 | `a1b3f702f8e0d777a4450eb3fb2914e6aacec32003df7243a75a6573419cc4ff` | DL-1 yalnız AP bağdaştırıcılarını yazar, ortak portları tüketir; O-18 / kategori D35; D26 / D28 sonrası eski cümleler; AC-15 bütün DL-1 yazma komutları; 400 `AP_CURRENCY_CODE_INVALID`; MOD-0143 satırı D26/3; D30 hizalama |
| MOD-0120 | `5fb91d46f97d174d13f895a2f25285543db269f0a73ed48b51ae3ccc47887f27` | AC-31 (DL-1 oluşturma / silme, DL-2 açma); AC-2 / AC-5 "kısmı" ayrımı; D35; D30; 400 `RECEIVABLE_CURRENCY_CODE_INVALID`; kısmi indeks değer kümesi yazımı (`$in` yok; `$or` desteği gerçek replica set'te ölçülecek); AC-32 (d) DL-1; **test yolları** `services/Diten.FinanceTreasuryService/tests/**` Repo Scope ve Test Expectations |
| MOD-0124 | `3608e6f9d091cb98c4845aa6cfb5886c5f4fed90cb58911229a634db46efd6fb` | O-3 durumu (D29/1; teyit `ready-for-dev` engeli); D30; **AC-29 DL-1** (CentralAudit dev kanıtı, üç satırda tutarlı); D35; **indeks hizmeti tek kural** (barındırılan hizmet iskelette, MOD-0126 DL-1; MOD-0124 yalnız kendi koleksiyonlarının indeks tanımları); **MOD-0126 sabitlemesi** `65a61914…` (aynı commit); O-7 / O-14 / O-17 teyitleri checklist'e |
| MOD-0289 | `785291d2e16688fd81d480120cd18d1463ec362f636bca86d0cb6da88bd4a839` | gap 1–2; kaynak servis 5353 satır; `Guid.Empty` atıfları (34 atama, `:209`, `:293`); RCS §3 `BNK` / `BAC` durumu (D29/2, D30) |

**Paketler arası hizalama (CT):** biçimi geçerli ama MOD-0048 listesinde olmayan para birimi dört Treasury pack'inde aynı:
**400** (girdi alanı hatası) — `PAYMENT_CURRENCY_CODE_INVALID`, `AP_CURRENCY_CODE_INVALID`, `RECEIVABLE_CURRENCY_CODE_INVALID`,
`CASH_POSITION_INVALID_CURRENCY_CODE`. Liste okunamazsa 503 (pack'teki mevcut hane / kaynak kuralı).

**Açık kalanlar:** MOD-0119'da para birimi listesine ulaşılamaması için ayrı bir kod yok (pack'te yalnız küsurat kaynağı 503);
MOD-0120'de kısmi indekste `$or` desteği gerçek replica set'te ölçülecek; hazırlık kayıtları eski pack hash'lerine bağlı —
pack'ler incelendikten sonra güncellenir. Bütün pack'ler `draft`; bu düzeltmeler geliştirme izni değildir.

Bu sahip denetimi resmî CT-APR-02 kaydı değildir. Belge incelemesidir; build / test / runtime yok.
