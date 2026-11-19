# WP-0178-E3-FIX-01 — CT kabul kaydı — 2026-11-13

- CT: MVP-5 WMS CT (oturum `ca7382cf`). Test ve sabotaj doğrulaması ayrı worktree'de alt ajanla (`agent-a76070d9d65f7c92c`, detached `49fd410b7`); canlı tur CT tarafından, sahibin oturumuyla (Beste giriş yaptı; CT şifre görmedi).
- Hedef: `49fd410b7` — fix(sce): MOD-0178 E3-FIX-01 — outbound screens: premium confirm, quantities in the reader's culture, "Outbound" translated, preview shows the chosen lot. Ebeveyn `333e91559` (MVP-1 hat dalı).
- İş paketi: `execution/domains/supply-chain-execution/work-packs/mvp5-wms/WP-0178-E3-FIX-01-ui-l10n.md`; kaynak tur kaydı `mod-0178-e3-live-tour-2026-11-13.md` (G-1, G-2 + sahip gözlemleri).
- Sonuç: **VERIFIED.**

## Diff

- 27 dosya, hepsi `frontend/Diten.Web` ve `frontend/Diten.Web.Tests` altında; servis, sözleşme, Gateway, `ocelot.json` değişmedi.
- Madde 1: `_ActionDialogs.cshtml`'deki iki Bootstrap modal kaldırıldı (yalnız anahtara bağlı `outboundActionToken` kaldı); `details.js`'te İptal ve Sevke Hazır `window.showConfirm` (MOD-0013); FEFO gerekçeleri tahsis başına zincirleme pencereler, sonunda yeni idempotency anahtarlı tek istek. İptal `type: danger`, textarea, 10–1000 karakter doğrulayıcı, vazgeç "Kapat". Üç sayfada ham `Swal.`, `bootstrap.Modal`, `alert(`, `confirm(` yok.
- Madde 2: `quantityText` / `dimensionsText` `Intl.NumberFormat` (latin rakam, en çok 6 ondalık); Detay, önizleme, PickTasks, Packing. Servise giden değer nokta-ondalık; giriş kutuları gruplamasız (7 dilde ölçüldü).
- Madde 3: hiçbir `*.tr.resx` değerinde "outbound" yok; `Nav.Page.OUTBOUNDSHIPMENTS` tr "Çıkış Sevkiyatları"; `OutboundShipmentsIndex` 7 dilde 219/219; yeni `PreviewChosenLot`, `StagingNotAllowed` 7 dilde.
- Madde 4: önizleme `lotOverride` gönderir (`rules.lineBody(r)`), lot değişince yeniden koşar, `line.deviation` "Seçtiğiniz lot…" satırıyla gösterilir; proxy ve servis zaten destekliyordu (sözleşme değişmedi).
- Test değişiklikleri geçerli: `WarehouseS2ScreensTests` Detay testi sıkılaştı (iki modal kimse için çizilmez); kapatma düğmesi eşiği 9 → 7 (gerçek sayı 8; 48 px kuralı korunuyor, sabotaj e); `global-confirm-input-type` sayım satırı gerçek iki çağrıyla eşleşiyor.

## Testler

| Koşu | 49fd410b7 | 333e91559 |
|---|---|---|
| Yeni vitest dosyası | 10/10 | – |
| Tam vitest | 5528, 13 kırmızı | 5518, 13 kırmızı |
| Diten.Web.Tests Warehouse | 326/326 | 326/326 |
| Diten.Web.Tests tam | 4542, 3 kırmızı | 4542, 3 kırmızı |

- 13 vitest kırmızısı iki uçta bayt-eşit (dialog-one-implementation Windows yol farkı, global-products-register, governance-users-error-codes-ct ×3, governance-users-error-codes, gsku-register, platform-administrators-own-record, wcn-detail-last-four, wcn-dialog-one-language, wcn-dialog-rhythm, wcn-dialog-seven-defects ×2); Warehouse dosyası yok.
- 3 Web.Tests kırmızısı iki uçta aynı (ReturnDetailsBehaviorTests, SandopPlansPageTests, CapacityPlansPageTests).
- resx-check iki uçta FAIL ve çıktılar bayt-eşit (main borcu: fr/es/zh/ar/ru 35 eksik anahtar); bu commit SharedResource'ta yalnız tr menü değerini değiştirdi.
- AccountKindAcceptanceHost.Tests koşulmadı; 27017 kullanılmadı.

## Sabotajlar (CT'nin bağımsız beşi; her biri geri alındı, git diff boş)

| # | Sabotaj | Sonuç |
|---|---|---|
| a | Üç `rules.js`'te `quantityText` ham metni döndürür | yeni vitest'te 5 kırmızı |
| b | `form.js` önizlemeye `lotId: ''` | "item 4: the chosen lot goes into the preview" kırmızı |
| c | İptal yine `bootstrap.Modal` + `modalOutboundCancel` div | vitest 2 kırmızı + `WarehouseS2ScreensTests.The_Details_page_draws_each_dialog_only_for_its_key` kırmızı |
| d | tr resx'e "Outbound Sevkiyatlar" geri | "G-2 / item 3 … no Turkish text says outbound" kırmızı |
| e | PickTasks kapatma düğmesinden `wh-touch` kaldır | `AC45_F7_F8…` kırmızı |

Geliştiricinin 18 sabotajı tek tek yeniden koşulmadı.

## Canlı tur (tr; filo ana checkout'tan dotnet watch ile FIX-01'i aldı)

| Adım | Sonuç |
|---|---|
| Başlıklar | "Çıkış Sevkiyatları", "Yeni çıkış sevkiyatı"; PickTasks sütunu "ÇIKIŞ SEVKİYATI"; sayfada "outbound" yok |
| Önizleme | FEFO 2 lotu (G1-20261020) seçilince önizleme kendiliğinden yenilendi (doğal change olayı); lot, "FEFO önerisi lot AMX-2604" notu ve gerekçe alanı göründü; lot listesinde "346 alınabilir" |
| Kayıt | `GMG-OUT-000003` Allocated, G1-20261020, gerekçe kayıtlı, rezervasyon Tutuluyor |
| Toplama | panelde "2", outbound Picked |
| Paketleme | ağırlık "1,5" girildi ve "1,5" gösterildi; serviste `1.500` kg; `GMG-PKG-000002` Closed, Packed |
| Sevke Hazır | yeni premium onay diyaloğu (halka ikon, kayıt numarası, İptal / Sevke Hazır); ReadyToShip, readyAt 10:42:54 |
| Detay ve önceki kayıt | GMG-OUT-000001: "2 EA", "1,5 kg", "2 / 2"; Detay'da `.modal` sayısı 0 |
| 375 px | Detay sayfası yatay kaydırma yok (scrollWidth 375) |

Canlıda görülmeyen: Sevke Hazır sırasında FEFO gerekçe diyaloğu (gerekçe kayıtta verildiği için çıkmadı) — vitest ile kapsandı. Not: CT'nin ilk üç kayıt denemesi, test kodunun sayfadaki ilk gönder düğmesine ("Çıkış Yap") basmasından dolayı oturumu kapattı; FIX-01 kusuru değildir (`btnSaveOutbound` ile kayıt çalıştı).

## Düşük gözlemler (kabulü engellemez)

- `quantityText` üç `rules.js`'e kopyalanmış; ortak yardımcıya taşınabilir.
- Kapatma düğmesi eşiği ≥7, gerçek 8 (önceki eşik de bir boşluk bırakıyordu).
- PickTasks `_DetailsQuickView.cshtml` hâlâ sayfada çizilen Bootstrap modallar içeriyor (WP kapsamı dışı; takip).
- Detay'da FEFO sapma gerekçesi "Rezervasyon" sütununda "Tutuluyor" altında görünüyor; MOD-0178-FU01'e not.

## Test verisi

`GMG-OUT-000002` (geliştirici, iptal edildi), `GMG-OUT-000003` (CT, ReadyToShip). Push / PR yapılmadı.
