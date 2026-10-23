---
decision_id: PVG-MOD0230-INTAKE-FIELDS-03
status: approved — sahip üç noktada Control Tower önerisini kabul etti (2026-10-22, sohbette "karar"); QPPV onayı bekleyen kısımlar QPPV'ye bilgi olarak gider, kural bugünden uygulanır
prepared_at_local: 2026-10-22
prepared_by: PVG Control Tower (Claude Code oturumu); bu kayıt kendi adına karar vermez
decided_by: depo sahibi (rol); kişi adı ve e-posta yazılmaz
supplements: docs/records/decisions/2026-10/pvg-mod0230-intake-fields-01.md ve pvg-mod0230-intake-fields-02.md (o kayıtlar değiştirilmez — docs-organization K4)
trigger: paket revizyonu 05 taslağında (module-pack-author, sha256 d5087832…) açılan OD-PVG-11 ve OD-PVG-12; yazar ajanın raporundaki CRM ülke kopyası boşluğu (Control Tower ölçtü)
scope: yalnız MOD-0230 alım alanları; operasyonel runtime kapısı kapalı kalır
---

# MOD-0230 — ülke listesi sonrası, bildiren bağının kaldırılması ve bağlı kaynaktan gelen ülke (KR-1…KR-3)

> Bağımsız inceleme 04'ün F-1 ve F-3 bulgularını işlerken paket revizyonu 05 taslağı, karar kayıtlarının çözmediği iki
> nokta için açık karar satırı açtı (OD-PVG-11, OD-PVG-12) ve karar verilene kadar bu istekleri reddetti. Üçüncü nokta
> yazar ajanın raporunda kaldı; karar satırı açılmadı. Bu kayıt üçünü kapatır.

## Ölçülen olgu (KR-3)

- MOD-0150 (CRM) kişi kaydında ülke `CountryRef` alanıdır ve MOD-0048 konum kodudur (paket "Required Interface
  Contracts", MOD-0150 satırı).
- Revizyon 05 ülke kuralı tablosu, ülke listesi yokken ya da okunamıyorken `ReporterCountry` için liste kodunu reddeder.
- Sonuç: MOD-0150 sözleşmesi MOD-0048 listesinden önce gelirse, CRM kişisinden kopyalanan ülke kodu bildirenin ülkesi
  kuralına takılır ve vaka açılamaz. Bu, EK-3'teki "PV hiçbir bildirimi geri çevirmez" ilkesiyle çelişir.

## Kararlar

| # | Konu | Karar (sahip, 2026-10-22) | Ayrıca gereken onay |
|---|---|---|---|
| KR-1 (OD-PVG-11) | Ülke listesi geldikten sonra bildiren ülkesi; kayıtlı ülke kodunun korunması (iki ülke alanı) | (a) `ReporterCountry` = "unknown" her liste durumunda (yok / var / kesintili) kabul edilir; tanımlayıcı sayılmaz (revizyon 05 "Reporter identifiers"). (b) Kayıtlı bir liste kodu güncellemede değişmeden gelirse her liste durumunda korunur (liste okunamıyorken ya da kod listeden çıkmışken de); yalnız yeni ya da değişen kod o anki listeyle karşılaştırılır. (b) iki ülke alanında da geçerlidir: `EventCountry` ve `ReporterCountry` (sahip, 2026-10-22, sohbette "kapsasın"). Gerekçe: PV hiçbir bildirimi geri çevirmez (EK-3); sonradan değişen bir liste eski kaydı bozmaz (AL-6 anlık kopya ilkesi). | QPPV — bildiren ülkesi düzenleyici içerik sayılırsa (bilgi) |
| KR-2 (OD-PVG-12) | Bildiren bağının kaldırılması | Bağ yalnız açık bir "bağı kaldır" işaretiyle kaldırılır. İşaretli güncellemede sunucu `ReporterSource = Manual` yazar, `ReporterSourceId`'yi boşaltır, anlık kopya değerlerini elle girilmiş değer olarak bırakır; denetim kaydı değişikliği yalnız alan adıyla gösterir. Bağlı kayıtta seçim çifti olmadan gelen işaretsiz güncelleme reddedilmeye devam eder (sessiz bağ kopması yok). İşaret ile seçim çifti aynı istekte gelirse reddedilir. Gerekçe: eksik çift, istemci hatasından ayırt edilemez; açık işaret ölçülebilir. | QPPV — bildiren bağı MOD-0231 mükerrer kontrolünün dayanağıdır (AL-7) |
| KR-3 | Bağlı kaynaktan (CRM, kişi dizini) gelen ülke | Bağlı kaynaktan kopyalanan ülke, kopyalama anında ülke kuralı tablosunun `ReporterCountry` sütunlarına uyuyorsa `ReporterCountry`'ye yazılır; uymuyorsa `ReporterCountry` boş (null) kalır ve vaka bu yüzden reddedilmez. Kopya sonradan elle düzeltilebilir (EK-4). Kaynak kayıt değişmez (XMC-001). | yok |

## Pakete etkisi

Paket revizyonu 05 taslağında OD-PVG-11 ve OD-PVG-12 satırları bu kayda bağlanır ve "Evet" olarak kapanır; ülke kuralı
tablosunun "Open - OD-PVG-11" hücreleri KR-1'e göre doldurulur; BG-29 / BG-31 / BG-32 ve ilgili satırlar KR-2'nin
"bağı kaldır" işaretini ölçer; anlık kopya ve CRM ön doldurma bölümleri KR-3'ü yazar ve bir kabul testi alır. Bu
hizalamayı `module-pack-author` yapar; Control Tower denetler.

## Yerini alan / yerine geçen

- Yok. Bu kayıt intake-fields-01 ve -02'yi değiştirmez; yalnız onların çözmediği üç noktayı karara bağlar.
