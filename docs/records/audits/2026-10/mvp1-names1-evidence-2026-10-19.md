# WP-MVP1-NAMES1 — kanıt (2026-10-19, ajan sonucu, CT bekliyor)

- **Dal:** `feature/sce/wp-mvp1-names1`, MVP-1 ucu `2452eb7cf` üstünde; worktree `C:\Users\Ali\erp-vnext-wt-names1`. Yalnız yerel commit; push / PR / canlıya alma yok (§8).
- **Sahip kararları:** 1-A RECALL **1.8.0** (`UserRef.name`, `UserAt.name`); 2-A INVENTORY **1.5.0** (`TransactionItem.createdBy { userId, name? }`); ad kuralı = FIX-LIVE2 "adlar A".
- **Durum:** DONE. DUR-SOR'a takılan yer yok; iki madde sözleşmenin dışında kaldı ve FU önerisi oldu (§7).

## 1. (a) Ölçüm

### RECALL — `UserRef` / `UserAt` taşıyan her yanıt yeri

Kullanımın kendisi `recall.openapi.yaml`'dan; sınıf koddan ölçüldü. Vaka `OwnerUserId` yalnız açılışta atanıyor (`CaseCommandHandlers.cs:83`, başka atama yok) → **açan**. `DrillCaseRow.owner` = `run.StartedBy` → **başlatan**.

| şema · alan | yanıt (işlem) | sınıf | ad kaynağı (aynı kişi + aynı an) |
|---|---|---|---|
| CaseRef.owner | listCases, getDashboard (`openCaseRows`) | eylem yapan (açan) | `Opened` olayı, `openedAtUtc` |
| CaseDetail.owner | getCase + her komut yanıtı | eylem yapan (açan) | `Opened` olayı |
| CaseDetail.scopeSubmittedBy / scopeApprovedBy / effectivenessCompletedBy | aynı | eylem yapan | olayın kendi anı |
| Closure.closedBy · Cancellation.cancelledBy | aynı | eylem yapan | `Closed` / `Cancelled` olayı |
| PendingRevision.revisedBy | aynı | eylem yapan | `ScopeRevised` olayı |
| PendingAction.setBy | aynı | eylem yapan (yanıtta zaman yok) | aynı vakanın `recall.case.action-prepared` denetim kaydı (en son) |
| TimelineEntry.actor | getCaseTimeline | eylem yapan | olayın kendisi |
| Notification.recordedBy · ConsumptionDeclaration.recordedBy · Segregation.confirmedBy | kayıt komutları, getCaseCustomers | eylem yapan | ilgili olay |
| ReturnLink.linkedBy *(additionalProperties)* | getCaseReturns, linkReturn | eylem yapan | `ReturnLinked` olayı |
| DrillCaseRow.owner | listCases (tatbikat satırı) | eylem yapan (başlatan) | `Started` tatbikat olayı, `startedAtUtc` |
| DrillRun.startedBy · steps[].completedBy · pendingResult.setBy · completedBy · cancelledBy *(additionalProperties)* | getDrillRun, listDrillRuns, koşu komutları | eylem yapan | tatbikat olayları |
| **DrillPlan.owner** | listDrillPlans, plan komutları | **atanan** | servis ad GÖNDERMEZ; Web güncel etiketi |
| EvidenceExport.exportedBy | listEvidenceExports, createEvidenceExport | eylem yapan | dışa aktarma denetim kaydı |
| RecallSettings.updatedBy · SettingsHistoryEntry.changedBy | getSettings, getSettingsHistory, updateSettings | eylem yapan | ayar değişiklik satırı |

İki sınıfa da uymayan yer yok.

### INVENTORY — `TransactionItem`

`getTransactions` tek işlem (sözleşme 1.4.0 içinde başka `TransactionItem` yanıtı yok). Kaynak: `InventoryTransaction.CreatedBy` + `CorrelationId`.

### Web — kişiyi gösteren yerler

| sayfa · yer | önce | sonra |
|---|---|---|
| Vaka Detayı başlığı (Açan) ve kapanış / iptal satırı | Auth güncel etiketi ya da "Bilinmeyen kullanıcı" (FU-CLEAN1 c) | servisin eylem anındaki adı ya da "Bilinmeyen kullanıcı"; kimlik yalnız `title`'da |
| Vakalar listesi · Sahip (O-CLEAN1-1) | `name \|\| userId` (kimlik ad gibi) | aynı kural (`fmt.person`), kimlik `title`'da |
| Tatbikat · plan sorumlusu (atanan) | `name \|\| userId` | Web'in güncel etiketi (PeopleAsync yalnız burada), yoksa "Bilinmeyen kullanıcı" |
| Tatbikat · adım tamamlayan | `name \|\| userId` | servisin adı ya da "Bilinmeyen kullanıcı" |
| Kanıt Paketi · dışa aktarma günlüğü (O-CLEAN1-1) | `name \|\| userId` | aynı kural |
| Ayarlar · güncelleyen / değişiklik geçmişi (O-CLEAN1-1) | `name \|\| userId` | aynı kural |
| Hareket geçmişi (S-2) · Kaydeden | yoktu (BG-05) | yeni sütun (kaynak: MOD-0173-FU01 §7 BG-05 "Recorded by" sütunu) |
| Hareket makbuzu (Details) · Kaydeden | yoktu | "Belge ve tarihler" bölümünde alan |
| Karantina tutma / bloke ayrıntısı · hareket izi | `byUserName \|\| ''` (alan hiç dolmuyordu) | servis dolduruyor; yoksa "Bilinmeyen kullanıcı" |

### SCE dışı tüketici

Depo genelinde `inventory/transactions` / `TransactionItem` araması: yalnız `Diten.InventoryService` ile `Diten.Web`'in `StockMovementsController`, `LotMovementsController` ve `LotTraceController`'ı (MOD-0174). SCE dışında (MVP-6 SupplyChain, Manufacturing, diğerleri) tüketici yok. MOD-0174 Web denetleyicileri yeni alanı okumuyor; Web takımı yeşil.

## 2. Commit'ler

| commit | adım |
|---|---|
| `45e1f524d` | MOD-0173 · INVENTORY 1.5.0 sözleşme + servis (createdBy, tek okuma, şirketli ortak okuyucu) |
| `4cf4f0f23` | MOD-0173 · dondurulmuş-dosya testinin sürüm beklentisi 1.5.0 |
| `1c14dae8c` | MOD-0177 · RECALL 1.8.0 sözleşme + servis (RecallPersonNamesBehavior, IRecallActorRecords) |
| `72a36e273` | MOD-0175 · hareket izinde `byUserName` (var olan isteğe bağlı alan) |
| `d448114f2` | MOD-0173 · Web: hareket geçmişi sütunu + makbuz alanı |
| `c7f1eae14` | MOD-0177 · Web: Recall sayfaları tek kişi kuralı, güncel etiket yalnız atanana |
| `f1f2d990a` | MOD-0175 · Web: hareket izinde "Bilinmeyen kullanıcı" |

## 3. Madde madde

- **Ortak okuyucu (`IAuditActorNames`):** şirketle sınırlı eklemeli aşırı yükleme (`CompanyId` = kuyruğun yazdığı istek şirketi); mevcut yöntem ve Expiry çağıranı değişmedi.
- **INVENTORY 1.5.0:** `createdBy.userId` = kaydeden; `name` = kuyruğun hareket correlation'ı + kaydeden adı, aynı kiracı ve şirket; kayıt yoksa `name` yok; kullanıcısız harekette `createdBy` yok. Sayfanın adları tek okumada. Testler: `TransactionCreatedByTests` (ad; eski hareket → yalnız `userId`; başka şirket / kiracı satırı → ad yok; sayfa başına 1 okuma, `Keys = 4`, `Company = LE`).
- **RECALL 1.8.0:** ad defteri `RecallActorNames`. Komut alanı ve kaydını tek zamanla yazdığı için kişi (kullanıcı, eylemin saniyesi) çiftiyle bulunur. Kayıtlar dört depoda (vaka olayları, tatbikat olayları, denetim, ayar geçmişi) birer `$or` sorgusuyla okunur; kuyruk adları tek okumayla gelir. **Tek yer:** `RecallPersonNamesBehavior` sorgu ve komut fark etmeksizin her recall yanıtını adlandırır; ad okuması başarısız olursa yanıt adsız döner, istek düşmez. Testler: `RecallNamesTests` (olayın kendi `ActorName`'i, kuyruk yolu, zaman tüneli, başka şirket / kiracı, liste başına tek okuma `Keys = 3`), `RecallDrillNamesTests` (başlatan adlı, atanan sorumlu adsız).
- **Karantina:** tutma ve bloke ayrıntısının hareket izinde `byUserName`; QUARANTINE sözleşmesi değişmedi (alan zaten tanımlıydı, hiç dolmuyordu). Test: `QuarantineMovementNameTests`.
- **Web:**
  - Recall'da kural tek yerde: `RecallCaseView.personName`, ortak `_RecallL10n` ile her sayfada; Vaka Detayı'ndaki ayrı etiket kaldırıldı.
  - `RecallController`: 8 okumadaki `PeopleAsync` (Auth güncel etiketi) kaldırıldı; yalnız `DrillPlans` (atanan) kaldı. Bu bir davranış değişikliği: eylem yapan kişide kayıt yoksa artık güncel ad değil "Bilinmeyen kullanıcı" görünür (sahip kararı A).
  - Inventory: kural `people.js` içinde (saf, Node ölçer). Makbuzda ad, kaydı yapan oturumun token adı; servisin denetim kuyruğu kuralının aynısı.
  - Yeni metinler 7 dilde: `ColRecordedBy`, `UnknownUser`, `Quarantine.Details.Moves.UnknownUser`.
- **Sözleşme ve sahteler:** Web'de sözleşme şeması **kopyası yok**; `WebContractSchemas` ve `ContractRoutes` canlı dosyayı okuyor, yani 1.5.0 kendiliğinden geçerli. Geçmiş sahtesi `createdBy` taşıyor ve bağlayıcıdan geçiyor; `createdBy.email` ve `userId`'siz `createdBy` kırmızı.

### Değişen test beklentileri (tek tek)

- `InventoryBundleDescriptionNoteTests`: 1.0.0 karşılaştırmasında sürüm 1.5.0, eklemeli önekler listesine `createdBy` alt ağacı; 1.4.0 testi canlı dosya yerine `inventory-bundle-v1.4.0` fixture'ı.
- `ContractSchemaValidatorTests.The_contract_file_is_the_frozen_v1_bundle`: `version: 1.5.0`.
- `RecallRulesAndShapeTests`: test adı `…_1_7_0_…` → `…_1_8_0_…`, sürüm 1.8.0, 1.8.0 changelog iddiaları eklendi.
- `RecallContract170Tests`: "sonra" dosyası `recall-v1.7.0` fixture'ı.
- `index.l10n.js` zorunlu anahtar listesine `UnknownUser` (eklemeli).

## 4. Sözleşme farkı

- `inventory-bundle.openapi.yaml` (+9 / −1): `info.version` 1.4.0 → 1.5.0; changelog sonuna 1.5.0 satırı; `TransactionItem.properties.createdBy` (object, `required: [userId]`, `userId` uuid, `name` string, açıklama). `TransactionItem` zorunlu listesi yok; değişmedi.
- `recall.openapi.yaml` (+4 / −1): `info.version` 1.7.0 → 1.8.0; changelog başına 1.8.0 satırı; `UserRef.properties.name`, `UserAt.properties.name` (string, açıklama). Zorunlu listeler aynı.
- README: INVENTORY satırına 1.5.0, yeni RECALL 1.8.0 satırı.
- Testle kanıt: `The_1_5_0_change_is_the_optional_createdBy_of_TransactionItem_only`, `The_1_8_0_change_is_the_optional_name_of_UserRef_and_UserAt_only`; önceki sürümler bayt bayt sabitlendi (SHA-256 testleri).

## 5. §7 sayıları (önce → sonra, filtresiz tam assembly)

| ölçüm | önce (`2452eb7cf`) | sonra (dal ucu) |
|---|---|---|
| Inventory | 1286 + 4 | **1302 + 4**, 0 kırmızı (+16) |
| Web | 2607 / 2607 | **2620 / 2620** (+13) |
| Mimari | 71 / 79, aynı 8 kırmızı | 71 / 79, **aynı 8, adlar birebir** |
| Gateway | 164 / 165 | 164 / 165 |
| Inventory yeni-denetimsiz | 0 | **0** (yeni komut yok, ölçüldü) |
| Derleme | Web.Tests 24 uyarı · Inventory.Tests 0 | aynı uyarı kümesi; `Diten.Web.sln` 0 hata (18 uyarı, hepsi dokunulmayan 4 dosyada), `Diten.InventoryService.sln` 0 / 0 |

Mimari 8: DocsPathGuardTests ×5 (`AuthorityFixture_RejectsInvalidEvidence` ×3, `NoCodeFilePointsIntoDocsOutsideTheFiveFolders`, `AuthorityFixture_ReadsExactFilesAndProvenance`), `MongoTestDatabaseGuardTests.PerRunDatabaseExceptionListStaysHonest`, `AuditTrailStandardTests` ×2 (`EveryWriteCommand_IsAudited_OrADeclaredException_OrKnownDebt`, `EveryServiceWithWriteCommands_HasALedger_AndCommandNamesAreUnambiguous`).

Testler 27017'ye bağlanmadı. `AccountKindAcceptanceHost.Tests` çalıştırılmadı. Dev-secret ile token imzalanmadı.

## 6. Sabotaj tablosu

Araç: dayanak tam bir kez → boz → (derle) → filtre kırmızı → özgün baytlar + touch → yeşil. JS sabotajları yalnız `node --test` ile, derlemesiz. Sonda SHA-256 eşit, `git status --porcelain` aynı.

| # | bozulan satır | kırmızı | geri |
|---|---|---|---|
| s1 | `CentralAuditActorNames` şirket filtresi kaldırıldı | 3 test: `TransactionCreatedByTests` (CompanyId), `RecallNamesTests` (CompanyId), `QuarantineMovementNameTests` | yeşil |
| s2 | aynı okuyucunun kiracı filtresi `f.Empty` | 2 test: `TransactionCreatedByTests` (TenantId), `RecallNamesTests` (TenantId) | yeşil |
| s3 | INVENTORY: sayfa adları satır başı okuma | `TransactionCreatedByTests.The_names_of_a_page_are_one_read_bound_to_the_company` | yeşil |
| s4 | RECALL: kişi başı okuma | `RecallNamesTests.The_case_list_names_its_rows_with_one_queue_read_bound_to_the_company` | yeşil |
| s5 | RECALL `UserRef.required: [userId, name]` | 5 test: `RecallContract180Tests` + sözleşme doğrulayan istemcide adsız yanıtlar | yeşil |
| s6 | INVENTORY `createdBy.required: [userId, name]` (Web) | `ContractRoutesTests.A_valid_body…` (adsız eski hareket) + `History_list_carries_the_poster_and_the_recorded_name` | yeşil |
| s6b | aynı (servis) | `InventoryBundleDescriptionNoteTests.The_1_5_0_change_is_the_optional_createdBy_of_TransactionItem_only` | yeşil |
| s7 | karantina izi adsız eşleniyor | `QuarantineMovementNameTests` | yeşil |
| w1 | `people.js`: ad yoksa kimlik | node 1 / 2 kırmızı | yeşil |
| w2 | Vakalar sahibi kimliğe düşer | node 2 / 3 kırmızı | yeşil |
| w3 | `personName` ad yoksa kimlik | node 1 / 3 kırmızı | yeşil |
| w4 | Web güncel etiketi yeniden bir eylem yapana (tatbikat koşusu) | `RecallNames1WebTests.The_live_label_fill_is_called_only_for_the_drill_plans` | yeşil |
| w5 | makbuz ad yoksa kimlik | `InventoryRecordedByTests.The_receipt_shows_the_name_else_unknown_user_with_the_id_only_as_title` | yeşil |
| w6 | sahteye `createdBy.email` | `InventoryProxyControllerTests.History_list_carries_the_poster_and_the_recorded_name` (bağlayıcı) | yeşil |

**14 / 14** kırmızı → yeşil.

## 7. Önizleme (kendi portları, 127.0.0.1, ortam değişkeniyle zorlanmış)

- Servis 5184 (`ASPNETCORE_URLS` + `Kestrel__Endpoints__Http__Url`), geçit 5185, Web host 5186. 5180–5199 ve 5067 önceden boş ölçüldü. Her süreç açılır açılmaz ölçüldü: 38500 → yalnız 5184, 37744 (node) → yalnız 5185, 19588 → yalnız 5186. Beklenmeyen port yok.
- Önizlemeye özgü:
  - bir hareket adlı token'la (Zeynep Arslan) tohumlandı
  - geçitte `display-label` taklidi, adı "(güncel etiket)" ekiyle döndürüyor; bir eylem yapanda bu ek görünürse hata yakalanmış olur
- Sonuçlar:
  - **Vaka Detayı:** "Açan · Ayşe Demir · 19.10.2026 16:56", "19.10.2026 tarihinde Mehmet Kaya kapattı"; sayfada "(güncel etiket)" yok, kimlik ad olarak yok.
  - **Hareket listesi (Kaydeden):** adlı tohum hareketi "Zeynep Arslan"; recall karantinasında Qa2'nin hareketi kuyruk adıyla "Mehmet Kaya"; ad kaydı olmayan eski hareketler "Bilinmeyen kullanıcı" (kimlik yalnız `title`'da). Dar panelde sütun duyarlı tablonun satır ayrıntısına iniyor ("Detaylar" modalı: `Kaydeden: Zeynep Arslan`).
  - **Vakalar:** RC-2026-0001 "Ayşe Demir"; ad kaydedilmeden açılan RC-0002 ve RC-0003 "Bilinmeyen kullanıcı".
  - **Tatbikat planı sorumlusu (atanan):** "Zeynep Arslan (güncel etiket)" — Web'in güncel etiket yolu.
  - **Karantina tutma ayrıntısı:** hareket izi "Mehmet Kaya".
- **375 px:** `/StockMovements`, `/Recall/Cases`, `/Recall/Cases/Details/…`, `/Recall/Drills`, `/Recall/Evidence`, `/Recall/Settings`, `/Quarantine`, `/Quarantine/Details/…` → 8 / 8 sayfada `scrollWidth = 375`.
- Ekran görüntüleri oturum scratchpad'inde (`names1-preview/shots/`, 7 dosya; repoya konmadı). İndirme yapılmadı.

## 8. Yeni FU önerileri

- **O-NAMES1-1:** Arka plan akışında, kuyruk satırı olmayan bir correlation ile kaydedilen hareketler (önizlemede recall karantina dağıtımında Qa2'nin iki hareketi) kullanıcı bilindiği hâlde "Bilinmeyen kullanıcı" gösteriyor. Öneri: hareket kaydına, kayıt anında token adını eklemeli bir `ActorName` alanı olarak yazmak (RecallCaseEvent kalıbı; sözleşme değişmez).
- **O-NAMES1-2:** `DrillRun.steps / pendingResult / completedBy / cancelledBy` ve `ReturnLink.linkedBy` sözleşmede tanımsız; `additionalProperties` altında yayınlanıyor. Bunlar artık ad da taşıyor. Öneri: ileride eklemeli bir sürümde bu alanları `UserRef` / `UserAt` olarak tanımlamak.
- **O-NAMES1-3:** RECALL yanıtları Web `ContractRoutes`'a bağlı değil (Recall Web sahteleri sözleşmeyle ölçülmüyor). Öneri: `/api/recall` tabanını bağlamak.
- **O-NAMES1-4:** Bu makinede Python yok. `verify_datatable_page.py` ve DataTable dokunma kancası (`list_screen_touch_hook.py`) çalışamıyor. Hareket geçmişine sütun eklemenin dokunma protokolü elle ölçüldü (görünüm başlığı, `columns` / `columnDefs` hedefleri, 375 px).
- **O-NAMES1-5:** Karantina kişi kuralı (FU-0175-Q5-02: kısa kimlik + "bilinmeyen kullanıcı", güncel etiket) bu WP'nin kuralından ayrı ("kimlik asla ad yerine", eylem anındaki ad). Hizalama bir ürün kararı.

## 9. Kalan süreç yok (ölçüldü)

- Önizleme servis / geçit / host durduruldu (38500, 37744, 19588); 5180–5199 ve 5067'de dinleyen yok.
- Kendi yetim `mongod`'um 13160 (önizleme servisi, geçici `--dbpath`) ile derleme süreçlerim (VBCSCompiler 37716, MSBuild düğümleri ×9) durduruldu.
- Kalan tek süreç sahibin `mongod` 5264'ü (değişmedi); `dotnet.exe` yok.
- `docs` junction'ı yalnız bağ olarak kaldırıldı (hedef yerinde); tarayıcı sekmesi kapatıldı.
