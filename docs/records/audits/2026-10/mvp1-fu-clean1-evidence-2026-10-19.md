# WP-MVP1-FU-CLEAN1 — kanıt (2026-10-19, ajan sonucu, CT bekliyor)

- **Dal:** `feature/sce/wp-mvp1-fu-clean1`, MVP-1 ucu `437e52c9e` üstünde; worktree `C:\Users\Ali\erp-vnext-wt-clean1`. Yalnız yerel commit; push / PR / canlıya alma yok (WP §9).
- **Kapsam:** MOD-0173 / 0174 / 0175 / 0176 / 0177 açık MVP-1 FU'ları (Web + Inventory). Her modülün değişikliği kendi "owner step MOD-01xx" commit'inde.
- **Durum:** DONE — Grup 1'de c kısmen (kalan kısım sözleşme ister → A/B), j atlandı (kural gereği); Grup 2'de m'nin Web kısmı yapıldı, FU-0173-UI-USER-01 ve n DUR-SOR.

## 1. Commit'ler (sırayla)

| commit | madde |
|---|---|
| `089467b8b` | MOD-0174 · b FU-QUI1-LOTSTATUS — lot durum / göç onay gövdesi `subtextHtml`; çift kaçış kaldırıldı |
| `fd746417e` | MOD-0173 · a FU-QUI1-ESC-TEST — hareket iptali / lokasyon onayında `subtext` ve `entityName` düz metin |
| `3edb89a65` | MOD-0175 · a + b bekçisi — `web-scripts-guard.test.mjs` (742 klasik + 5 modül script ayrışır; MVP-1 METİN seçenekleri kaçışsız, işaretlemesiz) |
| `0ae7361d1` | MOD-0177 · e FU-0177-SWEEP-01 + f SWEEP-02 — süpürücü yeniden deneme sınırı (varsayılan 10, `Inventory:Recall:QuarantineSweep`), sınırda Failed + gerekçe + olay / denetim; dağıtıcı uyarısında kiracı / şirket |
| `0d80d52b1` | MOD-0177 · l O-R5-5 — üretimden alınan (GOODS_RECEIPT_PROD) lot hammadde adayı olmaz; kaynakta tek lot türü |
| `b98152b9c` | MOD-0177 · k O-LIVE2-5 — REVIEW / RELEASE / AUTHOR anlamları kanıtta (servis resx) ve ekranda (Web resx) 7 dilde |
| `3bca36b36` | MOD-0177 · e düzeltmesi — `RECALL_QUARANTINE_RETRY_LIMIT` `RecallContract` sabiti (sözleşme dosyası değişmedi) |
| `c2ee96f62` | MOD-0173 · d FU-EVIDENCE-AUDIT-NAMES — ortak denetim-ad okuyucusu `Application/Common/IAuditActorNames` + `Persistence/CentralAuditActorNames` |
| `d0159c924` | MOD-0176 · d — FEFO kanıtı ortak okuyucuyu kullanır; Expiry kopyası silindi |
| `83d8c3cfe` | MOD-0177 · d — Recall kanıtı ortak okuyucuyu kullanır; Recall kopyası silindi; mimari kural (Expiry ↔ Recall bağı yok) |
| `b19ccb7ef` | MOD-0177 · c FU-0177-HEAD-NAMES (kısmen) — Vaka Detayı başlığında ad ya da 7 dilde "Bilinmeyen kullanıcı"; kimlik ad yerine gösterilmez (yalnız `title`) |
| `9be3f5767` | MOD-0175 · h FU-0175-UI-THEME-01 — pasif sekme `var(--bs-paper-bg)`; liste filtrelerinde `data-placeholder` |
| `2e11996bc` | MOD-0175 · g FU-0175-UI-MANUALHOLD-01 — "Stoğu karantinaya al" (quarantineStock, mode Manual, tek kova) |
| `8a92871b5` | MOD-0173 · i FU-WEB-CONTRACT-FAKES — `ContractRoutes` + Inventory sahte gövdeleri |
| `359532230` | MOD-0174 · i — Trace / LotMigration sahteleri bağlandı (uyumsuzluk yok) |
| `fff2bfe0f` | MOD-0175 · i — QuarantinePeople sahtesi bağlandı (tam HoldDetail, tam Settings) |
| `b19a322af` | MOD-0176 · i — ExpiryF5 sahtesi bağlandı (uyumsuzluk yok) |
| `1104427d9` | MOD-0177 · m FU-0177-PLAN-OWNER — tatbikat planında sorumlu seçici |
| `909918b79` | MOD-0175 · g önizleme bulgusu — kuyruk sayfasında antiforgery belirteci |

81 dosya, +1829 / −162 (kod + test; bu belge ve paket notları ayrı docs commit'inde).

## 2. Madde madde

- **a FU-QUI1-ESC-TEST.** `Inventory/StockMovements/index.js` iptal onayı `subtext: sentence`, `Inventory/Locations/form.js` `subtext` / `entityName` düz metin (dialog kaçışlar). Bekçi `web-scripts-guard.test.mjs` (4 test, `WebScriptsGuardNodeTests` köprüsü): her sayfa script'i ayrışır (paket yok: klasik `vm.Script`, modül `node --check`); MVP-1 modüllerinde `subtext` / `entityName` değeri `esc(...)` ya da işaretleme değil; bekçinin kendi yakalama örnekleri; lot durum / göç / bloke kaldır gövdeleri `subtextHtml`.
- **b FU-QUI1-LOTSTATUS.** `Traceability/Lots/status.js` `subtextHtml: body.outerHTML` (3 `ui.esc` kaldırıldı), `LotMigration/index.js` `subtextHtml: bodyHtml`. `global-confirm-text-is-text.test.js` pin listesine iki dosya eklendi (eklemeli).
- **c FU-0177-HEAD-NAMES (kısmen).** `case-view.js personName(u, t)`: ad → ad; ad yok → "Bilinmeyen kullanıcı" (`Recall.Case.UnknownUser`, 7 dil), kimlik `title`'da. Açan, kapatan, iptal eden aynı kuralla. Ad bugün Web'in Auth display-label okumasından gelir. **Kalan (BLOCKED → A/B):** kanıt paketindeki ad yolu (`RecallCaseEvent.ActorName` / denetim kuyruğu) ekrana RECALL sözleşmesinde ad alanı olmadan gelemez. A: `RecallUserRefDto` / `RecallUserAtDto`'ya eklemeli `name` (RECALL sözleşme sürümü — DUR-SOR). B: bugünkü hâl (Auth etiketi + yerelleştirilmiş geri düşüş).
- **d FU-EVIDENCE-AUDIT-NAMES.** Tek okuyucu `IAuditActorNames` (nötr katman), `InventoryCollections.CentralAuditOutbox` sabiti; Expiry ve Recall kopyaları silindi; `FeatureIsolationArchitectureTests` (2): Expiry ↔ Recall arasında yalnız kabul edilmiş iki bağ.
- **e FU-0177-SWEEP-01.** `RecallQuarantineRequest.DeferredAttempts`; cevapsız her tur sayılır; sınırda Failed + `RECALL_QUARANTINE_RETRY_LIMIT` + "No answer after {n} sweeps (limit {max}); last: {reason}" + olay / denetim; Failed bir daha alınmaz. Bozuk satır (catch) da sürümlü güncellemeyle sayılır. `RecallSweepLimitTests` (4).
- **f FU-0177-SWEEP-02.** Dağıtıcının uyarısı `recall.quarantine.dispatch_deferred {RequestId} tenant {TenantId} company {LegalEntityId}`; sınır uyarısı da kiracı / şirketli; kişi verisi yok.
- **g FU-0175-UI-MANUALHOLD-01.** Kuyruk araç çubuğunda "Stoğu karantinaya al" yalnız `quarantine.holds.create` ile (UAS-001: modal, script ve belirteç çizilmez). Lot → AVAILABLE satırları → miktar (≤ eldeki) → onay penceresi gövdesi `subtextHtml` (satır `textContent` ile), gerekçe ≥ 10 zorunlu; gövde `mode: Manual`, tek kova. UXH-001 "?" yardımı (`Help.QUARANTINEMANUALHOLD.*`, 7 dil). Testler: `QuarantineManualHoldTests` (4), `quarantine-manualhold.test.mjs` (4). **Önizleme bulgusu:** kuyruk sayfasında antiforgery formu yoktu → POST 400; düzeltildi (`909918b79`), test + sabotaj g2.
- **h FU-0175-UI-THEME-01.** Pasif sekme `shadow-sm` + `background-color: var(--bs-paper-bg)` (`bg-white` yok); filtrelerde `data-placeholder`. `QuarantineThemeTests`. Ölçüm (önizleme): açık tema pasif sekme `rgb(255,255,255)` = paper; koyu tema `rgb(43,44,64)` = `--bs-paper-bg #2b2c40` (beyaz değil).
- **i FU-WEB-CONTRACT-FAKES.** `Contracts/ContractRoutes.cs`: yedi eski sahtenin `On(...)` kaydı, yolun sahibi sözleşmenin (servers tabanı) bir 2xx cevabı olmalı — kapalı nesne, format, pattern, enum (WebContractSchemas kuralları); sözleşmesi olmayan servisler `Unbound`'da gerekçesiyle (Auth users, Platform org units, eski MDM yolları). `RequireOff`: bilerek sözleşme dışı gövde yalnız adı verilen ihlale izin verir. `ContractRoutesTests` (15). Ölçüm modu (log) kaldırıldı, bağlayıcı her zaman fırlatır. **Sözleşme değişmedi.** Uyumsuzluklar (hepsi sahte düzeltildi):
  1. `InventoryProxyControllerTests` stockable-items satır 3–4 `itemKind` yok (zorunlu) → `GlobalProduct` eklendi; satır 4 `skuLevel: Pallet` bilerek sözleşme dışı (açık-enum koruması) → `OnOff(…, "/items/3/skuLevel: enum")`.
  2. `InventoryProxyControllerTests` ürün okuması `itemId: "x"` (uuid değil) → okunan kimlik.
  3. `MovementFormTests` stockable-items satırlarında `itemKind` yok → eklendi.
  4. `MovementFormTests` `/api/product-master/skus` öneki getSku'yu UoM gövdesiyle cevaplıyordu (`conversions` SkuRef'te yok) → `skus/{id}/uom` ayrı rota + getSku için SkuRef gövdesi.
  5. `MovementFormTests` `lotControlled: "yes"` bilerek sözleşme dışı (tür koruması) → `OnOff(…, "/lotControlled: type")`.
  6. `LocationProxyControllerTests` tekil lokasyon cevapları LocationRef'ti; sözleşme LocationDetail (`version`, `createdAt`, `createdBy` zorunlu) → `Detail(...)` (POST 201, iki GET).
  7. `QuarantinePeopleTests` tutma ayrıntısı `holdNumber: QH-2026-0001` pattern'e uymuyor (`^KRN-\d{4}-\d{4,}$`), 18 zorunlu üye yok → tam HoldDetail `KRN-2026-0001`.
  8. `QuarantinePeopleTests` ayarlar `{}` → tam Settings (7 zorunlu üye).
  Trace, LotMigration, ExpiryF5: uyumsuzluk yok.
- **j FU-0177-PERM-ALIAS.** Atlandı: paylaşılan izin testine bu WP'de dokunulmadı (WP kuralı: "yalnız o test zaten değişiyorsa").
- **k O-LIVE2-5.** `Recall.Ev.W.meaning.{REVIEW,RELEASE,AUTHOR}` servis `RecallEvidenceText_{7}` ve Web `Recall.{7}` (+ `Recall.Keys`); bilinmeyen kod → MOD-0022 `meaningText`, o da yoksa kod (değişmedi). İki yönlü eşlik bekçisi `EvidenceTextParityTests` `Recall.Ev.W.*` önekini zaten kapsıyor (yeni anahtarlar kapsamda). Önizleme: REVIEW imzası "İnceleme". Not: Quarantine RELEASE anlamı da kullanılıyor (O-LIVE2-5 metnindeki "envanterin tek kodu APPROVE" ifadesi düzeltilmeli).
- **l O-R5-5.** `RecallDrillCandidates.RawMaterialLotsAsync` GOODS_RECEIPT_PROD ile de alınan lotu dışlar. `RecallDrillCandidateKindTests`.
- **m FU-0177-PLAN-OWNER.** Kapı `/Recall/lookups/users` (recall.drills.manage; kapı tablosuna satır eklendi) → AuthService'in var olan `GET /api/users/lookup` (auth.users.lookup, kiracı token'dan; yalnız etkin kullanıcı; etiket — e-posta / rol yok). Paylaşılan yardımcı `QuarantinePeople.SearchPath / Options` (yol yalnız metin + limit; uuid + etiket, tekil). Kural `drill-view.js ownerSearch / ownerOptions / ownerOf`; mevcut sorumlu ilk seçenek. 4 metin × 7 dil. Testler: `RecallPlanOwnerTests` (5 — sorguda başka kiracı / büyük limit sunucuya gitmez; token + X-Tenant-Id arayanın), `recall-clean1.test.mjs` (pass 3). Önizleme: "Ze" → Zeynep Arslan; kaydedilen plan `owner.userId` = seçilen kişi.

### Değişen test beklentileri (tek tek)

- `MovementFormTests.An_unknown_flag_on_the_sku_blocks_the_form`: teoriye ikinci parametre `offContract` (yalnız `"yes"` satırı sözleşme dışı kaydedilir); dört satırın beklentisi aynı.
- `MovementFormTests.The_stock_effect_is_before_and_after_in_decimal_on_the_server`: UoM rotası `skus/{id}/uom` + getSku gövdesi (eski kayıt getSku'yu UoM gövdesiyle cevaplıyordu); beklenen sonuç (24 / 30.5 / 6.5) aynı.
- `InventoryProxyControllerTests`, `LocationProxyControllerTests`, `QuarantinePeopleTests`: yalnız sahte gövdeleri (yukarıdaki 8 madde); hiçbir assert değişmedi.
- `RecallDataGateTests.Gates`: `UserLookup` satırı (yeni kapı — tablo eksiksiz olmalı).
- `RecallClean1NodeTests`: `# pass 2` → `# pass 3` (yeni Node testi).
- `ExpiryPersonNameReadTests`: `IExpiryAuditNames` → ortak `IAuditActorNames` (d; aynı davranış).
- `RecallEvidenceLive2Tests`: Web'de olması beklenen kod listesi 3 anlam koduyla uzadı (k).
- `global-confirm-text-is-text.test.js`: pin listesine 2 dosya (a / b).

## 3. §8 doğrulama — önce → sonra

| ölçüm | önce (`437e52c9e`) | sonra (dal ucu) |
|---|---|---|
| Inventory testleri | 1278 + 4 atlanan, 0 kırmızı | **1286 + 4 atlanan, 0 kırmızı** (+8). İlk uç koşusunda 1 kırmızı: `CentralAuditInventoryDeliveryTests.An_orphan_row_is_released_and_delivered_as_unconfirmed_never_as_succeeded` (yük altında; sınıf tek başına 3 / 3, tam takım yeniden 1286 / 1286) — dokunulmayan kod, O-CLEAN1-3 |
| Web testleri | 2575 / 2575 | **2607 / 2607** (+32) |
| Mimari | 71 / 79, aynı 8 kırmızı | 71 / 79, **aynı 8 kırmızı, adları birebir** |
| Gateway | 164 / 165 | 164 / 165 (aynı tek kırmızı) |
| Inventory "yeni denetimsiz" | 0 | **0** |
| Derleme (test projeleri, `--no-incremental`) | Web.Tests 24 uyarı 0 hata · Inventory.Tests 0 / 0 | aynı (uyarı kümesi dosya + kod olarak birebir) |
| Çözüm derlemesi | — | `Diten.Web.sln` 0 hata (18 uyarı, hepsi önceden var olan dosyalarda) · `Diten.InventoryService.sln` 0 / 0 |

Testler hiçbir koşuda 27017'ye bağlanmadı (`INVENTORY_TEST_MONGO` boş → kendi replica set'i). `AccountKindAcceptanceHost.Tests` çalıştırılmadı.

## 4. Sabotaj tablosu

Araç: dayanak tam bir kez → boz → derle → filtre (kırmızı) → özgün baytlar + touch → derle → filtre (yeşil). Sonda SHA-256 eşit, `git status --porcelain` aynı (iki koşuda da `true / true`).

| madde | bozulan üretim satırı | kırmızı olan test | geri |
|---|---|---|---|
| a | `StockMovements/index.js` `subtext: ui.esc(sentence)` | `WebScriptsGuardNodeTests.Every_page_script_parses_and_the_confirm_text_options_hold` | yeşil |
| b | `Lots/status.js` `subtextHtml` → `subtext: body.outerHTML` | aynı bekçi | yeşil |
| c | `case-view.js personName` ad yoksa kimliği döndürür | `RecallClean1NodeTests.The_case_page_person_rule_holds_against_the_shipped_files` | yeşil |
| d | `CentralAuditActorNames` kiracı filtresi `Guid.Empty` | `ExpiryPersonNameReadTests.An_audit_name_is_found_only_for_its_own_correlation_and_its_own_user`, `RecallEvidenceLive2Tests.A_person_is_the_signer_at_signing_or_the_audit_name_of_the_same_event_and_never_another_events_name` | yeşil |
| e | dağıtıcı sınırı `count < int.MaxValue` | `RecallSweepLimitTests` (1 / 4) | yeşil |
| f | dağıtıcı uyarısından kiracı çıkarıldı | `RecallSweepLimitTests` (1 / 4) | yeşil |
| g | `Index.cshtml` `@if (Perms.Has("quarantine.holds.create"))` → `@if (true)` | `QuarantineManualHoldTests` (1 / 3) | yeşil |
| g2 | `_ManualHoldModal.cshtml` antiforgery formu silindi | `QuarantineManualHoldTests.With_the_create_key_the_page_carries_the_antiforgery_token_the_write_sends` | yeşil |
| h | `_Header.cshtml` pasif sekme `bg-white shadow-sm`, stil yok | `QuarantineThemeTests` (1 / 3) | yeşil |
| i | `ContractRoutes` nesneleri kapatmıyor (`additionalProperties: false` yok) | `ContractRoutesTests` (1 / 15, `/extra`) | yeşil |
| k | zh Web resx'ten `Recall.Ev.W.meaning.REVIEW` silindi (tek dil) | `EvidenceTextParityTests` (1 / 4) | yeşil |
| l | `RawMaterialLotsAsync` üretilen lotu dışlamıyor | `RecallDrillCandidateKindTests.A_lot_reached_by_both_paths_is_one_finished_good_candidate_and_no_raw_material_candidate` | yeşil |
| m | `UserLookup` sorgu dizgesini (başka kiracı, limit 50) lookup'a ekliyor | `RecallPlanOwnerTests.The_search_goes_to_the_tenant_lookup_with_the_callers_token_and_tenant_only` | yeşil |

13 / 13 kırmızı → yeşil.

## 5. Önizleme (kendi portları, 127.0.0.1)

- Düzenek LIVE2'nin kopyası (scratchpad `clean1-preview`): gerçek InventoryService test host'u 5181 (kendi replica set'i, koşu başına rastgele test anahtarı — dev-secret yok), Node geçidi 5182, Web host 5183. 5180–5199 önceden boş ölçüldü. Önizlemeye özgü: bir imza REVIEW anlamıyla tohumlandı; `quarantine.holds.create` anahtarı; geçitte yalnız önizlemeye ait `/api/users/lookup` taklidi (iki kişi). Auth display-label çalışmıyor → başlık "Bilinmeyen kullanıcı" gösterir (beklenen).
- **Olay (açık yazıyorum):** servis ilk kez bin dizininden başlatılınca Inventory API'nin kopyalanan `appsettings.json`'ındaki `Kestrel:Endpoints:Http:Url = localhost:5067` uygulandı ve süreç ~10 dk **5067'yi** dinledi (kuyruk sayfası "servise ulaşılamıyor" dedi, ölçünce görüldü). Port önceden boştu, filonun hiçbir süreci yoktu / etkilenmedi; süreç hemen durduruldu, sonra `Kestrel__Endpoints__Http__Url=http://127.0.0.1:5181` ile başlatıldı ve dinlediği portlar ölçüldü (yalnız 5181). → O-CLEAN1-4.
- **g:** düğme yalnız anahtarla; modal + "?" yardımı; fazla miktar (61 > 60 EA) alan hatası, istek gitmez; onay gövdesi işaretleme (`subtextHtml`), satır metin; boş gerekçe "Gerekçe en az 10 karakter olmalı."; geçerli gerekçe → **KRN-2026-0004** açıldı, sayaç 3 → 4, toplam 17 → 19 EA. (İlk deneme 400 → antiforgery bulgusu → `909918b79`.)
- **c:** RC-2026-0001 başlığı "Bilinmeyen kullanıcı · 19.10.2026 14:40" (kimlik `title`'da), "… Bilinmeyen kullanıcı kapattı"; sayfa metninde kullanıcı kimliği ad olarak yok.
- **h:** yukarıdaki açık / koyu ölçüm.
- **k:** Kanıt Paketi önizlemesi §7 İmzalar: "Etkinlik kontrolünü tamamla · Ayşe Demir · **İnceleme**", diğer ikisi "Onay"; ham `REVIEW` metni yok.
- **m:** "Ze" araması → seçenekler "siz (planı kaydeden kullanıcı)", Zeynep Arslan, Zehra Yıldız; kaydedilen plan `owner.userId = 5a5a…0001`.
- **375 px** (`scrollWidth` / genişlik): `/Quarantine` 375 / 375 (modal 327, içinde taşan öğe 0), `/Quarantine/Blocked` 375 / 375, `/Recall/Cases/Details/…` 375 / 375, `/Recall/Drills` 375 / 375 (plan modalı 327, taşan 0), `/Recall/Evidence` 375 / 375.
- Ekran görüntüleri oturum scratchpad'inde (`clean1-preview/shots/`, 13 dosya; repoya konmadı). İndirme yapılmadı.

## 6. Atlanan / DUR-SOR

- **j FU-0177-PERM-ALIAS** — atlandı (paylaşılan teste dokunulmadı).
- **c kalan kısmı** — RECALL sözleşmesine ad alanı (A) ya da bugünkü hâl (B).
- **FU-0173-UI-USER-01** — DUR-SOR. Hareket satırları kullanıcı taşımıyor (INVENTORY `TransactionItem`'da kullanıcı yok, QUARANTINE `MovementLink`'te yalnız `byUserName`). Web-only yol yok (satırda kimlik yok — ölçüldü). A: INVENTORY-BUNDLE `TransactionItem`'a eklemeli `createdBy: UserRef { userId, name? }` (sözleşme sürümü); ad Web'de bu WP'nin `QuarantinePeople` yardımcısıyla. B: bugünkü hâl (kişi yalnız denetim izinde).
- **n FU-0177-375-BROWSER** — DUR-SOR. Depoda tarayıcı aracı yok (Playwright / Selenium / Puppeteer hiçbir csproj / package.json'da yok); yalnız `frontend/Diten.Web/package.json` devDependencies `jsdom` + `vitest` — jsdom yerleşim hesaplamaz (`scrollWidth` 0), 375 px taşma ölçemez. A: `Microsoft.Playwright` NuGet + ayrı test projesi, CI'da `playwright install chromium` (~150 MB ilk indirme), Web host'u Kestrel'de açıp sayfa başına `scrollWidth <= 375`; ~1 gün + CI ayarı. B: npm `@playwright/test` (aynı tarayıcı indirmesi). C: bugünkü hâl (yapısal bekçi + önizlemede elle ölçüm — bu WP'de 6 ölçüm yapıldı). Paket eklenmedi.

## 7. Yeni FU önerileri

- **O-CLEAN1-1** Recall'ın diğer kişi yerleri hâlâ kimliğe düşüyor: Vakalar listesi sorumlu, `drills.js person()` (plan / koşu sorumlusu — önizlemede kaydedilen planın sorumlusu kimlik olarak çizildi), Kanıt Paketi dışa aktarma günlüğü `exportedBy`, Ayarlar `updatedBy` / `changedBy`. Öneri: `case-view.personName` kuralı + sayfa verisinde `QuarantinePeople` ad doldurma (yalnız Web).
- **O-CLEAN1-2** `ESignature/esign-pages.js:78` `subtext` ile HTML veriyor (Platform modülü — dokunulmadı, bildirildi).
- **O-CLEAN1-3** `CentralAuditInventoryDeliveryTests.An_orphan_row_is_released_and_delivered_as_unconfirmed_never_as_succeeded` yük altında sallantılı (arka plan dağıtıcısı yetim satırı testten önce alabiliyor; `ReleaseOrphansAsync >= 1` beklentisi). Öneri: testte dağıtıcı durdurulmuş fabrika ya da sonucun satır durumundan okunması.
- **O-CLEAN1-4** Önizleme / yardımcı host şablonu: bin dizininden başlayan bir host, kopyalanan servis `appsettings.json`'ındaki Kestrel 5067'yi alır — filo kuralı tehlikesi. Öneri: şablona `Kestrel__Endpoints__Http__Url` zorunlu + başlangıçta dinlenen port ölçümü.
- **O-CLEAN1-5** Yazma yapan her sayfa script'inin sayfasında antiforgery belirteci olduğunu ölçen genel bir bekçi (bu WP'deki eksik ancak önizlemede bulundu).
- **O-CLEAN1-6** O-LIVE2-5 metni düzeltmesi: envanter modüllerinde APPROVE tek kod değil (Quarantine RELEASE kullanıyor).

## 8. Kalan süreç yok (ölçüldü)

Önizleme host'u, servis ve geçit durduruldu; 5180–5199 ve 5067'de dinleyen yok. Kendi test / önizleme `mongod`'larım (4 adet: 26008, 25148, 8520, 23808 — geçici `--dbpath`, bugün 14:37–14:40) ve derleme süreçlerim (MSBuild düğümü 3380, VBCSCompiler 30220) durduruldu. Kalan tek `mongod` sahibin 5264'ü (değişmedi); `dotnet.exe` süreci yok. Önizleme kökündeki `docs` junction'ı yalnız bağ olarak kaldırıldı (hedef yerinde). Tarayıcı sekmesi kapatıldı.
