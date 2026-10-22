---
decision_id: PVG-MOD0230-INTAKE-FIELDS-02
status: approved — sahip beş noktada Control Tower önerisini kabul etti (2026-10-22, sohbette "öneriler"); QPPV ve MOD-0048 onayı bekleyen kısımlar o onaylara kadar uygulanmaz (taslak, commit edilmedi)
prepared_at_local: 2026-10-22
prepared_by: PVG Control Tower (Claude Code oturumu); bu kayıt kendi adına karar vermez
decided_by: depo sahibi (rol); kişi adı ve e-posta yazılmaz
supplements: docs/records/decisions/2026-10/pvg-mod0230-intake-fields-01.md @ e5490e6db (o kayıt değiştirilmez — docs-organization K4)
trigger: paket revizyonu 04 taslağında (module-pack-author) karar kaydının çözmediği beş nokta; Control Tower ölçümü
scope: yalnız MOD-0230 alım alanları; operasyonel runtime kapısı kapalı kalır
---

# MOD-0230 — alım alanları ek kararı (EK-1…EK-5) ve AL-3 düzeltmesi

> İlk kayıt (`pvg-mod0230-intake-fields-01.md`) commit edildiği için düzeltilmez; bu kayıt onu tamamlar ve bir ifadesini
> düzeltir. Mevzuata dayanan ifadeler genel bilgidir, depoda kaynağı yoktur; QPPV doğrulamadan kesinleşmez.

## AL-3 düzeltmesi (Control Tower hatası)

- İlk kayıtta AL-3 için "Ayrıca gereken onay: yok (referans veri okunur)" yazıldı. **Bu yanlıştı.**
- Ölçülen olgu: `execution/domains/platform-shared-services/module-packs/MOD-0048-lookups-reference-data.md` satır 392 ve
  555 — `GET /api/lookups/countries` yalnız Platform kiracı kurulum / destek ekranları içindir (`PlatformActor`
  politikası); başka bir kullanım için "ayrı onaylı karar" gerekir; uç bir bölge (territory) referans API'sine
  dönüştürülmez. MOD-0048-FU01 iş referans verisinde ISO 3166-1 alfa-2 ülke-pazar kümesi tutar; CRM kişi kaydındaki
  `CountryRef` de MOD-0048 konum kodudur.
- Doğru ifade: **AL-3 için MOD-0048 (Reference Data Management) sahibinin onayı gerekir.**

## Kararlar

| # | Konu | Karar (sahip, 2026-10-22) | Ayrıca gereken onay |
|---|---|---|---|
| EK-1 | Olay ülkesi ve bildiren ülkesinin kaynağı | MOD-0048 sahibinden PVG için onaylı bir ülke listesi (ISO 3166-1 alfa-2; iş referans verisi yolu) istenir. Liste gelene kadar `EventCountry` ve `ReporterCountry` yalnız PVG'nin "bilinmiyor" değerini kabul eder; sabit (hardcoded) ülke listesi yazılmaz. PVG listeye değer eklemez. | MOD-0048 sahibi |
| EK-2 | Farkına varma anı vaka açılırken zorunlu mu | Zorunlu. Varsayılan değer alınma anıdır (`ReceivedAtUtc`); bilgi daha önce bir çalışana geldiyse daha erken bir an seçilebilir. Kural `AwarenessAtUtc` ≤ `ReceivedAtUtc` aynen kalır. | QPPV — hangi zaman damgasının güvenilir sayılacağı (QPPV-02) |
| EK-3 | Kimliği belirsiz bildiren vaka açılırken kabul edilir mi | Kabul edilir. Vaka triyajda `ValidityReporterIdentifiable` ölçütünü karşılamaz, "geçersiz — takip gerekli" sonucunu alır. PV hiçbir bildirimi geri çevirmez. | QPPV (AL-5 ile birlikte) |
| EK-4 | Bağlı kaynaktan kopyalanan anlık kopya vakada düzenlenebilir mi | Düzenlenebilir. Değişiklik denetim kaydına yalnız alan adıyla yazılır (değer yazılmaz). Kaynak kayıt (CRM kişisi, kişi dizini) değişmez; bağ kimliği kalır. | yok |
| EK-5 | Alan sınırları | En çok 20 ürün satırı; `LotNumber` 64 karakter; `ReporterNameOrInitials` ve `ReporterProfession` 128'er karakter. Bağımsız inceleme 04 bu sınırları ayrıca değerlendirir. | yok |

## Pakete etkisi

Paket revizyonu 04 taslağındaki (commit edilmedi) "pack-author reading" ve "pack-author proposal" işaretleri bu kayda
bağlanır; AL-3'ün kaynak satırı bu kayıttaki EK-1'e atıf yapar; EK-4 kuralı DL-2 kapsamına eklenir. Bu hizalamayı
`module-pack-author` yapar; Control Tower denetler.

## Yerini alan / yerine geçen

- `pvg-mod0230-intake-fields-01.md` AL-3 "Ayrıca gereken onay: yok" → bu kayıttaki AL-3 düzeltmesi ve EK-1.
