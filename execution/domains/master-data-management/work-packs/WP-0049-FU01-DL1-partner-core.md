# WP-0049-FU01-DL1 — İş Ortağı Ana Verisi DL-1: çekirdek (sözleşme taslağı, kayıt, BPT kodu, 4 komut, okuma API'si, Platform kataloğu)

Tarih: 2026-11-13 · Veren: CT (merkez, Beste adına) · Sahip kararı: "DL-1'i başlatalım" (Beste, 2026-11-13) · Paket:
`execution/domains/master-data-management/module-packs/MOD-0049-FU01-business-partner-master-foundation.md` r4, LF sha256
`a4968cb5f0f513803eeccd7ac29b05f1336e2e33657872461ca015d5cc29dcf8` · Onay: `execution/approvals/approvals.tsv:96` (Ali / GMG-ERP, 2026-11-13, HIGH) ·
İnceleme: `docs/records/reviews/MOD-0049-FU01/2026-11-13-a4968cb5.md` (r4 UYGUN; F-1…F-5 düşük, "en geç DL-1 başında" — bu WP'nin §2'si kapatır) ·
Kararlar: `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:14-21` (BPT, Q-2…Q-7), `:28-30` (C-5).

## 0. Başlamadan
- Dal: tek çalışma dalı `feature/sce/mod-0173-inventory-ledger-valuation`. CT etiketi `ct/line-ff-2026-11-13q`. Etiket dalının atası ise devam;
  değilse `git merge --ff-only ct/line-ff-2026-11-13q`; olmazsa `git merge --no-ff ct/line-ff-2026-11-13q`; çakışmada `git merge --abort` ve CT'ye yaz.
- Paketin LF sha256'sını ölç (`tr -d '\r' < <paket> | sha256sum`); `a4968cb5` ile başlamıyorsa dur, CT'ye yaz.
- **Eşzamanlılık:** Platform'da MOD-0040-FU02 DL-3 yeni bitti (hatta, `07ad98464`; TTY katalogda). MOD-0012-FU01 DL-1 (sır kasası) aynı anda
  Platform'a dokunuyor. Bu işin Platform kısmı (X-4 / X-5 / X-6) ile o işin ortak dosyaları: `RecordCodeTypeCatalogTests.cs`, Platform test projesi,
  `tests/architecture` (`AuditTrailStandardTests`, Platform defteri). Ana checkout'ta derleme / test yok (dotnet watch ana checkout'u kilitler);
  derleme, test ve sabotaj ayrık bir worktree'de. Her commit'ten hemen önce hattı al (yukarıdaki kural); çakışmada **iki tarafı da koru**, tam takımı
  yeniden koş, CT'ye yaz.
- Dilim başı ölçümleri (paket §5.1 "sayılar dilim başında yeniden ölçülür"): `Program.cs` manifest kayıtları, `RecordCodeTypeCatalog` Mdm türleri,
  `TrustedRecordCodeCredentialAuthenticator` Mdm listesi (bugün `["BNK", "BAC", "AGR"]`, `:21`), `RuleAheadOfCatalogue["BPT"]` (bugün `:34`),
  MdmService'te `IRecordCodeAllocator` (CT ölçümü 2026-11-13: **yok**; Platform'da yalnız `IInProcessRecordCodeAllocator`), manifest SortOrder.
- Paketi oku: §2, §3, §4 (4.1–4.7), §5, §5.1, §6, §7, §8 (8.1–8.7), §10, §12, §13, §14, §16 DL-1 (AC-1…AC-16, AC-26), §17, §17.1, §21, §22, §24.

## 1. Kapsam (paket §5 / §23 / §24 DL-1 satırı; dışına çıkma)
- **İlk commit:** `docs/analysis/contracts/drafts/business-partner-read-v1.0-proposal.md` (§8.4 iki operasyon, tüketici kuralları, F-1…F-7 fikstürleri; AC-1).
- MDM Domain: varlıklar (`BusinessPartner`, `BusinessPartnerContact`, `BusinessPartnerRoleAssignment`, `BusinessPartnerHistoryEntry`), enum'lar,
  `Rules/BusinessPartnerRoleRules.cs`, depo arayüzleri (geçmiş deposunda güncelleme / silme üyesi yok).
- MDM Application `Features/BusinessPartner/**` (§10 Golden Compact düzeni; 4 komut, 4 sorgu, doğrulayıcılar, `BusinessPartnerModels.cs`).
- MDM Persistence: iki depo; X-2 (DI, `MdmDocumentClassMaps`, `MongoIndexReadiness` — §4.7 index'leri; boş vergi no alanı yazılmaz).
- MDM Api: `BusinessPartnersController` (8 uç), `Export/BusinessPartnerExportColumns.cs`, `Resources/BusinessPartnerExport{,.tr,.fr,.es,.zh,.ar,.ru}.resx`
  (7 dil), `ModuleRegistration/BusinessPartnerMasterManifestProvider.cs`; X-1 (`Program.cs` yalnız eklemeli kayıt satırları).
- X-3: MDM `IRecordCodeAllocator` portu + HTTP bağdaştırıcısı — bugün yok, bu dilim açar (§2).
- Platform (X-4 / X-5 / X-6, **tek commit**): `RecordCodeTypeCatalog`'a `BPT` satırı (`Group`, Mdm, `mdm.business-partners.create`),
  `TrustedRecordCodeCredentialAuthenticator` Mdm listesine `"BPT"`, `RecordCodeTypeCatalogTests`'te `RuleAheadOfCatalogue["BPT"]` satırı kalkar ve
  AC09'a `BPT` → `[Group]` iddiası eklenir. TTY ve diğer satırlar aynen kalır.
- Testler: `Diten.MdmService.Application.Tests/BusinessPartners/**`, `.../ModuleRegistration/BusinessPartnerMasterManifestProviderTests.cs`,
  `Diten.MdmService.Api.Tests/BusinessPartnerHttpTests.cs` (+ F-1…F-5 vakaları, §2).
- **Bu dilimde yok:** Web ekranı ve Web resx (DL-2; X-9), `BusinessPartnerExportWordsGuardTests` (DL-2, AC-27), Gateway rota testi (DL-3),
  Auth rol şablonu (X-10, ayrı WP), sözleşme dondurma ve RECORD-CODE-ALLOCATE 1.1.0 (CT).
- **X-8 (Gateway rotası):** pakete göre rota DL-1'in parçasıdır (canlı kanıt AC-15 ve MOD-0179 DL-4 için), testi DL-3'tür. `ocelot.json`'u
  **geliştirici yazmaz**; CT integration-agent'a ayrı talimat verir (§15 iki blok, `DELETE` yok). Rota hatta girene kadar AC-1…AC-14, AC-16, AC-26
  ilerler; AC-15 rotayı bekler.

## 2. Kararlar (bağlayıcı)
- Sahip: Q-1 = `BPT` (OTOMATİK, grup önekli), Q-2 = A (yol b `mdm-merkezi-iletim` + aynı işlemde yerel geçmiş; defter değişmez, X-12), Q-3 (90 gün
  yok, kişi silinince kalıcı, zamanlanmış temizlik yok), Q-4 (vergi no kiracıda tekil, listede maskeli, ayrıntıda `update` sahibine açık), Q-5
  (Compact sapmaları paketteki gibi, son rol kaldırılamaz), Q-6 (pasif kayıt salt okunur), Q-7 (domain-config yazıldı, `4adadf51a`), C-5 (rol uçları
  ve `assign-role` izni v1'de yok; rol oluşturmada).
- **X-3 / C-4 (CT):** MdmService'te kayıt kodu portu yok ve MOD-0289 / MOD-0217 DL-1'e girmedi → port **bu dilimde, MOD-0289'un imzasıyla** açılır:
  `AllocateAsync(prefixScope, legalEntityId, recordType, idempotencyKey)`. İkinci port, yerel sayaç, yedek kod ve ürün ailesi `CodeReservation`
  kullanımı yok. Bağdaştırıcı Platform `POST /api/internal/record-codes/allocate`'i çağırır; hata eşlemesi §8.3 (403 → 403, diğer her şey → 503
  `MDM_BP_RECORD_CODE_UNAVAILABLE`). Port dosyası yeni dosyadır; MOD-0289 / MOD-0217 sonra yalnız tüketir.
- **X-4 / X-5 / X-6 (Platform):** Platform sahibi merkez (Beste) — sahip kararı ile bu WP'nin parçası. Üçü aynı commit'te; `RecordCodeTypeCatalogTests`
  (AC16, AC09, AC14 tüketici eşlemesi) yeşil. `RuleAheadOfCatalogue["BPT"]` bu commit'le kalkar.
- **X-7 (CT adımı):** `docs/analysis/contracts/record-code-allocate.md` eklemeli **1.1.0** (`BPT`: yalnız Group, çağıran Mdm, izin
  `mdm.business-partners.create`) CT yazar; geliştirici sözleşme dosyasına dokunmaz. Kabul kapısıdır (§4).

**İnceleme r4 bulguları — CT kararları (paket metni bir sonraki içerik revizyonunda hizalanır; r4 onayı geçersiz olmasın diye bu WP taşır):**
- **F-1 (doğrulayıcı hata kodları + `EXPORT_TOO_LARGE`):** Ölçüm: MDM `Response<T>` (`Application/Common/Models/Response.cs`) `errorCodes` alanı
  taşımaz; `ValidationBehavior` doğrulayıcı iletilerini kodsuz 400 olarak döner; MDM'de kodlu ret gövdesinin tek emsali Brand dışa aktarım reddidir
  (`BrandsController.cs:118-125`, `{ isSuccessful, statusCode, errors, errorCodes:[{ code }] }`). Karar:
  (a) `BusinessPartnerErrorCodes`'a `MDM_BP_VALIDATION_FAILED` (400) eklenir — §12'nin alana özgü kodu olmayan her kuralı (ad, kısa ad, vergi no biçimi,
  adres, `pageSize`, `search`, `countryCode`, sıralama vb.) bu koddur; alana özgü kodlar (§8.5: `EXPECTED_VERSION_REQUIRED`, `REASON_REQUIRED`,
  `ROLE_UNKNOWN`, `STATUS_UNKNOWN`, `ROLES_REQUIRED`, `CONTACT_INVALID`) aynen kalır.
  (b) `BusinessPartnerErrorCodes.ExportTooLarge = ListExportContract.TooLargeCode` (`EXPORT_TOO_LARGE`, 413) sınıfa eklenir (değer Building.Blocks'tan
  okunur, Building.Blocks değişmez) — AC-27 (b) yansıması onu da kapsar.
  (c) 8 ucun **her** reddi Brand dışa aktarım gövdesi biçimindedir ve en az bir `errorCodes[{ code }]` taşır (doğrulayıcı reddi dahil); `errors`
  teknik İngilizcedir, değer içermez (`{PropertyValue}` yok). Mekanizma özellik içinde kalır (ör. doğrulayıcı kuralı iletisi olarak sabit kodu verir,
  controller bilinen kodları `errorCodes`'a taşır); **MDM ortak `Response<T>` ve `ValidationBehavior` değişmez** (AC-14). Başka bir özellik içi
  mekanizma seçilirse aynı tel biçimini verdiği rapora yazılır.
  (d) Paket §13 "400 + doğrulayıcı iletisi (7 dil)" şöyle okunur: servis 400 + kod; 7 dil ileti Web'de (DL-2). Test: her uç için en az bir ret vakası
  `errorCodes[0].code`'u iddia eder; `MDM_BP_VALIDATION_FAILED` ve 413 `EXPORT_TOO_LARGE` vakası ayrı. Sabotaj **S-F1:** controller'ın kod eşlemesini
  kaldır (doğrulayıcı reddi kodsuz döner) → doğrulayıcı vakası kırmızı.
- **F-2 (güncellemede vergi no temizleme):** AC-4'e vaka: vergi no'lu ortak `PUT` ile vergi no boş / null gönderilerek güncellenir → belgede
  `TaxNumber` / `TaxNumberNormalized` alanı **yoktur** (ham BSON; `$unset`, null ya da boş dizge değil); ardından aynı ülkede vergi numarasız ikinci
  ortağın oluşturulması 201, güncellenmesi 200 (gerçek Mongo). Sabotaj **S-4d:** güncellemede alanları kaldırmak yerine `TaxNumber = null`,
  `TaxNumberNormalized = ""` yaz → "alan yok" ve "ikinci vergisiz ortak" iddiaları kırmızı.
- **F-3 (dışa aktarım kümesi):** Brand kümesi ayrıntı DTO'su üzerinedir (`ListExportColumnSet<BrandDetailDto>`); bu pakette **izlenmez**:
  `BusinessPartnerExportColumns.Set` = `ListExportColumnSet<BusinessPartnerListItemDto>` (liste öğesi açık vergi no taşımaz). Ek olarak AC-16'ya gövde
  iddiası: vergi no'lu (11 ve ≤ 6 karakter) tohum kayıtlarla `format=csv` gövdesinde açık vergi no ve maskeli biçimi geçmez. Sabotaj **S-F3:**
  kümeyi `BusinessPartnerDetailDto` üzerine kurup bir sütunun değerini `TaxNumber`'dan oku → gövde iddiası kırmızı.
- **F-4 (Web köprüsü ve anahtar biçimi — DL-2'yi bağlar, DL-1'de tel biçimi):** Web'de ortak `wwwroot/assets/js/shared/diten-refusal.js`
  kullanılır (ekrana özel kod çeviri JS'i yazılmaz); anahtar biçimi `Error.{KOD}` (ör. `Error.MDM_BP_INACTIVE`; Traceability emsali), paketteki
  `Error_{kod}` yerine. AC-27 (b) ve S-27b bu biçimle okunur (`Error.MDM_BP_INACTIVE`). DL-1'in payı: F-1 (c) tel biçimi — köprünün okuduğu
  `errorCodes[{ code }]` her retten gelir.
- **F-5 (S-26b biçimden bağımsız):** S-26b şöyle tanımlanır: `CanRemove`'un `LastRole` dalını devre dışı bırak — dalı sil ya da eşiği bir aşağı kaydır
  (`LastRole` yalnız `assignedRoleCount == 0`'da); yazım biçimi serbest → (1, true) vakası `Allowed` döner, kırmızı. S-26a aynen.

## 3. Kurallar
- Kiracı: her sorgu ve yazma `TenantId` yüklemli; `TenantId` gövdede alan değildir (gönderilse bağlanmaz); başka kiracı **404** `MDM_BP_NOT_FOUND`
  (yok olanla aynı gövde), liste ve dışa aktarım başka kiracı satırı döndürmez. Silme yok (`DELETE` uç / komut yok; `IsDeleted` v1'de set edilmez).
- Yetki: `[HasPermission("mdm.business-partners.{action}")]` her eylemde, sınıf düzeyinde yasak; manifest tam 6 anahtar, `assign-role` yok (C-5).
- Denetim: 4 komut `IAuditableCommand` + `IAuditMetadataProvider` (yol b); yerel geçmiş varlıkla aynı Mongo işleminde; merkezi ayrıntıda kişisel alanlar
  ve `LegalName` / `ShortName` yalnız adıyla (F-11).
- Dil: kiracı modülü → 7 dil (en, tr, fr, es, zh, ar, ru). DL-1'de kullanıcıya görünen metin yalnız dışa aktarım başlık / durum / rol sözcükleridir
  (`BusinessPartnerExport` 7 resx); servis hata iletisi teknik İngilizce, kullanıcıya gösterilmez; MdmService'e hata resx'i eklenmez.
- Kişisel veri: liste, dışa aktarım ve sözleşme liste öğesi kişi / adres satırı / posta kodu taşımaz; vergi no listede yalnız maskeli (≤ 6 karakter
  tamamen `••••••`), dışa aktarımda hiç yok.
- Korunan yollar: `.antigravity/**` (BPT satırı kapıda yazıldı; bu dilim yazmaz), `ocelot.json` (yalnız integration-agent, X-8),
  `services/Diten.Building.Blocks/**`, `services/Diten.Platform/**` (yalnız X-4 / X-5 / X-6 satırları), Auth, Procurement, CRM, Inventory,
  MdmService'te §5 / §5.1 dışı her şey (mevcut MDM test dosyaları satır değişmeden kalır — AC-14), donmuş sözleşmeler, `execution/registries/**`.
- XMC-001: listede olmayan bir dosyaya dokunmak gerekirse dur ve sor. Yalnız açık yolla stage (`git add -A` / `git add .` yok); hook atlanmaz;
  push / PR yok.

## 4. Kabul kapıları (başlamayı bekletmez)
- X-7: RECORD-CODE-ALLOCATE 1.1.0 (CT) hatta / main'de.
- X-8: Gateway rotası (integration-agent) hatta — AC-15 için.
- A-5 / F-10: dev ortamında Platform `TrustedRecordCode:Mdm` kimlik bilgisi sırlarla dolu, `AllowedRecordTypes` `"BPT"` içerir, kiracı grup öneki hizalı
  (işletim; AC-15 önkoşulu). Depodaki `appsettings*.json` geliştirici tarafından değiştirilmez.
- `BUSINESS-PARTNER-READ` 1.0.0 FROZEN (CT, DL-1 kabulünde). MOD-0179 DL-4 kapısı = DL-1 kabulü + X-8 + FROZEN sözleşme.

## 5. Test ve kanıt
- MdmService'in bütün test projeleri filtresiz (`Diten.MdmService.Application.Tests`, `Diten.MdmService.Api.Tests`):
  `DITEN_TEST_MONGOD="C:/Program Files/MongoDB/Server/9.0/bin/mongod.exe"`; 27017 kullanılmaz. Bilinen ~73 ortam kırmızısı **dilim başı** koşusuyla
  adıyla karşılaştırılır; yeni kırmızı yok. Dilim başı ve sonu sayıları raporlanır.
- Platform (X-4…X-6'ya dokunulduğu için) filtresiz: `Diten.Platform.Application.Tests`, `Diten.Platform.BackgroundJobs.Tests`,
  `Diten.Platform.CompositionGuard.Tests`, `Diten.Platform.Eventing.Tests`; aynı `DITEN_TEST_MONGOD`. Ayrıca `tests/architecture/TenantArchitecture.ArchitectureTests`
  (`AuditTrailStandardTests` yeni borç satırı olmadan yeşil, AC-11).
- Build: `Diten.MdmService.sln` ve Platform API PASS.
- AC-1…AC-16 ve AC-26 (AC-15 canlı kanıt X-8 ve A-5 sonrası, `scripts/run_record.py` kaydı, negatif adım zorunlu); F-1…F-3 ek vakaları.
- AC-14 kanıtı: `git diff --stat <dilim-başı>..<dilim-sonu> -- services/Diten.MdmService/tests` yalnız yeni dosyalar.
- Sabotajlar paketteki adlarla: S-1, S-2a, S-2b, S-3a, S-3b, S-3c, S-4a, S-4b, S-4c, S-5, S-6a, S-6b, S-6c, S-7a, S-7b, S-8a, S-8b, S-8c, S-9, S-10a,
  S-10b, S-10c, S-11a, S-11b, S-11c, S-12a, S-12b, S-12c, S-12d, S-13, S-14, S-15, S-16, S-26a, S-26b (F-5 tanımıyla); bu WP'nin ekleri S-4d, S-F1, S-F3.
  Her biri: uygula → hedef vaka kırmızı → bayt-eşit geri al → yeşil; `git status` temiz.
- `Diten.AuthService.AccountKindAcceptanceHost.Tests` koşulmaz.

## 6. Rapor
Commit sha'ları (sözleşme taslağı ilk commit; Platform X-4…X-6 commit'i ayrıca); AC-1…AC-16, AC-26 her biri için test adı ve sonuç; F-1…F-5'in
nasıl kapandığı (test adı + sabotaj); sabotaj tablosu; MDM ve Platform filtresiz sayılar (önce / sonra) ve taban kırmızıları adıyla; X-3 portunun
imzası ve dosya yolu (MOD-0289 / MOD-0217 için); MOD-0012-FU01 DL-1 ile çakışma olduysa nasıl çözüldü; X-7 için önerilen 1.1.0 eklemeli metin
(CT yazacak); açık noktalar.
