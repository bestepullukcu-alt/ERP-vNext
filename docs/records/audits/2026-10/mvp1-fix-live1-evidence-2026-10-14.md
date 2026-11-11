# WP-MVP1-FIX-LIVE1 — kanıt (2026-10-14, agent result, pending CT)

> Dal `fix/mvp1-live1`, taban `acff1f7be`, worktree `C:\Users\Ali\erp-vnext-wt-live1`. Yalnız yerel; push / PR yok.
> Betikler: `mvp1-fix-live1-scripts-2026-10-14.md`. Ekranlar: `mvp1-fix-live1-shots/`.

## 0. Özet

| bulgu | kök neden | düzeltme |
|---|---|---|
| 1. `/Expiry/lookups/warehouses` → `value: null`, FEFO Sırası hesaplanamıyor | `ExpiryController.Options()` `o["id"]` okuyordu | ortak `LocationOptions` (`locationId`) |
| 2. Karantina Ayarları numune depo / lokasyon değeri `"null"` | `QuarantineController.Lookups` `Options(body, "id", …)` | aynı yardımcı |
| (2b, ölçerken bulundu) Karantina lot seçicisi değeri `null` | `Options(body, "id", "lotNumber")`; TRACE alanı `lotId` | `"lotId"` |
| 3. Stok Defteri LOT sütunu GUID gösteriyor | satır yalnız `lotId` taşıyor; çözücü yok | TRACE `listLots`, sayfadaki SKU başına bir çağrı |
| 4. Rastgele stoğu karantinaya alan düğme yok | — | **yapılmadı**, FU-0175-UI-MANUALHOLD-01 (§7) |

Commit: `6c9ac921b` (kod + testler) ve belge commit'i.

**Hata protokolü ölçümü — servis mi, sözleşme mi?** Servis sözleşmeye uyuyor, hatalı taraf Web:
- InventoryService'in `LocationRefDto.LocationId` ve `LocationsModels.cs:150,166` (`LocationId = Id(l.Id)`) okumaları sözleşmedeki `locationId`'yi (`location.openapi.yaml:209`) yazıyor.
- Web'in typed yolu (`LocationRefDto.LocationId`, `LocationPickerSource` `Text(item, "locationId")`) zaten doğruydu. Yalnız iki el yapımı `JsonNode` okuyucusu `id` okuyordu.
- BLOCKED değil.

## 1. Kök neden taraması — `frontend/Diten.Web/**` içinde LOCATION yanıtını okuyan her yer

| yer (taban) | okuma | sözleşme | sonuç |
|---|---|---|---|
| `Controllers/ExpiryController.cs:238` + `Options()` 630–639 | `o["id"]` | `LocationRef.locationId` (`location.openapi.yaml:209`) | **HATALI** → `LocationOptions` (`ExpiryController.cs:241`) |
| `Controllers/QuarantineController.cs:267–270` (`warehouses`, `locations`) | `Options(body, "id", "code", "name")` | aynı | **HATALI** → `LocationOptions` (`QuarantineController.cs:273`) |
| `Models/Inventory/LocationPickerSource.cs` (`ListAsync`, `SearchAllAsync`) | `Text(item, "locationId")` | aynı | doğruydu. `ListAsync` artık `LocationOptions`'ı kullanıyor (`LocationPickerSource.cs:125`); tek okuyucu |
| `Models/Inventory/LocationListAdapter.cs:157` (`Row`) | typed `LocationRefDto.LocationId` | aynı | doğru, değişmedi |
| `Controllers/LocationsController.cs` (detail / path / history / presence) | typed DTO'lar | aynı | doğru, değişmedi |
| `LocationListAdapter.PathText`, path girdileri `s.Id` | `getLocationPath` girdisi `id` | `location.openapi.yaml:92` (örnek `{ type, id, code }`) | **sözleşmesi `id`**, değişmedi |
| `Models/Inventory/MasterLabelResolver.cs:90` | `getLocation` gövdesinden `code` / `name` | `LocationDetail` | doğru, değişmedi |
| `ExpiryController.cs:613,626` `row["warehouse"]["id"]` | FEFO `WarehouseRef.id` | `fefo.openapi.yaml:536–540` (`required: [id]`) | **sözleşmesi `id`**, değişmedi |
| JS (`Locations/*.js`, `LocationTree/index.js`) `row.id` / `node.data.id` | Web'in kendi satır modeli (`LocationRowViewModel.Id` = `locationId`) | — | doğru, değişmedi |
| `LocationsController.cs:402` `Text(unit, "id")` | Platform org-unit listesi | LOCATION değil | değişmedi |

LOCATION dışında, ölçerken aynı sınıftan bir hata daha bulundu:
- Karantina'nın `lots` seçicisi `id` okuyordu (`QuarantineController.cs:278`, taban); TRACE `LotRef.lotId` (`trace.openapi.yaml:291`). "Stoğu bloke et" lot seçicisinin değeri canlıda `null` olurdu.
- Aynı denetleyicide tek satır olarak düzeltildi (`QuarantineController.cs:282`).
- Pozisyon (`/api/v1/workflow/lookups/positions`, `id`) ve tedarikçi (`supplierId`) okumaları değişmedi.

## 2. Testi sözleşmeye bağlama (asıl ders)

- `frontend/Diten.Web.Tests/Contracts/WebContractSchemas.cs`: InventoryService `ContractSchemas` yönteminin Web kopyası. YAML → JSON Schema 2020-12; nesneler kapalı (`additionalProperties: false`); `nullable` ve format desteği var.
- Kapsadığı şemalar:
  - LOCATION `LocationList` / `LocationSearchPage`;
  - TRACE `LotList`;
  - INVENTORY `BalancePage`;
  - FEFO `FefoOrder` (Expiry'nin gateway'den okuduğu FEFO 1.2.0 şekli).
- `ContractGateway.cs`: sahte Gateway. Her 2xx gövdesi kayıt anında sözleşmeye karşı doğrulanır; uymayan gövde **kaydedilemez** (`ContractViolationException`).
- Web.Tests'e `YamlDotNet 16.3.0` + `JsonSchema.Net 7.3.4` eklendi; Inventory testleriyle aynı sürümler. csproj'a yalnız ekleme yapıldı.
- Doğrulayıcı daha ilk koşuda kendi test gövdemdeki bir hatayı da yakaladı: INVENTORY `BalancePage` şemasında `page` / `pageSize` yok.
- Kapsam dışı kalan: mevcut testlerin kendi `FakeGateway`'leri (7 dosya) değiştirilmedi ("yalnız yeni dosyalar" kuralı). Mevcut 1458 testin sahte gövdeleri bu yüzden henüz sözleşmeye bağlı değil → **O-LIVE1-1**.

## 3. Stok Defteri lot numarası

- TRACE'de "kimliklerle lot" okuması yok. `validateTrace` yalnız `exists` döndürüyor (`trace.openapi.yaml:505–515`).
- Bu yüzden yerleşik desen kullanıldı: `GET /api/trace/lots?skuId&skuLevel&page=1&pageSize=200`. Aynı desen `LotsController:313` ve `LotMigrationController:124`'te de var.
- `Models/Traceability/LotNumberResolver`: sayfada kayıtlı (GUID) lotu olan **her farklı SKU için tek çağrı**, paralel; lot başına çağrı yok. Test: 3 satır / 2 SKU → 2 TRACE çağrısı.
- Çağrı yalnız kullanıcının kendi `trace.lots.read` anahtarıyla yapılır. Anahtar yoksa TRACE'e hiç gidilmez.
- Okunamayan lotta kimlik kalır ve "lot numarası okunamadı" ipucu (7 dil, `StockLedgerIndex.LotUnresolvedHint`) gösterilir.
  - Okunamama nedenleri: SKU'nun ilk 200 lotunda yok, çağrı hatalı ya da anahtar yok.
  - ar / zh metinleri lot sözlüğüne uyar (`التشغيلة`, `批次`). `Fu5TermsAndHistoryTests` bunu ölçüyor; ilk ar metni bu testte kırmızıydı ve düzeltildi.
- Yazılmış (kayıtsız) lot metni bugünkü kuralla kalır ("kayıtsız" rozeti).

## 4. Testler

| | önce | sonra |
|---|---|---|
| Web (`Diten.Web.Tests`) | **1458 / 1458** | **1468 / 1468** (+10, `LiveLookupContractTests`) |
| build | 0 hata, 24 uyarı | 0 hata, 24 uyarı (yeni uyarı yok) |

Koşulmayanlar:
- `AccountKindAcceptanceHost.Tests` koşulmadı.
- Servis suite'leri koşulmadı; servis kodu değişmedi.

### 4.1 Önce-kırmızı (taban kodunda, yeni testler — `red-web.txt`)

| test | taban | sonra |
|---|---|---|
| (a) `A_the_FEFO_warehouse_picker_values_are_the_contract_locationId` | kırmızı: `value` null | yeşil |
| (a) `A_FEFO_order_with_the_picked_warehouse_reaches_the_service…` | kırmızı: `fefo-order?` isteğinde `warehouseId` yok | yeşil |
| (b) `B_the_quarantine_sample_warehouse_and_location_options…` ×2 | kırmızı: `value` null | yeşil |
| (b) `B_the_quarantine_lot_picker_values_are_the_contract_lotId` | kırmızı: `value` null | yeşil |
| (c) `C_the_ledger_shows_lot_numbers_with_one_TRACE_read_per_SKU…` | kırmızı: `lotNumber` yok | yeşil |
| (c) `C_without_trace_lots_read_the_ledger_asks_TRACE_nothing…` | kırmızı: `lotUnresolved` yok | yeşil |
| (c) `C_the_ledger_lot_cell_draws_the_number_and_the_unresolved_hint…` | kırmızı | yeşil |
| (d) `D_a_fake_location_row_with_id_instead_of_locationId_is_refused…` | yeşil (doğrulayıcı yeni) | yeşil |
| (d) `D_a_fake_lot_row_with_id_instead_of_lotId_is_refused…` | yeşil | yeşil |

### 4.2 Sabotajlar (`sabotage.js`; her biri `git checkout` ile geri alındı, porcelain boş)

| | sabotaj | kırmızıya dönen test | sonuç |
|---|---|---|---|
| s1 | Expiry `lookups/warehouses` yeniden `o["id"]` okuyor (tabandaki `Options()`) | (a) `A_the_FEFO_warehouse_picker…` | **kırmızı** 1/1, temiz |
| s2 | Karantina düzeltmesi geri (`Options(body, "id", "code", "name")`) | (b) ×2 | **kırmızı** 2/2, temiz |
| s3 | `ContractGateway` doğrulaması kapalı (`if (false && …)`) | (d) `D_a_fake_location_row…` | **kırmızı** 1/1, temiz |
| s4 (ek) | defter çözülen lot numaralarını yok sayıyor | (c) `C_the_ledger_shows_lot_numbers…` | **kırmızı** 1/1, temiz |

## 5. Canlı benzeri doğrulama (kendi yığın, yerleşik tarayıcı)

**Yığın.** FUB2 yığınından türetildi. Kendi süreçleri:
- mongod (rastgele port, asla 27017);
- Auth 15056 (`DevSeeds__MockUsers=true`, yalnız kendi yığınımda);
- Platform 15057;
- Gateway 15191: geçici ocelot kopyası; **5067 → kendi Inventory'm**, çünkü F5 / R2'den beri `/api/locations`, `/api/expiry`, `/api/trace` ve `/api/quarantine` 5067'de;
- Inventory 15176, Web 15001;
- `pm-stub.js` 14036 ve LE taklidi 14037, ikisi de `--max-http-header-size=65536` ile.

Kimlik ve veri:
- Token yalnız kendi AuthService girişimden alındı; JWT sırrı rastgele ve yalnız bellekte. Dev secret ile token imzalanmadı.
- Lokasyonlar canlıdaki zincirle aynı: **IST → D01 "İstanbul Merkez Depo" → D01-A → A-01-01-01** (+ NUMUNE-01).
- İki AVAILABLE lot: L-2027-0331 (100) ve L-2026-1231 (50).

| # | ekran | beklenen | sonuç |
|---|---|---|---|
| 1 | `/Expiry/FefoOrder` | depo seçeneği `fe16ae72-…` = "D01 · İstanbul Merkez Depo" (canlıda `null`); D01 + SKU → "Sırayı hesapla" | ✅ sıra: 1 L-2026-1231 (31.12.2026, 50, Yaklaşıyor), 2 L-2027-0331 (31.03.2027, 100, Normal) — ekran 01 |
| 2 | `/Quarantine/Settings` | numune depo / lokasyon seçenek değerleri dolu | ✅ `qr-s-sample-wh`: `fe16ae72-…`=D01 · İstanbul Merkez Depo; `qr-s-sample-loc`: `c5c3766b-…`=A-01-01-01, `57022069-…`=NUMUNE-01; kayıtlı eşleme "D01 · İstanbul Merkez Depo → NUMUNE-01" — ekran 02 |
| 3 | `/StockLedger` | LOT sütununda lot numarası | ✅ L-2027-0331 / L-2026-1231 — ekran 03 |

- Şifre loglarda 0 kez geçiyor; `pw.txt` kapanışta silindi.
- **İlk koşu** "Sequence contains no elements" ile durdu. Bu tabanda örnek kullanıcılar açık katılımlı (`DataSeeder.MockUsersOptInConfigurationKey`). Bayrak yalnız kendi AuthService ortamıma verildi ve yığın yeniden başlatıldı.
- Tarayıcı paneli arada çizmeyi durdurdu. Bu yüzden seçimler sayfa içi JS ile `change` olayı gönderilerek yapıldı (`select` + "Sırayı hesapla"). Değerler DOM'dan okundu.

## 6. Süreçler ve PID'ler

| | 4010 | 5000 | 5001 | 5056 | 5057 | 5058 | 5059 | 5060 | 5061 | 5065 | 5066 | 5067 | 5070 | 27017 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| önce | 3236 | 34960 | 44000 | 13872 | 34208 | 30672 | 23080 | 6400 | 30676 | — | 32912 | 39468 | — | 4904 |
| sonra | 3236 | 34960 | 44000 | 13872 | 34208 | 30672 | 23080 | 6400 | 30676 | — | 32912 | 39468 | — | 4904 |

- Filoya istek gönderilmedi.
- Kendi süreçlerim (ikinci koşu): auth 25724, platform 39152, gateway 7472, pm-stub 27320, le-stub 25916, inventory 14460, web 35300. Yığın yalnız kendi listesini kapattı; kendi portlarımın hiçbiri dinlemiyor.
- İlk koşunun 7 süreci de kendi `stop` dosyasıyla kapatıldı.

## 7. Açıklar (CT için)

- **O-LIVE1-1:** mevcut Web testlerinin 7 dosyadaki kendi `FakeGateway`'leri sözleşmeye bağlı değil. Örnek: `LocationProxyControllerTests` `Ref()` gövdeleri `locationId` taşıyor, ama doğrulanmıyorlar.
  - Öneri: `ContractGateway`'e taşınmaları için ayrı bir FU. Bu WP'de yalnız yeni dosya açıldı.
- **FU-0175-UI-MANUALHOLD-01 (bilgi, bu WP'de yapılmadı):** arayüzde rastgele bir stoğu karantinaya alan düğme yok. `quarantineStock` yalnız yeniden test kuyruğunda: `Views/Quarantine/Retest.cshtml:14`, `wwwroot/assets/js/Quarantine/retest.js:4`.
- **O-LIVE1-2:** stok defterinde bir SKU'nun 200'den çok lotu varsa sonrakiler "okunamadı" görünür. Bu durumda doğru çözüm TRACE'te kimliklerle toplu okuma; o bir sözleşme işi.
- **O-LIVE1-3:** Web.Tests'e iki test paketi eklendi (YamlDotNet, JsonSchema.Net). Inventory testleriyle aynı sürümler.

## 8. Diff kapsamı

`git diff --stat acff1f7be..HEAD`, belge commit'inden önce 22 dosya:
- `frontend/Diten.Web/**`:
  - Controllers: Expiry, Quarantine, StockLedger;
  - Models: `LocationOptions` (yeni), `LotNumberResolver` (yeni), `LocationPickerSource`, `InventoryListAdapter`, `StockLedgerViewModels`;
  - StockLedger JS, l10n bridge ve `_IndexL10n`;
  - 7 resx.
- `frontend/Diten.Web.Tests/**`: `Contracts/` altında 3 yeni dosya + csproj'a ekleme.

Belge commit'i bunlara ekler:
- bu dosya, betikler belgesi ve 3 ekran görüntüsü;
- §19 kısa notları: MOD-0173-FU02, MOD-0173-FU01, MOD-0176, MOD-0175.

Servis, sözleşme ve manifest değişmedi. R3 ve FIX-WEBGATES dosyalarına dokunulmadı.
