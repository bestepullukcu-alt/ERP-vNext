# WP-MVP1-FIX-LIVE3 — kanıt (2026-10-20)

Dal `feature/sce/wp-mvp1-fix-live3`, taban MVP-1 dalı `feature/sce/mod-0173-inventory-ledger-valuation` @ `03c697e1a`. Yerel commit'ler, push yok.
Worktree `C:\Users\Ali\erp-vnext-wt-live3`; ana çalışma dizininde derleme yapılmadı, filo portlarına (5000–5070, 4010) dokunulmadı.

## 1. Taban (ölçüldü)

| Ölçüm | Taban |
|---|---|
| Web | 3929 / 3931 — 2 kırmızı main'den: `NavManifestL10nGuardTests.Every_manifest_module_has_a_Nav_Module_key_in_all_seven_languages`, `…Every_manifest_provider_in_the_repo_is_parsed_and_yields_a_module_code_and_pages` (SafetyStock) |
| Inventory | 1294 + 4, 16 kırmızı: `PrismConsumedContractSmokeTests` "Prism did not listen … within 90 s" (yeni worktree'de soğuk Prism). Sınıf tek başına: 16 / 16 yeşil → **1310 + 4** |
| Mimari | 7 kırmızı (AuditTrail ×2, DocsPath ×5 — adlar §4) |
| Gateway | 298 / 298 |
| Inventory yeni-denetimsiz | 0 |
| DataTable doğrulayıcı (StockMovements, proxy) | 19 satır |

## 2. Maddeler

### a) Hareketlerde lot numarası (bulgu 1)

- `StockMovementsController.List`, Stok Defteri'nin (FIX-LIVE1) kullandığı `LotNumberResolver` ile lot numaralarını çözer. Okuma sayfa başına, sayfadaki her SKU için tek `listLots` çağrısıdır; satır başına çağrı yoktur (N+1 yok). Çağrı yalnız okuyanın kendi `trace.lots.read` yetkisiyle yapılır.
- `StockMovementRowViewModel.LotNumber` / `LotUnresolved` alanları eklendi; `InventoryListAdapter.MapTransactions` bunları doldurur.
- `index.js` `lotCell` lot hücresini şöyle çizer:
  - kayıtlı lot: numarası; okuyucunun `trace.lots.read` yetkisi varsa Lot'a bağlantı (`/Lots/Details?lotId=`), kimlik yalnız `title`'da
  - kayıtlı ama numarası okunamayan lot: 7 dilde "Bilinmeyen lot" (`UnknownLot`), kimlik yalnız `title`'da
  - yazılmış lot metni: değişmedi (W-UI-02 rozeti)
- Duyarlı satır ayrıntısı aynı çizimi kullanır.
- Filtre (`lotId`), S-1 "Geçmiş" ve "Düzelt" (Edit sorgusu) kimlikle çalışmaya devam eder.
- Testler:
  - `InventoryProxyControllerTests.History_list_shows_the_lot_number_with_one_trace_read_per_sku`
  - `…History_list_without_trace_lots_read_asks_TRACE_nothing_and_names_no_lot`
  - `stockmovements-lot.test.mjs` (3) + `StockMovementsLotTests` (2: Node koşucusu; sayfa `unknownLot`'u çiziyor, 7 dil farklı ve dolu)
- Sözlük: ar "تشغيلة" (`Fu5TermsAndHistoryTests` "دفع"yı yasaklıyor; ilk taslaktaki "دفعة" bu testle yakalandı ve düzeltildi).

### b) Arama ipucu (bulgu 2)

Ölçüm (`dt-defaults.js` / `diten-datatable.js` değiştirilmedi):
- İngilizceye düşen yedekler: `Search` ('Search...'), `ColumnVisibility`, `Import`, `Action`, `Print`, `PDF`, `Copy`.
- `ShowAll` yedeği her dilde Türkçe ("Tümünü Göster").
- Fabrikanın boş kalan sözcükleri: `Filter`, `Apply`, `Reset`, `SaveView`, `RecordSaved`, `ErrorOccurred`.
- Kabuk köprüsü yalnız `Action`, `Print`, `PDF`, `Copy` taşıyor; Dt* metinleri `_DataTableL10n`'den geliyor.

| Sayfa | Eksik ortak anahtarlar (önce) | Yapılan |
|---|---|---|
| Quarantine Index / Blocked / Retest / Settings | Search, Filter, Apply, Reset, ShowAll, SaveView, ColumnVisibility, RecordSaved, ErrorOccurred | `_MvpOneListL10n` |
| Recall Cases | aynı | `_MvpOneListL10n` |
| Expiry Approaching / Exceptions / Expired | aynı | `_MvpOneListL10n` |
| Traceability Lots / LotMovements / LotMigration / Serials | aynı + Import (Lots) | `_MvpOneListL10n` |
| Inventory StockLedger / StockMovements | Search (iki listede arama kapalı), Import | `_MvpOneListL10n` (sayfanın kendi sözcükleri korunur) |
| Inventory Locations | Import | `_MvpOneListL10n` |
| e-İmza ×5 | yok (`esign-list-l10n` tamam) | değişmedi |

- `Views/Inventory/_MvpOneListL10n.cshtml`: `SharedResource`'tan 14 sözcük, 7 dil.
- `mvp1-list-l10n.js` yalnız sayfanın boş bıraktığı anahtarı doldurur; sayfanın kendi sözcüğü kazanır.
- Kısmi görünüm 15 görünümün `Scripts` bölümünde liste betiğinden önce.
- Koruma: `MvpOneListL10nTests` (20 sayfa çizilir, `Search` ve diğerleri dolu, liste betiğinden önce; `SharedResource` 7 dilde dolu) + `mvp1-list-l10n.test.mjs` (2).

### c) "ve" (bulgu 3)

- Kaynak: tema `core.css` `.nav-tabs .nav-link, .nav-pills .nav-link { text-transform: capitalize; }` (ortak, değişmedi). Temanın kendi `.text-none { text-transform: none !important; }` sınıfı var.
- MVP-1 görünümlerindeki **24 sekme / hap bağlantısına** `text-none` eklendi (13 görünüm: Inventory ×5, Traceability LotMigration, Quarantine `_Header`, Expiry `_Header` / Exceptions / Policy, Recall `_Header` / CaseDetails, e-İmza MySignature).
- Aynı sorundan etkilenen etiketler (iç sözcük küçük harfli), örnekler:
  - tr: "Raf Ömrü Kuralları ve Ayarlar", "Şirket ayarları", "Ürün bazında görünüm", "Değişiklik geçmişi"
  - en: "Shelf-life rules and settings", "Quarantine queue", "Evidence pack"
  - fr: "Paramètres de la société", "Règles de durée de vie et paramètres"
  - es / ru: aynı biçim
- Test: `MvpOneTabCaseTests` (tema kuralı ve `.text-none` ölçülür; 13 görünümün her `nav-link`'i `text-none`; çizilen Expiry sekmeleri).

### d) FEFO "bugün" (bulgu 4)

| Yer | Bugünkü hesap | Kural kaynağı | Yapılan |
|---|---|---|---|
| Expiry `ExpiryLotView.Today / Day / Days` (pano, yaklaşan, süresi dolmuş, FEFO sırası, kanıt dönemi) | `DateOnly.FromDateTime(now.UtcDateTime)` | MOD-0176 **ASSUMPTION-0176-05** "today = UTC date (same as LotExpirySweeper); posting rules use PostingDate" | kod aynı; panoda "Bugün (UTC)" |
| Lot son kullanma / sistem süresi dolma (`TraceMapping.Today`, `LotExpirySweeper`) | UTC | MOD-0174 "`ExpiryDate < today (UTC)`" | aynı |
| Karantina `QuarantineSupport.Today` (yeniden test, imha tarihi kuralları) | UTC (`DateTime.UtcNow`) | (aynı UTC çizgisi) | aynı |
| Karantina "bekleme süresi … gün" | süre (giriş → şimdi), takvim günü değil | — | aynı |
| Recall `notifiedOn` / `declaredOn` ≤ bugün | UTC | — | aynı; Recall panosu "Bugün (UTC)" |

- Saat dilimi kaynağı ölçüldü ve **bulunamadı**:
  - token'da `zoneinfo` / `tz` claim'i yok
  - Inventory'de saat dilimi okuyan bir port yok
  - Platform'da kiracı / şirket saat dilimi alanı yok; yalnız sistem saat dilimi listesi var
- Pakette UTC kuralı yazılı olduğu için karar kuralının ilk şıkkı uygulandı:
  - `Expiry.Dashboard.Subtitle`, `Expiry.Dashboard.SubtitleToday` ve `Recall.Dashboard.Subtitle` 7 dilde "(UTC)" taşır
  - iki pano tarihi zaten UTC biçimliyor (`timeZone: 'UTC'`)
- Testler:
  - `TodayIsUtcLabelTests` (Web, 4)
  - `ExpiryTodayIsUtcTests` (Inventory: 21:30 UTC = İstanbul 00:30 → gün UTC günü)

## 3. Sabotajlar (bozuldu → kırmızı → geri alındı → SHA eşit)

| # | Sabotaj | Kırmızı | SHA |
|---|---|---|---|
| a1 | denetleyici lot numaralarını eşlemeye geçirmez | `History_list_shows_the_lot_number…` | `324689bb0059e4f2` |
| a2 | çözülemeyen lot kimliği metin olarak gösterir | `stockmovements-lot.test.mjs` 1 / 3 | `e36475909cb16be0` |
| b | Recall Vakaları'ndan `_MvpOneListL10n` kaldırıldı | `MvpOneListL10nTests` (Recall/Cases) | `0f16a594e283020b` |
| c | Expiry `_Header`'dan `text-none` kaldırıldı | `MvpOneTabCaseTests` 2 / 3 | `a9436fbca44ef488` |
| d | fr'de "(UTC)" kaldırıldı | `TodayIsUtcLabelTests` (SubtitleToday) | `26718e240abfaff6` |

## 4. §8 doğrulama (dal ucu, filtresiz tam derlemeler)

| Ölçüm | Önce | Sonra |
|---|---|---|
| `Diten.Web.sln` / `Diten.InventoryService.sln` | 0 hata | 0 hata; uyarı sayıları aynı (Web test projesi 26, sln 8; Inventory 0) |
| Web | 3929 / 3931 | **3962 / 3964** — yalnız 2 SafetyStock kırmızısı |
| Inventory | 1310 + 4 | **1311 + 4**, hepsi yeşil |
| Mimari | 7 | aynı 7: `AuditTrailStandardTests.EveryServiceWithWriteCommands_HasALedger_AndCommandNamesAreUnambiguous`, `…EveryWriteCommand_IsAudited_OrADeclaredException_OrKnownDebt`, `DocsPathGuardTests.AuthorityFixture_ReadsExactFilesAndProvenance`, `…AuthorityFixture_RejectsInvalidEvidence` ×3, `…NoCodeFilePointsIntoDocsOutsideTheFiveFolders` |
| Gateway | 298 / 298 | 298 / 298 |
| Inventory yeni-denetimsiz | 0 | 0 |
| DataTable doğrulayıcı | 19 | 19 (yeni açık yok) |

## 5. Önizleme (kendi portları, 127.0.0.1, ortam değişkeniyle zorlanmış)

- Servis 5194, geçit 5195, Web host 5196; 5180–5199 önceden boş ölçüldü. Açılışta ölçülen: 9836 → yalnız 5194, 25448 → yalnız 5195, 24508 → yalnız 5196. Beklenmeyen port yok.
- **Lot numarası:**
  - `/StockMovements/api` 15 satır döndü: 14 kayıtlı lot numarasıyla (ör. `INV-2026-0000015 fde3be3a… → P2-D7B45695`), 1'i yazılmış lot metni.
  - Sayfa için geçide giden TRACE çağrısı **1**: `GET /api/trace/lots?skuId=…&skuLevel=Lsku&page=1&pageSize=200`.
- **Türkçe arama ipucu:** çizilen sayfanın `mvp1-list-l10n`'inde `Search` = "Ara.." (`SharedResource` tr değeri), ShowAll = "Tümünü Göster", ColumnVisibility = "Sütun Görünürlüğü". Ölçülen sayfalar: Hareketler, Karantina, Recall Vakaları, Expiry Yaklaşan / Süresi dolmuş / İstisnalar, Lotlar.
- **"ve":** Expiry Policy sekmesi `class="nav-link text-none …"`, metin "Raf Ömrü Kuralları ve Ayarlar".
- **"bugün":** ölçüm anı UTC 21:58, İstanbul 00:58 (20.10). Pano `asOf` = 2026-10-19, etiket "Bugün (UTC) {0} · …" — ekran artık UTC dediğini söylüyor.
- **Ölçülemeyen:** uygulama içi tarayıcı `http://127.0.0.1:5196`'ya gitmeyi reddetti; tekrar denenmedi. 375 px ve ekran görüntüsü alınamadı. HTML / JSON çıktıları oturum scratchpad'inde (`live3-preview/shots/`).

## 6. Süreçler

- Durdurulanlar:
  - önizleme Web host 24508, geçit 25448, servis 9836
  - servisin geride kalan `mongod`'u 37140
  - kendi derleme sunucularım (VBCSCompiler 29748, MSBuild düğümleri ×9)
- Sonra: 5180–5199'da dinleyen yok; tek `mongod` sahibinin 5264'ü (dokunulmadı); worktree / önizleme komut satırlı süreç 0.
