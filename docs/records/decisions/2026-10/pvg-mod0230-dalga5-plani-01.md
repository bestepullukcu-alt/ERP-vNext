---
status: approved
decided: 2026-10-27
module: MOD-0230
work_packages: WP-0230-DL7A, WP-0230-DL7B
---

# PVG-MOD0230-DALGA5-01 — Beşinci dalga planı (ekran, DL-7)

> **Karar sahibi:** sahip ("dalga5-plan", 2026-10-27, PVG Control Tower sohbeti).
> **Kayıt:** PVG Control Tower.
> **Paket:** revizyon 11 (`918dd660…`, `main`'de onaylı, #261).

## Durum

Paketin DL-7 dilimi (satır ~1878) şu önkoşulları istiyor: DL-6, OD-PVG-8, OD-PVG-10 ve AC-20 için DL-4'ün AC-19'u. AC-32'nin alan girdileri için ayrıca DL-4'ün AC-31'i gerekiyor.

| Önkoşul | Durum |
|---|---|
| DL-6 | WP-0230-DL6, CT dalında VERIFIED |
| OD-PVG-8 ve OD-PVG-10 | 2026-10-21'de kapandı |
| AC-19 ve AC-31 (DL4A) | `main`'de, #250 |

AC-32'nin seçicileri (ürün, kişi, ülke) hâlâ bekliyor. Bekledikleri: MOD-0290, MOD-0150 ve MOD-0288 okuma sözleşmeleri ile ülke listesinin kaynağı.

## Karar

| # | Karar |
|---|---|
| D5-1 | **WP-0230-DL7A.** Kapsam: AC-1 (`_LayoutTenantShell`), AC-2 (`[HasPermission]`, 10 eylemin 10'unda, `pvg.case-intake-triage.*` ailesinden), AC-3 (UAS-001: yetkisiz kullanıcıya iskelet yok), AC-14 (liste doğrulayıcısı; yalnız OD-PVG-10'daki bilinen sapma kalır), AC-15 (yedi dilde `CaseIntakeTriageIndex.*.resx`, aynı anahtar kümesi), AC-20 (sayfalama, toplam / süzülmüş sayı, kayıt sürümünün taşınması, 409'un kontrollü hata olarak gösterilmesi). Yollar: paketin DL-7 "Yollar" alanı. |
| D5-2 | **WP-0230-DL7B.** Kapsam: AC-32'nin alan girdileri. `_Form.cshtml`'e şunlar eklenir: ürün listesi (elle), farkına varma anı, olay ülkesi ("bilinmiyor" ile), rapor türü, bildiren için elle giriş yolu; triyaj formuna dört ölçüt; düzenleme formunda "bağı kaldır". Etiketler yedi dilde olur. Ürün, kişi ve ülke seçicileri ile şirket alıcısının önceden doldurulması kapsam dışıdır; sözleşmeleri bekler. |
| D5-3 | **Sıra.** DL7A önce yapılır. DL7B, DL7A CT dalına birleştikten sonra başlar; çünkü ikisi aynı denetleyiciye, görünümlere ve JS dosyalarına dokunuyor. |
| D5-4 | **PR.** Sahibin kuralları geçerli: tek PR, zorunlu olmadıkça PR yok. İşçi dalları CT dalına birleşir. Bütün dalgalar bitince tek bir PR açılır. |
| D5-5 | **Kanıt.** DL-7 için gerekli seviye E4. Bu işler vitest ve render testleriyle E2 kanıt üretir. Ekranın canlı ölçümü E3–E4 ister ve kabul canlı kapıya bağlı kalır. |

## Gerileme değerlendirmesi

- Bugün ekran kayıt yapamıyor. Bunun nedenleri DL2B'den (K2) ve DL4A'dan geliyor: sürüm taşınmıyor, yeni alanlar yok. DL7A ile DL7B bu durumu düzeltir.
- `[HasPermission]` yeni anahtarları ister. Bu anahtarlar henüz hiçbir role verilmedi; grant MOD-0018'in işi. Bu yüzden yetki verilene kadar ekranı açan her kullanıcı "yetkiniz yok" yanıtı alır. Bu UAS-001'in istediği güvenli durumdur. Prompt bunu açıkça yazar.

## Kuyruk

`CT-QUEUE.tsv` dosyasına iki satır `PLANNED` olarak eklenir:
- WP-0230-DL7A (bağımlılık WP-0230-DL6)
- WP-0230-DL7B (bağımlılık WP-0230-DL7A)
