# WP-MVP1-FU-CLEAN2 — kanıt (2026-10-19)

Dal `feature/sce/wp-mvp1-fu-clean2`, taban MVP-1 dalı `feature/sce/mod-0173-inventory-ledger-valuation` @ `60f5d2b2f` (SYNC3). Yerel commit'ler, push yok.
Worktree `C:\Users\Ali\erp-vnext-wt-clean2`; ana çalışma dizininde derleme yapılmadı, filo portlarına (5000–5070, 4010) dokunulmadı.

## 1. Taban (ölçüldü, adlarıyla)

| Ölçüm | Taban | Not |
|---|---|---|
| Web | 3780 / 3782 | 2 kırmızı main'den: `NavManifestL10nGuardTests.Every_manifest_module_has_a_Nav_Module_key_in_all_seven_languages`, `…Every_manifest_provider_in_the_repo_is_parsed_and_yields_a_module_code_and_pages` (MOD-0191 SafetyStock nav anahtarları — bizim değil) |
| Inventory | 1286 + 4, 16 kırmızı | 16'sı `PrismConsumedContractSmokeTests` — "Prism did not listen … within 90 s" (yük altında zaman aşımı). Sınıf tek başına yeniden koşuldu: **16 / 16 yeşil** → taban 1302 + 4 |
| Mimari | 139 / 146, 7 kırmızı | AuditTrail ×2, DocsPath ×5 (aşağıda adlar) |
| Gateway | 298 / 298 | |
| Inventory yeni-denetimsiz | 0 | |
| Web derleme / Inventory derleme | 26 / 0 uyarı, 0 hata | |
| DataTable doğrulayıcı (StockMovements, proxy, gaps) | 22 satır (3 bilgi + 19 açık) | |
| `page-help-ledger.test.js` (vitest) | 2 kırmızı / 8 | DEMANDPLANNING (Planning, bizim değil) + 15 Expiry / Recall kodu (bizim); `InventoryGovernance/Index.cshtml` donuk satırı (görünüm WP-0176-F5 ile kaldırılmıştı — bizim) |
| `diten-enter-primary.test.js` (vitest) | 1 kırmızı / 72 | R4: `Expiry/_DecisionDrawer.cshtml` (bizim, işaretsiz) |

Mimari kırmızılar (taban = son): `AuditTrailStandardTests.EveryServiceWithWriteCommands_HasALedger_AndCommandNamesAreUnambiguous`,
`AuditTrailStandardTests.EveryWriteCommand_IsAudited_OrADeclaredException_OrKnownDebt`, `DocsPathGuardTests.AuthorityFixture_ReadsExactFilesAndProvenance`,
`DocsPathGuardTests.AuthorityFixture_RejectsInvalidEvidence` (×3 senaryo), `DocsPathGuardTests.NoCodeFilePointsIntoDocsOutsideTheFiveFolders`.

## 2. Maddeler

### a) FU-0173-MOV-ACTORNAME — hareket kaydına kayıt anındaki ad

- `InventoryTransaction.CreatedByName` (eklemeli, boş olabilir). Göç yok: eski kayıtlarda alan yok.
- Port `Application.Common.IPostingActorName` (`Current`, `Use`); Api uygulaması `PostingActorName`: arka plan akışının verdiği saklı ad, yoksa token adı (`ICentralAuditScope.ActorDisplayName` — `name`, yoksa `given_name` + `family_name`; kuyruğun yazdığı değer). Auth çağrılmaz.
- `InternalMovementPostingService` adı yalnız aktör = çözülmüş kapsam kullanıcısı olduğunda yazar (başka bir aktör kimliği kapsam kullanıcısının adını ödünç almaz).
- Arka plan: `RecallQuarantineRequest.RequestedByName` (eklemeli) onayda / "Yeniden dene"de token adıyla yazılır; `RecallQuarantineSweeper` onu porta verir. Karantina giriş süpürücüsü (`QuarantineLedgerReads`) giriş hareketinin saklı adını verir.
- Okuma sırası tek yerde: `InventoryMapping.PostingName` = saklı ad → kuyruk adı (NAMES1) → yok; `QueueKeys` saklı adı olan satır için kuyruğa hiçbir şey sormaz. `TransactionItem.createdBy.name` ve karantina `MovementLink.byUserName` aynı kuralı kullanır. Sözleşme değişmedi (INVENTORY 1.5.0 `createdBy.name` zaten isteğe bağlı).
- Kapsam dışı kalan (ölçüldü): `QuarantineWorkflowOutcomeConsumer` (MOD-0023 iş akışı sonucu) yalnız `CompletedBy` kimliğini taşır, saklı ad yok → bu yoldan kaydedilen hareketlerde ad yine kuyruk → yok sırasıyla. Öneri O-CLEAN2-1.
- Testler: `RecallMovementActorNameTests` (3: arka planda kaydedilen recall karantina hareketi onaylayanın adı, adsız onaylayan → ad yok, iki kiracı / şirket kendi adı), `TransactionCreatedByTests` +3 (token adı saklanır, saklı ad kuyruk adını yener ve kuyruğa okuma yapılmaz, başka kiracı / şirket token'ı adı görmez).

### b) FU-0177-DRILL-STEP-SCHEMA — RECALL 1.9.0

Önce kodda ölçüldü: servis DTO'larındaki her `RecallUserRefDto` / `RecallUserAtDto` alanı sözleşmedeki `UserRef` / `UserAt` referanslarıyla karşılaştırıldı. Tanımsız olanlar yalnız tatbikat koşusu ve iade bağlantısı:

| Şema | Eklenen (isteğe bağlı) | Servis DTO'su |
|---|---|---|
| `DrillRun` | `startedBy` (UserRef) | `RecallDrillRunDto.StartedBy` |
| `DrillRun.steps[]` | `step` (DrillStep), `completedAtUtc`, `completedBy` (UserRef) | `RecallDrillStepDto` |
| `DrillRun` | `pendingResult` {`setBy` (UserRef), `setAtUtc`, `expiresAtUtc`} | `RecallDrillPendingResultDto` |
| `DrillRun` | `completed`, `cancelled` (UserAt) | `RecallDrillRunDto.Completed / Cancelled` |
| `ReturnLink` | `linkedBy` (UserAt) | `RecallReturnLinkDto.LinkedBy` |

`additionalProperties` hiçbir yerde değişmedi (dört nesne `true` kaldı), zorunlu liste değişmedi. 1.8.0 dosyası fixture `recall-v1.8.0.openapi.yaml` (SHA-256 `a6127312…5ba7b`, `60f5d2b2f` blob'u). `RecallContract190Tests`: fixture baytı + yalnız beyan edilen eklemeler (onlar çıkarılınca 1.8.0 ile veri olarak eşit). Contracts README'ye 1.9.0 satırı. `inventory-bundle` değişmedi.

### c) FU-WEB-CONTRACT-FAKES-RECALL

- `ContractRoutes`'a `("/api/recall", "recall.openapi.yaml")`; `Check` isteğe bağlı `method` alır (POST yanıtı yalnız POST işlemine göre ölçülür).
- `RecallContractFakes.Bound`: Recall sahtelerinin (`RecallDataGateTests.FakeGateway`, `RecallCaseDetailWebTests.Recorder`) her 2xx JSON yanıtı RECALL 1.9.0'a göre ölçülür; tek tolerans çıplak `{}` yer tutucusunun zorunlu üye eksikliği. Bilerek sözleşme dışı gövde yalnız adıyla (`RecallContractFakes.Off(…, ihlal)`).
- `Placeholder`: sözleşmenin biçiminde yer tutucu (`{}` nesne, `[]` dizi, indirme için boş dosya).
- Bulunan uyumsuzluklar (hepsi sahte gövdede düzeltildi, sözleşme değil):
  1. `GET /drill-plans`, `GET /cases/{id}/customers`, `GET /cases/{id}/stock`, `POST /cases/{id}/quarantine-requests` (202): dizi yanıta `{}` → `[]`
  2. `GET /evidence/exports/{id}/file`: dosya işlemine `application/json` `{}` → boş `application/octet-stream`
  3. `RecallDoorTests` birleşik liste: 6 kimlik uuid değildi (`c1`, `d1`, `i1`, `s1`, `u1`, `u2`) → uuid
  4. `RecallDoorTests.Open_case…`: 3 üyeli parça ve uuid olmayan `caseId` → tam `CaseDetail`
  5. `RecallR7bWebTests` tatbikat kapıları: 3 üyeli parça → tam `DrillRun`; liste kapısı aynı parçayı alıyordu → `DrillRunPage`
- Sınır (raporlandı): `ContractRoutes` 2xx durum kodunu ayırt etmez (200 / 201); CLEAN1 davranışı korundu.
- Testler: `RecallContractFakesTests` (9).

### d) D-KAR-1 — Karantina kişi gösterimi

- `QuarantineUi.person` / `personHtml` tek ortak kuralı kullanır: `StockMovementPeople` (`people.js`, NAMES1) — ad, yoksa "Bilinmeyen kullanıcı", kimlik yalnız `title`. Kopya yok; `_QuarantineL10n` her karantina sayfasında `people.js`'i `quarantine-ui.js`'ten önce yükler.
- `unknownPerson` (kısa kimlik · bilinmeyen kullanıcı) kaldırıldı; `details.js` 4 çağrı, `settings.js` onaylayıcı adayları `personHtml`'e geçti.
- `Quarantine.Common.UnknownUser` artık tek başına duruyor: en / tr / fr / es / ru değerleri büyük harfle başlıyor ("Bilinmeyen kullanıcı"), hareket izindeki değerle 7 dilde aynı (test).
- Testler: `quarantine-people.test.mjs` (Node, gönderilen dosyalar), `QuarantinePeopleTests` (değişen beklenti, §7).

### e) UXH-001 "?" paneli

Önce ölçüldü: 46 kodumuzun hepsi kiracı kabuğunda (`_LayoutTenantShell`), hiçbirinin `PageHelpResource` metni yoktu, hiçbiri defterde değildi (31'i testin WP-LANE-MAIN-SYNC-02 listesinde, Expiry 8 + Recall 7 hiçbir yerde → tabanda kırmızı). Metinler sayfa başına görünüm / JS'e karşı yazıldı (her adım gerçek bir düğme, alan, sütun ya da sekme), 7 dil, alıntılar o dilin resx değeri.

| Grup | Kodlar | Kabuk | Metin önce → sonra | Defter önce → sonra | Enter |
|---|---|---|---|---|---|
| Stok (MOD-0173) | STOCKLEDGER, STOCKLEDGERCREATE, STOCKMOVEMENTS, STOCKMOVEMENTCREATE, STOCKMOVEMENTEDIT, STOCKMOVEMENTDETAILS, LOCATIONTREE, LOCATIONS | Kiracı | yok → 8 × 5 adım | satır yok (MAIN_SYNC_2 listesinde) → listeden çıktı | `#offcanvasLocationForm`: işaretsiz (istisna listesinde) → `data-dt-primary="enter"`, istisna satırı çıktı |
| İzlenebilirlik (MOD-0174) | LOTS, LOTDETAILS, LOTCREATE, LOTEDIT, LOTMOVEMENTS, SERIALS, LOTTRACE, LOTMIGRATION | Kiracı | yok → 8 × 5 | MAIN_SYNC_2 → çıktı | yan panel yok (modal) |
| Karantina (MOD-0175) | QUARANTINEQUEUE, QUARANTINEDETAILS, QUARANTINEDECIDE, DECISIONSUBMIT (3), BLOCKEDSTOCK, BLOCKEDDISPOSITION (4), RETESTQUEUE (4), QUARANTINESETTINGS | Kiracı | yok → 8 sayfa | MAIN_SYNC_2 → çıktı | yan panel yok (modal) |
| Son kullanma / FEFO (MOD-0176) | EXPIRYDASHBOARD, EXPIRYAPPROACHING (4), EXPIREDSTOCK, FEFOORDER (4), EXPIRYEXCEPTIONS, EXPIRYEXCEPTIONDETAILS, EXPIRYPOLICY, EXPIRYEVIDENCE (4) | Kiracı | modül resx'inde (panelin okumadığı yer; 2 kod manifest dışı: EXPIRYEXPIRED / EXPIRYFEFOORDER) → panelde, manifest kodlarıyla | hiçbir yerde (kırmızı) → metinli | `#ex-drawer`: iki işaretsiz `.btn-primary` (R4 kırmızı) → ikisi `chord` (karar hemen kaydolur / imzalanır) |
| Geri çağırma (MOD-0177) | RECALLDASHBOARD, RECALLTRACE (4), RECALLCASES (4), RECALLCASEDETAILS, RECALLDRILLS, RECALLEVIDENCE (4), RECALLSETTINGS (3) | Kiracı | modül resx'inde (R6) → panelde (FU-0177-HELP-PANEL) | hiçbir yerde (kırmızı) → metinli | yan panel yok (modal) |
| e-İmza (MOD-0022) | ESIGNREQUESTS, ESIGNVERIFICATION (4), ESIGNAUDITEXPORT (4), ESIGNMYSIGNATURE, ESIGNPOLICY, ESIGNMEANINGS (4), ESIGNSAMPLEAPPROVALS | Kiracı (7 dil) | sayfa içi not `#esignPageHelp` → panelde; not ve 7 `ESign.Help.*` anahtarı kaldırıldı | MAIN_SYNC_2 → çıktı | yan panel yok |

(Adım sayısı belirtilmeyenler 5.) Ek olarak:
- Her görünüm kendi kodunu söyler (`ViewData["PageCode"]`, 39 görünüm; Recall'da zaten vardı): alt rota (Ayrıntı, Karar, Düzenle…) üst sayfanın yardımını göstermez.
- `PageHelpCatalog`: 46 kod için adım başına ikon (ikonların hepsi gönderilen `iconify-icons.css`'te var). `Related.*` yazılmadı (isteğe bağlı).
- Modülde kalan kopya yok: Recall 53, Expiry 48, e-İmza 7 yardım anahtarı modül listelerinden ve 7'şer resx'ten çıktı.
- Yerinde kalanlar, gerekçesiyle:
  - `Help.QUARANTINEMANUALHOLD.*` ("Stoğu karantinaya al" modal yardımı) — CLEAN1 g, modalın kendi yardımı
  - Karantina iş kuralı uyarıları (`Quarantine.Blocked.Info`, `Quarantine.Retest.Info`, `Quarantine.Submit.Intro`) — sayfanın kullanımını değil kaydın sonucunu söyleyen kural notları
  - liste sayfalarının başlık alt yazıları (`PageDescription`) — kabuğun başlık deseni, yardım değil
- Defter: kendi satırımız yoktu; bizim WP'nin kaldırdığı görünümün donuk satırı (`InventoryGovernance/Index.cshtml`) çıktı, başlık frozen 81 → 80. Testin MAIN_SYNC_2 listesi 32 → 1 (yalnız BOMS, Üretim).
- FROZEN `_Layout`: kodlarımızın hiçbiri donuk kabukta değil (ölçüldü); kabuklar değişmedi.
- Testler: `MvpOnePageHelpTests` (46 × 3 teori + 1: 7 dil yapı, ikon = adım, görünüm kodu, kabuk panelinin gerçek çizimi; modülde kopya yok), `ESignaturePageHelpWebTests` (yeniden yazıldı), `ExpiryWebTests` / `RecallWebTests` (§7).

### f) StockMovements DataTable uyumu

Doğrulayıcı (değiştirilmedi): `verify_datatable_page.py . --area Inventory --module StockMovements --api-profile proxy --format gaps`. **Önce 22 satır (19 açık) → sonra 19 satır (16 açık).**

| # | Açık | Sınıf | Eylem |
|---|---|---|---|
| 1 | Liste fabrikasında satır eylemi delegasyonu | ortak dosya (`diten-datatable.js`) | dokunulmadı, rapor |
| 2 | Sunucu dışa aktarma (BL-452) | servis uç noktası yok (INVENTORY sözleşmesinde export yok) | DUR-SOR, yeni uç yok; sayfa dışa aktarma menüsünü kaldırıyor (pack §2/§20) |
| 3, 7, 8, 14, 15 | `#offcanvasDetailsPreview`, `ViewDetails`, `QuickView`, `.js-quick-view` | uymuyor | defter satırı değişmez; hızlı görünüm / ayrıntı önizlemesi tasarımda yok (makbuz yalnız kayıttan sonra) |
| 4, 5, 6, 9, 10, 11, 12, 13 | L10n `Active`, `Passive`, `Edit`, `AreYouSure`, `Search`, `Export`, `Import`, `Status` | uymuyor | sayfada durum anahtarı, düzenleme, toplu onay, arama (BG-04), dışa / içe aktarma yok; kullanılmayan metin eklenmedi |
| 16 | `const endpoint = '/Inventory/StockMovements/api'` | uymuyor | sayfa rotası alan öneksiz `/StockMovements` (MOD-0173-FU01 §8); doğrulayıcının yolu alan önekli varsayar |
| (önce 16–17) | `getAuthHeaders()` tanımı / kullanımı | uygulanır → **kapandı** | fabrikanın tek başlık kuralı (`DitenDataTable.getAuthHeaders`) sayfanın kendi çağrılarında |
| (önce 19) | jQuery DataTable eklentisi | gerçek açık → **kapandı** | `onListError` v2 API'si (`list.dt`, yoksa `new DataTable.Api(table)`); davranış aynı |

Testler: `stockmovements-v2.test.mjs` (gönderilen `index.js`, fabrika sınırında sahte: ilk yükleme hatası dışa aktarma menüsünü v2 API ile kaldırır, jQuery çağrılmaz; sunucu sayfalama, sıralama / arama yok, duyarlı kontrol sütunu, "Kaydeden", satır eylemleri) + `StockMovementsV2Tests` (C# koşucusu).

## 3. Sabotajlar (bozuldu → kırmızı → geri alındı → SHA eşit, porcelain temiz)

| # | Sabotaj | Kırmızı | SHA önce = sonra |
|---|---|---|---|
| a | saklı ad okunmaz (`PostingName` saklı adı atlar) | 3: `The_stored_name_wins…`, `A_posting_answers_its_poster…`, `A_background_recall_quarantine_movement…` | `5aefc4bb8e2861bb` |
| b | 1.9.0'da `startedBy` zorunlu yapıldı | `RecallContract190Tests.The_1_9_0_change…` | `767ad9275af23ebe` |
| c | Recall sahtesine sözleşme dışı `extraField` | `RecallDoorTests.Open_case…` (`/extraField: false schema`) | `c447e078fca08035` |
| d | Karantina kimliği ad olarak gösterir | `quarantine-people.test.mjs` 2 / 3 | `daec04eda049052f` |
| e1 | STOCKMOVEMENTS `Purpose` tr'den silindi | `MvpOnePageHelpTests` (STOCKMOVEMENTS) + ledger guard 2 test | `5f57cf99dc07c8e6` |
| e2 | yazılan sayfanın defter satırı bırakıldı (STOCKMOVEMENTS + başlık 247) | ledger guard "a page whose text is written is out of the ledger" | `c3c3104baa5960c6` |
| f | v2 → jQuery eklentisine geri | `stockmovements-v2.test.mjs` 2 / 3 + doğrulayıcı açığı geri geldi | `47fcd54a4811d331` |

## 4. §8 doğrulama (dal ucu, filtresiz tam derlemeler)

| Ölçüm | Önce | Sonra |
|---|---|---|
| `Diten.Web.sln` / `Diten.InventoryService.sln` | — | 0 hata (Web 8, Inventory 0 uyarı; dokunulan dosyalarda yeni uyarı yok) |
| Web | 3780 / 3782 | **3929 / 3931** — yalnız 2 SafetyStock kırmızısı (adlar §1) |
| Inventory | 1302 + 4 | **1310 + 4**, hepsi yeşil |
| Mimari | 7 kırmızı | aynı 7 (adlar §1); CiServiceTests yeşil (Python 3.12) |
| Gateway | 298 / 298 | 298 / 298 |
| Inventory yeni-denetimsiz | 0 | 0 |
| ledger guard (vitest) | 2 kırmızı | 1 kırmızı: DEMANDPLANNING (Planning ekibi, DUR-SOR) |
| `diten-enter-primary.test.js` | 1 kırmızı | 72 / 72 |
| Node (dokunulan) | — | `quarantine-people` 3 / 3, `stockmovements-v2` 3 / 3, `inventory-names1` 2 / 2. `shipment-details-datetime` doğrudan koşuda saat dilimi ister (5 kırmızı); C# koşucusu (`ShipmentDetailsDateNodeTests`, TZ'yi kurar) yeşil — bizim değil |
| DataTable doğrulayıcı | 22 | 19 |

## 5. Önizleme (kendi portları, 127.0.0.1, ortam değişkeniyle zorlanmış)

- Servis 5191, geçit 5192, Web host 5193 (`ASPNETCORE_URLS` + `Kestrel__Endpoints__Http__Url`). 5180–5199 önceden boş ölçüldü. Her süreç açılır açılmaz ölçüldü: 7832 → yalnız 5191, 24848 → yalnız 5192, 3420 / 33208 / 28428 (Web host, kaynak değişikliği sonrası iki kez yeniden başlatıldı) → yalnız 5193. Beklenmeyen port yok.
- **Arka plan recall hareketi:** C4 vakası: onay sırasında 0175 çağrısı düşürüldü, KR-B süpürücüsü hareketi sonradan kaydetti; onaylayanın token adı "Selin Aydın". `/StockMovements/api`: `INV-2026-0000015 | STATUS_TRANSFER → QUARANTINE | createdByName "Selin Aydın"`. Aynı kişinin istek içinde adsız token'la kaydettiği iki hareket (`…0012`, `…0007`) ad taşımıyor; kuyruk adı olan `…0005` "Mehmet Kaya".
- **Karantina bilinmeyen kişi:** tutma `7c99694d…` `createdBy` ve geçmiş `by` adsız (hesap servisi bu kullanıcıyı tanımıyor). Sayfanın gönderdiği kural, sayfanın kendi tr sözlüğüyle: `<span title="2dc6bae6-8ebc-4073-ab6a-3f3caa9bb89a">Bilinmeyen kullanıcı</span>`; sayfa `people.js`'i `quarantine-ui.js`'ten önce yüklüyor.
- **"?" paneli (tr), host'un çizdiği sayfa:**
  - Hareketler: `STOCKMOVEMENTS`, 3 sekme, 5 adım — "Şirketin bütün stok hareketleri, en yenisi üstte. …"
  - Karantina kuyruğu: `QUARANTINEQUEUE`, 3 / 5 — "Kalite kararı bekleyen her karantina satırını izleyin …"
  - FEFO panosu: `EXPIRYDASHBOARD`, 3 / 5 — "Şirketinizin son kullanma riskini tek bakışta görün …"
  - Recall vaka: `RECALLCASEDETAILS`, 3 / 5 — "Bir geri çağırma vakasını taslaktan kapanışa yürütün …"
  - e-İmza İmzam: `ESIGNMYSIGNATURE`, 3 / 5 — "Elektronik imza atarken kullanabileceğiniz imza örneğinizi saklayın …"; sayfa içi not yok
  - Karantina ayrıntı: `QUARANTINEDETAILS` (alt rota kendi kodunu gösteriyor)
  - Hiçbirinde "hazırlanıyor" yok.
- **Ölçülemeyen (BLOCKED parçası):** uygulama içi tarayıcı paneli `http://127.0.0.1:5193`'e gitmeyi reddetti (site izni verilmedi). Bu yüzden StockMovements v2'nin tarayıcıda çalışması, 375 px ölçümleri ve ekran görüntüleri alınamadı. v2 davranışı Node testiyle gönderilen dosyada ölçüldü. Ekran görüntüsü yerine HTML / JSON çıktıları oturum scratchpad'inde (`clean2-preview/shots/`, repoya konmadı). İndirme yapılmadı.

## 6. Süreçler

- Önizleme Web host 28428, geçit 24848, servis 7832 ve onun `mongod`'u 7688 durduruldu.
- İlk iki başarısız servis açılışından kalan yetim `mongod`'larım 39076 ve 37516 durduruldu.
- Sonra: 5180–5199'da dinleyen yok; çalışan tek `mongod` sahibinin 5264'ü (dokunulmadı).
