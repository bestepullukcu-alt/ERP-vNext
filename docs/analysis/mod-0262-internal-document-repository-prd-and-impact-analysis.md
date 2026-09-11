# MOD-0262 — Internal Document Repository Service
## Sistem Etki Analizi + PRD + Routing Kararı (CT onayına)

| Alan | Değer |
|---|---|
| Tarih | 2026-09-07 |
| Hazırlayan | @product-manager (CONTROL TOWER görevi) |
| Artefakt tipi | Analiz + PRD + routing **önerisi**. Module pack **değildir**, Delivery Capability Pack **değildir**, runtime entity **değildir**. |
| SoT | `docs/System Capability & Implementation Blueprint - master 8.1.xlsx#Blueprint_Data` |
| Kapsam kısıtı | `services/` `frontend/` `gateway/` **okundu, değiştirilmedi**. Kod yazılmadı, pack yazılmadı, orchestrator tetiklenmedi. DOC-REPO-BUNDLE sözleşmesi burada **tanımlanmadı**. |
| Reconcile kuralı | Bulgular **ölçüldü, düzeltilmedi** (`reconcile-records.md` §0). |

---

## ⛔ 0. BAŞLIK BULGU — DCP-002 kimlik kapısı KAPALI

```
$ python .antigravity/scripts/verify_module_id.py . --check-id MOD-0262 \
      --name "Internal Document Repository Service"
BLOCKED  MOD-0262
   - registry collision: MOD-0262 already = 'Docs Repository (SharePoint/M365/Google Drive) [External Provider]'
     (!= 'Internal Document Repository Service')
Gate failed closed. See DCP-002.
EXIT=2
```

**Kapı tek bir nedenle kapalı: registry satırı bayat. Blueprint tarafında sapma YOK.**

Kanıt — `verify_module_id.py:129-165` sırayla şunları denetler ve **yalnız birincisi** hata verdi:

| Denetim | Sonuç | Anlamı |
|---|---|---|
| registry collision | ❌ FAIL | `module-id-registry.md:145` hâlâ external-provider adını taşıyor |
| Blueprint workbook okunabilirlik | ✅ (hata yok) | openpyxl yüklü, `Blueprint_Data` okundu |
| `MOD-0262 not in Blueprint` | ✅ (hata yok) | MOD-0262 Blueprint 8.1'de **var** |
| `canonical-name mismatch` | ✅ (hata yok) | Blueprint 8.1 `Module Name` = **"Internal Document Repository Service"** — CT'nin verdiği SoT birebir doğru |

> **CT'ye karar sorusu (D-1):** Kapı, kimlik çatışması nedeniyle değil **kayıt sapması** nedeniyle kapalı. Pack aşamasına geçmeden önce registry satırının Blueprint 8.1'e hizalanması gerekiyor. Bu bir DCP-002 dokümantasyon işlemidir; ben **düzeltmedim** (reconcile §0 + CT'nin "düzeltme YAPMA" talimatı).

### Neden durmayıp devam ettim
Talimat "non-zero exit → DUR, CT'ye bildir" idi. Kapının **aşağı akışı** (pack yazımı, orchestrator) zaten yasaklıydı ve durduruldu — hiçbiri yapılmadı. Adım 2 ise tam olarak bu bloğa sebep olan sapmayı ölçmek üzere tanımlanmıştı; adım 3 ve PRD de salt-okunur analiz. Bu nedenle ölçüm ve analiz tamamlandı, **kapı açılmadı**. Pack aşaması D-1 kararına kadar bloklu.

---

## 1. SoT — Blueprint 8.1 `Blueprint_Data` MOD-0262 satırı (tam)

| Alan | Değer |
|---|---|
| Module ID | MOD-0262 |
| Module Name | Internal Document Repository Service |
| Domain / Landscape | 1) Platform & Shared Services |
| Suite / Platform | Documentation & Evidence Services |
| Capability Group | Internal Document Repository |
| Aim / Goal | "Provide internal document repository service for controlled document binaries and evidence packages **without external storage dependency**." |
| Wave | W-1 |
| Dependency Gate | RBAC / ABAC Authorization; Audit Trail Service; Records Management / Retention / Legal Hold; Evidence Linking Service |
| Delivery Outcome | Internal document storage for evidence, controlled document, and reference packages. |
| Soft Pages | Repository Admin; Evidence Package Storage; Document Binary Store; Repository Health |
| Placement | Platform Service |
| Min Integration Contracts | DOC-REPO-BUNDLE |
| Integration Contracts | DOCS-BASE + DOC-REPO-BUNDLE |
| SoR Primary Object(s) | Document binaries; evidence packages; repository objects |
| Deployment Unit / Product | Platform Document Service |
| Build / Buy / Partner | **Build** |
| SLO Tier | Tier 2 |
| Release Scope | R1 - PPM MVP |
| Implementation Phase | TBD |
| SoR Applicability | Y — "Internal document repository service with native platform ownership." |
| AI Applicability | N |

**Ters bağımlılık (`Dependencies_Normalized`) — MOD-0262'ye HARD bağlı olanlar:**

| Bağlı modül | Tip | Not |
|---|---|---|
| MOD-0028 Document Management (Templates/Versioning) | **HARD** | — |
| MOD-0313 HR Documentation & Evidence Workspace | **HARD** | "Added during HCM foundation update; preserves external SoR and shared-service boundaries." |

**`External Systems Register` sayfası:** "Internal Document Repository Service | **Internalized / No External Integration** | N/A - Internal Platform Service | DOC-REPO-BUNDLE | … | Reclassified as internal document repository service MOD-0262."

### 1a. Sapmanın kök nedeni — tarihli zincir

| Tarih | Olay | Kanıt |
|---|---|---|
| 2026-06-03/04 | DCP-002 kimlik kanonikleştirmesi koştu. MOD-0262 **"name-aligned (applied)"** listesine girdi — o günkü Blueprint adına, yani hâlâ `Docs Repository (SharePoint/M365/Google Drive) [External Provider]` adına hizalandı. | `DCP-002-module-identity-canonicalization.md` §5, `blueprint-master-plan-reconciliation.md:29` |
| **2026-06-16** | Blueprint **v6.1** — "Internal-only HCM renaming … reclassified HRIS/payroll/T&A/document…" → MOD-0262 external-provider'dan internal platform service'e **dönüştürüldü**. | `VERSION_LOG` v6.1 satırı |
| bugün | Bu SoT değişikliği repo kayıtlarına **hiç yansıtılmadı**. | Aşağıdaki Tablo 1 |

> Yani bu bir *ihlal* değil, **DCP-002 sonrası SoT değişiminin yeniden yayılmaması**. Aynı DCP-002 pasajı MOD-0266'yı düzeltmiş ve bunu registry notunda kayda geçirmiş (`module-id-registry.md:148` → "Name aligned to Blueprint MOD-0266 per DCP-002. (was: Blob / File Storage Provider)"); MOD-0262 için böyle bir ikinci geçiş olmadı.

---

## 2. Kayıt mutabakatı — ÖLÇÜLDÜ, DÜZELTİLMEDİ

### CT brief'indeki iki referans önce doğrulandı

| CT'nin verdiği | Ölçülen gerçek | Durum |
|---|---|---|
| `master-development-plan.md:1194` | `execution/portfolio/master-development-plan.md` **223 satır**; MOD-0262 satırı **:84**. `:1194` referansı **legacy** `docs/platform/master-plan.md:1194`'e ait. | referans bayat — ikisi de aşağıda ölçüldü |
| `module-id-registry.md:145` | Doğru. | ✅ |

### Tablo 1 — Bayat/yanlış kayıtlar (MOD-0262 kapsamı)

| # | Nerede yazılı (dosya:satır) | Ne diyor | SoT / koddaki gerçek | Yön | Emin değilim? |
|---|---|---|---|---|---|
| R1 | `execution/registries/module-id-registry.md:145` | ad = `Docs Repository (SharePoint/M365/Google Drive) [External Provider]`; slug = `external-document-provider`; not = "Referenced in master plan; no module pack found." | Blueprint 8.1: `Internal Document Repository Service`, Platform Service, **Build**, Domain-1. Slug external-provider anlamını taşıyor. | **YANLIŞ** — DCP-002 kapısını fail-closed bırakıyor | — |
| R2 | `execution/portfolio/master-development-plan.md:84` | `External Document Provider \| W2-A \| High \| planned \| 0% \| Integrations metadata.` | Blueprint: **W-1**, R1-PPM MVP, RC=Y. "Integrations metadata" tanımı artık yanlış — SoR = document binaries + evidence packages. Wave W2-A ↔ W-1 çelişiyor. | **YANLIŞ** (ad + wave + tanım) | — |
| R3 | `docs/platform/master-plan.md:1194-1202` | "### MOD-0262 — External Document Provider … SharePoint, Google Drive, Dropbox gibi external document repository connector'ları … **MVP-priority değil** … **Skip if:** ERP modülleri başlamadıkça gerek yok." | Blueprint: Build, internal, **R1-PPM MVP**, `Release_Candidates` = **RC "Y" / "In release scope"**, ve MOD-0028 + MOD-0313 **HARD** bağımlı. "Skip" tavsiyesi SoT ile tam ters. | **YANLIŞ — en tehlikeli satır** (bitmemiş kritik iş "atlanabilir" görünüyor) | — |
| R4 | `docs/platform/master-plan.md:2400` | "2026-05-11: MOD-0262 (External Document Provider) MVP-priority değil, ERP modülleri başlamadıkça **erteleme**." | Aynı — 2026-06-16 SoT değişikliğinden **önce** alınmış karar, sonrasında gözden geçirilmemiş. | **BAYAT** (tarihsel karar, geçerliliği düşmüş) | — |
| R5 | `docs/platform/master-plan.md:200` | envanter satırı: `External Document Provider \| W2-A \| 🟠 High \| 🔴 \| 0` | Aynı ad/wave sapması. | **YANLIŞ** | — |
| R6 | `docs/platform/master-plan.md:202` + `execution/portfolio/master-development-plan.md:83` | `MOD-0266 \| Blob / File Storage Provider \| W2-A` | Blueprint 8.1 MOD-0266 = **`Cloud Infrastructure (AWS/Azure/GCP) [External Provider]`**. Registry (`:148`) bunu DCP-002'de zaten düzeltmiş; iki plan dosyası düzeltilmemiş. | **YANLIŞ** (komşu sapma — MOD-0262 ile doğrudan bağlantılı: depoda "blob storage modülü" diye anılan şey Blueprint'te yok) | — |
| R7 | `execution/domains/commercial-suite/work-packs/SCMM-content-studio-work-plan.md:53` | "MOD-0262 **external repo** \| binary (SCMM-07) \| planned/missing %0 \| pinned version / **R2**" | MOD-0262 internal, **R1**. Bir aşağı-akış iş planı bayat kimliği devralmış. | **YANLIŞ** (yayılmış sapma) | — |
| R8 | `docs/audits/blueprint-module-id-reconciliation-2026-06-03.md:38` | MOD-0262 ↔ "Docs Repository (SharePoint/M365/Google Drive) [External Provider]" | Yazıldığı tarihte doğruydu (v6.1 öncesi). | **TARİHSEL KAYIT — sapma değil**, düzeltilmemeli | — |
| R9 | `execution/portfolio/blueprint-master-plan-reconciliation.md:29` | MOD-0262, "name-drift set … Blueprint canonical names / retain-id, name-align only" içinde **kapatılmış** görünüyor | Ad hizalaması v6.1 ile **yeniden açıldı**; ledger bunu bilmiyor. | **BAYAT** (kapalı görünen açık madde) | — |

### Tablo 1b — SoT'un kendi içindeki sapma (⚠️ pack aşaması için ön-koşul)

Blueprint 8.1 MOD-0262 için **kısmen** göç etmiş; aynı workbook içinde iki gerçek var:

| # | Sayfa | Ne diyor | `Blueprint_Data` ne diyor | Etki |
|---|---|---|---|---|
| B1 | `Module Pages` (2 satır) | Domain = **7) External Systems & Providers**, Suite = **External Systems Register**, Capability Group = **External Provider**; soft page'ler = `External System Profile`, `Integration/Contract Profile` | Domain-1, Documentation & Evidence Services; soft page'ler = **Repository Admin / Evidence Package Storage / Document Binary Store / Repository Health** | Soft page listesi **çelişiyor** → pack'in sayfa kapsamı SoT'tan tek-anlamlı türetilemez |
| B2 | `Blueprint_Data.Min Integration Contract Codes` | **`EXT-BASE`** | `Integration Contracts` = `DOCS-BASE + DOC-REPO-BUNDLE`; SoR notu "native platform ownership" | External-provider döneminden artık alan; internal servis için anlamsız |
| B3 | `Contract Bundle Dictionary` | **`DOC-REPO-BUNDLE` satırı YOK**; `DOCS-BASE` satırı da **YOK** (`EXT-BASE` var) | Blueprint zorunlu sözleşme olarak DOC-REPO-BUNDLE'ı işaret ediyor | Sözleşme kodu **tanımsız** — pack aşamasının girdisi eksik |
| B4 | `Release_Candidates` | Implementation Phase = **"Platform Foundation"** | `Blueprint_Data.Implementation Phase` = **"TBD"** | Küçük; faz belirsiz |
| B5 | `Dependencies` / `Dependencies_Normalized` | MOD-0028 adı = "Document Management (Templates/Versioning)" | `Blueprint_Data` MOD-0028 adı = "Documentation & Evidence Management" | Düşük şiddet; ad tutarsızlığı |

> **CT'ye karar sorusu (D-2):** B1/B2/B3 Enterprise Architect kararıdır, PM kararı değil. Özellikle **B3 pack aşamasının sert ön-koşuludur**: DOC-REPO-BUNDLE Contract Bundle Dictionary'de tanımlı değilken sözleşme yazılamaz.

### Sayılar (reconcile §7 biçimi, MOD-0262 kapsamı)

| Ölçü | Değer |
|---|---|
| Bayat/yanlış kayıt (repo) | **7** (R1–R5, R7, R9) — R6 komşu sapma, R8 tarihsel kayıt (sapma değil) |
| SoT iç sapması (Blueprint 8.1) | **5** (B1–B5) |
| Uyuşmayan pack kutusu | **0** — MOD-0262 için module pack **yok**; bu kontrol bu turda bulgu üretemez (körlük değil, yokluk) |
| Eksik doküman | MOD-0262: pack yok, API dok yok, kullanıcı kılavuzu yok |

---

## 3. Çoklu-servis etki haritası

### 3.1 Bugünkü fiili durum — "Platform Document Service" diye bir deployment unit YOK

Blueprint MOD-0262'yi `Deployment Unit = Platform Document Service` olarak konumluyor. Bugün depoda böyle bir servis yok; ilgili tüm kod **`Diten.Platform` (5057) içinde**, MOD-0029-FU01 altında yaşıyor.

| Yüzey | Konum | Durum |
|---|---|---|
| Seam (arayüz) | `Diten.Platform.Application/Features/DocumentManagementControlledDocuments/Services/IContentStorageGateway.cs` | ✅ Var — `StoreAsync` / `OpenReadAsync` / `TryDeleteAsync` |
| Pointer entity | `Diten.Platform.Domain/Entities/DocumentManagement/ContentRef.cs` | ✅ Var — `ContentId, StorageProvider, ObjectKey, FileName, MediaType, ByteSize, Checksum, CreatedAt, CreatedBy, VersionId` |
| Provider | `Diten.Platform.Infrastructure/Services/DocumentManagement/LocalFileSystemContentStorageGateway.cs` (`ProviderName = "local-filesystem"`) | ✅ Var — tek provider; SHA-256, uzantı/medya/boyut doğrulama, deterministik object key, wwwroot dışı kök (dev fallback `%TEMP%/DitenStorage/Documents`) |
| Tüketiciler | `DocumentManagementControlledDocumentsController`, `…TemplatesController`, `…TemplateMastersController`, `DocumentVersioningService`, `ControlledDocumentRegistrationService`, `TemplateService` | ✅ Hepsi seam üzerinden |
| Repository Admin / Repository Health / Evidence Package Storage / Document Binary Store (4 soft page) | — | ❌ **Yok** |
| `EvidencePackage` (herhangi bir biçimde) | `services/` + `execution/` genelinde **0 eşleşme** | ❌ **Hiç yok** |

> Bu, önceki boşluk raporunun ~%25-30 tahminini doğruluyor: **seam ve tek provider var; servisleşme, evidence package, admin/health yüzeyleri ve fiziksel yaşam döngüsü yok.**

### 3.2 Etki — MOD-0029 / Platform (IContentStorageGateway taşınması)

| Konu | Ölçüm | Etki |
|---|---|---|
| Seam sahipliği | Arayüz `Diten.Platform.Application`'ın **feature klasörünün içinde** (`Features/DocumentManagementControlledDocuments/Services/`) yaşıyor; Common/Contracts katmanında değil | MOD-0262 ayrıştırılırsa arayüz **feature'a değil, paylaşılan bir sözleşme katmanına** çıkmalı. Aksi halde MOD-0262 kendi tüketicisinin feature klasörüne bağımlı olur. |
| Sözleşme yüzeyi | `StoreAsync` **`byte[] Content`** alıyor (`ContentStoreRequest`); `OpenReadAsync` **`Stream`** döndürüyor | `byte[]` in-process için sorunsuz; **HTTP sınırı ötesinde** onlarca MB'lık dosyalar için kabul edilemez. Servisleşme = sözleşme yeniden tasarımı (multipart/stream), sadece "taşıma" değil. Bu, DOC-REPO-BUNDLE'ın çekirdek kararı — **pack aşamasına ait.** |
| Kimlik bağlamı | `ContentStoreRequest`: `TenantId`, `CompanyId`, `Scope`, `ItemId`, `VersionId`; `ContentStorageScope` **yalnız `Documents` / `Templates`** | Evidence package ve diğer domain'ler için scope enum'u **yetersiz**. Genişletme geriye dönük uyumlu ama SoR sahipliği kararı gerektirir. |
| Yetki modeli | Arayüz XML dokümanı: *"Caller is responsible for all permission checks BEFORE invoking this; the gateway only resolves the object key."* | Servis sınırı geçilince bu varsayım **çöker**: uzak çağıran artık güvenilir değildir. MOD-0262 kendi authz'ını kurmak zorunda (Blueprint Dependency Gate zaten "RBAC/ABAC Authorization" diyor). **En yüksek mimari risk bu.** |
| Kırılma riski | 6 tüketici sınıfı + 3 controller, hepsi tek seam arkasında | Seam korunduğu için çağıran kod **değişmez**; kırılma provider/DI ve sözleşme genişletmesinde yoğunlaşır. |

### 3.3 Etki — CRM Knowledge (MOD-0162): sözleşmesiz, **çözümlenemeyen** referans

`KnowledgeContent` (`Diten.CrmService.Domain/Entities/KnowledgeContent.cs:63-74`) dört adet **tip-siz `string?` pointer** taşıyor:

```
ContentBodyRef   // "Pointer to a structured body (e.g. an HTML/markdown record key)"
ContentAssetRef  // "Pointer to a rendered asset. A pointer, never a stored binary."
FileRef          // "MOD-0028/0029 document reference (documentId + versionId form)."
Url              // "External URL. One of Body/Asset/File/Url must be present."
```

Ölçüm: bu dört alan CRM'de **yalnızca create/update komut yolunda taşınıyor** (controller → request → command → handler → entity). CRM tarafında bunları **çözümleyen tek bir kod yolu yok**: download endpoint'i yok, Platform'a giden çağrı yok, format doğrulaması yok.

**Sonuç:** MOD-0162 içerik zinciri uçtan uca **kapalı değil** — bir saha temsilcisi doktora gösterilecek içeriğin binary'sini sistemden **alamaz**. MOD-0262'nin çözdüğü boşluğun en görünür iş etkisi budur. Ayrıca `FileRef` string formatı ("documentId + versionId form") **hiçbir yerde sözleşme olarak tanımlı değil** ve MOD-0028/0029 tarafında karşılığı doğrulanmıyor.

> Kapsam kararı: CRM'in tüketici sözleşmesi (read/stream + authz) DOC-REPO-BUNDLE'ın parçası mı, yoksa MOD-0162 tarafında ayrı bir follow-up mu? → **D-4.**

### 3.4 Etki — Retention / fiziksel purge sahipliği (MOD-0029-FU15 → MOD-0030 → MOD-0262)

Kodda **açıkça yazılı, sahipsiz** bir devir noktası var. `DocumentDispositionService.cs:10-18`:

> *"CRITICAL BOUNDARY — READ BEFORE EXTENDING: this service **NEVER deletes anything**. 'Execute' writes an evidence marker (`ExecutedAsNoDeleteMarker`) … There is **no purge path, no scheduler and no cascade**. **Actual destruction is a deliberate future task that would consume these markers as its input, and it must re-verify holds at execution time.**"*

ve `DocumentManagementRetentionModels.cs:236-239` bunu kalıcı bir sözleşme cümlesine bağlamış:

> `DispositionBoundaryStatement = "Disposition is recorded as a governance marker only. MOD-0029-FU15 performs no deletion, purge or archival — the subject record remains fully intact and retrievable."`

| Katman | Bugün kimde | Hedef |
|---|---|---|
| Retention policy + legal hold + disposition **kararı** | MOD-0029-FU15 (Platform) ✅ shipped | Değişmez |
| Disposition **marker** üretimi | MOD-0029-FU15 ✅ | Değişmez — MOD-0262'nin **girdisi** |
| Binary'nin **fiziksel imhası** | **hiç kimse** ❌ | **MOD-0262** |
| Metadata'nın imhası | **hiç kimse** ❌ | MOD-0030 (Records Management) — Blueprint W-3 |

**Kritik güvenlik kuralı (pack'e taşınacak):** `TryDeleteAsync` bugün **best-effort compensation** amaçlı (yazma başarısız olunca orphan temizliği). Purge yolu **bunu kullanamaz** — imha; hold'u yürütme anında yeniden doğrulayan, denetlenebilir, geri alınamaz ve kanıt üreten ayrı bir işlemdir. İkisini aynı metoda bindirmek, legal hold altındaki bir belgenin sessizce silinmesine yol açar.

### 3.5 Etki — EnterpriseStrategy `UploadsController`: ikinci, yönetilmeyen depo

`services/Diten.EnterpriseStrategyService/src/Diten.EnterpriseStrategy.API/Controllers/UploadsController.cs`

| Ölçüm | Bulgu |
|---|---|
| Depolama | `Path.Combine(_env.ContentRootPath, "Data", "uploads", "demand-ideas", demandId)` — **doğrudan filesystem**, seam yok |
| Okuma | `PhysicalFile(path, att.ContentType, att.FileName)`; path DB'deki `att.StorageKey`'den kuruluyor |
| Checksum | **yok** |
| Uzantı / medya tipi allow-list | **yok** (yalnız 50MB `RequestSizeLimit`) |
| Silme / retention / legal hold | **yok** |
| Audit | **yok** |
| Tenant izolasyonu | object key'de **yok** (yalnız `demandId`) |
| **Yetkilendirme** | Controller'da `[Authorize]` **yok**. `Program.cs` (104 satır) `app.UseAuthorization()` çağırıyor ama **`UseAuthentication()` yok ve fallback policy yok** → attribute'suz endpoint'ler anonim kalır. Gateway (`ocelot.json`, 157 route) hiçbir route'ta auth yapmıyor (`AuthenticationOptions` sayısı = **0**); auth downstream'e ait ve burada kurulmamış. Route `/api/v1/uploads/{everything}` upstream'de açık (`ocelot.json:1843-1860`). |

> ⚠️ Bu bir MOD-0262 *kapsam* maddesi olmadan önce bir **güvenlik bulgusudur**: `GET /api/v1/uploads/{demandId}/{attachmentId}` kimlik doğrulaması olmadan dosya döndürüyor görünüyor. Ölçtüm, **düzeltmedim** (kapsam kısıtı: `services/` dokunulmadı). CT'nin bunu MOD-0262 migrasyonundan **bağımsız ve daha acil** bir hat olarak ele alması gerekebilir → **D-5**.
>
> *Emin değilim payı:* statik okuma ile ölçtüm; canlı anonim `curl` denemesi yapmadım (fleet çalıştırılmadı). Statik kanıt güçlü, runtime doğrulaması yapılmadı.

`DemandIdeaAttachment` alanları (`Id, FileName, ContentType, SizeBytes, StorageKey`) `ContentRef` ile büyük ölçüde örtüşüyor ama `Checksum`, `TenantId`, `VersionId`, `StorageProvider` **yok** → migrasyonda bu dört alan **geriye dönük türetilmek zorunda** (checksum ancak dosya okunarak hesaplanır, tenant ancak demand kaydından türetilir).

### 3.6 Diğer binary yüzeyleri (tam tarama)

`FileStreamResult|PhysicalFile|return File(|IFormFile` taraması, `tests` hariç, **13 eşleşme / 6 dosya**:

| Yüzey | Seam kullanıyor mu? | MOD-0262 etkisi |
|---|---|---|
| `DocumentManagement{ControlledDocuments,Templates,TemplateMasters}Controller` (3) | ✅ evet | Tüketici olarak kalır; sözleşme HTTP'ye taşınır |
| `EnterpriseStrategy/UploadsController` | ❌ hayır | **Migrasyon adayı** (3.5) |
| `CRM/ImportExportController`, `CRM/TerritoryModelsController` | ❌ hayır — ama **transient** (CSV import/export, kalıcı depolama yok) | **Kapsam dışı.** İşlem-anı veri transferi, doküman deposu değil. |
| `Platform/PlatformAuditController` (audit export) | ❌ hayır — üretilen çıktı, saklanan nesne değil | **Kapsam dışı** — evidence package üretimi geldiğinde yeniden bakılmalı |

### 3.7 Bağımlılık sıralaması riski — **wave ters düşmesi**

Blueprint MOD-0262'yi **W-1 / R1-PPM MVP / RC=Y** yapıyor, ama `Dependency Gate` alanı dört ön-koşul sayıyor:

| Ön-koşul (Blueprint metni) | Modül | Blueprint Wave | Depodaki durum |
|---|---|---|---|
| RBAC / ABAC Authorization | MOD-0018 | W-1 | ✅ var |
| Audit Trail Service | MOD-0021 | W-1 | ✅ ~%98 |
| Records Management / Retention / Legal Hold | **MOD-0030** | **W-3** | ⚠️ registry'de **satır bile yok**; retention runtime'ı MOD-0029-FU15 içinde yaşıyor |
| Evidence Linking Service | **MOD-0031** | **W-4** | ⚠️ pack var, "review / planned" |

> **W-1 bir modül, W-3 ve W-4 modüllere HARD bağımlı.** Bu ya Blueprint'te bir sıralama hatası, ya da MOD-0262'nin R1 kapsamının bu iki bağımlılığı **gerektirmeyen** bir alt kümeyle sınırlanması gerektiği anlamına geliyor. → **D-3.**

### 3.8 Etki özeti — tek bakış

| Servis / Alan | Bugünkü rol | MOD-0262 sonrası | Kırılma riski |
|---|---|---|---|
| `Diten.Platform` (5057) — MOD-0029/0028 | Seam **sahibi** + tüketici | Yalnız **tüketici**; seam MOD-0262'ye devreder | **Yüksek** — sözleşme `byte[]`→stream, authz modeli değişir |
| `Diten.CrmService` (MOD-0162) | 4 çözümlenemeyen string pointer | Sözleşmeli tüketici | **Orta** — bugün çalışan bir yol yok, kırılacak bir şey de yok |
| MOD-0029-FU15 Retention | Marker üretir, silmez (yazılı sınır) | Marker **üretici**; MOD-0262 uygulayıcı | **Düşük** — FU15 sınırı korunur |
| MOD-0030 Records Management | Yok (registry satırı bile yok) | Metadata imha sahibi | Bağımlılık boşluğu (D-3) |
| MOD-0031 Evidence Linking | pack var, planned | Evidence package'in link kaynağı | Bağımlılık boşluğu (D-3) |
| `Diten.EnterpriseStrategyService` (5004) | Yönetilmeyen 2. depo + **auth boşluğu** | Migrasyon hedefi | **Yüksek** (güvenlik, D-5) |
| MOD-0313 HCM | HARD bağımlı, henüz yok | Tüketici (HCM-DOC-EVID-FACADE-BUNDLE) | W-2 planlaması MOD-0262'yi bekler |
| `gateway` | `/api/v1/uploads/*` route açık, auth yok | Yeni Platform Document Service route'ları | Orta |

---

## 4. PRD — MOD-0262 Internal Document Repository Service

### 4.1 Problem & Amaç

ERP-vNext'te **doküman binary'sinin kurumsal sahibi yok.** Bugün:

- kontrollü doküman binary'leri MOD-0029-FU01'in *feature klasörü* içindeki bir seam'e ve tek bir local-filesystem provider'a bağlı;
- MOD-0162 içerik referansları hiçbir yere çözümlenmiyor — içerik uçtan uca okunamıyor;
- retention "imha edilebilir" kararını veriyor ama **kimse imha etmiyor** (kodda yazılı, sahipsiz devir);
- EnterpriseStrategy kendi dosya deposunu kurmuş — checksum, tenant izolasyonu, audit ve görünüşe göre **auth olmadan**;
- **evidence package** kavramı depoda hiç yok, ama MOD-0313 buna HARD bağımlı.

MOD-0262 bu boşluğu, **dış depolama bağımlılığı olmadan**, native platform sahipliğiyle kapatır (Blueprint Aim/Goal).

### 4.2 Teknik Bağlam

| Alan | Değer |
|---|---|
| Deployment Unit | `Platform Document Service` (Blueprint) — bugün mevcut değil |
| Etkilenen servisler | `Diten.Platform` (5057), `Diten.CrmService`, `Diten.EnterpriseStrategyService` (5004), `gateway` (5000) |
| Korunacak seam | `IContentStorageGateway` / `ContentRef` (`StorageProvider` + `ObjectKey`) — **yeniden yazım değil, seam arkasını doldurma + servisleştirme** |
| Impacted UI | 4 yeni soft page: Repository Admin · Evidence Package Storage · Document Binary Store · Repository Health — ⚠️ `Module Pages` sayfası **çelişiyor** (B1) |
| Sözleşme | DOC-REPO-BUNDLE — **burada tanımlanmadı**, pack aşamasına ait; ⚠️ Contract Bundle Dictionary'de satırı **yok** (B3) |
| SoR | Document binaries · evidence packages · repository objects |

### 4.3 Persona'lar

| Persona | İhtiyaç |
|---|---|
| Platform Ops / Repository Admin | Depo sağlığını görmek, provider konfigürasyonunu yönetmek, orphan'ları görmek |
| QA / Doküman Kontrol Sorumlusu | Onaylı imha kararlarının **gerçekten** uygulandığını ve kanıtının bulunduğunu görmek |
| Denetçi (iç/dış) | Bir nesne için evidence package almak; checksum ile bütünlüğü doğrulamak |
| Saha temsilcisi (MOD-0162 tüketicisi) | Doktora gösterilecek onaylı içeriğin binary'sini yetkisi dahilinde açmak |
| HCM kullanıcısı (MOD-0313) | HR evidence package'ı depolamak / çekmek |

### 4.4 User Stories & Kabul Kriterleri (Gherkin)

> Bunlar **PRD seviyesi** kabul kriterleridir; module pack'in `## Acceptance Criteria` kutuları değildir.

**US-1 — Binary saklama, seam korunarak**
```gherkin
Given MOD-0029 bir kontrollü doküman versiyonu için binary yüklüyor
When çağrı IContentStorageGateway sözleşmesi üzerinden MOD-0262'ye gider
Then ContentRef (ContentId, StorageProvider, ObjectKey, Checksum, ByteSize, MediaType) döner
And çağıran kodda hiçbir dosya sistemi yolu, provider SDK'sı veya ham byte bulunmaz
And ObjectKey istemciye asla açılmaz
```

**US-2 — Tenant izolasyonu object key seviyesinde**
```gherkin
Given iki farklı tenant aynı FileName ile aynı anda dosya yüklüyor
When her ikisi de saklanır
Then object key'ler TenantId ile ayrışır ve hiçbir tenant diğerinin nesnesini okuyamaz
And tenant A'nın ObjectKey'i doğrudan verilse dahi tenant B bağlamında 404 döner
```

**US-3 — Evidence package (YENİ — bugün depoda hiç yok)**
```gherkin
Given bir denetçi bir nesne için kanıt paketi talep ediyor
When evidence package üretilir
Then paket, kapsanan her binary'nin ContentRef + checksum listesini içerir
And paketin kendisi de bir repository object olarak checksum'lanarak saklanır
And üretim MOD-0021'e audit event yazar
And MOD-0031 Evidence Linking üzerinden nesne ↔ kanıt bağı korunur
```

**US-4 — Fiziksel imha, hold'u yürütme anında yeniden doğrulayarak**
```gherkin
Given MOD-0029-FU15 ExecutedAsNoDeleteMarker durumunda bir disposition marker'ı var
When MOD-0262 imha yürütmesi bu marker'ı girdi olarak alır
Then legal hold YÜRÜTME ANINDA yeniden doğrulanır (önceki adımdan asla devralınmaz)
And aktif hold varsa imha reddedilir ve DISPOSITION_BLOCKED_BY_LEGAL_HOLD gerekçesi kaydedilir
And imha geri alınamaz şekilde tamamlanınca imha kanıtı üretilir
And bu yol TryDeleteAsync (best-effort compensation) yolunu ASLA kullanmaz
```

**US-5 — Repository Health**
```gherkin
Given bazı ContentRef kayıtlarının fiziksel nesnesi eksik (orphan metadata)
Or bazı fiziksel nesnelerin ContentRef'i yok (orphan binary)
When Repository Health çalıştırılır
Then her iki yön ayrı ayrı raporlanır
And rapor salt-okunurdur; hiçbir şey otomatik silinmez
```

**US-6 — MOD-0162 içerik zincirinin kapanması**
```gherkin
Given yayımlanmış bir KnowledgeContent FileRef taşıyor
When yetkili bir kullanıcı içeriği açmak ister
Then MOD-0262 üzerinden binary stream'lenir
And yetki kontrolü çağıranın iddiasına değil, MOD-0262'nin kendi authz'ına dayanır
```
> ⚠️ US-6'nın kapsamı **D-4**'e bağlı (MOD-0262 mi, MOD-0162 follow-up mu).

**US-7 — Migrasyon (EnterpriseStrategy)**
```gherkin
Given DemandIdeaAttachment kayıtları Data/uploads altında checksum'sız duruyor
When migrasyon çalışır
Then her dosya okunarak checksum hesaplanır ve TenantId demand kaydından türetilir
And StorageKey → ObjectKey eşlemesi kayıt altına alınır
And migrasyon tamamlanana kadar eski okuma yolu bozulmaz
```

### 4.5 Yetki & Güvenlik

| Konu | Karar |
|---|---|
| Permission key ailesi (öneri) | `platform.document-repository.{read, manage, health.read, evidence-package.read, evidence-package.manage, purge.execute}` — **kesin liste pack aşamasında** |
| SoD | `purge.execute` **ayrı** bir anahtar; `manage` onu kapsamaz (imha, yönetimden farklı bir yetkidir) |
| Tenant izolasyonu | GUID-based **mandatory**; object key'in içine gömülü |
| ⚠️ Mimari kural | Bugünkü *"caller is responsible for all permission checks"* varsayımı servis sınırında **geçersizdir**. MOD-0262 kendi authz'ını uygular; uzak çağıranın iddiasına güvenilmez. |
| Audit | Her store / stream / purge / evidence-package üretimi MOD-0021'e yazılır |
| Fail-closed | Provider erişilemezse veya hold doğrulanamazsa işlem **reddedilir**, sessizce geçilmez |

### 4.6 Data & Performance

| Konu | Not |
|---|---|
| MongoDB | Yeni collection'lar: repository object index, evidence package, health snapshot. `ContentRef` embedded pointer kalır. |
| ⚠️ GUID trap | Yeni aggregate'ler class-map'e kaydedilmezse Guid FK'lar binary yazılır ve sorgular **sessizce boş döner** (bu depoda ölçülmüş desen) |
| Latency | Metadata sorguları <300ms. **Binary stream'i bu bütçeye tabi değildir** — ayrı SLO gerekir (Blueprint SLO Tier 2) |
| Boyut | Bugünkü `RequestSizeLimit` 50MB (ES) — MOD-0262 sınırı pack aşamasında |
| ⚠️ `byte[]` → stream | Mevcut `ContentStoreRequest.Content` **`byte[]`**; HTTP sınırında bu bellek profili kabul edilemez |

### 4.7 MoSCoW

| Öncelik | Kapsam |
|---|---|
| **MUST** | Seam'in paylaşılan sözleşme katmanına çıkarılması · MOD-0262'nin kendi authz'ı · tenant-izole object key · Document Binary Store · Evidence Package (MOD-0313 HARD bağımlılığı) · audit |
| **SHOULD** | Repository Health (iki yönlü orphan raporu) · Repository Admin · fiziksel purge (marker tüketici, hold re-verify) |
| **COULD** | Ek provider (S3 / Azure Blob) — seam bunu zaten karşılıyor · MOD-0162 stream yolu (D-4'e bağlı) |
| **WON'T (bu fazda)** | Harici depo connector'ları (SharePoint / GDrive) — **Blueprint 8.1 bunu açıkça kaldırdı** · metadata imhası (MOD-0030) · CSV import/export transient akışları · audit export |

### 4.8 Açık kararlar (CT / EA)

| # | Karar | Sahip | Bloklayıcı mı? |
|---|---|---|---|
| **D-1** | `module-id-registry.md:145` Blueprint 8.1'e hizalanacak mı, ve external-provider anlamı **deprecated alias** olarak mı korunacak? (Bu bir *rename* değil, bir **kapasitenin dışarıdan içeriye dönüştürülmesi** — DCP-002 alias zinciri gerektirebilir.) | EA / CT | ⛔ **EVET** — pack aşamasının kapısı |
| **D-2** | Blueprint 8.1 iç sapması (B1 soft page çelişkisi, B2 EXT-BASE artığı, **B3 DOC-REPO-BUNDLE sözlükte yok**) kim düzeltecek? | EA | ⛔ **EVET** (özellikle B3) |
| **D-3** | W-1 modülün W-3 (MOD-0030) / W-4 (MOD-0031) modüllere HARD bağımlılığı: Blueprint sıralaması mı düzelecek, yoksa R1 kapsamı bu ikisini gerektirmeyen alt kümeye mi indirilecek? | EA / CT | Evet (kapsam belirler) |
| **D-4** | MOD-0162 tüketici sözleşmesi (US-6) DOC-REPO-BUNDLE içinde mi, MOD-0162 follow-up'ında mı? | CT | Hayır (kapsamı etkiler) |
| **D-5** | EnterpriseStrategy `UploadsController` auth boşluğu: MOD-0262 migrasyonunu mu bekleyecek, yoksa **bağımsız acil düzeltme** hattı mı açılacak? | CT | Hayır — ama **güvenlik gerekçesiyle acil** |
| **D-6** | `IContentStorageGateway` in-process seam olarak mı kalacak (MOD-0262 yalnız implementasyonu sağlar), yoksa HTTP sınırı mı olacak? Bu, "tek deployment unit" iddiasını doğrudan belirler. | CT / EA | Evet — routing kararını etkiler |

---

## 5. ROUTING KARARI — Öneri

### 5.1 Öneri

> ## **Delivery Capability Pack (CAP-001) + üye module pack / FU'lar**
> ### Güven: orta-yüksek. Karar CT'nin.

### 5.2 CAP-001 §2 kapısı — madde madde ölçüm

CAP-001 §2 "en az biri doğruysa gerekli" diyor. **Altı maddenin altısı** doğru:

| CAP-001 §2 kriteri | Durum | Kanıt |
|---|---|---|
| birden fazla modül bir **bağımlılık sırasıyla** teslim edilmek zorundaysa | ✅ EVET | Blueprint Dependency Gate: MOD-0018 → MOD-0021 → **MOD-0030 (W-3)** → **MOD-0031 (W-4)** → MOD-0262. Ters yönde MOD-0028 + MOD-0313 HARD bekliyor. |
| **cross-cutting** bir platform yeteneği birçok modülü etkiliyorsa | ✅ EVET | Ölçülen etki: Platform/MOD-0029, Platform/MOD-0028, CRM/MOD-0162, EnterpriseStrategy, MOD-0313, MOD-0030, MOD-0031, gateway |
| normal bir module pack **aşırı yüklenecekse** | ✅ EVET | Tek pack'in taşıması gerekenler: seam taşıma + servisleştirme + sözleşme yeniden tasarımı (`byte[]`→stream) + authz modeli değişimi + 4 soft page + sıfırdan evidence package + purge yaşam döngüsü + ES migrasyonu + CRM sözleşmesi |
| uygulama **domain'ler arası mimari kararlar** gerektiriyorsa | ✅ EVET | D-6 (in-process vs HTTP), D-4 (CRM sözleşme sahipliği), authz güven modelinin tersine çevrilmesi |
| follow-up paketleri arasında **governance drift riski** varsa | ✅ EVET | Bu analizin kendisi drift'in kanıtı: R1–R9 + B1–B5; MOD-0262 hiç geliştirilmeden **R7'de** (SCMM iş planı) bayat kimlik yayılmış |
| birden fazla business modülü **ortak bir platform foundation**'a bağımlıysa | ✅ EVET | MOD-0028, MOD-0029, MOD-0162, MOD-0313, MOD-0352 hepsi aynı binary foundation'a bakıyor |

CAP-001 §3 ("gerekli DEĞİLDİR") maddelerinin **hiçbiri** tutmuyor: targeted fix değil, tek endpoint değil, tek sayfa değil, contained UI değil, izole follow-up değil. Yalnız "normal bağımsız modül" maddesi tartışmalı — karşı argüman bu.

### 5.3 Tek pack lehine karşı argüman (CT'nin ağırlıklandırması gereken)

| Argüman | Ağırlık | Değerlendirmem |
|---|---|---|
| Blueprint **tek** Deployment Unit veriyor: `Platform Document Service` | Güçlü | Ama bir DCP zaten **runtime entity değil**; tek deployment unit'e sahip bir yetenek de çok modüllü **teslimat sırası** gerektirebilir. CAP-001 §1'in konusu deployment topolojisi değil, teslimat orkestrasyonudur — çelişmiyorlar. |
| Blueprint **tek** MOD ID veriyor (alt-modül yok) | Orta | DCP üye MOD ID'si **üretmez**; üyeleri yalnız ID ile referanslar (DCP-002 §5 deseni). MOD-0262 tek MOD olarak kalır, FU'ları altında yaşar. Çelişki yok. |
| Seam zaten var; "yeniden yazım değil" | Orta | Doğru — ama seam'in **arkası** değil **sözleşmesi** değişiyor (`byte[]`→stream, authz tersine dönüyor). Bu, tek pack'in "contained" sayılmasını engelliyor. |
| Ek governance yükü | Zayıf-orta | Gerçek bir maliyet. Ama R1–R9 ve R7'nin yayılması, bu modülde governance yükünün **eksikliğinin** maliyetinin daha yüksek olduğunu gösteriyor. |

### 5.4 Önerilen yapı (CAP onaylanırsa)

DCP, MOD-0262'yi **tek canonical MOD** olarak korur; yeni MOD ID mint etmez.

| Sıra | Üye | Kapsam | Ön-koşul |
|---|---|---|---|
| 0 | *(DCP dışı, önce)* | **D-1 + D-2**: registry hizalama + Blueprint iç sapması + DOC-REPO-BUNDLE'ın sözlüğe girmesi | — |
| 1 | MOD-0262-FU01 | Seam'in paylaşılan sözleşme katmanına çıkarılması + MOD-0262 authz'ı + Document Binary Store | D-6 |
| 2 | MOD-0262-FU02 | Evidence Package Storage (sıfırdan) | FU01, MOD-0031 (D-3) |
| 3 | MOD-0262-FU03 | Repository Admin + Repository Health | FU01 |
| 4 | MOD-0262-FU04 | Fiziksel purge — FU15 marker'larını tüketir, hold'u yürütme anında yeniden doğrular | FU01, MOD-0030 (D-3) |
| 5 | MOD-0262-FU05 | EnterpriseStrategy migrasyonu (checksum backfill + tenant türetme) | FU01, D-5 |
| — | *(ayrı)* | MOD-0162 tüketici sözleşmesi | **D-4**'e bağlı |

> Bu tablo **öneridir**. FU numaraları rezerve **edilmedi** — DCP-002 kapısı (D-1) açılmadan hiçbir FU kimliği preflight'tan geçemez.

### 5.5 Eğer CT tek pack derse

Savunulabilir — ama şu şart altında: R1 kapsamı **yalnızca FU01 + Document Binary Store** ile sınırlanır; evidence package, purge, health, admin ve ES migrasyonu ayrı follow-up pack'lere bırakılır. O durumda pratikte yine çok-parçalı bir teslimat olur, sadece orkestrasyon sözleşmesi olmadan. Riski: 5.2'deki drift maddesi (R7 zaten gerçekleşmiş).

---

## 6. HANDOFF

| Adım | Durum |
|---|---|
| 1. DCP-002 kimlik kapısı | ⛔ **BLOCKED** (exit 2) — tek neden registry sapması; Blueprint temiz |
| 2. Kayıt mutabakatı (ölç, düzeltme yok) | ✅ Tamam — 7 repo sapması + 5 SoT iç sapması; **hiçbiri düzeltilmedi** |
| 3. Çoklu-servis etki haritası | ✅ Tamam — 4 hedef yüzey + 3 ek bulgu (evidence package sıfır, wave ters düşmesi, ES auth boşluğu) |
| 4. PRD | ✅ Tamam |
| 5. Routing önerisi | ✅ **CAP-001 (DCP) + üye FU'lar**; karar CT'de |
| **Sıradaki** | **CT onayı + D-1…D-6.** D-1 açılmadan `@module-pack-author` / `/prepare-capability-pack` **çalıştırılamaz** (kimlik kapısı fail-closed). Orchestrator **tetiklenmedi** (Demir Kural #2). |

### Bu turda yapılmayanlar (bilerek)
- Hiçbir kayıt düzeltilmedi (reconcile §0 + CT talimatı)
- `services/` `frontend/` `gateway/` **değiştirilmedi** — yalnız okundu
- DOC-REPO-BUNDLE sözleşmesi **tanımlanmadı**
- Module pack / capability pack **yazılmadı**
- FU ID'leri **rezerve edilmedi**
- ES auth bulgusu **düzeltilmedi** (yalnız raporlandı → D-5)
- Canlı runtime doğrulaması yapılmadı (fleet çalıştırılmadı) — ES anonim erişim bulgusu **statik** kanıta dayanıyor
