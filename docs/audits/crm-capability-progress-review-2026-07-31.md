# CRM Capability Progress Review — Yapılanlar, Kalanlar ve Önerilen Yol Haritası

> Tarih: 2026-07-31 · Kapsam: Commercial Suite (CRM) — MOD-0149 / MOD-0150 / MOD-0151 · Hedef tenant: `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
> Tür: **durum değerlendirme raporu** — kod yazılmadı, runtime değiştirilmedi, smoke çalıştırılmadı.
> **Final verdict: PARTIAL** (gerekçe §14.5).

---

## 1. Executive Summary

CRM tarafında bugün gelinen nokta: **müşteri/kontak master katmanı bitmiş, territory katmanı %60 civarı, satış (lead/opportunity/forecast) ve saha (visit/route) katmanı hiç başlamamış** durumda.

| Blok | Durum | Kanıt |
|---|---|---|
| CRM service scaffold + gateway + RBAC + reference data + 7 dil | **PASS** | `docs/audits/diten-crm-service-scaffold-implementation-2026-07-14.md` |
| MOD-0149 Customer 360 / Account / WorkPlace | **PASS (Review-ready %95)** | `mod-0149-final-review-hardening-closeout.md`, registry satır 103 |
| MOD-0150 Contact & Relationship | **PASS (Closeout %100)** | `mod-0150-final-validation-closeout.md` |
| MOD-0151 Territory Management | **PARTIAL (FU01–FU05 kod PASS, FU05 canlı closeout eksik, FU06–FU09 yok)** | FU01–FU05 + FU04A audit'leri |
| MOD-0152/0153/0154/0155 (Lead / Opportunity / Forecast / Field Sales) | **NOT STARTED** | registry satır 107–110 |

**Territory Management hangi seviyede?** 9 FU'luk plandan FU01, FU02, FU02A, FU02B, FU03, FU04 **PASS**; FU04A **PARTIAL**; FU05 **kod PARTIAL + canlı apply 2026-07-31'de ilk kez çalıştı ama raporlanmadı**; FU06–FU09 **yok**. Kabaca **%60**.

**CRM genel yüzde:**

| Ölçü | Hazırlık | Gerekçe |
|---|---|---|
| CRM Core (MOD-0149+0150+0151) | **~%85** | İki modül closeout, biri %60 |
| Commercial Suite tamamı (24 modül, MOD-0149…0172 + 0282…0284) | **~%13** | 24 modülün 2'si bitti, 1'i yarıda, 21'i başlamadı |
| **Pharma CRM için hazırlık** | **~%30** | Doktor/hastane/eczane/distribütör master + zone/microzone tanımı + MR pozisyon ataması VAR; **visit planning, route planning, digital detailing, survey, GPS check-in, MicroTarget, frequency/cadence YOK** |
| **Tenant-agnostic CRM için hazırlık** | **~%40 fonksiyonel / ~%85 tasarımsal** | Model doğru şekilde tenant-agnostic (§8); ama generic CRM'in bel kemiği olan Lead→Opportunity→Forecast hiç yok |

**En kritik iki gerçek:**
1. **Tüm CRM kodu commit edilmemiş.** `services/Diten.CrmService/`, `frontend/Diten.Web/{Views,Controllers,wwwroot/assets/js,Resources/Views}/CRM/` git'te **untracked** (789 dosya, `feature/crm-integration` branch'inde). Tek `git clean` bütün MOD-0149/0150/0151 emeğini siler. **Bu rapordaki en yüksek riskli madde.**
2. **Registry, gerçeği yansıtmıyor.** `execution/registries/module-implementation-status.md:106` MOD-0151'i "Başlanmadı %0" gösteriyor; gerçekte FU01–FU05 çalışır durumda. Aynı dosyanın başlığı hâlâ "Commercial Suite — Reserved, no code yet" ve "`services/Diten.CrmService/` does not exist" diyor.

---

## 2. CRM Foundation — Yapılanlar

| # | Alan | Durum | Kanıt | Açıklama |
|---|---|---|---|---|
| F1 | CRM service scaffold | **PASS** | `diten-crm-service-scaffold-implementation-2026-07-14.md`; `services/Diten.CrmService/src/**` | 5 katman (Api/Application/Domain/Infrastructure/Persistence), port **5061**, Mongo, CQRS+MediatR |
| F2 | Gateway integration | **PASS** | `gateway/Diten.ApiGateway/ocelot.json` | `/api/crm/**` → 5061. Frontend'de direct-5061 çağrısı yok (FU04A guard §17 doğruladı) |
| F3 | Navigation / menu | **PARTIAL** | `mod-0149-platform-catalog-navigation-permission-hardening.md` | Page descriptor var (`PageCode=ACCOUNTS`, `crm.account.read`) ama **`IsNavigationVisible=false`** + statik tenant-shell `<li>`. MOD-0285 data-driven nav migration açık follow-up (çift menü riski nedeniyle bilinçli) |
| F4 | RBAC / permission | **PASS** | `commercial-suite-crm-domain-rbac-preflight-2026-07-14.md`, `mod-0151-territory-permission-catalog-seed-97c5-grant-2026-07-23.md` | `crm.account.*` 9/9, `crm.contact.*`, `crm.account-contact.*`, `crm.account-relationship.*`, `crm.territory.*` 5 anahtar seed + 97c5 grant. D7 kararı: `crm.territory.delete` ve `crm.micro-zone.manage` **bilerek yok** |
| F5 | Reference data (MOD-0048) | **PASS** | `mod-0149-crm-reference-data-authoring-closeout.md`, `mod-0151-fu01-live-smoke-retry-2026-07-23.md` | Territory: 12/12 set, 73/73 published value, `isReady=true`. Account/Contact setleri publish edildi. **Hardcoded fallback yasağı** kod düzeyinde korunuyor |
| F6 | Contract endpoints | **PASS** | `Features/Territory/Contract/TerritoryContractDto.cs` | `GET /api/crm/territory-management/contract` — capability flag'leri (`supportsAssignmentRules`, `supportsAccountAssignmentApply`, `supportsResourceReplacement`, … `supportsWorkflowActivation=false`) |
| F7 | Frontend shell / CRM navigation | **PASS** | `frontend/Diten.Web/Views/CRM/{Accounts,Contacts,TerritoryManagement}` | 3 alan, golden-reference compact vertical; compact verifier Accounts/Contacts 94/0 PASS |
| F8 | Tenant isolation | **PASS** | FU01 audit, cross-tenant 404 testleri | Payload'da `TenantId` yok; claim'den okunuyor (FU04A guard §17) |
| F9 | Gateway-only erişim | **PASS** | FU04A §17, FU05 smoke §12 | Frontend yalnız `:5000`; `:5061` yalnız health |
| F10 | 7 dil RESX parity | **PASS** | FU04A §14, MOD-0150 closeout | en/tr/fr/es/ru/zh/ar; TerritoryManagement dahil parity tam |
| F11 | MOD-0021 audit wiring | **PARTIAL** | `mod-0149-final-review-hardening-closeout.md` | Seam ve `HttpCrmAuditPublisher` var; HTTP wiring opt-in/fail-soft — canlı audit trafiği doğrulanmadı |
| F12 | **Source control** | **FAIL** | `git status --untracked-files=all` | CRM'in tamamı untracked (789 dosya). Aşağıda R1 |

---

## 3. MOD-0149 Customer 360 / Account / WorkPlace — Yapılanlar / Eksikler

**Blueprint kimliği** (Blueprint_Data'dan doğrudan): CapabilityGroup `CRM Core`, Wave `W-1`, SoR `accounts/customers`, Soft Pages `Customer 360 Integration View · Hierarchy Trace · Evidence Pack`, Min contract `CRM-CORE-BUNDLE`.

### Yapılanlar

| Kabiliyet | Durum | Not |
|---|---|---|
| Account master (CRUD + bulk delete) | **PASS** | `Features/Account/**`, `AccountCode = ACC-{YYYY}-{seq}` tenant+yıl bazlı üretim |
| WorkPlace | **PASS (ayrı aggregate değil)** | WorkPlace **ayrı entity değil**; `account-type`/`workplace-type` reference + `AccountAttributeValue` ile modellendi. Legacy WorkPlace zenginliği (geo lat/lon, responsible person, bed number, category/definition) attribute yüzeyine eşlendi — pack §25 "reference schema" yöntemi |
| Account hierarchy (parent/child) | **PASS** | `ParentAccountId` + cycle guard; `LinkParentAccountCommand`/`Unlink…`; `GetAccountHierarchyHandler` |
| Customer 360 detail | **PASS** | `Views/CRM/Accounts/Details.cshtml` + Child Accounts + Related Contacts + Related Accounts kartları; browser golden flow attempt-2 **PASS** |
| Country / City / District / adres / geo | **PASS** | MOD-0048'den okunur, CRM kopya tutmaz (SoR boundary). Lat/Lon Account'ta tutuluyor (location foundation) |
| AccountCode / ExternalReference | **PASS / PARTIAL** | AccountCode PASS. `AccountExternalReference` entity + unique (SourceSystem+ExternalId) PASS; **UI'da SourceSystem hardcoded `"default"`, tek referans** → GAP-CRM-02 |
| Account type desteği (hospital/pharmacy/clinic/distributor/wholesaler) | **PASS** | `account-type`: organization · hospital · pharmacy · clinic · distributor · wholesaler · corporate-group · branch · other. `workplace-type`: hospital · pharmacy · clinic · health-center · warehouse · office · other |
| Pharma dışı tenant uygunluğu | **PASS** | Tip listesi **reference-data**, kodda değil. Ayakkabı firması için `dealer`, `store`, `franchise` eklemek yalnız MOD-0048 publish işidir — kod değişmez. Bu tasarımın en güçlü tarafı |

### Eksikler

| Eksik | Şiddet | Nereye |
|---|---|---|
| Accounts listesinde Country/City filtresi | Düşük | GAP-CRM-01 |
| External Reference: SourceSystem dropdown + çoklu referans + lookup-by-external-id | Orta | GAP-CRM-02 |
| Türkiye district datasının tamamı (bugün yalnız Edirne'nin 9 ilçesi) | Düşük ama **saha planlamasını bozar** | GAP-CRM-03 → MOD-0048 import |
| Import/export endpoint'leri (Account) | Orta | Deferred; MOD-0150'de var, MOD-0149'da yok |
| MOD-0021 audit HTTP wiring | Orta | F11 |
| Blueprint "Evidence Pack" soft page | Orta | MOD-0149 için üretilmedi |

### Territory tasarım kararı — doğru mu?

> **Account içine `TerritoryId`/`ZoneId`/`MicroZoneId` gömülmedi. Territory ownership MOD-0151'den türetiliyor.**
> Kaynak: MOD-0151 pack §11.3 — *"MOD-0149 Account entity'sine ZoneId/MicroZoneId/TerritoryId eklenmez (mimari kural / pack ihlali)"*; MOD-0149 pack §3.1 `CoverageSummary` placeholder.

**Değerlendirme: Evet, doğru — ve bu raporun en iyi tasarım kararı.** Gerekçeler:

1. **Territory zamana bağlı, Account değil.** Bir account 2026'da A zone'una, 2027'de B zone'una düşer. Gömülü alan geçmişi ezer; `AccountTerritoryAssignment` effective-dated satır tutar ve eskisini `ended` yapar (silmez).
2. **Çok boyutluluk.** Aynı account aynı anda farklı business unit / product portfolio için farklı territory'lere ait olabilir (pack §11.1). Tek `TerritoryId` alanı bunu ifade edemez.
3. **SoR temizliği.** Account master MOD-0149'un, coverage MOD-0151'in. Gömme, iki modülün aynı alana yazmasını gerektirirdi.
4. **SAP/Salesforce ile hizalı.** Salesforce'ta da `Territory2` ↔ `Account` ilişkisi `ObjectTerritory2Association` üzerinden kurulur, Account üzerinde alan değildir.

**Bedeli:** Her "bu account'un MR'ı kim?" sorusu bir join/projection ister → bu yüzden `CoverageSummary` projection'ı ve FU09 readiness API'leri **zorunlu**, opsiyonel değil.

---

## 4. MOD-0150 Contact & Relationship Management — Yapılanlar / Eksikler

**Blueprint:** CapabilityGroup `CRM Core`, Wave `W-3`, SoR `contacts`, DepGate `Customer 360; Consent & Preference Mgmt`, Min contract `CRM-CORE-BUNDLE + CONSENT-BINDING`.

**Verdict: Closeout PASS / %100** — `mod-0150-final-validation-closeout.md` (CrmService 77/77 test, compact verifier 94/0, RESX 7 dil parity).

| Kabiliyet | Durum | Not |
|---|---|---|
| Contact master | **PASS** | FU01; `contact-type` 9 değer (doctor · pharmacist · responsible-person · department-contact · decision-maker · procurement · medical · administrative · other) |
| AccountContactLink | **PASS** | FU03; uniqueness + `contact-role` (7 değer) + primary flag; 360 projeksiyonları |
| Contact external refs | **PASS** | `ContactExternalReference` |
| Account-to-Account relationship | **PASS** | FU04; **metadata-driven** — `direction`/`inverseLabelCode`/`selfAllowed` MOD-0048 metadata'sından. `preferred-pharmacy`, `refers-to`, `served-by`, `nearby` tipleri mevcut |
| Contact import/export | **PASS** | FU06 + XLSX template/dry-run/apply (task1+task2 audit'leri) |
| Consent/preference seam | **PASS (seam)** | FU05 — **read-only no-op + mask + fail-soft**. Gerçek consent engine MOD-0164'te, bilinçli olarak burada değil |
| PII/KVKK hardening | **PASS** | `mod-0150-contact-location-pii-kvkk-hardening.md` |

### Eksikler

| Eksik | Şiddet | Nereye |
|---|---|---|
| Contact professional field'larının Contact Type'a göre cascade'i | Orta | GAP-CRM-04 — MOD-0048 `appliesTo` metadata gerekiyor; hardcoded mapping yasak |
| MOD-0164 gerçek Consent engine | Yüksek (pharma/KVKK) | MOD-0164, W-2 — **CRM'de en erken gereken greenfield modül** |
| Blueprint "Contact Sync Monitor" / "Consent Trace View" soft page'leri | Orta | Üretilmedi |

### Contact territory kararı (açık yazım — talep edildiği gibi)

> **v1'de ayrı `ContactTerritoryAssignment` YOKTUR ve olmamalıdır.**
> Kaynak: MOD-0151 pack §11.2.

- Contact coverage **türetilir**: `Contact` → `AccountContactLink` (MOD-0150) → `Account` → `AccountTerritoryAssignment` (MOD-0151).
- **Primary Account varsa** default contact territory odur.
- **Birden çok Account link'i varsa** contact birden çok derived coverage gösterir — bu bir **çakışma değil, union'dır**; ekranda "hangi account üzerinden" gösterilir.
- MOD-0151, Contact'a `ZoneId`/`TerritoryId` alanı **eklemez**.
- **Contact-level override future follow-up olarak kalır** (bir doktor bağlı olduğu hastaneden farklı bir MR'a atanacaksa).

**Durum: karar verildi, kod yazılmadı.** Derived contact coverage read-model'i bugün **yok** — FU09 kapsamında veya §13'teki Öneri-1 olarak açılmalı. Bu, pharma için kritik: MR'ın ekranında "benim doktorlarım" listesi bu türetmeden gelir.

---

## 5. MOD-0151 Territory Management — FU Bazlı Durum

**Blueprint:** Wave `W-4`, SoR `territories`, DepGate `Customer 360; Workflow Designer`, Soft Pages `Territory Model Viewer · Change Approval Trace · Evidence Pack`, Min contract `CRM-TERRITORY-BUNDLE`.
**Çekirdek model kararı:** tek `TerritoryNode` + `TerritoryLevel` (division/country/region/area/zone/**microzone**). MicroZone ayrı aggregate/permission/set **değildir**.

| FU | Durum | Neler yapıldı | Eksik kalanlar | Riskler | Sonraki aksiyon |
|---|---|---|---|---|---|
| **FU00** Pack approval | **PASS** | Canonical konumlandırma, D1–D7 kararları, runtime scope kontrollü açıldı | — | Pack scope her FU'da yeniden yetkilendirilmeli (disiplin borcu değil, kural) | — |
| **FU01** Backend core | **PASS** | Contract endpoint; TerritoryModel + TerritoryNode; level/cycle/tarih containment; code uniqueness; reference fail-closed; MicroZoneProfile koşullu doğrulama; tenant izolasyonu + cross-tenant 404; Gateway smoke 23/23 | — | — | Kapalı |
| **FU02** UI / Model Viewer | **PASS** | Menü, DataTable, model formu, hierarchy viewer, node create/edit, 7 dil | Blueprint'in "Territory Model Viewer" soft page'i karşılandı | — | Kapalı |
| **FU02A** Country + BU Scope | **PASS (addendum ile)** | Country single-select (MOD-0048), Business Unit multi-select, `BusinessScopes` persistence, duplicate normalize, Division Scope kaldırıldı | **Brand Scope yok** (bilinçli) | Almiba/Tutukon/Bekant'ın business unit sanılması | Brand/Product master sonrası follow-up |
| **FU02B** Lifecycle | **PASS** | draft→active→inactive→archived, draft-only soft-delete, single-active-model guard, computed expiry, node status senkronizasyonu, audit seam. Canlı smoke **72/72 PASS** | Background expiry scheduler yok (bilinçli karar) | — | Kapalı |
| **FU03** Assignment Rules + Preview | **PASS** | geography / account-list / account-type / product-portfolio / business-scope rule'ları; **yan etkisiz preview**; conflict tespiti + priority kazanan + politika önerisi; Preview ekranı | — | Preview'un yan etkisiz olduğu **yapısal olarak** garanti (ayrı handler, ayrı endpoint) — korunmalı | Kapalı |
| **FU04** Resource Assignments | **PASS** | Tarihsel CRUD, PositionRef/PersonRef seam, coverage scope, metadata-driven validation, exclusivity guard (duplicate primary, MR cross-scope, override, çok-node uyarısı) | Person/HCM endpoint'leri 403 (PersonRef seam manuel) | — | FU04A ile devam etti |
| **FU04A** Resource Lifecycle / Replacement / Transfer / Current Responsibility | **PARTIAL** | Aşağıda ayrıntılı | Aşağıda | Aşağıda | FU04B + FU06 |
| **FU05** Account Assignment Apply + History | **PARTIAL (kod) / kanıt eksik** | `AccountTerritoryAssignment`; active-model-only all-or-nothing apply; effective dating; eski atama `ended` (silinmez); reason zorunlu override; model/account history; CoverageSummary projection; Apply/History UI | **Canlı closeout raporu FAIL (2026-07-28)**; 2026-07-31'de ilk başarılı apply gerçekleşti (CC-2026-000018 → AZ-R-BAKU) ama **audit raporu yazılmadı** | Kanıt belge değil oturum hafızası; conflict 409 / override / end zinciri canlıda hiç koşmadı | **P0: FU05 Live Smoke Closeout** |
| **FU06** Workflow Approval | **NOT STARTED** | — | Tamamı | `supportsWorkflowActivation=false` | Pack authorization + impl |
| **FU07** Evidence Pack | **NOT STARTED** | — | Tamamı | Blueprint'in zorunlu soft page'i | FU06 sonrası |
| **FU08** Import/Export | **NOT STARTED** | — | Tamamı | Model elle girilemez ölçekte | FU07 sonrası |
| **FU09** Visit/Route Readiness API | **NOT STARTED** | — | Tamamı | MOD-0155 doğrudan CRM tablolarını okumaya kalkar | FU08 sonrası |

### FU04A ayrıntı (talep edildiği gibi açık yazım)

**Yapılanlar:**
- **Position-based lifecycle uygulandı.** `RoleCode` **artık canonical değil**; canonical kimlik **`TerritoryPositionRef` + `PositionCode`**. Snapshot: `PositionId`, `PositionCode`, `PositionTitle`, `PositionType`, `SourceSystem`. Eski `PositionId/PositionCode/PositionName` düz alanları yalnız doküman uyumu için mirror bırakıldı. UI terminolojisi Role → Position.
- **Draft proposed → active çalışıyor.** Draft modelde açılan assignment `proposed` (planning-only, current sorgusuna girmez); model aktive edilince fail-closed policy+conflict kontrolünden geçip `active` oluyor.
- **Active create / end / replacement / transfer çalışıyor.** Hepsi effective date + zorunlu reason ister; replacement ve transfer çift yönlü provenance (`ReplacedAssignmentId`/`ReplacementAssignmentId`/`TransferFrom…`/`TransferTo…`) ile bağlı; kaynak kayıt **silinmez, `ended` olur**.
- **Current responsibility ve history API/UI var.** 5 additive endpoint (`/replace`, `/transfer`, `/resource-responsibilities/current`, `/resource-assignments/history`, `/resources/{id}/territory-responsibilities`) + Current Responsibility ve Assignment History panelleri.
- Deterministic position policy: MR→zone/microzone, Area Manager→area, Regional Manager→region, Product Manager→BU-wide, HOC/Commercial Manager→model-wide. Kaynak açıkça `fu04a-deterministic-position-policy-v1` olarak işaretlendi.
- Test: **343 passed / 5 skipped / 0 failed**. Canlı gateway-only smoke: proposed→active, replacement (Ayşe→Mehmet), transfer (Marmara→Karadeniz) doğrulandı.

**Verdict PARTIAL'ın 4 gerekçesi:**
1. **Position Directory runtime authority değil** — canonical position metadata'sı çalışma zamanında okunamıyor; deterministic policy seam kullanılıyor.
2. **Mongo transaction native değil** — yerel standalone topolojide compensating rollback var; kesin crash-atomicity replica set/mongos ister.
3. **FU04A dışı DataTable borçları** — TerritoryManagement modül-geneli verifier **73 PASS / 18 FAIL**; fail'ler mevcut compact/offcanvas/bulk/quick-view şablon borçları.
4. **Conflict/override canlıda tekrarlanmadı** — davranış yalnız testlerle doğrulandı.

---

## 6. Territory Management — Sıradaki İşler

### 6.1 MOD-0151 FU04B — Resource Assignment Plan vs Current Visibility

**Ne:** Draft/proposed (planlanan) assignment ile active/current assignment'ın yan yana karşılaştırılması — *"plan buydu, şimdi bu"* görünürlüğü.
**Nerede:** Territory Model Detail içinde **read-only "Plan vs Current" tab'ı**. Yeni ana menü sayfası **değil**.
**Neden ilk sırada:** FU04A `proposed` ve `active` kavramlarını yarattı ama kullanıcı ikisini tek ekranda göremiyor; planlama ile gerçek arasındaki sapma bugün görünmez. Ayrıca FU06 approval'ın diff'ini üretecek okuma yüzeyi budur — FU04B önce gelirse FU06 diff'i sıfırdan yazılmaz.
**Önemli uyarı:** **FU04B pack §22'de tanımlı değil.** Yani iki adım gerekir: (a) pack'e FU04B scope eklenmesi, (b) runtime scope authorization (FU04A/FU05'teki `*-pack-runtime-scope-authorization-*.md` deseni). Doğrudan implementasyona geçilmemeli.
**Risk:** Read-only sınırı aşılıp "plandan gerçeğe uygula" butonu eklenmesi → o FU06'nın işi.

### 6.2 MOD-0151 FU05 Live Smoke Closeout

**Ne:** Account assignment apply zincirinin canlıda kapatılması: active model → matched preview row → apply → history → coverage → duplicate **409** → reason'sız **400** → reason'lı override → end.
**Neden:** 2026-07-28 closeout **FAIL** verdi (aktivasyonda sunucu hatası, preview `Unauthorized`). 2026-07-31'de ilk başarılı apply gerçekleşti (4 backend bug fix'inden sonra: model-window `.Date` karşılaştırması, node-effective `.Date`, standalone Mongo transaction fallback, `EffectiveTo` parallel-array index) **ama audit raporu yok** — kanıt belgeye dönüşmedi.
**Ön koşul:** Matched account/rule datası hazırlanmalı (rule kriteri ile gerçekten eşleşen account'lar).
**Ayrıca kapatılmalı:** `AccountTerritoryAssignment`'ın `RegisterClassMaps`'e eklenmesinden **önce** yazılmış binary-id kayıtları görünmez orphan durumda — temizlik/yeniden apply kararı verilmeli.

### 6.3 MOD-0151 FU06 — Workflow Approval + Controlled Activation

**Ne:** MOD-0023 Start Instance, submit/approve/reject, Transition Gate, `TerritoryChangeRequest`, approval trace, before/after diff, **immutable approved snapshot**, Change Approval Trace sayfası (Blueprint'in zorunlu soft page'i).
**Politika kararı — verilmesi gereken:**

| İşlem | Öneri | Gerekçe |
|---|---|---|
| Resource assignment (MR ataması/değişimi) | **Direct apply** — workflow şart değil | Sık, operasyonel, geri alınabilir; approval koyulursa saha durur |
| **Territory model activation** | **Approval-required** | Tüm saha organizasyonunu değiştirir; Blueprint DepGate zaten "Workflow Designer" diyor |
| **Account assignment apply (toplu)** | **Approval-required, eşik üstü** | N satırdan fazla etkileyen apply onay istesin; tekil manuel override reason ile direct |
| Node ekleme/düzenleme (draft) | Direct | Draft zaten operasyonel değil |

**Not:** Pack §20 zaten şu kuralı taşıyor: *"FU06 approval-governed activation'da workflow approval eksikse → Block 409/422 (sahte onay yasağı)"*. FU02B manual activation'a uygulanmaz.

### 6.4 MOD-0151 FU07 — Evidence Pack / Audit Export
Territory değişikliklerinin denetlenebilir kanıt paketi; `TerritoryEvidencePack` + correlation id + MOD-0021 wiring. Blueprint MOD-0151 için "Evidence Pack" soft page'ini **zorunlu** sayıyor.

### 6.5 MOD-0151 FU08 — Import/Export Hardening
XLSX template / export / upload / **dry-run** / safe apply + satır bazlı hata raporu (MOD-0150 deseni birebir tekrar edilebilir). 5.000 account'lu bir modeli elle girmek imkânsız.

### 6.6 MOD-0151 FU09 — Visit/Route Readiness API
`GET account territory coverage`, `microzone accounts`, `resource coverage`, **derived contact coverage**, coverage roll-up. MOD-0155 bunları okumazsa doğrudan CRM koleksiyonlarına girmeye çalışır → SoR ihlali.

---

## 7. CRM Core — Sonraki Modüller

Blueprint_Data'dan doğrudan çıkarılan canonical bilgiyle:

### MOD-0152 Lead Management (W-3, SoR `leads`, DepGate: Workflow Designer + Data Contract Registry)
- **Neden gerekli:** Henüz müşteri olmayan ilgiyi yakalamak ve yönlendirmek. Bugün CRM'de "müşteri adayı" kavramı hiç yok — her şey doğrudan Account olarak açılıyor, bu da master'ı kirletiyor.
- **Pharma karşılığı:** Yeni açılan eczane/klinik, ilk kez ziyaret edilen doktor, kongre/etkinlikten gelen HCP ilgisi. Ayrıca ihale/hastane alım süreçlerinin ön aşaması.
- **Generic karşılığı:** Ayakkabı firmasında yeni bayi başvurusu; FMCG'de yeni satış noktası; B2B'de web/fuar lead'i.
- **Ön koşullar:** MOD-0023 Workflow (routing için — runtime **mevcut**), MOD-0048 lead-source/lead-status setleri, MOD-0151 territory (routing'in "kime düşer" cevabı).
- **0149/0150/0151 ilişkisi:** Lead **convert** edildiğinde MOD-0149 Account + MOD-0150 Contact üretir. Routing MOD-0151 territory'sini tüketir. Lead'in kendi coverage'ı olmamalı — territory'den türemeli.

### MOD-0153 Opportunity & Pipeline (W-3, SoR `opportunities`, DepGate: Lead Management + Metric Registry)
- **Neden:** Ölçülebilir satış hattı. Forecast'in girdisi.
- **Pharma:** Hastane ihalesi, distribütör yıllık anlaşması, formulary listing süreci. (Bireysel doktor ziyareti opportunity **değildir** — o MOD-0155 activity'sidir. Bu ayrım korunmalı.)
- **Generic:** Bayi kontratı, kurumsal satış fırsatı, mağaza açılışı.
- **Ön koşullar:** MOD-0152, metric/semantic registry.
- **İlişki:** Account/Contact zorunlu; territory ile owner belirlenir; MOD-0155'in Blueprint DepGate'i **budur**.

### MOD-0154 Forecasting & Quotas (W-4, SoR `forecasts`, DepGate: Scorecards + Metric Registry)
- **Neden:** Kota yönetişimi ve tahmin disiplini.
- **Pharma:** MR/zone bazlı satış kotası, brand bazlı hedef, cycle hedefi.
- **Generic:** Bayi kotası, bölge hedefi, mağaza hedefi.
- **Ön koşullar:** MOD-0153, **MOD-0151 territory** (kota territory'ye/pozisyona yazılır), product/brand master.
- **İlişki:** Kota `TerritoryNode` + `PositionRef` + dönem üçlüsüne bağlanır → FU04A'nın current responsibility API'si tam olarak bunun için var.

### MOD-0155 Field Sales / Visit Planning / Route Planning (W-4, SoR `visit plans`, DepGate: **Opportunity & Pipeline**, contract `CRM-VISIT-BUNDLE`)
- **Neden:** Pharma'nın asıl işi. **Legacy değeri en yüksek alan** (MicroTarget, Activity/Visit, ActivityReport, schedule engine, ziyaret çakışma kontrolü, frequency/cadence, hastane→yakın eczane rota önerisi).
- **Pharma:** Doktor/eczane ziyaret planı, MicroTarget, frequency, günlük rota, ziyaret raporu.
- **Generic:** Bayi/mağaza ziyaret planı, merchandiser turu, servis teknisyeni rotası.
- **Ön koşullar:** **MOD-0151 FU09** (coverage API), MOD-0149 geo (lat/lon — var), MOD-0048 tam district datası (**bugün eksik, GAP-CRM-03**), product/brand master, MOD-0280 Time Entry SoR kararı (efor overlap'i).
- **İlişki + dikkat:** Blueprint DepGate'i MOD-0153 diyor, ama pharma'nın işi doktor ziyaretidir, opportunity değil. **Öneri:** MOD-0155'in visit/route çekirdeği MOD-0153'ü beklemesin; yalnız "opportunity'ye bağlı ziyaret" alt-kabiliyeti MOD-0153'e gate'lensin. Bu, Blueprint'ten kontrollü ve gerekçeli tek sapma önerisidir — EA onayına tabi.

### MOD-0156+ Commercial adjacent / O2C
Price Lists (0156), Quote (0157), Quote-to-Contract (0158), Product Config (0159), Order Capture (0168), Billing (0169), Returns/Disputes/ATP (0170–0172). **SoR EA-TBD** — Finance/Order-Management ile paylaşımlı olabilir. Pharma'da "sales upload / good in transit" bağlantısı buraya düşer. **CRM'in şu anki sırasında değil.**

### Product / Brand master entegrasyonu
- **Neden:** Brand Scope, MR'ın brand portföyü, brand bazlı kota ve detailing içeriği hepsi buna bağımlı. Bugün D3 kararı gereği MOD-0048'de geçici `product-portfolio`/`brand-group` seam'i var.
- **Karar bekleyen:** Product master SoR = MDM (`Diten.MdmService`, port 5059 — **çalışıyor**). Brand master'ın orada mı ayrı bir modülde mi olacağı netleşmeli.
- **Bloklar:** MOD-0151 Brand Scope, MOD-0154 brand kotası, MOD-0155 detailing.

### Digital detailing / presentation content · Visit survey · GPS check-in/out
Üçü de **MOD-0155'in alt kabiliyetleri**, ayrı modül değil (scope şişirmemek için). Ayrıntı §8.

---

## 8. Pharma-Specific ama Tenant-Agnostic Tasarım Notları

| Konu | Karar | Bugünkü durum | Tenant-agnostic karşılığı |
|---|---|---|---|
| **Doktor = Contact, asla Patient değil** | Doktor `contact-type=doctor` olarak Contact'tır. CRM **hasta verisi tutmaz** — hasta verisi KVKK/GDPR özel nitelikli veridir ve bu sistemin SoR'u değildir | ✅ Uygulandı (`contact-type` 9 değer, `doctor` dahil) | Ayakkabıda `store-manager`, `buyer`; FMCG'de `category-manager` |
| **Hospital/Clinic/Pharmacy = Account/WorkPlace** | Ayrı entity değil; `account-type`/`workplace-type` reference değeri | ✅ Uygulandı | `dealer`, `store`, `franchise`, `distribution-center` eklemek yalnız MOD-0048 publish işi — **kod değişmez** |
| **MR = Resource/Employee/PositionRef** | MR ayrı bir CRM entity'si değil; `TerritoryResourceAssignment` içinde **PositionRef + PersonRef** ile temsil edilir | ✅ FU04A'da canonical hale geldi (RoleCode kaldırıldı) | `sales-rep`, `merchandiser`, `area-manager` — aynı seam, farklı position code |
| **Territory = zone/microzone (pharma) / sales region (diğer)** | Tek `TerritoryNode` + `TerritoryLevel`; microzone yalnız bir **seviye** | ✅ Uygulandı; MicroZone ayrı permission/aggregate **yok** (D7) | Perakendede `region > district > store-cluster`; bayi ağında `bölge > il > bayi grubu`. Seviye adları reference-data'dan gelir |
| **Visit planning** | MOD-0155; territory'yi **tüketir**, tanımlamaz | ❌ Yok | Bayi/mağaza ziyareti, merchandiser turu — aynı Visit/Activity modeli |
| **Digital detailing** | MOD-0155 alt kabiliyeti: "presentation content" olarak generic adlandırılmalı, "detailing" pharma etiketi UI label'ında kalmalı | ❌ Yok | Ürün kataloğu sunumu, koleksiyon sunumu, planogram |
| **Survey / feedback** | MOD-0155 alt kabiliyeti; soru seti reference/metadata-driven olmalı, kodda soru olmamalı | ❌ Yok | Bayi memnuniyeti, mağaza denetim formu, raf kontrolü |
| **GPS check-in/out** | **Generic capability** — "field visit verification". Blueprint MOD-0155 için zaten `geo/time binding, compliance evidence export` diyor | ❌ Yok | Her saha ziyaretinde aynı; teknisyen, kurye, merchandiser |

**Tasarım ilkesi (bugüne kadar tutarlı uygulanmış):** *pharma-spesifik olan her şey **reference-data değeri** veya **metadata** olarak modellenir; aggregate/enum/permission olarak değil.* Bu ilke MOD-0149/0150/0151'de ihlal edilmemiş. **Korunması gereken en değerli kural budur.**

**Tek risk:** `MicroZoneProfile` value object'i (yalnız `level=microzone`) pharma-yakın bir kavram. Bugün koşullu doğrulanıyor ve zararsız; ama içine pharma-özel alanlar (frequency, potansiyel skoru) eklenirse tenant-agnostic model kırılır. **Bu alan attribute/metadata olarak tutulmalı.**

---

## 9. Eski Sistem Analizlerinden Taşınacak Logic

Kaynak: [legacy-value-preservation.md](execution/domains/commercial-suite/legacy-value-preservation.md) + MOD-0149 pack §25/§26.
**Yöntem sözlüğü:** *rule capture* (kural pack'e yazılır, greenfield kodlanır) · *reference schema* (alanlar referans alınır, şema kopyalanmaz) · *do-not-migrate* (yalnız kavram korunur).

| Legacy logic | Taşındı mı? | Nerede karşılandı | Taşınmadıysa nereye | Modern tasarımda nasıl |
|---|---|---|---|---|
| **WorkPlace alanları** (hastane/eczane/klinik profil, geo, category/definition, responsible person, bed number, person quantity) | ✅ **Taşındı** | MOD-0149 — Account + `workplace-type` reference + `AccountAttributeValue` | — | Ayrı entity değil; zengin profil **controlled attribute** yüzeyinde. Legacy dinamik `Property/PropertyList` motoru kopyalanmadı |
| **Country/Zone/City/District mapping** | 🟡 **Kısmen** | MOD-0048 (country/city/district) + MOD-0151 (zone/microzone `TerritoryNode` olarak) | **District datası eksik** (yalnız Edirne) → GAP-CRM-03 | Coğrafya CRM'de canonical **tutulmaz**; MOD-0048'den okunur. Zone ≠ coğrafi referans, **satış organizasyonu node'u** |
| **MicroTarget süreleri** (targeting cadence, atama) | ❌ **Taşınmadı** | — | **MOD-0155** (+ hedefleme MOD-0167) | Rule capture. Frequency veri kaynağı **EA-TBD açık soru** — legacy tablo mu, yeni cadence config mi? Karar verilmeden MOD-0155 pack'i yazılamaz |
| **Doctor/hospital/pharmacy visit ilişkileri** | 🟡 **İlişki tarafı taşındı, visit tarafı yok** | MOD-0150 `AccountContactLink` (doktor↔hastane) + `AccountRelationship` (`preferred-pharmacy`, `refers-to`, `served-by`, `nearby`) | Visit → **MOD-0155** | İlişki tipleri **metadata-driven** (direction/inverse/selfAllowed) — legacy'nin sabit ilişki tablosu kopyalanmadı |
| **Hospital → nearby pharmacy / route logic** | 🟡 **Veri seam'i hazır, algoritma yok** | MOD-0150'de `nearby` ilişki tipi + MOD-0149'da lat/lon | **MOD-0155 route-plan** | Rule capture (geo-proximity rota). Geo verisi MOD-0048/MDM'den; legacy kopya değil. Algoritma yeniden yazılır |
| **External API ihtiyaçları** (OldSystem entegrasyonu) | ✅ **Doğru şekilde taşındı** | MOD-0149 `AccountExternalReference` (SourceSystem+ExternalId), MOD-0150 `ContactExternalReference` | UI hardening → GAP-CRM-02 | **Kritik do-not-migrate:** `OldSystemId` yalnız migration compat **veri alanı**; runtime dependency / localhost self-call / hardcoded IP-port **kurulmaz** |
| **Sales upload / Good in transit** (commercial bağlantılar) | ❌ **Taşınmadı** | — | **MOD-0168/0169/0172** (O2C) — SoR EA-TBD | CRM'in kapsamında değil; O2C bridge. MOD-0154 forecast bunun actual'ını okur |
| **Business Unit / Product / Brand kırılımları** | 🟡 **BU taşındı, Product/Brand geçici seam** | MOD-0151 `TerritoryBusinessScope` (Alpha/Beta/Gamma = **stabil BU**, D1) | Product/Brand → **MDM / Product master** | D3: geçici MOD-0048 `product-portfolio`/`brand-group` seam. Almiba/Tutukon/Bekant **brand'dir, BU değildir** — Brand Scope master hazır olana kadar açılmaz |
| **Field force route / micro zone logic** | 🟡 **MicroZone tanımı var, route yok** | MOD-0151 `TerritoryLevel=microzone` + `MicroZoneProfile` | Route → **MOD-0155**, yetki → MOD-0018-FU15 ABAC | MicroZone **MOD-0151'de tanımlanır, MOD-0155'te tüketilir**. Legacy MR yetki tablosu taşınmaz; ABAC ile çözülür |
| **Account/contact relationship geçmişi** | ✅ **Taşındı ve iyileştirildi** | MOD-0150 `RelationshipLifecycle` (effective-dated, end ≠ delete) | — | Legacy'de ilişki silinirdi; yeni modelde `ended` olur, geçmiş korunur |
| **Activity / ActivityReport / visit status lifecycle / çakışma kontrolü / aynı gün aynı activity engeli / schedule engine** | ❌ **Hiçbiri taşınmadı** | — | **MOD-0155** | Legacy'nin **en olgun ve kurala en zengin alanı**. Legacy status kolonları ve rapor formu birebir kopyalanmaz; state machine greenfield modellenir |
| **Campaign / PromoCampaign / CyclePeriod** | ❌ | — | **MOD-0165** | Rule capture (cycle period kuralları) |
| **TargetCustomer / UCLN / StrategyTemplate / SubjectList** | ❌ | — | **MOD-0167** | Reference schema + rule capture; segment eval greenfield |

### Doğrudan taşınmaması gerekenler (yeniden tasarlanmalı)
- Legacy DitenCRM controller/view/repository yapısı — **FROZEN**
- Magic number'lar (ör. `ClientTypeId 92/99`) → reference-data code'ları
- Country/City/Zone/Brand'in CRM içinde canonical tutulması → MOD-0048/MDM
- Frontend-only validation → backend validation zorunlu
- Authorization'sız endpoint modeli → `[HasPermission]` + MVC per-action guard + menü gate (defence-in-depth)
- Legacy'nin kendi rol sistemi → MOD-0018/AuthService

### Hâlâ açık EA soruları (MOD-0155 pack'ini bloklar)
1. **Frequency verisi** nereden beslenecek?
2. **Daywork / VisitMix** kaynakları legacy'de var mı, greenfield mi?
3. **HCP identity SoR** — doktor/eczacı/hastane kimliği CRM'de mi MDM'de mi? *(Legacy'de CRM içindeydi; kurumsal CRM+MDM stratejisinde ayrıştırılmalı.)*

---

## 10. SAP / Oracle / Salesforce Perspektifi

| Genel CRM capability alanı | Büyük sistemlerdeki karşılığı | Bizdeki karşılığı | Durum |
|---|---|---|---|
| Customer / Account master | SAP BP (Business Partner), Salesforce Account, Oracle Customer Hub | MOD-0149 | ✅ |
| Contact & relationship | SAP BP Relationships, SF Contact + AccountContactRelation | MOD-0150 (`AccountContactLink` + `AccountRelationship`) | ✅ |
| Territory management | SAP CRM Territory Management, **SF Enterprise Territory Management (Territory2)**, Oracle Sales Territory | MOD-0151 | 🟡 %60 |
| Sales coverage / assignment | SF `ObjectTerritory2Association` + assignment rules | MOD-0151 FU03 rules + FU05 `AccountTerritoryAssignment` | 🟡 |
| Lead / Opportunity | SF Lead/Opportunity, SAP Sales Cloud | MOD-0152/0153 | ❌ |
| Forecast / Quota | SF Collaborative Forecasts + Quotas | MOD-0154 | ❌ |
| Visit / activity management | SAP CRM Activity Mgmt, **Veeva CRM Call Cycle** (pharma standardı) | MOD-0155 | ❌ |
| Product / content presentation | Veeva CLM, SF Content | MOD-0155 alt kabiliyeti + MDM | ❌ |
| Integration / contracts | SAP CDS/OData, SF API contracts | Blueprint `CRM-CORE-BUNDLE` / `CRM-TERRITORY-BUNDLE` / `CRM-SALES-BUNDLE` / `CRM-VISIT-BUNDLE` | 🟡 tanımlı, katalog yok |

### İleride uyumlu olmak için dikkat edilecek 6 nokta (yeni modül **önerilmiyor**)

1. **Territory versiyonlama modeli doğru seçilmiş.** SF Territory2Model'de de model bir versiyondur ve aynı anda tek `Active` model olur — bizdeki single-active-model guard birebir aynı mantık. **Değiştirmeyin.**
2. **Assignment rule'ları "rule + preview + apply" üçlüsü olarak ayırmak** SF'in `Territory2Rule` + "Run Rules" desenidir. FU03/FU05 ayrımı doğru. Preview'un yan etkisizliği **yapısal** kalmalı.
3. **Account'a territory alanı gömmeme** kararı büyük sistemlerin tamamıyla hizalı — geri dönmeyin.
4. **Kimlik alanları:** büyük sistemler entegrasyonda **external id + source system** çifti ister. `AccountExternalReference` doğru; ama UI'daki hardcoded `"default"` SourceSystem ileride çoklu kaynak entegrasyonunu bozar (GAP-CRM-02 sanıldığından önemli).
5. **Contract bundle katalogu:** Blueprint 4 CRM bundle'ı adlandırmış ama repo'da bunların **endpoint/şema karşılıkları listelenmemiş**. SAP/Oracle entegrasyonu gün geldiğinde ilk istenecek şey budur → §13 Öneri-4.
6. **Position-based assignment** (RoleCode değil) SAP HR-Org / Oracle position management yaklaşımıyla hizalı. FU04A'nın doğru kararı; ancak Position Directory runtime authority olmadan yarım kalıyor → §13 Öneri-7.

**Yapılmayanlar (bilinçli):** Yeni büyük modül uydurulmadı, SAP/Veeva özellikleri birebir kopyalanmadı, capability plan dışına scope açılmadı.

---

## 11. Eksik ve Risk Listesi (önem sırasına göre)

### R1 — Kaynak kontrolü (KRİTİK)
Tüm CRM kodu **untracked** (789 dosya): `services/Diten.CrmService/`, `frontend/Diten.Web/{Controllers,Views,Resources/Views,wwwroot/assets/js}/CRM/`. Yanlış bir `git clean -fd` MOD-0149+0150+0151'in tamamını siler. **Bugün commit edilmeli.**

### R2 — Kanıt borcu (YÜKSEK)
- FU05 canlı closeout raporu **FAIL** durumunda; 2026-07-31'deki başarılı apply **belgelenmedi**.
- FU05 conflict 409 / override 400 / end zinciri canlıda **hiç** koşmadı.
- FU04A conflict/override canlıda tekrarlanmadı.

### R3 — Teknik borçlar (YÜKSEK)
- **Mongo standalone** → native transaction yok; compensation fallback ile idare ediliyor. Deployment **replica set/mongos** olmalı.
- **DateTimeOffset BSON array tuzağı** — instant-vs-date karşılaştırması ve parallel-array index/sort 500'leri tekrar edecek desen. Her yeni effective-dated alan bu riski taşıyor.
- **Yeni aggregate → `RegisterClassMaps`** unutulursa sorgu **sessizce boş döner** (AccountTerritoryAssignment'ta yaşandı). Checklist'e girmeli.
- **DataTable şablon borçları** — TerritoryManagement verifier 73 PASS / **18 FAIL**.
- MOD-0021 audit HTTP wiring seam düzeyinde.

### R4 — Domain eksikleri (YÜKSEK)
- Derived **contact territory coverage** read-model'i yok (karar var, kod yok).
- **Plan vs Current** görünürlüğü yok (FU04B).
- **Brand Scope** yok → Product/Brand master'a bağımlı.
- **Workflow approval** yok → `supportsWorkflowActivation=false`.
- **Evidence Pack** yok → Blueprint'in MOD-0149/0150/0151 için zorunlu saydığı soft page hiçbir modülde üretilmedi.

### R5 — Reference data eksikleri (ORTA-YÜKSEK)
- **Türkiye district datası eksik** (yalnız Edirne'nin 9 ilçesi). Territory zone tanımı ve ileride rota planlama bunun üstüne kurulacak → **saha planlamasını doğrudan bozar**.
- `medical-specialty`/`department-type`/`professional-title` setlerinde `appliesTo` metadata'sı yok (GAP-CRM-04).
- Product portfolio / brand group yalnız geçici seam.

### R6 — RBAC eksikleri (ORTA)
- Account/Contact assignment RBAC anahtarları pack kararı gereği **`model.read/manage` fallback** kullanıyor — ayrı `crm.territory.assignment.*` anahtarı yok.
- **MOD-0018-FU15 Real DataScopeResolver** yok → territory/field-force **ABAC scoping** yapılamıyor. Bir MR bugün API düzeyinde kendi zone'u dışını okuyabilir mi sorusunun net cevabı yok. Build-lane planı bunu **P0 blocker** olarak işaretlemiş.
- Person/HCM endpoint'leri 403 → PersonRef seam manuel.

### R7 — UI/UX eksikleri (ORTA)
- MOD-0285 nav migration (statik `<li>` + `IsNavigationVisible=false` ikilisi).
- Accounts Country/City filtresi yok.
- External Reference UI tek referans + hardcoded SourceSystem.

### R8 — Legacy taşınmamış alanlar (ORTA, MOD-0155 gelene kadar)
MicroTarget, Activity/Visit/ActivityReport, visit status state machine, çakışma kontrolü, aynı-gün-aynı-tip engeli, schedule engine, hastane→yakın eczane rota logic'i, frequency/cadence. **Legacy'nin en değerli bilgisi ve şu an yalnız tek bir matris satırında yaşıyor.** Kural çıkarımı ertelendikçe kaybolma riski artıyor.

### R9 — Pharma-specific riskler (ORTA)
- **HCP identity SoR açık** (CRM mi MDM mi) — MOD-0150/0155 için kritik.
- Consent engine (MOD-0164) yok; bugün yalnız read-only seam. Pharma'da HCP iletişim izni **yasal zorunluluk**.
- Frequency/cadence veri kaynağı belirsiz.
- `MicroZoneProfile`'ın pharma-özel alanlarla şişme riski.

### R10 — Multi-tenant / generic CRM riskleri (ORTA)
- Generic CRM'in bel kemiği **Lead→Opportunity→Forecast tamamen yok**; bugünkü CRM "master data + territory" ürünü, satış ürünü değil.
- Pharma dışı bir tenant için "ziyaret" kavramı da yok → ayakkabı/FMCG tenant'ı bugün bu CRM'i **kullanamaz**.
- Reference-data ile tenant farklılaştırma çalışıyor ama **tenant-tipi konfigürasyon bayrağı yok** (§13 Öneri-6).

### R11 — Governance drift (ORTA)
`module-implementation-status.md`: MOD-0151 "Başlanmadı %0", bölüm başlığı "Reserved, no code yet", "`services/Diten.CrmService/` does not exist". Üçü de yanlış. MOD-0023 için de benzer drift daha önce tespit edilmiş (F11).

---

## 12. Önerilen Backlog

| Sıra | İş | Modül/FU | Öncelik | Neden | Bağımlılık | Beklenen çıktı |
|---|---|---|---|---|---|---|
| 1 | **CRM kodunu commit et** (CrmService + Views/CRM + js/CRM + Resources) | Foundation | **P0** | 789 dosya untracked; tek komutla tüm CRM kaybolabilir | Yok | `feature/crm-integration` üzerinde temiz commit + `git status` kanıtı |
| 2 | **FU04B Plan vs Current — pack scope + runtime authorization** | MOD-0151 FU04B | **P0** | FU04B pack §22'de tanımlı değil; yetkisiz implementasyon pack ihlali | Yok | Pack §22'ye FU04B satırı + `mod-0151-fu04b-pack-runtime-scope-authorization-*.md` |
| 3 | **FU04B implementasyon** — Model Detail içinde read-only "Plan vs Current" tab'ı | MOD-0151 FU04B | **P0** | Planlanan vs gerçek sapma bugün görünmüyor; FU06 diff'inin okuma yüzeyi | Sıra 2 | Tab + karşılaştırma DTO'su + 7 dil RESX + test + smoke |
| 4 | **FU05 Live Smoke Closeout** — matched data hazırla, apply→history→coverage→409→400→override→end | MOD-0151 FU05 | **P0** | 07-28 FAIL; 07-31 apply belgesiz; conflict/override canlıda hiç koşmadı | Çalışan fleet + rule'la eşleşen account'lar | `mod-0151-fu05-live-smoke-closeout-retry-*.md` **PASS** + orphan binary-id kayıt kararı |
| 5 | **FU06 Pack Authorization** + direct-apply vs approval-required politikası | MOD-0151 FU06 | **P0** | FU06 pack'te tanımlı ama runtime scope yetkisi yok; politika kararı EA'ya ait | Sıra 4 | `mod-0151-fu06-pack-runtime-scope-authorization-*.md` + politika matrisi |
| 6 | FU06 implementasyon başlangıcı (MOD-0023 Start Instance + TerritoryChangeRequest) | MOD-0151 FU06 | **P0 (zaman kalırsa)** | MOD-0023 runtime hazır; approval Blueprint DepGate'i | Sıra 5 | Submit/approve/reject iskeleti |
| 7 | **Registry drift düzeltmesi** (MOD-0151 %0 → gerçek durum; "no code yet" başlığı) | Governance | **P0** | Yanlış registry yanlış planlama üretir | Yok | Güncel `module-implementation-status.md` |
| 8 | FU06 full closeout (transition gate, approval trace, before/after diff, immutable snapshot, Change Approval Trace sayfası) | MOD-0151 FU06 | **P1** | Blueprint zorunlu soft page | Sıra 6 | FU06 PASS + canlı smoke |
| 9 | **FU07 Evidence Pack + audit export** | MOD-0151 FU07 | **P1** | Blueprint zorunlu; denetlenebilirlik | Sıra 8, MOD-0021 | `TerritoryEvidencePack` + export + correlation id |
| 10 | **FU08 Import/Export Hardening** (XLSX template/export/dry-run/apply) | MOD-0151 FU08 | **P1** | Model elle girilemez ölçekte; MOD-0150 deseni hazır | Sıra 9 | Template + dry-run + satır bazlı hata raporu |
| 11 | **FU09 Visit/Route Readiness API** + derived contact coverage | MOD-0151 FU09 | **P1** | MOD-0155'in SoR-temiz tüketimi; contact coverage kararı kodlanmalı | Sıra 10 | Coverage/roll-up/microzone-accounts/derived-contact endpoint'leri |
| 12 | **Türkiye district datası import** | MOD-0048 ops | **P1** | Zone tanımı ve rota planlama bunun üstüne kurulacak | Yok | `imports/preview`→`commit` ile tam il→ilçe |
| 13 | **MOD-0018-FU15 Real DataScopeResolver** | MOD-0018 | **P1** | Territory/field-force ABAC scoping bloklu; build-lane P0 blocker demiş | Platform | MR'ın yalnız kendi zone'unu görmesi |
| 14 | Mongo replica set (dev/deployment) | Infra | **P1** | Native transaction; compensation fallback kalıcı çözüm değil | Yok | `SupportsTransactionsAsync=true` |
| 15 | TerritoryManagement DataTable şablon borçları (18 FAIL) | MOD-0151 UI | **P1** | Verifier borcu birikiyor | Yok | 91/0 |
| 16 | **MOD-0152 Lead Management** (pack → impl) | MOD-0152 | **P2** | Generic CRM çekirdeği; Account master'ı aday kaydıyla kirletmeyi durdurur | MOD-0149/0150, MOD-0023 | Lead + routing + convert |
| 17 | **MOD-0153 Opportunity & Pipeline** | MOD-0153 | **P2** | Pipeline görünürlüğü; forecast girdisi; MOD-0155'in Blueprint DepGate'i | Sıra 16 | Opportunity + stage model + Pipeline Cockpit |
| 18 | **MOD-0154 Forecast & Quota** | MOD-0154 | **P2** | Kota territory+position'a bağlanır; FU04A current responsibility API'si bunun için | Sıra 17, MOD-0151 | Forecast + quota + Quota Change Trace |
| 19 | **MOD-0155 Field Sales / Visit Planning — legacy preservation design pack (ERKEN)** | MOD-0155 | **P2 (pack erken)** | Legacy'nin en değerli kural kümesi kaybolmadan çıkarılmalı; build-lane "pack erken, impl geç" diyor | Legacy kural çıkarımı + 3 EA sorusu | MicroTarget/Activity/schedule/route kuralları yazılı pack |
| 20 | **MOD-0155 implementasyon** (visit + route çekirdeği) | MOD-0155 | **P2** | Pharma'nın asıl işi | Sıra 11, 12, 19 | Visit plan + route + Visit Plan Monitor |
| 21 | **MOD-0164 Consent & Preference** | MOD-0164 | **P2** | Pharma'da HCP iletişim izni yasal zorunluluk; MOD-0150 seam'i bunu bekliyor | MOD-0150 | Consent engine + Consent Trace View |
| 22 | Product / Brand master + Brand Scope | MDM + MOD-0151 | **P3** | Brand kotası, MR brand portföyü, detailing içeriği buna bağlı | MDM kararı | Brand master + MOD-0151 Brand Scope |
| 23 | Digital Detailing / presentation content | MOD-0155 alt | **P3** | Pharma'da ziyaret içeriği; generic'te ürün kataloğu sunumu | Sıra 20, 22 | Content + sunum kaydı |
| 24 | Visit Survey / feedback | MOD-0155 alt | **P3** | Doktor feedback / bayi memnuniyeti | Sıra 20 | Metadata-driven soru seti |
| 25 | GPS check-in/out verification | MOD-0155 alt | **P3** | Blueprint `CRM-VISIT-BUNDLE` zaten geo/time binding istiyor | Sıra 20 | Konum+zaman kanıtı + compliance export |
| 26 | Advanced analytics / Territory health dashboard | MOD-0151 + MOD-0154 | **P3** | Coverage boşluğu, atanmamış account, çakışma sayıları | Sıra 11 | Dashboard |

---

## 13. Ek Öneriler ("bence bunu da yaparsanız iyi olur")

| # | Öneri | Neden iyi olur | Bağlanacağı yer | Ne zaman | Scope riski |
|---|---|---|---|---|---|
| 1 | **Contact derived territory coverage read-model** | §4'teki karar bugün yalnız yazılı; kodda yok. MR'ın "benim doktorlarım" listesi bundan gelir. Yapılmazsa birileri Contact'a `TerritoryId` eklemeye kalkar | MOD-0151 **FU09** | FU09 ile birlikte | **Düşük** — read-only projection, pack §11.2 zaten yetkilendiriyor |
| 2 | **Resource Plan vs Current tab** | Zaten P0 backlog'da (FU04B). Ana menü sayfası değil, model detail tab'ı olması scope'u dar tutar | MOD-0151 **FU04B** | **Hemen** | Düşük — read-only sınırı korunursa |
| 3 | **Territory health dashboard** | Kapsanmayan account sayısı, atanmamış node, çakışan primary, süresi dolan model — bugün hiçbiri tek bakışta görünmüyor | MOD-0151 FU09 üzerine ince UI | **Sonra** (FU09'dan sonra) | **Orta** — "dashboard" kolayca yeni modüle dönüşür; tek sayfa + mevcut API'ler kuralı konmalı |
| 4 | **CRM integration contract catalog** | Blueprint 4 bundle adlandırmış (`CRM-CORE`/`TERRITORY`/`SALES`/`VISIT-BUNDLE`) ama endpoint/şema karşılıkları hiçbir yerde listelenmemiş. SAP/Oracle/dış sistem entegrasyonunda ilk istenecek belge | `execution/domains/commercial-suite/crm-integration-contracts.md` (yeni **doküman**, kod değil) | **Sonra** (FU07 civarı) | Düşük — dokümantasyon |
| 5 | **Field visit readiness checklist** | MOD-0155'e geçmeden "neyimiz hazır?" sorusunun tek sayfalık cevabı: district datası, geo alanları, coverage API, consent, position directory, frequency kaynağı. Yarım hazırlıkla MOD-0155'e girilmesini engeller | MOD-0155 pack ön hazırlığı | **Sonra** (P2 başında) | Düşük |
| 6 | **Pharma vs generic tenant configuration flags** | Bugün farklılaştırma reference-data ile yapılıyor (doğru) ama "bu tenant pharma mı" bilgisi hiçbir yerde yok → ileride UI label'ı, zorunlu alan, varsayılan seviye adları için gerekecek. **Feature flag değil, tenant profile** olmalı | Platform tenant profile + CRM tüketimi | **Sonra** (MOD-0155 öncesi) | **Orta-Yüksek** — kolayca "pharma modu" adında paralel kod yoluna dönüşür. Kural: bayrak yalnız **varsayılan reference set seçimini ve label'ı** etkiler, **iş kuralını asla** |
| 7 | **Position Directory alignment** | FU04A'nın PARTIAL sebeplerinden biri. HCM/MOD-0288 position master'ı runtime authority olmadan territory atamaları "deterministic policy seam"e dayanıyor | MOD-0288 / HCM ↔ MOD-0151 FU04A follow-up | **Sonra** (FU06 ile paralel) | Düşük — mevcut seam'in gerçek kaynağa bağlanması |
| 8 | **Legacy CRM rule traceability matrix** | §9'daki tablonun canlı ve satır-bazlı hâli: her legacy kural → hedef modül → durum (taşındı/taşınmadı/taşınmayacak) → kanıt. Bugün 13 satırlık özet var; MOD-0155'in gerçek kural sayısı bunun çok üstünde. **Ertelendikçe kural kaybı riski artıyor** | `legacy-value-preservation.md` genişletmesi + MOD-0155 pack | **Hemen başlat, MOD-0155 pack'iyle bitir** | Düşük — doküman |
| 9 | **Demo scenario pack** | Uçtan uca tek senaryo: Türkiye modeli → Marmara/İstanbul/Beylikdüzü → Alpha BU → hastane+eczane account'ları → doktor contact'ları → rule → preview → apply → MR ataması → replacement. Hem smoke datası hem satış demosu hem regression fixture'ı | `execution/domains/commercial-suite/` altında senaryo + seed script | **Hemen** (FU05 smoke datası zaten gerekiyor — aynı işle çıkar) | Düşük — smoke ihtiyacıyla birleşir |

---

## 14. Final Yol Haritası

### 14.1 Pazartesiye kadar en gerçekçi hedef
Sırayla ve bu kapsamla:
1. **CRM kodunu commit et** (sıra 1) — en yüksek risk, en düşük efor.
2. **FU05 Live Smoke Closeout PASS** (sıra 4) — apply zaten çalışıyor; eksik olan matched data + conflict/override/end zinciri + rapor.
3. **FU04B pack authorization + implementasyon** (sıra 2–3) — read-only tab, dar kapsam.
4. **FU06 pack authorization + politika matrisi** (sıra 5) — karar işi, kod değil.
5. **Registry drift düzeltmesi** (sıra 7) — 15 dakikalık iş.

**Zaman kalırsa** FU06 implementasyon başlangıcı (sıra 6). **Gerçekçi beklenti: 1–5 biter, 6 başlar.**

### 14.2 Territory Management ne zaman "closeout" denebilir?
**FU07 (Evidence Pack) bittiğinde.** Gerekçe: Blueprint MOD-0151 için üç soft page zorunlu kılıyor — *Territory Model Viewer* (FU02'de ✅), *Change Approval Trace* (FU06), *Evidence Pack* (FU07). Üçü tamamlanmadan Blueprint uyumu iddia edilemez.
- **FU08** (import/export) ve **FU09** (readiness API) closeout'un parçası değil, closeout **sonrası** hardening/enablement'tır.
- Ek closeout şartları: FU05 canlı zincir PASS, DataTable verifier borcu kapalı, Mongo replica set üzerinde native transaction doğrulanmış.
- **Tahmin: FU04B + FU05 closeout + FU06 + FU07 = Territory closeout.**

### 14.3 CRM Core için sıradaki büyük modül
**MOD-0152 Lead Management.** Gerekçe:
- Blueprint W-3 (MOD-0155'ten önce), DepGate'i (Workflow Designer) **mevcut**.
- Bugün "müşteri adayı" kavramı yok; her ilgi Account olarak açılıyor ve master'ı kirletiyor.
- MOD-0153'ün ön koşulu, MOD-0153 de MOD-0155'in Blueprint DepGate'i → **kritik yolun başlangıcı**.
- Generic ve pharma tenant'ın **ikisi için de** anlamlı; tenant-agnostic dengeyi bozmaz.

Hemen ardından **MOD-0153 Opportunity**, sonra **MOD-0154 Forecast**.

### 14.4 MOD-0155 Visit Planning'e ne zaman geçilmeli?
**İki ayrı zamanlama:**
- **Legacy preservation design pack'i HEMEN** başlamalı (P2 sırası 19, ama takvimsel olarak erken). Legacy'nin en olgun kural kümesi bugün tek bir matris satırında yaşıyor; her geçen ay kural kaybı riski. Build-lane planı da "pack erken, impl geç" diyor.
- **Implementasyon: MOD-0151 FU09 + MOD-0153 sonrası.** Ön koşullar: FU09 coverage API, **Türkiye district datası**, MOD-0018-FU15 ABAC, product/brand master, ve üç açık EA sorusunun (frequency kaynağı, daywork/VisitMix, HCP identity SoR) cevaplanması.
- **Blueprint sapma önerisi (EA onayına tabi):** MOD-0155'in visit/route çekirdeği MOD-0153'ü beklemesin; yalnız "opportunity'ye bağlı ziyaret" alt-kabiliyeti MOD-0153'e gate'lensin. Pharma'nın işi doktor ziyaretidir, opportunity değil.

### 14.5 Final Verdict

## **PARTIAL**

**PASS tarafı:**
- Rapor mevcut kaynaklara göre çıkarıldı — 60+ audit raporu, 5 governance dokümanı, 4 module pack, canlı kod ağacı ve **Blueprint_Data sheet'i doğrudan xlsx'ten okunarak** (MOD-0149…0155 canonical satırları: wave, DepGate, SoR, soft pages, min contract).
- Yapılanlar ve yapılacaklar modül/FU bazlı netleşti; 26 satırlık backlog P0–P3 önceliklendirmesiyle çıktı.
- Pharma + tenant-agnostic dengesi §8'de karara bağlandı; mevcut tasarımın bu dengeyi **koruduğu** doğrulandı.
- Eski sistem analizleri §9'da 13 başlıkta taşındı/taşınmadı/taşınmamalı olarak değerlendirildi.
- Capability plan dışına çıkılmadı; yeni modül uydurulmadı. Tek sapma **önerisi** (MOD-0155 ↔ MOD-0153 gate gevşetmesi) açıkça EA onayına tabi olarak işaretlendi.

**PARTIAL gerekçeleri:**
1. **Birincil legacy analiz dokümanları bu repo'da yok.** DitenCRM / CrmV2 / legacy pharma kaynak analizleri repo dışında; §9 değerlendirmesi `legacy-value-preservation.md` matrisi ve MOD-0149 pack §25 üzerinden yapıldı — yani **ikincil özet** üzerinden. Legacy kural sayısının tamamının bu 13 satıra sığmadığı neredeyse kesin (§13 Öneri-8).
2. **FU05'in bugünkü gerçek durumu belgeye dayanmıyor.** 2026-07-31'de apply'ın çalıştığı bilgisi oturum hafızasından geliyor; `docs/audits/` altında karşılığı yok. En güncel FU05 canlı raporu hâlâ **FAIL**.
3. **Yüzde değerleri yorum içerir.** "%60 territory", "%30 pharma hazırlık" gibi rakamlar FU sayımı ve kapsam ağırlığından türetilmiş tahminlerdir; ölçülmüş metrik değildir.
4. **Canlı doğrulama yapılmadı** (bu task'ın kapsamı gereği): fleet çalıştırılmadı, endpoint çağrılmadı, Mongo okunmadı.

**FAIL değil, çünkü:** CRM dışı scope şişirilmedi, eski sistem analizleri dikkate alındı, pharma dışı tenant uyumluluğu §8'de ayrıca değerlendirildi, capability plan sınırları korundu.

---

## 15. Bu koşuda oluşturulan dosyalar

| Dosya | Aksiyon |
|---|---|
| `docs/audits/crm-capability-progress-review-2026-07-31.md` | Created |

Kod, runtime, module pack, reference data, RBAC, Gateway ve Mongo değiştirilmedi. Smoke çalıştırılmadı.
