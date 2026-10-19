# WP-SYNC-144 — PR #144 sonrası CRM / MDM uyum borçları (BL-640 / 641 / 642 + W3E sayfaları)

> **Kaynak:** PR #144 (Kapanış hattı) main'e girdi (`6b8a62d5a`). Test dalına senkron: `f8750edc7` (2026-10-16).
> Borç defteri: `docs/roadmap/backlog/product-backlog.md` BL-640 / BL-641 / BL-642. **Süre: 2026-11-13** (muafiyetler biter, CI kırmızıya döner).
> Yol haritası: [ROADMAP-visit-workspace](ROADMAP-visit-workspace.md) sıra 7b.

## Kapsam ve CT ölçümü (2026-10-16)
- **BL-642:** 5 kiracı sayfasının "?" yardım metni yok: COMPETITORPRODUCTS, CRMDOCTORINSIGHTTHRESHOLDS, OBJECTIONRESPONSES, VISITEVIDENCESETTINGS, VISITWORKSPACE.
  - `frontend/Diten.Web/tests/page-help-ledger.test.js` → `MAIN_SYNC_3_PAGES`.
- **BL-641:** 3 yan panelin birincil düğmesinde `data-dt-primary` yok.
  - `CRM/KnowledgePaths/_PresentationNotes.cshtml`, `CRM/PotentialThresholds/Index.cshtml`, `CRM/VisitEvidenceSettings/Index.cshtml`.
  - Muafiyet: `tests/diten-enter-primary.test.js` (satır ~454-456, ~498-500).
- **W3E (deftere hiç girmedi):** CRMVISITQRCODES ve CRMVISITEVIDENCEFLAGS `CrmManifestProvider`'da kayıtlı.
  - `PageHelpResource`'ta metinleri yok, `MAIN_SYNC_3_PAGES`'te de değiller → `page-help-ledger.test.js` CI'da büyük olasılıkla kırmızı.
  - Bu makinede vitest çalışmıyor (Node 16), yerelde ölçülemiyor.
- **W3E-E4R2 ile gelen yeni paneller / pencereler:**
  - Ziyaret kanıtı: yeni istisna çekmecesi, hızlı görünüm eylemleri, kaydet onayı.
  - Eşikler: düzenleme çekmecesi + kaydet onayı.
  - Kurum QR kodları: oluştur / iptal / yazdır pencereleri.
- **K-TABS `diten-tabs.js`:** sekme şeridinde ← / → / Home / End için kendi `keydown` dinleyicisi var. Kural §3: "sayfa kendi `keydown` dinleyicisini yazmaz; kısayol `DitenShortcuts` ile". Ortak bir bileşen (WAI-ARIA tablist), sayfa dinleyicisi değil. Ama `diten-shortcuts.test.js` (vitest) bunu yakalayabilir → ölçülmeli.

## §36.1 Agent Prompt — W-UXH (paste-ready · yalnız Web · BL-640 ile paralel)
```text
@[.antigravity/agents/frontend-ui-ux.md] @[.antigravity/agents/l10n-agent.md]
WP: WP-SYNC-144 · W-UXH — UXH-001 uyumu: 7 CRM kiracı sayfasına "?" yardım metni (7 dil) + CRM yan panelleri / pencereleri için Enter / chord işareti; BL-642 ve BL-641 muafiyet satırları çıkar
Repository: C:\tmp\sync144-uxh (worktree, test/crm-content-visit-e2e güncel başı — main 3912d568b senkronlu) · Branch: wp/sync144-uxh · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-SYNC-144-uxh-mdm.md (kapsam ve CT ölçümü). Kural: .antigravity/rules/page-help-and-keyboard.md (UXH-001 — tamamını oku: §1 Enter / chord, §2 yardım anahtarları, §3 kısayol katmanı, tablo "birincil işlem türleri"). Borç: docs/roadmap/backlog/product-backlog.md BL-641 / BL-642. Örnek: PageHelpResource'ta metni olan bir kiracı sayfası (ör. USERS / TASKS) + PageHelpCatalog adım ikonları.
MEVCUT: Resources/PageHelpResource.{7 dil}.resx · Services/Help/PageHelpCatalog.cs · tests/page-help-ledger.test.js (MAIN_SYNC_3_PAGES) · tests/diten-enter-primary.test.js (BL-641 adlı satırları) · tests/diten-shortcuts.test.js · tests/page-help-rule-contract.test.js · wwwroot/assets/js/diten-tabs.js (K-TABS, kendi keydown'u).
NE:
(1) Yardım metni (BL-642 + W3E): COMPETITORPRODUCTS, CRMDOCTORINSIGHTTHRESHOLDS, OBJECTIONRESPONSES, VISITEVIDENCESETTINGS, VISITWORKSPACE, CRMVISITQRCODES, CRMVISITEVIDENCEFLAGS — her biri için Help.{KOD}.Purpose, .Audience, (gerekiyorsa .Immediate), Step1..n (en çok 5) + Step{n}Title; 7 dil (en, tr, fr, es, zh, ar, ru — kiracı modülü); adım ikonları PageHelpCatalog'da. METİN SAYFANIN BUGÜNKÜ DAVRANIŞINI anlatır (WP-VW-W3 / W4 / W3E belgeleri ve §37 notları: ör. Ziyaret kanıtı = kiracı ayarı + iç sekmeler + süreli istisna Workflow onayı; Kanıt işaretleri = işaret ipucudur ihlal değildir, 92 gün, temsilci özeti; Kurum QR = kod oluştur / iptal / yazdır, şehir süzgeci; çalışma alanı = takvim + yürütme + QR / konum kanıtı); tırnaklı düğme / alan adları ekrandaki metinle birebir. Yeni metin yazarken mevcut çevirileri değiştirme. MAIN_SYNC_3_PAGES'ten 5 satır çıkar (liste boşalırsa nesneyle birlikte kaldır — testin "yalnız küçülür" kuralına uy); W3E iki sayfası deftere HİÇ girmez (metinle gelir).
(2) Enter / chord (BL-641 + yeni paneller): kural tablosuna göre her CRM yan panelinin / penceresinin birincil düğmesine data-dt-primary: kaydet / oluştur / onay penceresi açan → "enter"; sonuçlu (yayımla, onaya gönder, onayla, iade, iptal, erken bitir, QR iptal, "Kaydet ve uygula" — tüm temsilcilere hemen uygulanır) → "chord" (aria-keyshortcuts + ipucu kuralın §1'i). Kapsam: KnowledgePaths _PresentationNotes; PotentialThresholds düzenleme çekmecesi + kaydet onayı; VisitEvidenceSettings ayar formu + kaydet onayı + yeni istisna çekmecesi + istisna hızlı görünüm eylemleri; VisitQrCodes oluştur / iptal / yazdır (tekli + toplu). Her karar raporda tabloyla (panel · düğme · enter/chord · gerekçe). diten-enter-primary.test.js'ten BL-641'in 3 adlı satırı çıkar.
(3) diten-tabs.js ve kısayol kuralı: diten-shortcuts.test.js / page-help-rule-contract.test.js'in keydown kuralı bu dosyayı yakalıyor mu ÖLÇ. Yakalıyorsa: kuralın izin verdiği yol (DitenShortcuts.register ya da kuralın ortak bileşen istisnası) — kendi başına yeni istisna satırı EKLEME; çözüm kuralın içinde yoksa DUR, raporla (karar kullanıcı / Kapanış). Yakalamıyorsa raporda belirt.
KORU/YAPMA: sayfa davranışları, K-STD / K-TABS / K-THEME aynen; ortak dosyalar (_Layout, diten-shortcuts.js, diten-datatable.js, backbone-custom.css, _TabCard) DEĞİŞMEZ (XMC-001; gerekirse DUR, raporla); başka modülün yardım metni / satırı DEĞİŞMEZ; defterde YENİ muafiyet satırı EKLEME; Help.Panel.* ortak anahtarlarına dokunma; kalıcı veri yazma YOK; kullanıcının sekmesine harness yok.
DOĞRULA: vitest bu makinede çalışmaz (Node 16) → page-help-ledger, diten-enter-primary, diten-shortcuts, page-help-rule-contract testlerinin bu sayfalara düşen ölçümlerini Node 16'da çalışan bir xUnit + RunNode testiyle ya da testlerin kendi tarama kodunu çağıran küçük bir Node betiğiyle yeniden üret (üretim dosyalarını okuyarak) ve sonucu raporla; CI asıl hakemdir. resx: 7 dilde anahtar tamlığı (Purpose / Audience / Step{n} + Title, boşluksuz sıra), değer ≠ anahtar, en ≠ tr. Web.Tests (xUnit) tam takım (-o repo içi), taban karşılaştırması + 3 sabotaj (.bak + touch): (1) bir sayfanın tr Step2Title'ı silinsin → kırmızı; (2) VisitEvidenceSettings "Kaydet ve uygula" chord → enter → kırmızı; (3) MAIN_SYNC_3_PAGES'e satır geri eklensin (defter büyür) → kırmızı. Görsel kanıt (AYRI sekme; fleet açıksa): 2 sayfanın "?" paneli (tr + ar), bir chord düğmesinin ipucu.
Commit: "fix(web): WP-SYNC-144 W-UXH — page help for 7 CRM tenant pages (7 languages), Enter / chord marks on CRM side panels and dialogs; BL-642 and BL-641 ledger rows removed" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```


## §37 CT kabul — W-UXH E2 ACCEPTED (2026-10-19) — BL-641 + BL-642 kapandı
Test dalına alındı: `4b88fed8a` (ajan `9c57ec535`), çakışmasız. BL-641 / BL-642 `DURUM: KAPANDI` yapılıp `product-backlog-closed.md`'ye taşındı (defter K2 / K3).

**Ne geldi:**
- **Yardım metni:** 7 sayfa × 7 dil (dil başına 90 anahtar) + `PageHelpCatalog` adım ikonları:
  COMPETITORPRODUCTS, CRMDOCTORINSIGHTTHRESHOLDS, OBJECTIONRESPONSES, VISITEVIDENCESETTINGS, VISITWORKSPACE, CRMVISITQRCODES, CRMVISITEVIDENCEFLAGS.
  - Tırnaklı her ad o dilde sayfanın kendi resx'inden okunuyor ve dilin tırnak biçimiyle yazılıyor (en / tr / zh “…” · fr « … » · es / ar / ru «…»).
  - Ajanın denetimi Rusça Eşikler sınıf adlarındaki uyumsuzluğu yakaladı ve düzeltti.
- **Defter:** `MAIN_SYNC_3_PAGES` boşaldı; nesnesi, sabiti, süzgeçteki kullanımı ve kendi testi kaldırıldı.
- **Enter / chord:**
  - `_PresentationNotes` pnSave → enter · Eşikler pt-save → enter · pt-save-confirm → enter.
  - Ziyaret kanıtı ayar formu Kaydet → enter · "Kaydet ve uygula" → chord · yeni istisna "Onaya gönder" → chord.
  - İstisna hızlı görünümünde iade / iptal / erken bitir onayı → chord (betikte; ipucu `DitenEnterPrimary.decorate`).
  - QR yeniden üret / iptal → chord · yazdır → enter.
- `diten-enter-primary.test.js`'ten BL-641'in 3 satırı çıktı.

**CT kontrolü:**
- **Diff:** yalnız işaret öznitelikleri, resx ve katalog satırları değişti; sayfa davranışı aynı. Ortak dosyalara dokunulmadı.
- **Testler** (CT `-o`): Web tam takım 2683/3. Kırmızılar bilinen 3: 2 `ShipmentDetailsDateNode` + `WorkCenterMenu` `-o` artefaktı. Ajan 2683/3, taban 2669/3 (+14).
- **Node 16 bekçi taraması (ajan):** `page-help-ledger` 1 kırmızıya indi; kalan DEMANDPLANNING (MOD-0188, başka modül, tabanda da kırmızı). `page-help-rule-contract` 43/0, `diten-enter-primary` 12/0. CI asıl hakem.
- **CT sabotajları (ajanın dokunmadığı kurallar):**
  - (1) "Onaya gönder" chord → enter → U5 **kırmızı**.
  - (2) tr `SaveConfirm` ekran metni değişti ("Kaydet ve uygula X") → U2 **kırmızı** (yardım metnindeki tırnaklı ad ekranla eşleşmiyor).
  - İkisi de md5 ile geri alındı.

**Açık / takip:**
- **FU-UXH-MODAL (Kapanış / ortak modül):** Ortak Enter modülü (`DitenEnterPrimary`) yalnız offcanvas yan panelleri dinliyor; modal pencereler kural §3'te kapsam dışı. Bu yüzden şu işaretler bugün **etkisiz**: iki kaydet onayı modalı, QR yeniden üret / iptal modalı, ayar formu Kaydet (sayfa formu), yazdır (sayfa). İşaretler doğru niyeti taşıyor; ajan yanlış bilgi vermemek için `aria-keyshortcuts` eklememiş. Çalışması için ortak modüle modal desteği gerekir (XMC-001).
- **DEMANDPLANNING:** `page-help-ledger`'da CI'da kırmızı olabilir; MOD-0188 (#146) sahibine bildirilmeli.
- **`diten-tabs.js` ve kısayol kuralı:** Bekçiler `wwwroot` altında `keydown` taramıyor, takılmadı. Ama kural metni "sayfa kendi keydown'unu yazmaz" diyor; ortak WAI-ARIA bileşen istisnası kuralda açıkça yazılmalı → Kapanış'a not.
- **Ad farkı:** QR sayfasında "?" panel başlığı menü adını ("Ziyaret QR Kodları") gösteriyor, sayfa başlığı "Kurum QR kodları"; önceden de böyleydi. Menü adı düzeltmesi küçük ayrı iş (FU-QR-NAV-NAME).


## §36.1 Agent Prompt — BL-640 (paste-ready · yalnız MDM)
**CT ölçümü (2026-10-19):**
- `MongoWorkflowApprovalStore<T>` yalnız `CompetitorProposalDecision` için kullanılıyor (`CompetitorProductRepository` içinde).
- `ProcessedEvent`, `MdmEventInboxRepository`'nin iç sınıfı ve yalnız `InsertOneAsync` ile yazılıyor; bütün yazma sorunu yok, yalnız eşleme kaydı eksik.
- `CompetitorProductClassMaps` her yerde `SetIgnoreExtraElements(true)` kullanıyor.
- Muafiyetler: `MdmDocumentClassMapsTests.cs` → `MainSyncUncoveredTypes` (CompetitorProduct, CompetitorProposalDecision, ProcessedEvent) ve `MainSyncLenientWholeWrites` (CompetitorProduct, "T" = MongoWorkflowApprovalStore<T>).
- Yerel ve canlı veritabanlarında W4 rakip ürün kayıtları VAR → eski belgeler okunmaya devam etmeli.

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SYNC-144 · BL-640 — MDM rakip ürün kayıtları (CompetitorProduct, CompetitorProposalDecision, ProcessedEvent) MdmDocumentClassMaps kaydına alınır; bütün belge yazıları (ReplaceOneAsync) alan düzeyine iner ya da eşleme katı olur; MdmDocumentClassMapsTests'teki BL-640 muafiyet satırları çıkar
Repository: C:\tmp\sync144-bl640 (worktree, test/crm-content-visit-e2e güncel başı) · Branch: wp/sync144-bl640 · commit bu dala, push YOK

Paket belgesi: execution/domains/commercial-suite/work-packs/WP-SYNC-144-uxh-mdm.md (kapsam + bu bölüm). Borç: docs/roadmap/backlog/product-backlog.md BL-640 (süre 2026-11-13). Önce oku: src/Diten.MdmService.Persistence/Configurations/MdmDocumentClassMaps.cs (kayıt, Verify, "repository first, map second" BL-569 tuzağı, GUID gösterimi) + bu kayda alınmış bir türün emsali (ör. GlobalProduct / FinishedGood: eşleme + alan düzeyi yazı ya da katı eşleme — hattın seçtiği desen hangisiyse onu izle); tests/Diten.MdmService.Application.Tests/MdmDocumentClassMapsTests.cs (MainSyncUncoveredTypes, MainSyncLenientWholeWrites — "yalnız küçülür" kuralı) + MdmDocumentClassMapsIsolationTests.cs. Hafıza tuzakları: Mongo GUID alt türü (standard / C# legacy) — kayıtlı türler yanlış GUID gösterimiyle yazılırsa sorgular sessizce eşleşmez.
MEVCUT: Configurations/CompetitorProductClassMaps.cs (SetIgnoreExtraElements(true), kendi RegisterClassMap'leri) · Repositories/CompetitorProductRepository.cs:48 ReplaceOneAsync (CompetitorProduct bütün yazı) · Repositories/MongoWorkflowApprovalStore.cs:51 ReplaceOneAsync (yalnız CompetitorProposalDecision kullanıyor; tenant + CAS) · Repositories/MdmEventInboxRepository.cs ProcessedEvent (iç sınıf, yalnız InsertOne) · DependencyInjection.cs ~121.
NE:
(1) Üç tür MdmDocumentClassMaps'in Roots listesine (ProcessedEvent iç sınıfsa NestedDocument deseniyle — CanonicalCodeCounter emsali); CompetitorProductClassMaps'teki eşlemeler kayda taşınır, çift kayıt OLMAZ (kayıt tek kaynak; CompetitorProductClassMaps ya kaldırılır ya da kayda delege eder). Alt / gömülü tipler de kapsanır.
(2) Bütün belge yazıları: CompetitorProductRepository güncellemesi ve MongoWorkflowApprovalStore<T> kaydı → hattın kayıttaki emsaliyle aynı desen (alan düzeyi $set / sürüm koşullu güncelleme ya da katı eşleme + bilinen alan seti). CAS / sürüm kontrolü ve kiracı sınırı (TenantId filtresi) aynen korunur; çakışma davranışı (ör. 409 / VersionConflict) değişmez.
(3) MdmDocumentClassMapsTests: MainSyncUncoveredTypes ve MainSyncLenientWholeWrites'tan BL-640'ın 5 satırı çıkar (sözlükler boşalırsa testin "yalnız küçülür" mantığına uygun şekilde kaldır); yeni muafiyet satırı EKLEME.
(4) GERİYE UYUM (zorunlu): bugünkü eşlemeyle yazılmış belgeler (alan adları, GUID gösterimi, sürüm alanı, iç içe listeler) yeni eşlemeyle okunur ve güncellenince bilinmeyen / sonraki sürüm alanı KAYBOLMAZ. Veri göçü gerekiyorsa (GUID alt türü farkı vb.) DUR, raporla — kendiliğinden göç betiği yazma.
KORU/YAPMA: rakip ürün / öneri API sözleşmeleri, Workflow onay akışı (SoD, gönder / karar, olay yayını), CRM'in okuma yüzeyleri aynen; yalnız MDM Persistence + testler; başka MDM türlerinin eşlemesine / kaydına DOKUNMA (XMC-001 — gerekirse DUR, raporla); localhost:27017'ye BAĞLANMA (testler kendi geçici mongod'u ile); kalıcı veri yazma YOK.
DOĞRULA: MDM testleri — (a) Verify üç türü ve alt tiplerini kapsar; (b) eski biçimde (bugünkü CompetitorProductClassMaps ile) yazılmış bir CompetitorProduct / CompetitorProposalDecision / ProcessedEvent belgesi yeni eşlemeyle okunur (gerçek mongod; BsonDocument ile hazırlanmış eski şekil); (c) bilinmeyen bir alan taşıyan belge güncellenince o alan KORUNUR (BL-640'ın asıl riski); (d) CAS çakışması ve kiracı izolasyonu aynen (başka kiracının kaydı güncellenmez); (e) GUID alanları sorguda eşleşir. MdmDocumentClassMapsTests + IsolationTests yeşil, BL-640 satırları yok + 3 sabotaj (.bak + touch): (1) CompetitorProduct Roots'tan çıkarılsın → kırmızı; (2) CompetitorProductRepository yeniden ReplaceOneAsync'e dönsün → (c) kırmızı; (3) muafiyet satırı geri eklensin → "yalnız küçülür" kırmızı. MDM tam takım (-o repo içi) + mimari, taban karşılaştırması.
Commit: "fix(mdm): WP-SYNC-144 BL-640 — competitor product, proposal decision and processed event types on MdmDocumentClassMaps; field-level writes keep unknown fields; BL-640 exemption rows removed" + son satır Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>. Rapor TÜRKÇE (§22). K13.
```

## §37 CT kabul — BL-640 E2 ACCEPTED (2026-10-19) — SYNC-144 KAPANDI
Test dalına alındı: `93171f288` (ajan `16fef0d6a`), çakışmasız. BL-640 `DURUM: KAPANDI` yapılıp arşive taşındı.

**Ne geldi:**
- CompetitorProduct, CompetitorProposalDecision ve ProcessedEvent (`NestedDocument`) `MdmDocumentClassMaps` köklerinde; 7 alt tip kayıt gezintisiyle kapsanıyor.
- `CompetitorProductClassMaps.cs` ve DI çağrısı silindi; eşlemenin tek kaynağı kayıt.
- Yeni `FieldLevelUpdate.SetMappedFields`: eşlenmiş üyelerin `$set`'i, `_id` hariç. `CompetitorProductRepository` ve `MongoWorkflowApprovalStore<T>` artık `ReplaceOne` yerine bunu kullanıyor. Kiracı + kimlik + beklenen sürüm koşulu ve yarış kaybında davranış aynı.
- `MainSyncUncoveredTypes` ve `MainSyncLenientWholeWrites` boşaldı ve kaldırıldı.

**Ajanın ölçümü (önemli):** Eski `CompetitorProductClassMaps` iki kök türü HİÇ kaydetmemiş. Inherited `Id` üzerindeki `MapIdMember` `ArgumentOutOfRangeException` attı ve `TryRegister` bunu "yarış" sanıp yuttu. Türler bugüne kadar sürücünün katı AutoMap'iyle çalışmış: yeni alan taşıyan belge okunamazdı. Saklı şekil (`_id`, Guid alt tür 04, `IsSelectable` yazılmıyor) kaydın eşlemesiyle birebir aynı → **veri göçü gerekmedi**.

**CT kontrolü:**
- **Diff:** yalnız MDM Persistence + testler; API, Workflow ve diğer MDM türleri aynı.
- **Testler** (CT `-o`, kendi geçici mongod'u): ilgili sınıflar (class map, rakip ürün, BL-640, onay) 181/181, 0 atlanan.
  - Ajanın tam takımı: Application 2927/53 (taban 2902/51; fark zamanlama kaynaklı, tek başına 59/59), Api 1144/3, mimari 125/13; Api ve mimaride aynı adlarla tabanla eşit.
  - Ajan kendi testinin yorumundaki "localhost:27017" ifadesini BL595 bekçisinin yakaladığını gördü ve düzeltti.
- **CT sabotajı (ajanın dokunmadığı kural):** `MongoWorkflowApprovalStore` güncelleme filtresinden kiracı koşulu kaldırıldı (`Filter.Empty`) → `WorkflowApprovalInfrastructureTests…loads_with_the_given_tenant_only…` **kırmızı**; md5 ile geri alındı.

**Bilinen sınır (kabul, takip):** **FU-MDM-LIST-ELEMENT-FIELDS.** Alan düzeyi yazı üst düzeydeki bilinmeyen alanları koruyor, ama `$set` edilen listeler (fiyatlar, fiyat geçmişi, onay turları…) bütün yazılıyor. Sonraki bir sürümün liste elemanına eklediği alt alan, eski sürümün güncellemesinde kaybolur (Item'ın iki listesiyle aynı bilinen durum). Bekçinin "bütün dizi `$set`" taraması `BsonDocument` ile yapılan `$set`'i görmüyor. Ajan muafiyet eklemek yerine raporladı; tarama genişletilirse bu alt tipler adlı borç olur (Kapanış / MDM sahibi kararı).

**SYNC-144 sonucu:** (1) main senkronu ✅ · (2) W-UXH ✅ (BL-641 + BL-642) · (3) BL-640 ✅. Üç borç da 13 Kasım sınırından önce kapandı. BL-645 (referans kiracı) kullanıcı ve Kapanış kararı olarak açık.
