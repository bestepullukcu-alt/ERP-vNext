---
status: approved
decided: 2026-10-28
module: MOD-0230
work_packages: WP-0230-DL4D, WP-0230-DL7B
---

# PVG-MOD0230-SECENEK-01 — Rapor türü ve ürün rolü seçeneklerini servis verir

> **Karar sahibi:** sahip ("secenek-servis", 2026-10-28, PVG Control Tower sohbeti).
> **Kayıt:** PVG Control Tower.
> **Dayanak:** WP-0230-DL7B prompt taslağı v1.0 (`execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL7B-v1.0.md` @ `13bed9f26`), "karar gereken noktalar" K-1 ve K-2.

## Durum

AC-32, formda rapor türü ve ürün rolü seçimini istiyor. Aynı ölçüt bu listelerin görünümlere ya da JS'e elle yazılmasını yasaklıyor ("no option list (country, report type, product role) is hardcoded in views or JS"). Dalga 2 kararı K1 de ekrandaki kopyaları yasaklıyor.

Seçenek kümeleri bugün yalnız serviste duruyor: `services/Diten.PvgService/src/Diten.PvgService.Domain/RegPvBase/PvgIntakeOptionSets.cs`. API'de bu kümeleri döndüren bir uç yok. Rapor türü kayıt açarken zorunlu. Bu yüzden bir kaynak olmadan ekran yeni kayıt açamaz.

Ölçüm (CT, 2026-10-28):
- Gateway'de `GET, PUT /api/pv-case-intake-triage/{intakeDraftId}` route'u var (`gateway/Diten.ApiGateway/ocelot.json`).
- Kayıt kimliği `pvg-draft-<guid>` biçiminde üretiliyor (`InMemoryPvgIntakeDraftRepository.cs:125`). Bu yüzden `options` gibi sabit bir yol parçası hiçbir kayıt kimliğiyle karışmaz.
- Yeni uç bu mevcut route'tan geçebilir. `ocelot.json` değişmez ve AC-16'nın "tam dört route" koşulu korunur.

## Karar

| # | Karar |
|---|---|
| S-1 | PvgService, PVG seçenek kümelerini döndüren salt okunur bir uç sunar: rapor türü, ürün rolü ve ülke için "bilinmiyor" değeri. Kaynak tek yerdir: `PvgIntakeOptionSets`. Uç kimlik doğrulama ister (DL4B ile aynı JWT kuralı) ve kiracı bağlamı denetiminden geçer. Kiracı verisi döndürmez. |
| S-2 | Ekran (DL7B) listeyi aynı kökenli MVC vekili üzerinden bu uçtan alır. Görünümde ve JS'te seçenek kodu listesi bulunmaz. Ekranda görünen ad, koda göre sayfa resx'inden gelir. Karşılığı olmayan bir kodda kodun kendisi gösterilir ve test bunu ölçer. |
| S-3 | Servis işi **WP-0230-DL4D** olarak kuyruğa `PLANNED` eklenir; bağımlılığı WP-0230-DL4B. WP-0230-DL7B'nin bağımlılığı WP-0230-DL4D olur. |
| S-4 | Paket **revizyon 13** hazırlanır. Kapsamı: (a) yeni uç: API sözleşmesi, ölçüt ve DL-4 dilimi; (b) AC-32: seçenek kaynağı S-2'ye göre yazılır, triyajın dört ölçütü çıkarılır (S-5); (c) AC-14: "13 items" yerine kategori ifadesi (DL7A prompt incelemesi v1.2 F-4); (d) AC-20: vekil eşlemesinin C# test katmanı (W7) açıkça yazılır; (e) AC-15: yedi dilde aynı anahtar kümesine ek olarak, CEVIRI-01 C-3'teki "İngilizceyle aynı değer yok" kuralı yazılır. Revizyon bağımsız incelemeden ve HIGH onaydan geçer; onay satırı tek bir zorunlu PR ile `main`'e gider. |
| S-5 | **Triyajın dört ölçütü DL7B'den çıkar (CT okuması, paket gereği).** Paket bu ölçütleri "after QPPV confirms AL-5" koşuluna bağlıyor (veri modeli satırı ~1001; QPPV satırı ~621). API'nin triyaj isteğinde de bu alanlar yok (`PvgCaseIntakeTriageEndpoints.cs`, `PvgCaseIntakeTriageRequest`). Paket, yetki sırasında plan kaydının üstünde olduğu için dalga 5 planının D5-2 maddesi bu ölçüde düzeltilir. Ölçütler, QPPV onayından sonra AC-28 ile aynı işte yapılır. |

## Gerileme değerlendirmesi

- Paket revizyonu, `main`'deki onaylı sürümü değiştirir. Kart S-86 ve S-5 gereği WP-0230-DL8'in kabulü (kayıt 03, revizyon 12) yeni sürüme yeniden bağlanır. Bunun için smoke koşusu yeni commit'te tekrar alınır. Gateway değişmediği için sonucun aynı olması beklenir.
- DL7A'nın VERIFIED kaydı ve dalga 3–4 kayıtları revizyon 12'ye bağlı kalır. Bu kayıtlar `docs/records/**` altında değiştirilmez (K4). Revizyon 13, bu işlerin ölçütlerini daraltmaz; yalnız ekler ve açıklar.
- Dalga sonu PR'ı bu revizyonun onayını taşıyacağı için zorunlu PR maddesi (a) kapsamına girer. Ayrı bir PR açılmaz.
- DL7B, DL4D bitip CT dalına birleşince başlar.
