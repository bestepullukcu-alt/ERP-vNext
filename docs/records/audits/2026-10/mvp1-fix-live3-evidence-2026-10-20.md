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

## 7. Ek (CT, 2026-10-20): e ve f

Taban notu: MVP-1 dalının belge commit'i `83c923825` (G1 notları) bu dala `--no-ff` ile alındı. Yalnız 7 paket dosyası değişti, çakışma çıkmadı.

### e) FU-INT1-02 — sahip kararı G1-D3 = A

- `CorrelationIdMiddleware` artık geçersiz değeri reddetmiyor. Geçersiz sayılanlar: UUID olmayan değer (`req-1`), boş UUID, birden çok değer.
- Bu durumda sunucu yeni bir UUID üretir. Kullanıldığı yerler:
  - istek bağlamı (`ICorrelationContext`)
  - yanıt başlığı
  - hareket, denetim ve outbox kayıtları
- Gelen metin yalnız tek bir uyarı satırına yazılır: `inventory.correlation.replaced {Header} {Supplied} -> {CorrelationId}`.
  - Metin en çok 64 karakter; kırpılınca kaç karakterin atıldığı `"...(+n)"` ile gösterilir.
  - Yazdırılabilir ASCII dışındaki karakterler, `"` ve `\` → `\uXXXX`.
  - Hiçbir kayıtta kimlik olarak kullanılmaz.
- Geçerli tek UUID olduğu gibi geçer ve log satırı yazılmaz.
- Testler:
  - `AuditCorrelationTests.A_non_uuid_correlation_id_is_replaced_by_a_fresh_uuid_used_everywhere` — eski "400" beklentisinin yerine geçti, gerekçe G1-D3. 201 dönüyor; yeni UUID başlıkta ve üç kayıtta; `req-1` hiçbir kayıtta yok.
  - `CorrelationReplacementTests`, 4 test: log satırı; geçerli UUID değişmez ve log yok; aşırı uzun değer kırpılır ve kaçışlanır; boş değerler.
- Sabotaj: istek yeniden 400'e döndürüldü → 3 test kırmızı. Geri alınınca SHA `c20011d27d2b0495` eşit.

### f) Çekirdek kural 3 — anahtarı gövdede kontrol eden üç uç

| Uç | Önce (gövdede) | Kabul edilen anahtarlar | Sonra |
|---|---|---|---|
| `POST /api/quarantine/decisions` (`QuarantineController.CreateDecision`) | `DecisionKeys.Any(Holds)` → 403 | `quarantine.decisions.create`, `quarantine.blocked.dispose`, `quarantine.blocked.unblock` | `[HasAnyPermission(…)]`; hangi anahtarın gerektiği kararın türüne bağlı (§4.6), bu kontrol gövdede kaldı → adıyla istisna (`BodyKeyedActions`) |
| `POST /api/quarantine/decisions/{id}/submit` (`SubmitDecision`) | aynı | aynı üçü | `[HasAnyPermission(…)]`; gövde kontrolü kalktı |
| `PATCH /api/trace/lots/{lotId}` (`TraceController.UpdateLot`) | `canDraft \|\| canReleased` → 403 | `trace.lots.update`, `trace.lots.update-released` | `[HasAnyPermission(…)]`; hangi anahtarın gerektiği lotun durumuna bağlı → adıyla istisna (`BodyKeyedActions`) |

- `HasAnyPermissionAttribute`, Inventory'nin kendi yetki öznitelikleri yanında eklemeli olarak yazıldı.
  - Politika: `AnyPermission:a|b`.
  - `AnyPermissionAuthorizationHandler`, `PermissionAuthorizationHandler` ile aynı tam claim eşleşmesini yapar; rol kısayolu yok.
  - 401 / 403 yanıtları aynı sözleşme gövdesiyle `ContractAuthorizationResultHandler`'dan çıkar.
  - Kayıt: `PermissionPolicyProvider` yeni öneki tanır; `DependencyInjection` handler'ı ekler.
- Yeni servis geneli bekçi `AuthorizationGuardTests` (önceden yalnız Karantina denetleyicisine özel bir bekçi vardı):
  - her HTTP eylemi anahtarını öznitelikte taşır (eylemde ya da denetleyicide);
  - taşımayan her eylem adıyla, gerekçesiyle istisnadır;
  - ölçülen istisnalar yalnız üç e-imza konu sağlayıcısı (`Quarantine/Expiry/RecallESignSubjectsController.GetSignableSubject`); gerekçe ESIGN-BUNDLE §6 — her ret tek 404 olmalı (sızıntı yok), öznitelik ise 403 döner.
- İzin kataloğu (A1): `QuarantineManifestTests` ve `LotSerialTrackingManifestCompletenessTests` artık `[HasAnyPermission]` anahtarlarını da okuyor. PATCH'in iki anahtarı önceden testte elle ekleniyordu; şimdi öznitelikten okunuyor.
- Değişen test beklentisi: `QuarantineManifestTests.…every_action_carries_a_key` içindeki `CreateDecision` / `SubmitDecision` istisnası kalktı; iki eylem de öznitelik taşıyor.
- Uç testleri (`AnyPermissionEndpointTests`, gerçek host):
  - kabul edilen 8 anahtar–uç çiftinin her biri yetki kapısını geçer; boş gövde olağan doğrulamaya takılır (400 / 404 / 428);
  - anahtarsız istek sözleşme 403'ünü alır ("The caller lacks the permission required for this operation.");
  - token'sız istek 401 alır.
- Sabotaj: `SubmitDecision`'dan öznitelik kaldırıldı → 4 kırmızı (iki bekçi, Karantina bekçisi, 403 testi). Geri alınınca SHA `1f07ba4f19f72ca0` eşit.

### §8 (e + f sonrası, dal ucu, filtresiz)

| Ölçüm | Sonuç |
|---|---|
| Web | 3962 / 3964 — yalnız 2 SafetyStock kırmızısı |
| Inventory | **1333 + 4**, hepsi yeşil |
| Mimari | aynı 7 |
| Gateway | 298 / 298 |
| Yeni-denetimsiz | 0 |
| Derleme | 0 hata, uyarı sayıları aynı |
| DataTable doğrulayıcı | 19 |
