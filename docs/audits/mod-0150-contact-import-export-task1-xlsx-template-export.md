# MOD-0150 — Contact Import/Export Task 1: XLSX Template + Reference Helper + Existing-Data Export

**Date:** 2026-07-23 · **Module:** Contact & Relationship Management (`Diten.CrmService` + `Diten.Web`) ·
**Type:** Post-closeout enhancement (no new module / permission / reference set / seed / Gateway route) ·
**Verdict:** **PASS** (import upload/dry-run/apply intentionally deferred to Task 2)

Bu gate, MOD-0150 FU06'nın API-only import/export iskeletini **kullanılabilir bir ürün akışının ilk yarısına**
dönüştürür: çok sayfalı **XLSX import şablonu**, **MOD-0048 published-values ReferenceData yardımcı sayfası**,
şablonla birebir aynı kolon şemasına sahip **mevcut veri export'u** (opsiyonel Related Account linkleri + historical
linkler + Notes), ve Contacts ekranında **permission-aware** şablon indirme / export aksiyonları. CSV yolu
değiştirilmedi. MOD-0150 closeout PASS %100 durumu bozulmadı.

---

## 1. Preflight

| Konu | Durum |
|---|---|
| MOD-0150 status | Closeout PASS (Review-ready) %100 — FU01–FU06 + FU05 + Location/PII-KVKK Hardening + Historical Lifecycle |
| Mevcut FU06 import/export | Import = **JSON body** (`{dryRun, rows[]}`), export/template = **CSV**; 9 endpoint, 401 route-proof; UI yok |
| Bilinen boşluklar (design gap analysis) | Template = yalnız başlık satırı · export'ta `ContactId`/external-ref yok (round-trip imkânsız) · location/Gender/`ReportsToContactId`/`CrossCountryReason` şemada yok · AccountLinks export yalnız GUID · UI'da Import butonu `ComingSoon` |
| Bu task kapsamı | **Yalnızca** template + reference helper + export + UI + audit/registry |
| Kapsam dışı (uygulanmadı) | Import upload, XLSX parse, dry-run/preview, apply, upsert/update/end, AccountRelationship XLSX, yeni permission/reference set/seed, hardcoded fallback, direct 5061, hard delete, Territory/Visit/Patient/Consent |
| Protected paths | `.antigravity/**`, `Views/Archive/**`, `Controllers/Archive/**`, `_Layout.cshtml`, `ocelot.json` — **hiçbirine dokunulmadı**; `ocelot.json`'daki değişiklik bu oturumdan önce mevcuttu (MOD-0251 WIP), yeni route gerekmedi (mevcut `/api/crm/contacts/{everything}` wildcard'ı yeterli) |

---

## 2. Format / Dependency Decision

| Decision | Chosen Option | Reason |
|---|---|---|
| Ana kullanıcı formatı | **XLSX** (template + export) | Çok sayfa (Instructions/Contacts/AccountLinks/ReferenceData) tek dosyada; in-cell dropdown ile referans hatası daha dosyada engellenir; kullanıcı Excel'de doldurup geri yükleyebilir |
| CSV | **Korundu, değiştirilmedi** | `format` yoksa veya `format=csv` ise FU06 davranışı birebir aynı (`text/csv`, aynı başlıklar, aynı dosya adı). XLSX tamamen additive bir seçenek |
| XLSX kütüphanesi | **ClosedXML 0.102.2** — **dependency added** (`Diten.CrmService.Application.csproj`) | Data validation (dropdown), çok sayfa, stil ve named-range API'si en zengin olan; repoda XLSX kütüphanesi yoktu. Yerel NuGet önbelleğinde mevcuttu (offline restore çalıştı); transitif bağımlılıklar: DocumentFormat.OpenXml, ExcelNumberFormat, SixLabors.Fonts, System.IO.Packaging, XLParser |
| Parse yeri | **Uygulanmadı (Task 2)** | Bu taskta hiçbir XLSX okuma/parse yolu yok; karar Task 2'ye bırakıldı (öneri: backend parse, mevcut satır sözleşmesi korunur) |
| Hücre tipi | **Tüm veri hücreleri metin (`@`)** | GUID, ISO tarih, `+90` çevirme kodu ve baştaki sıfırlı posta kodu Excel'in yerel ayara bağlı otomatik dönüşümünden korunur → round-trip bozulmaz |
| AccountRelationship | **Bu taskta yok** | Contact + AccountLinks yeterli; ilişki sayfası ayrı follow-up (mevcut CSV/JSON endpoint'leri olduğu gibi duruyor) |

---

## 3. XLSX Template Design

`GET /api/crm/contacts/import-template?format=xlsx[&includeAccounts=true]`

| Sheet | Purpose | Key Columns | Result |
|---|---|---|---|
| **Instructions** | Amaç, `Operation` sözlüğü, zorunlu alanlar, sistem kolonları, historical-end kuralı, PII/KVKK + Notes uyarısı, dry-run bilgisi, referans set durumu | (serbest metin, ilk açılan sayfa) | ✅ `TemplateVersion 1.0` + `GeneratedAtUtc` + korelasyon damgası; zorunlu set yayınlanmamışsa **kırmızı blok uyarı** |
| **Contacts** | Contact master satırları | 23 kolon: `Operation, ContactId, ExternalSystem, ExternalId, FirstName, LastName, DisplayName, ContactType, ContactStatus, Gender, ProfessionalTitle, Specialty, Department, CountryCode, CityCode, DistrictCode, AddressLine, PostalCode, PreferredLanguage, PhoneCountryCode, Phone, Email, Notes` | ✅ Boş; header dondurulmuş + autofilter; sistem kolonları renkli işaretli |
| **AccountLinks** | Contact↔Account bağları | 16 kolon: `Operation, LinkId, ContactId, ContactExternalSystem, ContactExternalId, AccountId, AccountCode, AccountName, RoleCode, IsPrimary, Status, ValidFrom, ValidTo, ReportsToContactId, Notes, CrossCountryReason` | ✅ Boş; `AccountName` read-only helper olarak işaretli; **`EndDate` ayrı kolon yok** — `ValidTo` end tarihidir |
| **ReferenceData** | MOD-0048 published values | `SetCode, ValueCode, DisplayName, Description, IsActive, IsDeprecated, Metadata` | ✅ 12 set; yayınlanmamış set → `NOT_PUBLISHED` satırı (kırmızı); deprecated değer gri/italik ve **dropdown'a girmez** |
| **Accounts** (opsiyonel) | AccountCode bulma yardımcısı | `AccountId, AccountCode, AccountName, AccountType, CountryCode, CityCode` | ✅ Yalnızca `crm.account.read` varsa ve `includeAccounts=true` ise üretilir; read-only |

**Data validation (dropdown):** `Operation` (sabit protokol listesi: add/update/end/skip) + `ContactType, ContactStatus,
Gender, ProfessionalTitle, Specialty, Department, CountryCode, CityCode, DistrictCode, PreferredLanguage,
PhoneCountryCode` (Contacts) + `RoleCode` (AccountLinks). Kaynak **ReferenceData sayfasındaki canlı aralık**, satır içi
literal liste değil. `ErrorStyle=Warning` — dropdown kolaylıktır, güvenlik sınırı değildir; otorite sunucu tarafı
doğrulamasıdır. **Yayınlanmamış set → dropdown yok** (hardcoded fallback yasak). `AccountContactLink.Status` için
reference set **yoktur** ve icat edilmemiştir — entity sözleşmesindeki serbest iç lifecycle marker olarak kalır.

---

## 4. ReferenceData Helper Proof

| SetCode | Required? | Source | Result |
|---|---|---|---|
| `contact-type` | **Evet** | MOD-0048 published-values (Gateway consumer) | ✅ Listelenir + dropdown; yayınlanmamışsa Instructions'ta **blok uyarı** |
| `contact-status` | **Evet** | aynı | ✅ |
| `contact-role` | **Evet** (AccountLinks) | aynı | ✅ |
| `gender` | Hayır | aynı | ✅ / `NOT_PUBLISHED` |
| `professional-title` | Hayır | aynı | ✅ / `NOT_PUBLISHED` |
| `medical-specialty` | Hayır | aynı | ✅ / `NOT_PUBLISHED` |
| `department-type` | Hayır | aynı | ✅ / `NOT_PUBLISHED` |
| `country` / `city` / `district` | Hayır | aynı | ✅ / `NOT_PUBLISHED` |
| `preferred-language` | Hayır | aynı | ✅ / `NOT_PUBLISHED` |
| `phone-country-code` | Hayır | aynı | ✅ / `NOT_PUBLISHED` |
| `account-contact-link-status` | — | **Yok** | ✅ Set icat edilmedi; `Status` serbest marker |

**Seam:** yeni `IReferenceDataCatalogReader` (Application) + `GatewayReferenceDataValidator` implementasyonu
(Infrastructure) — mevcut `published-values?scope_key={tenant}` çağrısının **liste** karşılığı. Tenant-scope zorunlu;
tenant yoksa/çağrı başarısızsa `NotPublished` döner, **asla yerel liste üretmez**. `ValidateAsync` deprecated değeri
reddetmeye devam eder; katalog okuması deprecated değeri **işaretleyerek** gösterir (dokümantasyon amaçlı) ama
dropdown'a koymaz.

---

## 5. Export Design

`GET /api/crm/contacts/export?format=xlsx[&includeLinks&includeHistorical&includeNotes&includeAccounts&contactType&status&country&updatedAfter]`

| Export Type | Includes | Query Options | Permission | PII Behavior |
|---|---|---|---|---|
| Contacts only | Instructions + Contacts + ReferenceData | `format=xlsx` | `crm.contact.export` | Tam PII; Notes **boş**; audit counts+flags |
| Contacts + active links | + AccountLinks (aktif) | `includeLinks=true` | + `crm.account-contact.read` (**yoksa 403**) | aynı |
| Contacts + active + historical links | + ended/inactive linkler | `includeLinks=true&includeHistorical=true` | + `crm.account-contact.read` | aynı; `Status`/`ValidTo` net görünür |
| + Accounts lookup | + Accounts sayfası | `includeAccounts=true` | + `crm.account.read` (**yoksa 403**) | Account master read-model (PII değil) |
| Notes/gerekçe dahil | Notes + `CrossCountryReason` dolu | `includeNotes=true` | `crm.contact.export` | **Opt-in**; varsayılan kapalı |
| Filtered | yukarıdakilerin herhangi biri | `contactType`, `status`, `country`, `updatedAfter` | aynı | Filtre **alan adları** audit'e yazılır, değerleri değil |
| CSV (değişmedi) | FU06 CSV | (parametresiz) veya `format=csv` | `crm.contact.export` | FU06 davranışı |

**Round-trip:** export edilen Contacts/AccountLinks sayfaları template ile **birebir aynı kolon setine** sahiptir;
tek fark `Operation` kolonunun **boş** gelmesi (kullanıcı ne yapacağını kendisi seçer) ve sistem kimliklerinin
(`ContactId`, `LinkId`, `AccountId`, `ReportsToContactId`) **dolu** gelmesidir.

**`CrossCountryReason` kararı:** serbest metin olduğu ve kişisel veri taşıyabildiği için **Notes ile aynı opt-in kapıya
bağlandı** (`includeNotes=true`). Varsayılan export'ta boş gelir. Ayrı bir `includeSensitive` bayrağı icat edilmedi —
tek bir "serbest metin dahil et" kararı kullanıcı için daha anlaşılır ve yüzeyi dar tutar.

**Satır limiti (safe default, sabit):** `MaxContactRows = 5000`, `MaxLinkRows = 20000`, `MaxAccountLookupRows = 2000`
(`ContactWorkbookSchema`). Aşılırsa **controlled 400** + "filtre uygulayın" mesajı; sınırsız PII akışı ve timeout
engellenir. Config'e taşınması follow-up.

---

## 6. Frontend UI Summary

| Yüzey | Davranış |
|---|---|
| **Yerleşim** | Her iki aksiyon da Contacts DataTable'ının mevcut **Action (⚙) dropdown'ının içinde**, ayırıcının altında `Import` ile aynı grupta: `Print / CSV / Excel / PDF / Copy` (istemci tarafı grid dökümü) — ayırıcı — `Download Template · Export Contacts · Import` (sunucu tarafı). Toolbar'a yeni ikon buton eklenmedi |
| **Download Template** | Dropdown öğesi; `crm.contact.import` yoksa **hiç render edilmez**; `/CRM/Contacts/template?includeAccounts={crm.account.read}` (aynı origin MVC proxy → Gateway) |
| **Export Contacts** | Dropdown öğesi (`crm.contact.export` yoksa render edilmez) → **premium SweetAlert2 (MOD-0013) seçenek diyaloğu** |
| **Export with Related Accounts** | Aynı diyalogda `includeLinks` kutusu; `crm.account-contact.read` yoksa **kutu hiç gösterilmez**; `includeHistorical` yalnız `includeLinks` işaretliyken etkin |
| **PII/KVKK uyarısı** | Diyalogda `alert-warning` bloğu + Notes için ayrı açıklama; 7 dilde |
| **Grid filtresi devamlılığı** | Kullanıcının uyguladığı `contactType`/`status` filtreleri export sorgusuna taşınır (indirilen dosya ekranda görülenle eşleşir) |
| **Import butonu** | **Fake akış yok** — `ImportComingSoon` mesajı ("bir sonraki sürümde; şimdiden şablon indirip export alabilirsiniz") |
| **Gateway-only** | Tarayıcı 5061'e hiç gitmez; MVC proxy sunucu tarafında Gateway'i (5000) çağırır ve dosyayı stream eder |
| **Compact pattern** | Yeni sayfa/offcanvas/quickview eklenmedi; yalnız toolbar aksiyonu + onay diyaloğu |

Action dropdown'ına modül aksiyonu ekleyebilmek için `dt-defaults.js`'e **additive ve geriye dönük uyumlu**
`extraButtons.collectionBtns` (dizi) desteği eklendi; ayırıcı artık `collectionBtns` ve/veya `importBtn` varsa bir kez
basılıyor. Yalnız `importBtn` geçen mevcut modüller (CRM Accounts, DevEnablement GoldenReference Compact/Slim,
Platform Administrators, Platform ReferenceData) için çıktı **birebir aynı** kalır.

---

## 7. Changed Files

| File | Change | Why |
|---|---|---|
| `services/.../Application/Diten.CrmService.Application.csproj` | **+ClosedXML 0.102.2** | XLSX yazımı (dependency added) |
| `services/.../Application/Features/ImportExport/ContactWorkbookSchema.cs` | **yeni** | Tek kaynaklı sheet/kolon şeması + set bağlamaları + limitler |
| `services/.../Application/Features/ImportExport/Xlsx/ContactWorkbookModels.cs` | **yeni** | `ExportFileDto`, `ContactWorkbookOptions`, `ContactWorkbookRequest` |
| `services/.../Application/Features/ImportExport/Xlsx/ContactWorkbookBuilder.cs` | **yeni** | Workbook yazıcı (Instructions/Contacts/AccountLinks/ReferenceData/Accounts + dropdown + metin formatı) |
| `services/.../Application/Features/ImportExport/Handlers/ContactWorkbookHandlers.cs` | **yeni** | Template + export handler'ları, filtre, satır limiti, PII-safe audit, PII'siz dosya adı |
| `services/.../Application/Features/ImportExport/ImportExportCommands.cs` | +2 query | `BuildContactTemplateWorkbookQuery`, `ExportContactsWorkbookQuery` (CSV query'leri değişmedi) |
| `services/.../Application/Common/ReferenceValidation/IReferenceDataCatalogReader.cs` | **yeni** | MOD-0048 published-values **liste** seam'i + snapshot tipleri |
| `services/.../Infrastructure/ReferenceValidation/GatewayReferenceDataValidator.cs` | +`GetPublishedValuesAsync` | Aynı Gateway consumer'ı listeyi de okur (yeni HTTP yolu yok) |
| `services/.../Infrastructure/DependencyInjection.cs` | +1 kayıt | `IReferenceDataCatalogReader` → aynı validator örneği |
| `services/.../Api/Controllers/CRM/ImportExportController.cs` | `format=xlsx` dalları + fail-closed alt kapsam kontrolü | CSV korunur; `includeLinks`/`includeAccounts` için 403 |
| `services/.../Domain/Repositories/IContactExternalReferenceRepository.cs` + `Persistence/.../ContactExternalReferenceRepository.cs` | +`ListAllAsync` | Export'ta external-ref'i N+1 sorgu olmadan okumak |
| `services/.../tests/.../ContactWorkbookExportTests.cs` | **yeni (23 test)** | Şema/round-trip/referans/historical/Notes/limit/PII kanıtı |
| `services/.../tests/.../{ImportExport,ContactFoundation,ContactLocationPiiHardening}Tests.cs` | fake'lere +1 satır | Yeni repo metodu |
| `frontend/.../Controllers/CRM/ContactsController.cs` | +`DownloadTemplate`, +`Export`, +`StreamWorkbookAsync`, Index'te capability flag'leri | Gateway-only dosya proxy'si + permission gating |
| `frontend/.../Views/CRM/Contacts/Index.cshtml` | +`contacts-capabilities` JSON bloğu | Toolbar aksiyonlarının permission-aware olması |
| `frontend/.../Views/CRM/Contacts/_IndexL10n.cshtml` | +13 anahtar | Buton/diyalog metinleri |
| `frontend/.../wwwroot/assets/js/CRM/Contacts/index.js` | +capability okuma, +2 toolbar butonu, +export diyaloğu, Import mesajı | UI akışı |
| `frontend/.../wwwroot/assets/js/CRM/Contacts/index.l10n.js` | +13 required key | L10n bridge parity kontrolü |
| `frontend/.../wwwroot/assets/js/dt-defaults.js` | +`extraButtons.collectionBtns` (additive; ayırıcı tek sefer) | Action dropdown'ına modül aksiyonu ekleyebilmek |
| `frontend/.../Resources/Views/CRM/Contacts/ContactIndex.*.resx` (7) | +13 anahtar (72→**85**) | 7 dil parity |
| `docs/audits/mod-0150-contact-import-export-task1-xlsx-template-export.md` | **yeni** | Bu rapor |
| `execution/registries/module-implementation-status.md` | MOD-0150 satırına Task 1 notu | Registry |

**Dokunulmayanlar:** `ocelot.json` (yeni route gerekmedi), permission seed (`DataSeeder`), MOD-0048 set tanımları,
`Contact`/`AccountContactLink`/`AccountRelationship` entity'leri, mevcut CSV/JSON import-export yolu, `module-id-registry.md`.

---

## 8. Browser Golden Flow

> **Dürüstlük notu:** Fleet çalışır durumdaydı (5000/5001/5061 açık) ve CrmService hot-reload ile yeni yüzeyi
> yayınladı — bu **runtime kanıtı** aşağıda. Ancak **kimlik doğrulamalı** (97c5 CRM Admin oturumu) uçtan uca indirme
> akışı bir runtime token/oturum gerektirir; token yalnızca kullanıcı girdisiyle alınabilir, tahmin/üretim yapılmaz.
> Bu nedenle 8–15. adımlar **operatör tarafından koşulacak** olarak açık bırakıldı — sahte PASS yok.

| Step | Evidence | Result |
|---|---|---|
| 1. Yeni XLSX yüzeyi canlı serviste var | `GET :5061/swagger/v1/swagger.json` → `import-template` + `includeLinks`/`includeHistorical`/`includeNotes`/`includeAccounts` parametreleri **mevcut** | ✅ |
| 2. Gateway route + guard (template, xlsx) | `GET :5000/api/crm/contacts/import-template?format=xlsx` → **401** (routed + guarded, 404 değil) | ✅ |
| 3. Gateway route + guard (export, xlsx + opsiyonlar) | `GET :5000/api/crm/contacts/export?format=xlsx&includeLinks=true&includeHistorical=true` → **401** | ✅ |
| 4. CSV yolu bozulmadı | `GET :5000/api/crm/contacts/export` → **401** (aynı guard; kod yolu değişmedi, `format` yoksa FU06 dalı) | ✅ |
| 5. Frontend proxy fail-closed | `GET :5001/CRM/Contacts/template` ve `/export` → **302 → login** (anonim erişim yok) | ✅ |
| 6. Şablon gerçekten açılabilir bir XLSX ve 4 sayfa içeriyor | `ContactWorkbookExportTests` üretilen baytları ClosedXML ile **geri okur**: Instructions/Contacts/AccountLinks/ReferenceData ✅, Accounts yalnız istendiğinde ✅ | ✅ (unit) |
| 7. Dropdown ReferenceData'ya bağlı, yayınlanmamış sette yok | `Template_Binds_A_Dropdown_To_The_Published_Values_Of_A_Set` / `..._No_Dropdown_When_The_Set_Is_Not_Published` | ✅ (unit) |
| 8. Login → `/CRM/Contacts` → Download Template tıkla → dosya iner | operatör | ⏳ açık (Low) |
| 9. Export Contacts → dosya iner | operatör | ⏳ açık (Low) |
| 10. Export with Related Accounts (`includeLinks`) | operatör | ⏳ açık (Low) |
| 11. `includeHistorical` ile ended linkler dosyada | operatör (unit kanıtı ✅) | ⏳ açık (Low) |
| 12. `includeNotes` varsayılan kapalı | operatör (unit kanıtı ✅) | ⏳ açık (Low) |
| 13. PII/KVKK uyarısı diyalogda görünür | operatör (RESX 7 dil + kod ✅) | ⏳ açık (Low) |
| 14. Import butonu sahte akış başlatmaz | kod: `showToast(L.ImportComingSoon, 'warning')` — upload/parse yolu yok | ✅ (kod) |
| 15. Direct 5061 yok / console error yok | grep CLEAN (aşağıda); console operatör tarafından | ✅ / ⏳ |

> RESX değişiklikleri çalışan `Diten.Web` sürecinde **tam yeniden başlatma** ister (bilinen davranış); operatör
> smoke'undan önce fleet restart edilmelidir.

---

## 9. Validation Commands

| Command | Result | Notes |
|---|---|---|
| `dotnet build .../Diten.CrmService.Api.csproj` | ✅ **0 Uyarı / 0 Hata** | ClosedXML restore offline çalıştı |
| `dotnet test .../Diten.CrmService.Application.Tests.csproj` | ✅ **128/128** | 105 önceki + **23 yeni** (`ContactWorkbookExportTests`) |
| `dotnet build frontend/Diten.Web/Diten.Web.csproj` | ✅ **0 Uyarı / 0 Hata** | — |
| `dotnet build gateway/Diten.ApiGateway.csproj` | ✅ **0 Uyarı / 0 Hata** | route değişmedi |
| `verify_datatable_page.py --area CRM --module Contacts --reference compact` | ✅ **94 PASS / 0 FAIL** | Compact kontratı korundu |
| RESX parity (7 dil) | ✅ **85 / 85 × 7** | ContactIndex 72 → 85 |
| Live route smoke (5000) | ✅ 3/3 **401** | template xlsx, export xlsx+opts, export csv |
| Live proxy smoke (5001) | ✅ 2/2 **302→login** | fail-closed |
| Live OpenAPI (5061) | ✅ yeni parametreler yayınlanmış | hot-reload doğrulaması |

**Search guards**

| Guard | Sonuç |
|---|---|
| Yeni kodda hardcoded reference değeri | ✅ CLEAN (tek eşleşme FU06'nın mevcut `Status="active"` yazımı — bu taskta değişmedi) |
| Frontend'de direct 5061 | ✅ CLEAN (yalnızca açıklama satırı) |
| Import upload/parse (`IFormFile`/`multipart`/workbook okuma) yanlışlıkla eklenmiş mi | ✅ CLEAN (0 eşleşme) |
| Dry-run/apply yanlışlıkla eklenmiş mi | ✅ CLEAN (yeni kodda `dryRun` yok) |
| AccountRelationship XLSX | ✅ CLEAN (yalnızca "kapsam dışı" yorumu) |
| Yeni permission icat | ✅ CLEAN — kullanılan tüm anahtarlar katalogda mevcut (`crm.contact.import/export/read/create/update`, `crm.account-contact.read/manage`, `crm.account-relationship.*`, `crm.account.read`) |
| CRM local seed | ✅ CLEAN |
| Raw PII audit/log/error | ✅ CLEAN (audit yalnız sayaç/bayrak/filtre **alan adı**; test ile kanıtlı) |
| Hard delete / doğrudan Mongo mutasyonu | ✅ CLEAN (yeni kod salt-okunur) |
| Fake success / mock data | ✅ CLEAN (Import butonu açıkça "coming soon") |
| Patient clinical data | ✅ CLEAN (yalnızca **yasaklayan** uyarı metni) |
| Territory/SalesRep/Visit | ✅ CLEAN |

---

## 10. PII/KVKK Proof

| Scenario | Expected | Observed | Status |
|---|---|---|---|
| Export permission zorunlu | `crm.contact.export` | Controller `[HasPermission("crm.contact.export")]` + frontend `RequirePage` | ✅ |
| İlişkili link export'u ayrı yetki ister | `crm.account-contact.read` yoksa veri genişlemez | Backend **403** (`RequireSubScope`), frontend seçeneği hiç göstermez + bayrağı düşürür | ✅ |
| Account lookup sayfası ayrı yetki ister | `crm.account.read` | aynı fail-closed kapı | ✅ |
| Audit ham PII taşımaz | sayaç + bayrak + filtre alan adı | `Export_Audit_Detail_Carries_Counts_And_Flags_But_No_Raw_Pii` (ad/e-posta/telefon/not/filtre **değeri** yok) | ✅ |
| Dosya adı PII taşımaz | `contacts-export-{tenantShort}-{yyyyMMddHHmm}-{corr8}.xlsx` | `Export_File_Name_Carries_No_Personal_Data` | ✅ |
| Notes varsayılan dışa aktarılmaz | opt-in | `Export_Leaves_Notes_Empty_Unless_They_Are_Explicitly_Requested` | ✅ |
| Cross-country gerekçesi varsayılan dışa aktarılmaz | opt-in (Notes kapısı) | `Export_With_IncludeNotes_Adds_Notes_And_CrossCountryReason` | ✅ |
| `PhotoDataUri` hiçbir zaman export edilmez | şemada yok | `ContactWorkbookSchema.ContactColumns` içinde yok | ✅ |
| Kullanıcı uyarılır | UI + dosya içi | Export diyaloğunda `alert-warning` (7 dil) + Instructions §6 | ✅ |
| Hasta/klinik veri | yasak + uyarı | Instructions §6 açık yasak; import yolu yok | ✅ |
| Hata mesajı PII sızdırmaz | alan adı/kural | Limit hatası yalnız sayı+öneri; proxy ham gateway gövdesini render etmez | ✅ |

---

## 11. Boundary / SoR Check

| Object/Capability | Owner | Touched? | Boundary Risk |
|---|---|---|---|
| Contact master | MOD-0150 | Salt-okunur (export) | none |
| AccountContactLink | MOD-0150 | Salt-okunur (export) | none |
| AccountRelationship | MOD-0150 | **Hayır** | none |
| Account master (kod/ad/lookup) | MOD-0149 | Salt-okunur projeksiyon, permission-gated | none |
| Reference values | MOD-0048 | consume-only (yeni liste seam'i, yeni set yok) | none |
| Permission engine | MOD-0018 | consume-only (yeni key yok) | none |
| Audit | MOD-0021 | mevcut seam'e 2 olay adı (`crm.contact.exported`, `crm.contact.import-template.downloaded`) | none |
| Consent/preference | MOD-0164 | **Hayır** | none |
| Territory/Zone/SalesRep/Visit | MOD-0151/0155 | **Hayır** | none |
| Gateway routing | integration-agent | **Hayır** (mevcut wildcard) | none |

---

## 12. Out-of-Scope Guard

| Forbidden Item | Found? | Status |
|---|---|---|
| Import upload / XLSX parse | Hayır | ✅ |
| Dry-run preview / apply import | Hayır | ✅ |
| Contact update/upsert import | Hayır | ✅ |
| AccountLink update/end import | Hayır | ✅ |
| AccountRelationship XLSX import/export | Hayır | ✅ |
| Territory / SalesRep / Visit / Route import | Hayır | ✅ |
| Patient clinical data | Hayır (yalnız yasaklayan uyarı) | ✅ |
| Consent capture/import | Hayır | ✅ |
| Yeni permission | Hayır | ✅ |
| Yeni reference set | Hayır | ✅ |
| CRM local seed / hardcoded fallback | Hayır | ✅ |
| Direct 5061 frontend call | Hayır | ✅ |
| Manuel Mongo müdahalesi | Hayır | ✅ |
| Hard delete | Hayır | ✅ |
| Relationship graph | Hayır | ✅ |
| Yeni MOD ID / `module-id-registry.md` değişikliği | Hayır | ✅ |

---

## 13. Open Items

| Item | Severity | Owner | Blocks Task 2? | Notes |
|---|---|---|---|---|
| Kimlik doğrulamalı browser golden flow (indirme adımları 8–13) | Low | operatör | Hayır | Runtime 97c5 oturumu + fleet restart (RESX) gerekir; kod/unit/route kanıtı mevcut |
| Instructions sayfası yalnız İngilizce | Medium | UI/L10n | Hayır | Workbook backend'de üretiliyor; 7 dil için lokalizasyon seam'i (Accept-Language ya da tenant dili) Task 2/3 işi |
| Satır limitleri sabit (5000/20000/2000) | Low | backend | Hayır | Config'e (`Crm:Export:*`) taşınması follow-up |
| Filtreleme bellek içi (`ListAllAsync` + LINQ) | Medium | backend | Hayır | Büyük tenant'ta repository seviyesinde filtreli sorgu gerekir; limit guard riski sınırlıyor |
| `updatedAfter` filtresi UI'da yok | Low | UI | Hayır | API destekliyor; diyaloğa tarih alanı eklenebilir |
| `RowVersion`/concurrency kolonu yok | Medium | Task 2 | **Kısmen** | Re-import sırasında "stale row" tespiti isteniyorsa Task 2'de şemaya eklenmeli (şema versiyonu 1.0 → 1.1) |
| `AccountRelationship` XLSX | Low | follow-up | Hayır | Ayrı iş; mevcut CSV/JSON endpoint'leri duruyor |
| **Patient/Healthcare Privacy Scope** | **High (governance)** | EA/Security | Hayır | Açık kalmaya devam ediyor — CRM genel closeout bu değerlendirme olmadan tamamlanmaz |
| `Crm:Audit:Mode=http` runtime cutover | Medium | operatör | Hayır | Export/template olayları HTTP audit'e ancak bayrak açılınca gider (varsayılan logging seam) |
| MOD-0149 Accounts compact verifier FAIL (`_Form` ↔ `Details` section parity) | Medium | MOD-0149 | Hayır | **Bu taskla ilgisiz, mevcut durum** — Accounts dosyalarına dokunulmadı |

---

## 14. Registry / Status Update

- **Previous:** MOD-0150 — Closeout PASS (Review-ready), %100 (+ Location & PII/KVKK Hardening + Historical Lifecycle).
- **New:** MOD-0150 — Closeout PASS (Review-ready), %100 **+ Contact Import/Export Task 1 (XLSX Template + Reference
  Helper + Existing-Data Export) PASS**.
- **Reason:** Closeout kapsamı bozulmadan import/export deneyiminin ilk yarısı teslim edildi; CSV yolu korundu,
  builds/tests/verifier/RESX yeşil, boundary temiz, yeni permission/reference set/seed/route yok. Import upload,
  dry-run, apply, upsert/update/end ve sonuç raporu **bilinçli olarak Task 2'ye** bırakıldı.
- **MOD-0151 hazırlığı:** Contact location + Account link verisi artık tek dosyada, kimlikli ve round-trip'e hazır
  şekilde dışa aktarılabiliyor → territory tasarımı için veri okunabilirliği güçlendi (MOD-0151 implement edilmedi).

---

## 15. Final Verdict

**PASS** — XLSX template / ReferenceData helper / existing-data export ve permission-aware UI teslim edildi;
import upload/dry-run/apply bilinçli olarak ertelendi. Build 0/0 (4 proje), CrmService **128/128**, compact verifier
**94/0**, RESX **85×85×7**, canlı route smoke 401/302 fail-closed, search guard'ları CLEAN. Tek gerçek açık kalem
kimlik doğrulamalı indirme smoke'u (Low, kod+unit+route kanıtı mevcut) ve governance düzeyindeki
Patient/Healthcare Privacy Scope (High, ayrı değerlendirme).

### Zorunlu karar cümleleri

1. **Task 1 delivers template and export only; import upload, dry-run, apply, upsert, and end operations are
   intentionally deferred to Task 2.**
2. **Reference data values used by import templates must come from MOD-0048 published-values, not hardcoded lists.**
3. **Export may contain PII and requires explicit export permission and PII-safe audit.**
4. **Exported files preserve stable Contact and AccountLink identity for future round-trip import.**
5. **Historical AccountLink records are exported only when explicitly requested with `includeHistorical=true`.**
6. **Patient-related clinical data remains out of scope and requires a dedicated healthcare privacy scope.**

## 16. Next Recommended Prompt

**MOD-0150 Contact Import/Export — Task 2: XLSX Upload + Dry-run Preview + Safe Apply** — dosya yükleme + Task 1
şemasına göre parse, satır eşleştirme (ContactId → ExternalSystem+ExternalId → create), `Operation=update/end`
semantiği, historical-end kuralı (silme yok, `Status=ended`+`ValidTo`), import yolunda `CrossCountryPolicy`
uygulanması (bugün atlanıyor — governance açığı), creates/updates/ends/skips/errors/warnings preview UI'ı ve
onaydan sonra kontrollü apply + sonuç raporu.
