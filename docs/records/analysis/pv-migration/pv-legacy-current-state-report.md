# PV Legacy → ERP-vNext Taşıma & Doğruluk Analizi — Ana Rapor

> Durum: **ANALİZ (salt okuma).** Kod/dosya değiştirilmedi, taşıma çalıştırılmadı.
> Tarih: 2026-08-03 · Kapsam: 6 legacy repo + ERP-vNext (`C:\Users\user\Desktop\ERP-vNext`)
> Her iddia dosya yolu + sınıf/method/route kanıtıyla verilmiştir. Kanıtlanamayanlar açıkça "kanıtlanamadı" olarak işaretlenmiştir.

---

## 1. Executive verdict

**Legacy PV sistemi çalışan, veri kaydeden bir uygulamadır — ancak GxP/kayıtlı-sistem (validated system) seviyesinde DEĞİLDİR ve mevcut kodu ERP-vNext'e doğrudan taşımak yanlıştır.**

Kanıtlanan çekirdek gerçek: Safety Report (vaka) intake akışı UI → JS → `PvOrganization` API → MediatR handler → **MongoDB `PvOrganization` veritabanına gerçekten yazıyor** (dosya ekleri dahil). Yani "UI shell" değil; gerçek persistence var.

Ancak sistemin uyumluluk/üretim güvenilirliği düşüktür ve bu **koddan doğrudan** görülmektedir:

- **Audit trail YOK.** Tüm PV backend'lerinde `AuditTrail`/`AuditLog` = 0 eşleşme. Sadece `BaseEntity.CreatedBy/ModifiedBy` metadata alanları var (denetim izi değil).
- **E-signature YOK.** `ESignature`/`e-signature` = 0.
- **Endpoint-seviyesinde kimlik doğrulama/yetkilendirme YOK.** Tüm PV backend'lerinde tek `[Authorize]` `UserService/EmailController` üzerinde. `OrganizationService` `app.UseAuthorization()` çağırıyor ama `app.UseAuthentication()` ve JWT konfigürasyonu **yok** → gerçek koruma yok.
- **Tenant izolasyonu host/domain tabanlı**, kimlik tabanlı değil (`TenantResolutionMiddleware`: `context.Request.Host.Host` → `Tenant.Domain`). Servisler-arası çağrı `localhost:5000/.../GetTenantId`'yi **auth context iletmeden** yapıyor → izolasyon kırılgan.
- **Test yok.** Tek test projesi `Diten.Pv.TenantService.Tests` (reference-data testleri). Safety/Regulatory/User/Lookup/Survey için 0 test.
- **MedDRA / WHO Drug kodlama sözlüğü YOK** (0 eşleşme). Signal yönetimi = sadece bir bool bayrak (`SignalDetectionParticipation`).
- **Workflow motoru, background job, message bus / domain event YOK.** Durumlar manuel enum çevirmesiyle yönetiliyor; servisler senkron HTTP (Flurl) ile kuplajlı.

**"Validated shared Di10-PV database" iddiası koddan doğrulanamıyor.** "Di10-PV" kodda yalnızca **uygulama/marka adıdır** (login sayfası başlığı, OG meta, Google `ApplicationName = "Di10-PV"`). Böyle adlandırılmış bir veritabanı **yoktur**; gerçek kalıcılık 5 ayrı MongoDB'dir (`PvOrganization`, `PvTenant`, `PvUser`, `PvLookup`, `PvSurvey`, hepsi `mongodb://localhost:27017`). "Validated" iddiasını destekleyen hiçbir validation package / IQ-OQ-PQ / controlled release kanıtı repo'da yok. **FSAD** terimi kodda hiç geçmiyor.

**ERP-vNext'te PV/Safety/Regulatory Affairs domaini henüz YOKTUR** (greenfield). Modül registry'sinde `safety`/`regulatory`/`pharmacovigilance`/`adverse`/`ICSR`/`QPPV`/`PSMF`/`MedDRA` için 0 rezervasyon. Legacy `PharmacovigilanceWeb` yalnızca **PPM** yeteneği için referans (portfolio-delivery / DCP-003), dosyalar kopyalanmıyor. Buna karşılık ERP-vNext'in **paylaşımlı platform kabiliyetleri (RBAC, Audit, Workflow, Notification, Document Management, Reference Data, Org/Person/Position)** legacy'den çok daha güçlüdür ve PV'nin yeniden inşası için doğru temeldir.

**Nihai karar: CONDITIONAL GO** — PV ERP-vNext'te **iş kuralı yeniden uygulaması + veri migrasyonu** olarak inşa edilmeli; legacy kod **lift-and-shift edilmemeli**. GO şartı: (a) dokümanlardaki "validated Di10-PV database" iddialarının düzeltilmesi, (b) greenfield PV domain boundary'sinin registry'de açılması, (c) paylaşımlı platform modüllerinin (Audit/Workflow/RBAC/DocMgmt) tüketilmesi.

---

## 2. Scope and repositories inspected

| Repo | Rol | Teknoloji (kanıt) |
|---|---|---|
| `C:\CRM2\DitenPvOrganization` | **PV çekirdek backend** (Safety Report, Regulatory Report, Marketing Authorization, Organization, Lcppv/reconciliation, GlobalSku, Agreement) | .NET 8, MongoDB, CQRS/MediatR — `appsettings.json: DatabaseName=PvOrganization` |
| `C:\CRM2\DitenPvTenant` | Tenant + tenant-scoped reference (country/authority/active-ingredient/brand/pharmaceutical-form) | .NET 8, MongoDB `PvTenant`; host-based `TenantResolutionMiddleware`; **tek test projesi** |
| `C:\CRM2\DitenPvUser` | Kullanıcı/rol + auth + email(Gmail) + calendar | .NET 8, MongoDB `PvUser`; tek `[Authorize]` burada |
| `C:\CRM2\DitenPvLookup` | Global reference (yalnızca `Country` entity) | .NET 8, MongoDB `PvLookup` — **tek entity → gerçek bounded context değil** |
| `C:\CRM2\DitenPvSurvey` | Genel anket motoru (LCPPV reconciliation formu için) | .NET 8, MongoDB `PvSurvey` |
| `C:\CRM2\PharmacovigilanceWeb\Pharmacovigilance.WebUI` | ASP.NET MVC frontend (Sneat template) | Controller'lar thin `return View()`; veri `wwwroot/assets/js` fetch() ile gateway'e |

İncelenen View klasörleri (görev şartı): `PvSystem`, `SafetyReport`, `RegulatoryAffair`, `Registration`, `PharmaceuticalForm` — tümü statik shell + `Pagejs/**` JS backend çağrıları. Ayrıca `ProductRecord`, `ActiveIngredient` incelendi.

ERP-vNext: `execution/registries/module-id-registry.md`, `execution/domains/*`, `services/*`, `AGENTS.md`, MDM domain-config incelendi.

---

## 3. Evidence methodology

- **UI varlığı ≠ implementation.** cshtml view'lar `return View()` shell'dir; gerçek davranış `wwwroot/assets/js/Pagejs/**` içindeki `fetch()` çağrılarında ve backend MediatR handler'larında aranmıştır.
- **Persistence kanıtı** = handler içinde `_repository.Create/Update` + Mongo repository. İzlendi: `CreateSafetyReportHandler` → `_repository.Create(safetyReport)`.
- **Runtime smoke YAPILMADI** (fleet ayağa kaldırılmadı). Dolayısıyla "çalışıyor" iddiaları kod-yolu kanıtına dayanır; canlı doğrulama gerektiğinde "runtime doğrulanmadı" denmiştir.
- **Compliance iddiaları** yalnızca validation/IQ-OQ-PQ/controlled-release kanıtı varsa kabul; yoksa en fazla UNPROVEN.
- Güven seviyeleri: High (birebir kod), Medium (dolaylı/örüntü), Low (çıkarım).

---

## 4. Legacy architecture

**Topoloji:** 5 mikroservis (Clean Architecture: `Domain / Application / Infrastructure / Persistence / Core / API`) + 1 MVC frontend + bir gateway (`localhost:5000`, `/services/PvTenant`, `/services/PvOrganization`, ...). Frontend `config.js` gateway ve legacy user servisini (`:5050`) tanımlar.

**Veri katmanı:** MongoDB, servis başına ayrı DB. SQL Server connection string'leri mevcut ama `DatabaseType=MongoDb` → **SQL Server ölü konfigürasyon**. `BaseEntity` Mongo `ObjectId` + `Status`(soft-delete bool) + `Created/ModifiedDate/By`.

**Entegrasyon:** Senkron HTTP (`Flurl`) — `TenantAPIs`/`UserAPIs` doğrudan `http://localhost:5000/...` çağırır. Message bus/event YOK.

**Güvenlik/izolasyon (High confidence, kritik borç):**
- `TenantResolutionMiddleware` tenant'ı **Host header**'dan çözer (`GetByDomainAsync(host)` / `GetBySubDomainAsync`).
- Servisler-arası `GetTenantId()` (`Infrastructure/APIs/TenantAPIs.cs`) auth token iletmeden `localhost:5000`'e gider → host=`localhost` → tenant çözümü kırılgan/spoof'lanabilir.
- Endpoint'lerde `[Authorize]` yok; `CORS AllowAll`. Rol modeli (`User.RoleIds`) var ama **veri API'lerinde uygulanmıyor**.

**Dosya/ek:** `wwwroot\SafetyReport` altında **servis diskine** yazılıyor (object storage/DocMgmt değil); `Document` hem `SafetyReport.Documents` içinde gömülü hem ayrı `Document` koleksiyonunda; yeniden yüklemede eski `Status=false`.

Detaylar için: [Capability inventory](pv-legacy-capability-inventory.md), [Reuse matrix](pv-code-reuse-and-migration-matrix.md).

---

## 5. Legacy capability inventory (özet)

Tam matris: [pv-legacy-capability-inventory.md](pv-legacy-capability-inventory.md). Öne çıkanlar:

- **IMPLEMENTED_AND_PROVEN (kod-yolu):** Safety Report CRUD + ekleri; Marketing Authorization (Registration) CRUD; Regulatory Report + Task board; LCPPV Monthly Reconciliation (anket); Organization/GlobalSku/Agreement CRUD; Tenant + reference (country/authority/ingredient/brand/pharma-form); User/role CRUD + Gmail email + calendar.
- **PARTIAL:** Patient (ayrı entity ama zayıf bağ), Reporter (string), Causality (enum), Seriousness/Expectedness (bool), Submission hazırlığı (alanlar var, otorite gönderimi yok).
- **UI_ONLY / DATA_MODEL_ONLY:** Case narrative (serbest metin), Follow-up (string alan; `FollowUpTracker` sınıfı persist edilmiyor), Case versioning (`Version` string).
- **NOT_FOUND:** MedDRA, WHO Drug, Signal management (yalnızca bool), Duplicate detection (manuel bool), Audit trail, E-signature, Workflow engine, Message bus, gerçek Authority/E2B reporting.
- **CONFIGURATION_ONLY:** SQL Server bağlantıları (kullanılmıyor).

---

## 6. Proven end-to-end flows

Detay: [pv-legacy-capability-inventory.md](pv-legacy-capability-inventory.md) §B. Özet izler:

1. **Safety Report intake (PROVEN persistence):** `Views/SafetyReport/AddSafetyReport.cshtml` → `Pagejs/SafetyReport/AddSafetyReport.js` `POST .../PvOrganization/SafetyReport/CreateSafetyReport` → `SafetyReportController.CreateSafetyReport` → `CreateSafetyReportHandler` → `_repository.Create` (Mongo `PvOrganization`). Ek: `CreateSafetyReportDocuments` → disk `wwwroot/SafetyReport`. **Validation minimal** (yalnızca tenant/CountryId/GlobalSkuId boş kontrolü → 409). Audit/notification/workflow adımı **YOK**.
2. **Authorization izi:** Endpoint'te auth yok; "yetki" = frontend menü gizleme + host-based tenant. → **Sahte/yetersiz** olarak işaretlendi.
3. **Tenant izolasyon izi:** `GetSafetyReports` `x.TenantId == tenantId` ile filtreliyor; ancak `tenantId` host-based çözülüyor → izolasyon domain'e bağlı, kullanıcıya değil.
4. **Attachment izi:** `CreateSafetyReportDocuments` → local disk + `Document` koleksiyonu.
5. **Reconciliation izi:** `LcppvMonthlyReconcilationController` → LCPPV aylık **anket** (`GetQuestions`, `CreateLcppv`) + ekler. **Veritabanı-veritabanı vaka mutabakatı DEĞİL.**

---

## 7. ERP-vNext current-state comparison

Tam matris: [pv-erpvnext-capability-gap-matrix.md](pv-erpvnext-capability-gap-matrix.md).

- **PV çekirdek (safety case, patient, reporter, event, causality, submission, signal, MedDRA):** ERP-vNext'te **NOT_IMPLEMENTED** (registry'de 0 rezervasyon).
- **Product/SKU master (GlobalSku karşılığı):** `MOD-0290` **draft**, `runtime_code_allowed:false` → CONTRACT_ONLY.
- **Reference Data:** `MOD-0048` ready-for-dev (pack var) → FOUNDATION/PARTIALLY_AVAILABLE.
- **Org/Person/Position:** `MOD-0288` **done** (runtime Platform'da) → FULLY/ PARTIALLY_AVAILABLE (MAH/QPPV person seam).
- **RBAC/ABAC:** `MOD-0018` foundation + tenant-scoped JWT (AuthService) → PARTIALLY_AVAILABLE, legacy'den güçlü.
- **Audit:** `MOD-0021` ready-for-dev / implemented evidence → PARTIALLY_AVAILABLE.
- **Workflow:** `MOD-0023` review/planned → FOUNDATION/CONTRACT_ONLY.
- **Notification:** `MOD-0027` approved (pack) → PARTIALLY_AVAILABLE.
- **Document/Evidence:** `MOD-0028` review + `MOD-0029` Controlled Documents (runtime kanıtı bu repo'da mevcut) → PARTIALLY_AVAILABLE.

**Sonuç:** ERP-vNext PV-özel yeteneği ~%0; ancak PV'yi taşımak için gereken **yatay platform** kısmen/büyük ölçüde hazır.

---

## 8. Reuse classification (özet)

Tam matris: [pv-code-reuse-and-migration-matrix.md](pv-code-reuse-and-migration-matrix.md).

- **DIRECT_REUSE:** ~yok. (Farklı mimari, güvensiz, testsiz.)
- **ADAPTER_REUSE:** Legacy okuma API'leri geçici migration okuyucusu olarak (read-only).
- **BUSINESS_RULE_REIMPLEMENT:** Safety case alan seti, MA lifecycle (0-5), LCPPV süreci, seriousness/causality vokabülerleri → ERP-vNext aggregate'lerinde yeniden.
- **DATA_MIGRATION_ONLY:** Mongo koleksiyonları (SafetyReport, MarketingAuthorization, Organization, Lcppv, Patient) + ekler.
- **REFERENCE_ONLY:** cshtml/JS ekranlar (terminoloji/UX referansı).
- **RETIRE:** `TenantResolutionMiddleware` (host-based), auth boşluğu, SQL Server config, `DitenPvLookup` (tek entity), senkron Flurl kuplajı.

---

## 9. Data migration assessment (özet)

Tam: [pv-data-migration-assessment.md](pv-data-migration-assessment.md). Kritik noktalar: Mongo `ObjectId`'ler ERP'ye **`ExternalReference`** olarak korunmalı; tenant eşlemesi host→tenant tablosundan yeniden kurulmalı; **attachment'lar servis diskinde** (path taşınabilirlik riski); case number (`TrackingNumber`) immutability kod-zorunlu değil; **follow-up/version geçmişi yapısal tutulmadığı için taşınamaz** (yalnızca son durum).

---

## 10. SoR and boundary proposal (özet)

Tam: [pv-target-architecture-recommendation.md](pv-target-architecture-recommendation.md). İlke: RBAC→MOD-0018, Workflow→MOD-0023, Audit→MOD-0021, Evidence→MOD-0028/0029, Notification→MOD-0027, Reference→MOD-0048, Product→MOD-0290, Org/Person→MOD-0288. PV modülü bunları **yeniden üretmez**; yalnızca Safety Case / Regulatory Submission / PV Organization aggregate'lerini sahiplenir.

---

## 11. Eleven MUST FIX document claim audit (özet)

Tam: [pv-document-claim-validation-report.md](pv-document-claim-validation-report.md) + CSV. Genel sonuç: 11 iddianın hiçbiri koddan **TRUE** olarak doğrulanamadı. "Di10-PV" = marka adı; tek "validated shared database" **kanıtlanamadı (UNPROVEN/FALSE)**; reconciliation kaynağı kodda LCPPV anketidir; FSAD kodda yok. Detaylı verdict tablosu ilgili dosyada.

---

## 12. Compliance and validation gaps (özet)

Tam: [pv-erpvnext-capability-gap-matrix.md](pv-erpvnext-capability-gap-matrix.md) §Gap. P0: audit trail, e-signature, endpoint auth, validation evidence, tenant isolation. P1: MedDRA/WHODrug, case versioning, structured follow-up, duplicate detection, authority reporting.

---

## 13. Weighted completion percentages

Tam formüller: [pv-final-go-no-go-report.md](pv-final-go-no-go-report.md) §J. Özet (ağırlıklı, dosya adedi değil):

| Ölçüt | Değer | Güven |
|---|---|---|
| Legacy fonksiyonel tamamlanma | **%35–45** | Medium |
| Legacy üretim/uyumluluk güvenilirliği | **%10–20** | Medium-High |
| Legacy kod doğrudan yeniden kullanılabilirlik | **%5–10** | High |
| Legacy iş kuralı yeniden kullanılabilirlik | **%55–70** | Medium |
| Legacy veri migrate edilebilirlik | **%50–70** | Medium-Low |
| ERP-vNext PV hedefini mevcut karşılama | **%10–18** | Low-Medium |
| ERP-vNext'e taşıma için kalan geliştirme | **%82–90** | Low-Medium |
| Doküman iddialarının kanıtlanması | **%0–10** | High |

---

## 14. Target architecture

Bkz. [pv-target-architecture-recommendation.md](pv-target-architecture-recommendation.md).

## 15. Migration roadmap

Bkz. [pv-migration-roadmap.md](pv-migration-roadmap.md) (Phase 0–9, zorunlu cutover kapıları dahil).

## 16. Risks and stop-ship findings

- **P0 (STOP-SHIP):** Legacy'de audit/e-sig/auth/validation yokluğu → mevcut sistem GxP kayıtlı-sistem olarak sunulmamalı. Dokümanlardaki "validated Di10-PV database" ifadesi düzeltilmeden compliance dayanağı yapılmamalı.
- **P0:** Host-based tenant izolasyonu → veri sızıntı riski; ERP-vNext'te JWT/tenant-scoped ile değiştirilmeli.
- **P1:** Attachment'ların servis diskinde olması → migration'da erişilebilirlik/bütünlük riski.
- **P1:** Follow-up/version geçmişinin yapısal olmaması → tarihsel izlenebilirlik kaybı.

## 17. Final GO / CONDITIONAL GO / NO-GO decision

**CONDITIONAL GO.** Detay ve 11 nihai soru cevabı: [pv-final-go-no-go-report.md](pv-final-go-no-go-report.md).

## 18. ASSUMPTION / UNKNOWN / 🔴 TBD register

- **UNKNOWN (runtime doğrulanmadı):** Canlı fleet ile Safety Report create smoke; gerçek Mongo veri hacmi/kalitesi; iki MAH'ın tenant konfigürasyonu (host eşlemesi).
- **ASSUMPTION:** Gateway `localhost:5000` Ocelot benzeri; `DitenPPM`/PPM ERP-vNext'te portfolio-delivery olarak ele alınıyor (registry teyitli).
- **🔴 TBD:** "Validated" kanıt paketinin repo dışında (QMS/eQMS) olup olmadığı — bu repo'da yok; doküman sahibinden istenmelidir. FSAD'ın kod dışı bir kavram/sistem olup olmadığı.
