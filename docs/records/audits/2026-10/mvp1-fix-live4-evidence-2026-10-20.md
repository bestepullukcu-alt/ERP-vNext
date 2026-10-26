# WP-MVP1-FIX-LIVE4 — kanıt (2026-10-20)

Dal `feature/sce/wp-mvp1-fix-live4`, taban MVP-1 dalı `feature/sce/mod-0173-inventory-ledger-valuation` @ `c654429ca`. Yerel commit'ler, push yok.
Worktree `C:\Users\Ali\erp-vnext-wt-live4`; ana çalışma dizininde derleme yapılmadı, filo portlarına (5000–5070, 4010) ve canlı kiracı verisine dokunulmadı.

## 1. Taban (ölçüldü)

| Ölçüm | Taban |
|---|---|
| Web | 3962 / 3964 — 2 kırmızı main'den (`NavManifestL10nGuardTests` ×2, SafetyStock) |
| Inventory | 1319 + 4, 16 kırmızı (`PrismConsumedContractSmokeTests`, soğuk Prism "did not listen within 90 s"); sınıf tek başına 16 / 16 → **1335 + 4** |
| Mimari | 7 kırmızı (AuditTrail ×2, DocsPath ×5) |
| Gateway | 298 / 298 |
| Inventory yeni-denetimsiz | 0 |

## 2. Maddeler

### a) FU-0176-BAND-EXEMPT — bant muaf türleri bilsin

Kök neden: `ExpiryBands.movement.onSource` yalnız yöne bakıyordu (`direction === 'out'`). Hareket türünü hiç almıyordu, bu yüzden muaf bir çıkışta da bandı açıyordu. Servis o türde FEFO kontrolü yapmadığı için gerekçe sessizce kayboluyordu.

| Tür (formda) | Servis FEFO çıkış kontrolü uyguluyor mu (`FefoRules.cs` `ExpiryHookScope`) | Bant önce | Bant sonra |
|---|---|---|---|
| COUNT_LOSS | hayır — `Exempt` (CT-D4 / §8.3) | açılıyordu | yok |
| SCRAP | hayır — `Exempt` | açılıyordu | yok |
| WRITE_OFF | hayır — `Exempt` | açılıyordu | yok |
| WAREHOUSE_TRANSFER | hayır — `Exempt` (CT-F3) | açılmıyordu (yön `wh`) | yok |
| LOCATION_TRANSFER | hayır — `Exempt` | açılmıyordu (yön `loc`) | yok |
| OPENING_BALANCE, COUNT_GAIN, CUSTOMER_RETURN | giriş (çıkış kontrolü yok) | yok | yok |
| GOODS_ISSUE_SALES / GOODS_ISSUE_PROD | **evet** — AVAILABLE'dan lotlu çıkış (`ExpiryHookKind.Issue`) | — (formda yok) | açılır |

- **Formda elle girilebilip FEFO kontrolü uygulanan bir tür yok.** Formun 8 türünün hepsi ya muaf ya giriş; bu yüzden bant formda artık hiç açılmıyor. Kontrol uygulanan türler (satış ve üretim çıkışı) başka modüllerden gelir. Bandın kendisi bu türler ve sunucunun FEFO 422'si için eskisi gibi çalışıyor.
- Tek kaynak, `ExpiryBandRules`:
  - `FEFO_EXEMPT_TYPES`: Web'deki tek liste.
  - `outboundChecked(type, fromStatus, lotId)`: servisin kuralı — muaf değil, AVAILABLE'dan, lotlu.
  - Servis bu listeyi okuma ucu ya da sözleşme alanı olarak sunmuyor (ölçüldü).
  - Bekçi: `ExpiryBandExemptTests.The_web_exempt_list_is_the_services_list_value_for_value`. Servisin üretim kaynağını (`FefoRules.cs` `Exempt`, `WireNames.cs` üzerinden) okuyup Web listesiyle değer değer karşılaştırır.
- Form değişiklikleri (`form.js`):
  - türü ve kaynak satırın durumunu banda geçirir;
  - tür değişince bant yeniden karar verir;
  - bant gerekçe alanının zorunluluğunu kendisi yönetir. Kaynak bloğunun matris kuralı, bandın alanını artık "zorunlu" yapmıyor.
- Gerekçe yalnız bant istediğinde gönderilir. Bant kapanınca alan temizlenir ve zorunluluğu kalkar.
- Testler:
  - `expiry-band-exempt.test.mjs` (5): 9 muaf türde bant ve gerekçe yok (FEFO sırasında daha yakın lot olsa bile); kontrol uygulanan türde var; tutulan stoktan çıkışta yok; gerekçe yalnız bant isterken gider; tek liste.
  - `ExpiryBandExemptTests`: bekçi; formda kontrol uygulanan tür olmadığının ölçümü; Node koşucuları.

### b) FU-0175-BLOCK-QTY — bloke miktarı

- Satır seçimi miktarı artık **hiç yazmıyor**; boş alan boş kalıyor. "Kullanılabilir: 80 EA" alanın altında yazmaya devam ediyor.
- Yeni "Tümü" düğmesi tam miktarı yalnız tıklanınca yazıyor; satır seçilmeden pasif. Metin 7 dilde: `Quarantine.BlockStock.All` / `AllHint`.
- Kullanıcının yazdığı değer korunuyor; değere ekleme yapılmıyor.
- Aynı kalıbın ölçüldüğü diğer pencereler:
  - "Stoğu karantinaya al" (`manual-hold.js`): ön doldurma yok.
  - Karar ver, kısmi karar (`decide.js:58`): ikinci satır, toplamın tutma miktarına eşit olması için kalanı hesaplıyor. Bu bir kullanıcı girdisinden hesap, satır seçiminden ön doldurma değil; değiştirilmedi.
  - Recall ayrıştırma onayı (`case-detail.js:425`): tutulan miktarla doluyor. Recall bu WP'nin kapsamı dışında → O-LIVE4-2.
  - Hareket formunun `prefillQuantity` alanı (`form.js:898`): Düzelt / iade bağlamının kendi değeri; satır seçimi değil.
- Değişen test beklentisi: `quarantine-blocked.test.mjs` içindeki "an empty quantity takes the row amount as a suggestion" testi, "boş kalır" ve "Tümü" testleriyle değiştirildi. Gerekçe canlı bulgu: 80 + "10" = "80,00010". C# koşucusundaki geçme sayısı 10 → 11.

### c) FU-0175-HELP-CHIPS — yardım etiketleri (ölçüldü; değişiklik yok)

- Sunucu çizimi: `PageHelpCatalog.Labelled` her tırnaklı adı gerçek metin düğümüyle yazıyor (`<span class="diten-help-ui">Filtrele</span>`).
  - Önizlemede (tr) Bloke Stok adım 1'in etiketleri "Filtrele | Yalnız disposition bekleyenler | Durum | Uygula".
  - Görünen metin: "…filtre düğmesine (Filtrele) tıklayıp bloke nedeni, Yalnız disposition bekleyenler veya Durum seçin ve Uygulaya tıklayın."
- CSS (`backbone-custom.css` `.diten-help-ui`) yalnız çerçeve çiziyor: `content`, gizleme ya da `font-size: 0` yok. `diten-help.js` etiketlere dokunmuyor.
- Sonuç: metin sayfada ve DOM'da var. Ölçüm aracının erişilebilirlik ağacı rolsüz satır içi `span`'ları atlıyor. Ekran okuyucular bu metni okur, ama düğüm ayrı bir "generic" olarak görünür.
- Sorun ortak bileşende olduğu için DUR-SOR:
  - A: değişiklik yok (metin erişilebilir).
  - B: en küçük ortak düzeltme — etiketi anlamlı bir öğeyle çizmek (ör. `<kbd class="diten-help-ui">` ya da `role="text"` yerine `<b>`), tek satır, `PageHelpCatalog.Labelled`'de.
- Bizim resx / katalog satırlarımız doğru.
- Test: `HelpLabelTextTests.Blocked_stock_step_one_draws_every_quoted_name_as_text` — gerçek kabuk çizimi ve CSS kuralı.

### d) FU-0173-PICKER-CODE — ürün kodu arama (ölçüldü; DUR-SOR)

- Yol: `/StockMovements/lookups/products?q=…` → `ProductPickerSource.SearchProductsAsync` → geçit → PRODUCT-MASTER `GET /api/product-master/stockable-items?status=Active&q=…`.
  - Web'de yerel filtre yok; `q` olduğu gibi gidiyor.
  - Inventory servisi bu yolda değil.
  - Sözleşmede `q` için tanım: "kod ön eki ya da ad içinde geçen metin" (`product-master-bundle.openapi.yaml:436`).
- Canlıda bu ucu 4010 köprüsü (devpm) karşılıyor. Kaynağı bu depoda yok; 4010'a dokunulmadı.
- "Amoks" bulunup "GP-000123" bulunamıyorsa, köprü `q`'yu satırın adında arıyor ama ürünün GP kodunda aramıyor. Stockable-items satırlarının kodu SKU / LSKU kodu; ürünün GP kodu değil.
- DUR-SOR:
  - A: PRODUCT-MASTER (köprü, sonra MDM) `q`'yu satırın ürün kodunda da arasın; sözleşme bunu zaten söylüyor.
  - B: PM-LIVE köprüyü kaldırınca MDM'nin aramasıyla yeniden ölçülsün. Web değişmez.

### e) FU-0173-REQ-COUNTER — zorunlu sayaç

- Kök neden: ortak `required-fields-tracker.js` (değiştirilmedi) giriş dinleyicilerini yalnız açılışta görünen alanlara bağlıyor. Sonradan yalnız form içindeki bir `class` değişiminde yeniden sayıyor.
  - Gerekçe alanı açılışta gizli olduğu için yazmak sayımı tetiklemiyordu: 8 / 9'da kalıyordu.
  - Alan, kaynak bloğunun matris kuralıyla ayrıca her zaman "zorunlu" yapılıyordu.
- Düzeltme (bizim dosyalarımız):
  - bant alanı yalnız gerekçe isterken `required`;
  - alanın doluluğu bir sınıfla (`ex-band-reason-filled`) yansıtılıyor, böylece sayaç anında yeniden sayıyor;
  - matris bu alanı artık işaretlemiyor.
- Test: `movement-required-counter.test.mjs` (jsdom; gerçek ortak sayaç, gönderilen bant, gerçek `_FefoBand` kısmi görünümü).
  - Bant açıkken 8 / 9, gerekçe yazılınca **9 / 9**.
  - Bant kapalıyken (muaf tür) gerekçe sayılmıyor: 8 / 8.

## 3. Sabotajlar (bozuldu → kırmızı → geri alındı → SHA eşit)

| # | Sabotaj | Kırmızı | SHA |
|---|---|---|---|
| a1 | COUNT_LOSS Web muaf listesinden çıkarıldı | `expiry-band-exempt` 3 / 5 + bekçi | `e22791730aadc0a3` |
| a2 | Web listesine servis listesinde olmayan GOODS_ISSUE_PROD eklendi | bekçi + `expiry-band-exempt` 1 / 5 | `e22791730aadc0a3` |
| b | satır seçiminde ön doldurma geri getirildi | `quarantine-blocked` "leaves an empty quantity EMPTY" | `b70ef69fcacd28b7` |
| e | gerekçe doluluğunun sınıf yansıması kaldırıldı | `movement-required-counter` "9 / 9" | `e22791730aadc0a3` |

## 4. §8 doğrulama (dal ucu, filtresiz)

| Ölçüm | Önce | Sonra |
|---|---|---|
| Web | 3962 / 3964 | **3967 / 3969** — yalnız 2 SafetyStock kırmızısı |
| Inventory | 1335 + 4 | 1335 + 4 (servis değişmedi) |
| Mimari | 7 | aynı 7 |
| Gateway | 298 / 298 | 298 / 298 |
| Yeni-denetimsiz | 0 | 0 |
| Derleme | 0 hata | 0 hata, uyarı sayıları aynı (Web 26 / sln 8, Inventory 0) |
| `page-help-ledger.test.js` | 1 kırmızı (DEMANDPLANNING, Planning) | aynı |
| `diten-enter-primary.test.js` | yeşil | yeşil |
| DataTable doğrulayıcı | 19 | 19 |

## 5. Önizleme (kendi portları, kendi tohumu)

- Servis 5197, geçit 5198, Web host 5199; 5180–5199 önceden boş ölçüldü. Açılışta ölçülen: 27000 → yalnız 5197, 38592 → yalnız 5198, 24696 → yalnız 5199.
- **Bant:**
  - Önizleme servisinin gerçek FEFO sırası (`/Expiry/band/context`) iki uygun lot döndü; kip `RequireReason`.
  - Host'un sunduğu `expiry-bands.js` bu cevapla, kaynak lot sıradaki ikinci lot iken çalıştırıldı:
    - COUNT_LOSS, SCRAP, WRITE_OFF, WAREHOUSE_TRANSFER: bant yok, gerekçe yok, zorunlu değil;
    - GOODS_ISSUE_SALES: bant ve zorunlu gerekçe var.
  - Form sayfası (`/StockMovements/Create`) bant gerekçe grubunu gizli çiziyor.
- **Bloke:** miktar alanı değersiz çiziliyor; "Tümü" düğmesi (tr) pasif başlıyor, title "Satırın tüm miktarını yaz".
- **Yardım:** Bloke Stok adım 1'in etiketleri metin olarak çiziliyor (§2c).
- **Ölçülemeyen:**
  - Tarayıcı paneli `127.0.0.1:5199`'u açmadı; tekrar denenmedi. 375 px, ekran görüntüsü ve canlı sayaç tıklaması alınamadı; sayaç jsdom testinde ölçüldü.
  - Ürün kodu araması: önizleme geçidi PRODUCT-MASTER'ı taklit ediyor, köprüyü değil; ölçüm d'de koddan.
- Çıktılar oturum scratchpad'inde (`live4-preview/shots/`).

## 6. Süreçler

- Durdurulanlar: önizleme host 24696, geçit 38592, servis 27000 ve onun `mongod`'u 40668.
- Sonra: 5180–5199'da dinleyen yok; tek `mongod` sahibinin 5264'ü (dokunulmadı).

## 7. CT HOLD (2026-10-21): sayaç testi jsdom'suz

- **CT bulgusu:**
  - `node_modules` olmayan temiz bir worktree'de (`9b81fcad2`) Web 3966 / 3969 çıktı.
  - Üçüncü kırmızı `ExpiryBandExemptTests.The_band_rules_hold_against_the_shipped_scripts(file: "movement-required-counter.test.mjs")` → `Cannot find module 'jsdom'`.
  - CI'ın ilk aşaması da Web testlerini `npm ci` olmadan koşuyor.
- **Düzeltme (`a64846f65`, yalnız test):**
  - `movement-required-counter.test.mjs` artık `jsdom`'a bağlı değil.
  - Üretimdeki `required-fields-tracker.js` (ortak, değişmedi) ve `expiry-bands.js` dosya içindeki küçük bir DOM üzerinde olduğu gibi koşuyor. Bu DOM, iki betiğin kullandığı yüzeyi sağlıyor:
    - öğeler, öznitelikler, sınıf listeleri
    - kullanılan seçiciler: etiket, `#id`, `.sınıf`, `[öznitelik]`, `:not`, soy, virgül
    - olaylar
    - mikrogörev teslimli `MutationObserver` (tarayıcıdaki gibi)
  - Bant işaretlemesi hâlâ gerçek `_FefoBand` kısmi görünümü. Kopya ya da atlama (`skip`) yok.
- **Ölçtüğü aynı:**
  - bant açıkken 8 / 9 → gerekçe yazılınca 9 / 9;
  - bant kapalıyken (muaf tür) 8 / 8.
- **Sabotaj:** gerekçe doluluğunun sınıf yansıması kaldırıldı → "Zorunlu: 8 / 9" (canlı hatanın aynısı), kırmızı. Geri alınınca SHA `e22791730aadc0a3` eşit, porcelain temiz.
- **Temiz worktree doğrulaması:**
  - `git worktree add --detach ../erp-vnext-wt-live4-clean a64846f65` ile açıldı; `frontend/Diten.Web/node_modules` yok, `npm ci` çalıştırılmadı.
  - Web filtresiz: **3967 / 3969**. Kırmızılar yalnız `NavManifestL10nGuardTests` ×2 (SafetyStock, main'den); `Cannot find module` 0.
