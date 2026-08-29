# MOD-0151 Territory Management — Pack Prep / Blueprint-Aligned Architecture Design

> **Tarih:** 2026-07-23 · **Tür:** Module pack hazırlık + mimari tasarım (preflight) · **Runtime kod:** ÜRETİLMEDİ
> **Otorite sırası:** Blueprint Excel (`docs/System Capability & Implementation Blueprint - master 7.xlsx`, `Blueprint_Data`) >
> Domain Config (`execution/domains/commercial-suite/domain-config.md`) > AGENTS.md > `.antigravity/rules/`
> **Durum:** `NEEDS_USER_DECISION` (Q1–Q7)

---

## 1. Preflight

### 1.1 İncelenen kaynaklar

| Kaynak | Ne için |
|---|---|
| `docs/System Capability & Implementation Blueprint - master 7.xlsx` — `Blueprint_Data`, `Module Pages`, `Dependencies`, `Dependencies_Normalized`, `SoR_Map`, `Contract Bundle Dictionary` | MOD-0151 canonical satırı (ana otorite) |
| `execution/domains/commercial-suite/README.md` · `domain-config.md` · `crm-sor-boundary.md` · `crm-build-lanes.md` · `crm-rbac-integration-plan.md` · `legacy-value-preservation.md` | CRM domain sınırları, SoR matrisi, RBAC planı, legacy değer aktarımı |
| `module-packs/MOD-0149-customer-360-account-hierarchy.md` (özellikle §3.1 Zone/MicroZone ownership) | Account master sınırı + Coverage projection kontratı |
| `module-packs/MOD-0150-contact-relationship-management.md` | Contact SoR, Contact↔Account link, pack formatı |
| `docs/audits/mod-0150-contact-location-pii-kvkk-hardening.md`, `mod-0150-contact-import-export-task1/task2`, `mod-0150-final-validation-closeout.md`, `mod-0149-runtime-readiness-closeout.md` | Contact lokasyon/PII, import/export deseni, closeout durumları |
| `services/Diten.CrmService/**` (Account.cs, Contact.cs, Features/*, Controllers/CRM/*) | Mevcut runtime sınırı — `ZoneId/MicroZoneId/TerritoryId` hiçbir yerde yok (doğrulandı) |
| `docs/modules/MOD-0023-workflow-approvals-analizi.md` | Workflow Designer runtime gerçekliği (Start Instance + Transition Gate) |
| `services/Diten.Platform/.../Authorization/OrgDataScopeResolver.cs`, `EntitlementDataScopeKind.cs` | MOD-0018-FU15 gerçek durumu ve Territory scope boşluğu |
| `services/Diten.Platform/.../Entities/Organization/OrganizationUnit.cs` | Business Unit master adayı (MOD-0288) |
| `gateway/Diten.ApiGateway/ocelot.json` | `/api/crm/*` route konvansiyonu |
| `execution/registries/module-implementation-status.md` | MOD-0018/0021/0048/0288/0285/0220 gerçek olgunluk yüzdeleri |

### 1.2 Excel MOD-0151 satırının okunuşu (ham)

`Blueprint_Data` satırı birebir:

```
Module ID                      : MOD-0151
Module Name                    : Territory Management
Domain / Landscape             : 4) Enterprise Application Ecosystem
Suite / Platform               : Commercial Suite (CRM + O2C)
Capability Group               : CRM Core
Aim / Goal (Capability Group)  : Maintain customer master and commercial structure with governed
                                 integrations and consistent semantics.
Wave                           : W-4
Dependency Gate                : Customer 360; Workflow Designer
Delivery Outcome / Value       : Controlled territory changes; performance tracking
Soft Pages                     : Territory Model Viewer; Change Approval Trace; Evidence Pack
Placement                      : Domain App (CRM)
Min Integration Contracts      : CRM-TERRITORY-BUNDLE (territory model schema, approvals,
                                 audit/evidence export)
SoR Module                     : SoR: territories, assignments, territory change approvals
SoR Primary Object(s)          : territories
Integration Contracts          : DOMAIN-APP-BASE
Deployment Unit / Product      : Domain Applications
Build / Buy / Partner          : Buy/Partner
SLO Tier                       : Tier 2
Support Model                  : L1 Service Desk; L2 Domain App Ops; L3 Vendor / Partner
AI Enablement Tier             : Assist
AI Use Cases                   : Summarize; Extract; Recommend; Route
AI Dependency Gate             : Prompt Registry; HITL; Model Registry; Eval/Drift; Logging
AI Risk Class                  : Medium
SoR Applicability / AI Applic. : Y / Y
```

### 1.3 Prompt ↔ Excel farkları (Excel kazanır)

| Alan | Prompt'ta yazan | Excel'de gerçek | Karar |
|---|---|---|---|
| Support Model | L2 CRM Ops; L3 CRM Product/Tech Eng | **L2 Domain App Ops; L3 Vendor / Partner** | Excel esas; pack `support_model` alanı Excel'i yazar |
| Build/Buy/Partner | (yok) | **Buy/Partner** | ⚠️ Repo stratejisi in-house build (`Diten.CrmService`). Bu bir **EA governance sapması**; MOD-0149/0150 aynı satırlarda da build edildi → precedent var. Pack'te açıkça "in-house build, Blueprint Buy/Partner ile sapma — EA onayı" notu gerekir. Blocker değil. |
| Dependency Gate | Customer 360; Workflow Designer | Aynı + `Dependencies` sayfasında **MOD-0068 / MOD-0069 / MOD-0066 / MOD-0067 / MOD-0041 HARD** | AI gate'leri **yalnız AI Assist özelliği açılırsa** bağlayıcı. MOD-0151 v1 **AI-OFF** teslim edilir → gate düşer (bkz. §21 Q6) |
| Soft Pages | 3 sayfa | Excel `Module Pages` sayfası **3 named + ~25 generic CRM soft page** listeliyor | Named 3 sayfa **zorunlu**; generic sayfalar (Overview Dashboard, Approvals Inbox, Trace/Audit Viewer, Configuration…) opsiyonel/ileri FU |

### 1.4 No-implementation confirmation

Bu görevde **hiçbir** runtime kod, entity, endpoint, UI, migration, permission seed, reference set, registry kaydı veya yeni module ID üretilmedi. Yalnız okuma (Read/Grep/xlsx parse) yapıldı. `MOD-0151` zaten Blueprint-canonical bir ID'dir; yeni ID uydurulmadı. Bu dosya bir **audit/design** dokümanıdır, pack değildir.

---

## 2. Excel-derived Capability Summary

| Blueprint Field | Value | Required Design Implication |
|---|---|---|
| Module ID / Name | MOD-0151 / Territory Management | Pack `name` birebir "Territory Management" olmalı (DCP-002 canonical-name gate) |
| Domain / Suite / Group | Enterprise App Ecosystem / Commercial Suite (CRM + O2C) / **CRM Core** | `commercial-suite` domain'i, `Diten.CrmService` servisi (MOD-0149/0150 ile aynı bounded context) |
| Aim / Goal | "Maintain customer master and commercial structure with governed integrations and consistent semantics" | Territory = **commercial structure**'ın kendisi. "Governed" → approval + audit zorunlu; "consistent semantics" → MOD-0048 reference-driven, hardcoded enum yasak |
| Wave | W-4 | MOD-0149 (W-1) ve MOD-0150 (W-3) tamamlandıktan sonra. Bugün ikisi de runtime'da → wave ön koşulu **karşılanmış** |
| Dependency Gate | Customer 360; Workflow Designer | MOD-0149 **hard** (AccountId kaynağı), MOD-0023 **hard** (activation approval). MOD-0023 runtime'da mevcut → fake approval yasak, gerçek seam kurulabilir |
| Delivery Outcome | **Controlled territory changes; performance tracking** | İki zorunlu çıktı: (a) aktif model doğrudan mutate edilemez → change request + approval + versioning; (b) roll-up/read-model altyapısı hazırlanmalı |
| Soft Pages | Territory Model Viewer; Change Approval Trace; Evidence Pack | Bu 3 sayfa **teslim zorunlu**; UI önerisi bunlarla başlamalı |
| Placement | Domain App (CRM) | Tenant shell, `_LayoutTenantShell`, Golden Reference Compact |
| Bundle | CRM-TERRITORY-BUNDLE (territory model schema, approvals, audit/evidence export) | Kontrat 3 parçalı: **schema** + **approvals** + **evidence export**. Evidence export opsiyonel değil, bundle'ın parçası |
| SoR | **territories, assignments, territory change approvals** | Üç SoR sınıfı. `assignments` çoğul ve niteliksiz → hem account hem resource assignment MOD-0151'e ait. `SoR_Map` collision count = 0 → başka modülle çakışma yok |
| Integration Contract | DOMAIN-APP-BASE | Standart domain-app kontrat tabanı (tenant isolation, authz, audit, health) |
| SLO Tier | Tier 2 | Planning modülü; runtime-critical değil |
| AI Tier / Risk | Assist / Medium; gate: Prompt Registry, HITL, Model Registry, Eval/Drift, Logging | AI Assist (territory önerisi, çakışma özeti) **v1 kapsamı dışı**; açılırsa 5 hard gate devreye girer |
| Build/Buy/Partner | Buy/Partner | ⚠️ In-house build sapması → EA notu (bkz. §1.3) |

---

## 3. CRM Source-derived Requirements

| Source | Requirement | MOD-0151 Impact |
|---|---|---|
| `crm-sor-boundary.md` | "Territory / Zone / MicroZone tanımı + rep/account assignment = MOD-0151. **MicroZone burada tanımlanır**" | MicroZone MOD-0151'in içindedir; MOD-0155 yalnız tüketir |
| `crm-sor-boundary.md` | Employee / Sales Rep master = MOD-0288; Business Unit master = MOD-0288/Platform-Org; Brand/Product = MDM | ResourceAssignment **referans** tutar, employee/BU/brand master **kurmaz** |
| `MOD-0149 §3.1` (kesin karar) | "MOD-0149 provides location foundation; MOD-0151 owns coverage assignment." Account'ta `ZoneId/MicroZoneId` **yasak**; Coverage MOD-0151'den **read-only projection** (`CoverageSummary` DTO) | MOD-0151, MOD-0149'a bir **CoverageSummary projection API'si borçlu**. Bu bugün Account 360'ta "Not assigned" olarak duruyor → MOD-0151 bunu doldurur |
| `MOD-0149 §10` | Account'ta `CountryRef/CityRef/DistrictRef/Latitude/Longitude` var (MOD-0048 reference) | Geography rule'ları bu alanlar üzerinden çalışır; MOD-0151 adres kopyalamaz |
| `MOD-0150 §3` | Contact SoR + Contact↔Account link (`AccountContactLink`, M:N, role+primary) MOD-0150'de; "No Zone/MicroZone/Territory/SalesRep" | Contact coverage **türetilmiş** olmalı: Contact → AccountContactLink → Account → AccountTerritoryAssignment |
| `MOD-0150` Contact entity yorumu | Contact'ta country/coverage context "future MOD-0151 Territory / MOD-0155 Visit" için tutuluyor | Cross-country coverage kontrolü için hazır sinyal var; doğrudan ContactTerritoryAssignment gerekmiyor |
| `legacy-value-preservation.md` | "MR zone / micro-zone yetkisi → MOD-0151 (tanım) + MOD-0155 (tüketim), Rule capture + ABAC (FU15)"; "Legacy yetki tablosu taşınmaz" | Legacy MR-zone yetki tablosu **şema olarak kopyalanmaz**; kural olarak `TerritoryResourceAssignment` + data-scope'a çevrilir |
| `legacy-value-preservation.md` | "Hastane doktorları → yakın eczane rota önerisi = MOD-0155 route-plan; geo veri MOD-0048/MDM'den" | Mesafe/rota MOD-0151'de **yok**; MicroZone yalnız kümeleme birimi |
| `crm-rbac-integration-plan.md §3` | Mevcut MOD-0151 anahtar önerisi: `crm.territory.read/create/update/assign-rep/assign-account`, `crm.micro-zone.manage` | Bu liste **yetersiz** (model/activate/approval/evidence yok) ve `crm.micro-zone.manage` yanlış (MicroZone ayrı nesne değil, node level). §14'te supersede önerisi |
| `crm-rbac-integration-plan.md §6-7` | "Rep sadece kendi territory/micro-zone account'ları" kuralı MOD-0018-FU15'e bağlı; plan FU15'i `planned/reserved` sayıyor | **Güncellenmiş gerçek:** FU15 `OrgDataScopeResolver` %90 backend-done. Ama emit ettiği scope kind'lar OrgUnit/Position/ManagerChain/LegalEntity — **Territory yok** (bkz. §21 Q5) |
| `crm-build-lanes.md` | `crm-territory-core` lane = MOD-0151, W-4, "P1 (FU15 bağımlı)" | Lane sırası doğru; FU15 bağımlılığı artık kısmen çözülmüş |
| `MOD-0150 import/export task1/task2` | XLSX template → export → upload → **dry-run** → apply deseni + `crm.*.import/export` izinleri | FU08 birebir bu deseni izler; yeni import framework yazılmaz |

---

## 4. Legacy CRM Mapping

| Legacy Concept | Target Concept | Excel Support | CRM Source Support | Keep/Change | Reason |
|---|---|---|---|---|---|
| Country | `TerritoryNode(level=country)` + `CountryCode` (MOD-0048 `country` referansı) | Dolaylı (`territories`) | ✅ `crm-sor-boundary` (Country = MOD-0048) | **Change** | Ülke bir **reference value**; territory node'u ülkeyi kopyalamaz, referans verir |
| Division | `TerritoryNode(level=division)` | Dolaylı | ⚠️ dokümanlarda geçmiyor | **Keep (opsiyonel level)** | Çok ülkeli/çok-BU yapılarda üst kırılım; her tenant kullanmak zorunda değil |
| Region | `TerritoryNode(level=region)` | Dolaylı | ✅ (Regional Manager kavramı) | **Keep** | Klasik saha hiyerarşisi |
| Area | `TerritoryNode(level=area)` | Dolaylı | ✅ (Area Manager) | **Keep** | — |
| Zone | `TerritoryNode(level=zone)` | Dolaylı | ✅ `crm-sor-boundary`: "Territory / Zone / MicroZone tanımı MOD-0151" | **Keep** | MR'ın birincil sorumluluk birimi |
| MicroZone | `TerritoryNode(level=microzone)` + opsiyonel `MicroZoneProfile.AnchorAccountId` | Dolaylı | ✅ "MicroZone burada **tanımlanır**" | **Keep** | Ayrı aggregate **yapılmaz**; node level'dır |
| Business Unit (Alpha/Beta/Gamma) | `TerritoryBusinessScope` → `OrganizationUnit` (MOD-0288) **veya** `product-portfolio` reference kodu | ❌ Excel saymıyor | ✅ "Business Unit master = MOD-0288, read-only consume" | **Change** | Territory **level'ı değil**, kesişen bir **boyut**. Master MOD-0151'e ait değil (bkz. §9) |
| Alpha / Beta / Gamma | Stabil `BusinessUnit/ProductPortfolio` kodları (dönem bilgisi taşımaz) | ❌ | ⚠️ kaynak yok → Q1 | **Change** | Dönemsellik `TerritoryModelVersion` / `PlanningPeriod`'a taşınır; BU her çeyrek yeniden yaratılmaz |
| Production Admin | `TerritoryResourceAssignment(ResourceRole=admin)` veya destek rolü | ❌ | ⚠️ kaynak yok → Q2 | **Change** | Business unit değil; **resource/admin rolü**. BU listesine karıştırılmaz |
| Quarter setup | `PlanningPeriod` (`planning-period-type=quarterly`) + `TerritoryModel.VersionNumber` | Dolaylı ("controlled territory changes") | ✅ (versioning/approval kültürü) | **Change** | Çeyrek bir **planlama dönemi**, bir organizasyon nesnesi değil. Legacy'de çeyrek başına BU kopyalanması veri kopukluğu üretiyordu |
| Area Manager | `ResourceRole=area-manager`, `CoverageScope=territory-subtree` | ✅ (`assignments`) | ✅ | **Keep** | — |
| Regional Manager | `ResourceRole=regional-manager`, `CoverageScope=territory-subtree` | ✅ | ✅ | **Keep** | — |
| Medical Representative | `ResourceRole=medical-representative`, `CoverageScope=exact-territory` (zone/microzone) | ✅ | ✅ (MR zone yetkisi) | **Keep** | Legacy yetki **tablosu** taşınmaz, kural taşınır |
| Product Manager | `ResourceRole=product-manager`, `CoverageScope=product-portfolio` | ✅ | ⚠️ dolaylı | **Keep (change scope)** | TerritoryNode'a değil, **portfolio boyutuna** atanır |
| Business Unit resource | `ResourceRole=business-unit-manager`, `CoverageScope=business-unit` | ✅ | ⚠️ dolaylı | **Keep** | Cross-territory roll-up görüşü |
| Brand / Product list | **Sahiplenilmez** — `ProductPortfolioBrandAssignment` seam; brand kodu dış referans | ❌ | ✅ "Brand/Product/SKU = MDM, read-only consume" | **Change** | MOD-0151 product master kurmaz. MDM'de bugün yalnız LegalEntity var → Q3 |
| Clinic / Hospital / Pharmacy grouping | `AccountTerritoryAssignment` (MicroZone altında) + `AccountType` (MOD-0149/MOD-0048) | ✅ (`assignments`) | ✅ MOD-0149 AccountType | **Keep** | Gruplama = atama, yeni account tipolojisi değil |
| Center hospital / clinic | `MicroZoneProfile.AnchorAccountId` (opsiyonel) | Dolaylı | ✅ (legacy "merkez" kavramı) | **Keep (metadata-only)** | Anchor = planlama merkezi; rota başlangıcı **değil** (bkz. §12) |
| Manual territory move | `AccountTerritoryAssignment(AssignmentSource=manual|override)` + zorunlu `ChangeReason` + `TerritoryChangeRequest` | ✅ ("controlled territory changes", `territory change approvals`) | ✅ | **Keep (hardened)** | Legacy'de serbest taşıma vardı; artık **gerekçe + iz + onay** zorunlu |
| Yearly re-planning | Yeni `TerritoryModel` versiyonu (`planning-period-type=annual`, `BasedOnModelId`) | ✅ | ✅ | **Change** | Üzerine yazma değil, **supersede**. Geçmiş model arşivde kalır |
| Quarterly re-planning | Aynı mekanizma, `quarterly` PlanningPeriod | ✅ | ✅ | **Change** | BU/portfolio kimliği sabit kalır → geçmiş performans kopmaz |
| Mevcut account/contact import data readiness | MOD-0149 `AccountExternalReference` + MOD-0150 contact import verisi → territory import `AccountCode`/ExternalRef ile eşleşir | Dolaylı | ✅ MOD-0149 §10.1b, MOD-0150 import/export | **Keep** | Territory import **AccountId üretmez**; var olan account'ları eşler, bulunamayan satır dry-run'da hata verir |

---

## 5. Enterprise Benchmark Findings

| Vendor Pattern | Relevant Idea | MOD-0151 Design Use |
|---|---|---|
| **Salesforce** Enterprise Territory Management: `TerritoryModel` (Planning/Active/Archived) + `Territory2Type` + `Territory2Model` state machine | Model bir bütün olarak **draft'ta planlanır, tek seferde aktive edilir**; aynı anda tek Active model | `TerritoryModel.Status = draft→review→approved→active→superseded→archived`; scope başına tek aktif model |
| **Salesforce** `Territory2Rule` (assignment rules) + "Run rules" preview | Kurallar önce **preview** edilir, sonra apply | `POST .../preview-assignments` yan etkisizdir; `apply` ayrı komut + ayrı izin |
| **Salesforce** `ObjectTerritory2Association` (rule vs manual `AssociationCause`) | Atamanın **kaynağı** saklanır; kural yeniden koşunca manuel atama ezilmez | `AccountTerritoryAssignment.AssignmentSource ∈ {rule, manual, import, override}`; rule re-run manual/override kayıtlarını **ezmez** |
| **Salesforce** `UserTerritory2Association` | Kullanıcı↔territory ilişkisi ayrı nesne | `TerritoryResourceAssignment` ayrı aggregate; employee master'a dokunulmaz |
| **SAP CRM/S4** Territory Hierarchy: seviye adları **konfigüre edilebilir**, ağaç derinliği sabit değil | Seviyeler hardcoded enum değil | `TerritoryLevel` MOD-0048 reference set; tenant başına kullanılan seviye alt kümesi |
| **SAP** Territory validity (`Valid From/To`) + attribute-based assignment | Her düğüm ve atama **effective-dated** | Tüm entity'lerde `ValidFrom/ValidTo`; child tarihi parent tarihini aşamaz |
| **Oracle Sales** Territory *proposal* → simulate → **activate** akışı; aktif territory doğrudan düzenlenemez | Değişiklik bir **proposal nesnesi** üzerinden gider | `TerritoryChangeRequest` + `BeforeSnapshotRef/AfterSnapshotRef`; aktif model immutable |
| **Oracle** Territory coverage boyutları (geography, account, product, channel, customer size) | Territory tek boyutlu değil — **çok boyutlu kesişim** | `TerritoryNode` üzerinde Geo/Account/ProductPortfolio/Channel/Segment criteria; aynı account farklı boyutlarda birden çok territory'de olabilir |
| **Oracle/SAP** Quota & forecast territory hiyerarşisi üzerinden roll-up | Territory ağacı forecast'ın **boyut kaynağıdır**, forecast'ın kendisi değil | MOD-0151 yalnız roll-up read-model/seam üretir; MOD-0154 forecast'ı sahiplenir |
| **Salesforce/SAP** Territory realignment audit + "who moved this account, when, why" | Değişiklik izi zorunlu | `Change Approval Trace` sayfası + `TerritoryEvidencePack` (Excel'de zaten soft page) |
| **SAP** Sales Area (Sales Org × Distribution Channel × Division) — territory'den **ayrı** boyut | Organizasyon boyutu ≠ coğrafi boyut | Business Unit / Product Portfolio **level değil, scope**; §6'daki ayrımın kurumsal dayanağı budur |

---

## 6. Territory vs Business Unit vs Resource Clarification

| Concept | Is Territory? | Is Dimension/Seam? | MOD-0151 Ownership | Reason |
|---|---|---|---|---|
| Division / Region / Area / Zone / MicroZone | ✅ Evet (`TerritoryNode` level) | — | **Owns** | Excel `SoR: territories` |
| Country | ⚠️ Hem level hem reference | Reference (MOD-0048 `country`) | **Owns node, references value** | Ülke değeri MOD-0048; node MOD-0151 |
| Business Unit (Alpha/Beta/Gamma) | ❌ Hayır | ✅ Scope/dimension | **Referans (master MOD-0288)** | `crm-sor-boundary`: BU master = MOD-0288 read-only consume |
| Product Portfolio / Brand grubu | ❌ Hayır | ✅ Scope/dimension | **Seam (master MDM/Product)** | Brand/Product/SKU = MDM; MOD-0151 kod referansı tutar |
| Planning Period (yıl/çeyrek) | ❌ Hayır | ✅ Zaman boyutu | **Owns (model üzerinde)** | `TerritoryModel.EffectiveFrom/To` + `PlanningPeriodId` |
| Channel / Segment | ❌ Hayır | ✅ Kriter boyutu | **Referans (MOD-0048 / MOD-0167)** | Segment SoR = MOD-0167; MOD-0151 kriter kodu tutar |
| Account (hastane/eczane/klinik) | ❌ Hayır | Atama nesnesi | **Sadece atama** (`AccountTerritoryAssignment`) | Account master = MOD-0149 |
| Contact (doktor/eczacı) | ❌ Hayır | Türetilmiş kapsam | **Sahiplenmez** — derived read-model | Contact SoR = MOD-0150 |
| Employee / User (MR, manager) | ❌ Hayır | Atama nesnesi | **Sadece atama** (`TerritoryResourceAssignment`) | Employee master = MOD-0288/HCM |
| Position / OrgUnit | ❌ Hayır | ✅ Org boyutu | **Referans** | MOD-0288 SoR; resource hierarchy ≠ territory hierarchy |
| Production Admin | ❌ Hayır | Rol/grup | **Rol referansı** (Q2) | BU değil; admin/support rolü |
| Visit / Route / mesafe | ❌ Hayır | — | **Sahiplenmez** | MOD-0155 |
| Quota / Forecast | ❌ Hayır | — | **Sahiplenmez** (yalnız roll-up boyutu) | MOD-0154 |

---

## 7. Recommended Target Model

### 7.1 Açıklama

MOD-0151 üç SoR sınıfı etrafında kurulur (Excel birebir): **territories**, **assignments**, **territory change approvals**. Model şu ilkelerle tasarlanır:

1. **Versiyonlu plan nesnesi.** `TerritoryModel`, bir tenant + scope (country/division/BU/portfolio) + effective period için bir **plan sürümüdür**. Aktif model **immutable**'dır; değişiklik yeni draft/versiyon + change request ile olur. "Controlled territory changes" Excel çıktısının doğrudan karşılığı budur.
2. **Tek ağaç, konfigüre edilebilir seviyeler.** Division/Country/Region/Area/Zone/MicroZone **ayrı aggregate değildir**; hepsi `TerritoryNode` + `TerritoryLevel` (MOD-0048 reference). Bu, ülke bazlı derinlik farklarını (TR: Country>Region>Zone>MicroZone; DE: Division>Region>Area>Zone) şema değiştirmeden destekler.
3. **Atama ≠ master.** Account ve resource atamaları MOD-0151'e aittir; account/employee/BU/brand master'ları asla MOD-0151'e kopyalanmaz. Denormalize edilen tek şey **display snapshot**'tır (`AccountCode`), o da yetkili kaynak değildir.
4. **Kural → önizleme → uygula.** `TerritoryAssignmentRule` kuralları üretir, `preview-assignments` yan etkisiz sonuç döner, `apply` efektif-tarihli atama yazar. Rule re-run manuel/override atamaları ezmez.
5. **Onay gerçek.** Aktivasyon MOD-0023 üzerinden gider (Start Instance + Transition Gate). MOD-0023 hazır değilse **sahte onay üretilmez** → `approval-pending` durumunda kalır ve aktivasyon bloklanır.
6. **Kanıt ilk sınıf vatandaş.** `TerritoryEvidencePack`, Excel'in hem soft page'i hem bundle bileşeni. Model snapshot + atama sayıları + çakışmalar + onay izi + correlation id.
7. **Performans "hazırlık", "uygulama" değil.** MOD-0151 roll-up read-model ve coverage API'leri üretir; forecast (MOD-0154) ve ziyaret KPI'ları (MOD-0155) üretmez.

### 7.2 Aggregate / entity listesi

| # | Aggregate / Entity | Kind | Kısa tanım |
|---|---|---|---|
| 1 | `TerritoryModel` | aggregate root | Versiyonlu plan kabı; lifecycle + approval + activation |
| 2 | `TerritoryNode` | aggregate (model-scoped) | Hiyerarşi düğümü; level + kod + kriterler + geçerlilik |
| 3 | `TerritoryAssignmentRule` | aggregate (model-scoped) | Atama kuralı; tip + kriter + öncelik + çakışma politikası |
| 4 | `AccountTerritoryAssignment` | aggregate | Account↔Territory efektif-tarihli atama |
| 5 | `TerritoryResourceAssignment` | aggregate | Kişi↔(Territory\|BU\|Portfolio) rol bazlı kapsam ataması |
| 6 | `TerritoryChangeRequest` | aggregate | Kontrollü değişiklik + before/after snapshot + workflow bağı |
| 7 | `TerritoryEvidencePack` | aggregate (read-heavy) | Kanıt paketi metadata + export referansı |
| 8 | `TerritoryBusinessScope` | value object (`TerritoryModel` ve atamalar içinde) | BU/Portfolio/Brand-group/Channel/Segment referans üçlüsü (`ScopeType`, `ScopeCode`, `ExternalRef?`) — **master değil** |
| 9 | `MicroZoneProfile` | value object (`TerritoryNode` içinde, yalnız level=microzone) | `AnchorAccountId?`, `ClusterNotes?` — ayrı aggregate değil |
| 10 | `PlanningPeriodRef` | value object | `PeriodType` + `PeriodCode` + `From/To`; ayrı master modül çıkarsa referansa döner |
| 11 | `TerritoryCoverageReadModel` | query DTO (entity değil) | Account/Contact/Resource coverage projeksiyonları — MOD-0149/0155 tüketir |

**Alan detayları** prompt §H'deki listeyle birebir uyumludur; pack draft'ında tablo halinde yazılacaktır. Ek/değişen noktalar:

- `TerritoryNode.MicroZoneProfile` — `AnchorAccountId` node'un düz alanı değil, **level=microzone'a koşullu value object**. Diğer level'larda `null` olmak zorunda (validation).
- `TerritoryModel.BusinessScopes[]` — tek `BusinessUnitScope` yerine **çoklu** `TerritoryBusinessScope` listesi (bir model birden çok portfolyoyu kapsayabilir).
- `AccountTerritoryAssignment.AccountCodeSnapshot` — **yalnız display**, sorgu/eşleşme anahtarı değil, hiçbir zaman SoR değil.
- Hiçbir entity'de `Version` iş alanı olarak kullanılmaz (MOD-0149 §10 naming kuralı; concurrency için rezerve) → `VersionNumber` kullanılır.
- Tüm entity'ler `EntityBase` (tenant-owned), `TenantId` zorunlu, soft-delete, cross-tenant erişim **404**.

### 7.3 Ownership boundaries

**Owns:** territory model + versiyon/lifecycle · territory hiyerarşisi ve node'ları · territory level kullanımı (değer seti MOD-0048'de) · assignment rule'ları · account↔territory atamaları · resource↔territory/BU/portfolio atamaları · territory change request + approval trace · territory evidence pack + export · coverage read-model'leri.

**Does NOT own:** Account/WorkPlace master ve lokasyon (MOD-0149) · Contact ve Contact↔Account link (MOD-0150) · Employee/User/Position/OrgUnit/Business Unit master (MOD-0288) · Brand/Product/SKU master (MDM) · country/city/district ve tüm lookup değerleri (MOD-0048) · permission engine (MOD-0018) · workflow engine (MOD-0023) · audit store (MOD-0021) · evidence link/provenance store (MOD-0031) · quota/forecast (MOD-0154) · visit/route/mesafe (MOD-0155) · segment tanımı (MOD-0167).

---

## 8. Division / Region / Area / Zone / MicroZone Decision

**Karar: ayrı aggregate YAPILMAZ.** Tümü `TerritoryNode` + `TerritoryLevel` ile modellenir. Gerekçe: (a) seviye sayısı ülke/tenant'a göre değişir, ayrı aggregate şema değişikliği gerektirir; (b) hiyerarşi sorguları (subtree, roll-up, cycle check) tek koleksiyonda çok daha basit ve tutarlıdır; (c) Salesforce/SAP benchmark'ı da tek `Territory` nesnesi + tip/level yaklaşımını kullanır; (d) yeni seviye ihtiyacı (örn. `sub-region`) yalnız MOD-0048 reference set'e değer eklemekle çözülür.

| Level | Meaning | Required? | Configurable? | Notes |
|---|---|---|---|---|
| `division` | Satış organizasyonu/coğrafi üst kırılım (ülke üstü veya ülke içi) | Hayır | ✅ | Business Unit **değildir**; BU ayrı boyut (§9) |
| `country` | Ülke düzeyi düğüm | Hayır (ama tek-ülke tenant'ta pratik kök) | ✅ | `CountryCode` MOD-0048 `country` referansı |
| `region` | Bölge | Hayır | ✅ | Regional Manager kapsamı |
| `area` | Alt bölge | Hayır | ✅ | Area Manager kapsamı |
| `zone` | MR'ın birincil sorumluluk birimi | ⚠️ Pratikte zorunlu (MR ataması buraya bağlanır) | ✅ | Bir modelde en az bir leaf-benzeri seviye gerekir |
| `microzone` | Zone içi planlama kümesi (semt/hastane çevresi) | Hayır | ✅ | `MicroZoneProfile.AnchorAccountId` opsiyonel; MOD-0155 tüketir |

**Seviye sırası kuralı:** `TerritoryLevel` reference değerleri bir `sortOrder`/`rank` metadata taşır (MOD-0048 value metadata — MOD-0150 `account-relationship-type` direction/inverse metadata precedent'i ile aynı desen). Validation: child'ın rank'i parent'ın rank'inden **büyük** olmalı; **atlamak serbest** (Country→Zone geçerli), **geri gitmek yasak** (Zone→Region hata). Sıra hardcoded değildir; kod yalnız "rank artmalı" kuralını bilir.

---

## 9. Business Unit / Brand / Quarter Decision

| Concept | Decision | Reason | Data-loss Risk Avoided |
|---|---|---|---|
| **Business Unit (Alpha/Beta/Gamma)** | **Territory level DEĞİL.** `TerritoryBusinessScope(ScopeType=business-unit, ScopeCode)` boyutu. Master **MOD-0288 OrganizationUnit** referansı (Q1'e bağlı) | `crm-sor-boundary`: BU master = MOD-0288 read-only consume. Excel MOD-0151 SoR'unda BU yok | BU'yu territory ağacına gömmek, aynı bölgenin iki BU için iki kez modellenmesini ve hiyerarşi patlamasını doğurur |
| **BU master'ının sahibi** | MOD-0151 **kurmaz**. Bugün `OrganizationUnit`'te `unitType` alanı **yok** → ya MOD-0288'e `unitType` eklenir (MOD-0288'in işi), ya da MOD-0048 `business-scope-type` + tenant-owned `business-unit` reference set kullanılır | MOD-0151 org master fork edemez | BU'nun iki yerde tanımlanması (org + CRM) → drift |
| **Alpha/Beta/Gamma'nın doğası** | **Varsayım: stabil satış organizasyonu / portfolyo.** Dönemsel kampanya grubu ise `PlanningScope` olur ve MOD-0165'e seam bırakılır | Q1 — kullanıcı kararı gerekli | Yanlış varsayım: çeyrek başına BU kopyalanır → geçmiş performans karşılaştırması imkânsızlaşır |
| **Quarter / Year** | **BU değil, `PlanningPeriodRef` + `TerritoryModel.VersionNumber`.** Yeniden planlama = yeni model versiyonu (`BasedOnModelId`, `SupersededByModelId`) | Excel: "controlled territory changes" | Legacy'de çeyrek başına yeni BU/organizasyon yaratılması → BU kimliği kopar, trend kaybolur |
| **Production Admin** | **BU değil.** Varsayılan öneri: `TerritoryResourceAssignment(ResourceRole=admin)` veya tamamen CRM dışı bir destek rolü (MOD-0018 role) | Q2 — kullanıcı kararı gerekli | BU listesine operasyonel rol karışırsa portfolyo roll-up'ları bozulur |
| **Brand / Product** | MOD-0151 **product master sahibi olmaz.** `TerritoryBusinessScope(ScopeType=product-portfolio\|brand-group, ScopeCode)` + opsiyonel `ExternalRef` | `crm-sor-boundary`: Brand/Product/SKU = MDM | Ürün listesinin CRM'de kopyalanması (legacy Property/PropertyList hatası) |
| **ProductPortfolio ↔ Brand mapping** | **MOD-0151'e ait değil (seam).** MDM'de bugün yalnız `LegalEntity` var → gerçek Product/Brand master yok → Q3. Ara çözüm: MOD-0048 tenant-owned `product-portfolio` reference set; portfolio↔brand bağı ertelenir | Product master çıkana kadar tek doğru kaynak yok | Portfolio↔brand bağının CRM'de kalıcılaşması → sonra MDM ile çift kayıt |
| **Brand değişimi / valid-dating** | Portfolio↔Brand bağı **valid-dated** olmalı (owner modül hangisi olursa olsun). Atamalar `ScopeCode` üzerinden bağlanır, brand ID üzerinden değil | Brand yeniden adlandırılırsa/birleşirse geçmiş atama kopmamalı | Geçmiş territory-performans verisinin brand değişiminde geçersizleşmesi |

---

## 10. Resource Assignment Decision

| Role | Scope | Exclusivity Rule | Notes |
|---|---|---|---|
| `medical-representative` | `exact-territory` (zone veya microzone) | **Kesin:** Aynı örtüşen dönemde **farklı** BU/portfolyolarda birden fazla **aktif primary** MR ataması olamaz → 409/422 (override + gerekçe ile aşılabilir). **Politika:** Aynı BU içinde birden fazla zone'a bakabilir → izinli, ancak `workload/coverage-conflict` **uyarısı** üretir (block değil) | MR = MOD-0288 Person/User referansı; employee master kopyalanmaz |
| `area-manager` | `territory-subtree` (area veya region altı) | Aynı (model, node, BU-scope) için tek aktif primary | Alt zone/microzone resource + account coverage'ını görür. **Resource hiyerarşisi ≠ territory hiyerarşisi**: manager'ın altındaki kişiler MOD-0288 reporting chain'den, kapsadığı bölgeler territory subtree'den gelir; ikisi karıştırılmaz |
| `regional-manager` | `territory-subtree` (region veya division altı) | Aynı | — |
| `division-manager` | `territory-subtree` (division altı) | Aynı | Opsiyonel rol; division level kullanılmıyorsa atanamaz |
| `product-manager` | `product-portfolio` | Portfolyo başına tek aktif primary (öneri; warn seviyesinde de bırakılabilir) | **TerritoryId null**; tüm modelde ilgili portfolyonun performansını görür |
| `business-unit-manager` | `business-unit` | BU başına tek aktif primary | Cross-territory roll-up görünürlüğü |
| `viewer` | `model-wide` veya `territory-subtree` | Exclusivity yok | Salt okuma; `IsPrimary` her zaman `false` |
| `admin` | `model-wide` | Exclusivity yok | Yönetim yetkisi; operasyonel coverage değil. "Production Admin" bunun altına düşebilir (Q2) |

**Ortak kurallar:**
- `TerritoryResourceAssignment` **employee master tutmaz**: `PersonRef` (MOD-0288 person/position) + opsiyonel `UserId` (MOD-0018) referansları.
- `CoverageScope=business-unit|product-portfolio|model-wide` olduğunda `TerritoryId` **null olmalı**; `exact-territory|territory-subtree` olduğunda **zorunlu**.
- `IsPrimary=false` atamalar (yedek/vekil/ortak sorumluluk) exclusivity kurallarına takılmaz.
- Her atama efektif-tarihlidir; sonlandırma = `ValidTo` + `Status=ended`, **silme değil**.
- `ChangeReason` manuel/override kaynaklı atamalarda zorunlu.

---

## 11. Account / Contact Coverage Decision

| Object | Assignment Method | Source of Truth | Notes |
|---|---|---|---|
| Account → Territory | `AccountTerritoryAssignment` (rule/manual/import/override), efektif-tarihli | **MOD-0151** (atama) / **MOD-0149** (account master) | AccountId MOD-0149'dan; silinmiş/olmayan account → 400. Adres/geo **girdi**, kopyalanmaz |
| Account çoklu territory | İzinli — **farklı boyutlarda** (farklı BU, farklı portfolyo, farklı kanal, farklı model versiyonu) | MOD-0151 | Aynı (model + BU-scope + level) içinde **tek aktif primary** zorunlu |
| Account 360 "Coverage" alanı | MOD-0151 `CoverageSummary` read-only projection | MOD-0151 | MOD-0149 §3.1'in bekleyen kontratı; Account'a alan eklenmez |
| Contact → Territory | **Doğrudan atama YOK.** Türetilir: Contact → `AccountContactLink` (MOD-0150) → Account → `AccountTerritoryAssignment` | **MOD-0150** (contact + link) / **MOD-0151** (territory) | MR, kapsadığı account'lar üzerinden contact görür |
| Contact çoklu account | Türetilmiş kapsam **birleşimdir** (union); contact birden çok territory'de görünebilir | MOD-0150 + MOD-0151 | Çakışma değil, doğal sonuç. Ekranda "hangi account üzerinden" gösterilir |
| Contact doğrudan atama ihtiyacı | **Reddedildi (v1).** CRM kaynak dokümanlarında doğrudan kişi-territory gereksinimi kanıtı yok; MOD-0150 açıkça "No Zone/MicroZone/Territory" diyor | — | Gelecekte kanıt çıkarsa ayrı FU + ayrı karar; bugün eklenmez |
| Resource → Account görünürlüğü | `TerritoryResourceAssignment` ∩ `AccountTerritoryAssignment` | MOD-0151 | Enforcement MOD-0018/FU15 ile hizalanır (Q5) |

---

## 12. MicroZone Decision

**Nasıl çalışır:**
- MicroZone = `TerritoryNode(TerritoryLevel=microzone)`. Ayrı aggregate, ayrı koleksiyon, ayrı permission yok.
- Parent'ı **zone** (veya rank kuralına uyan başka bir üst seviye) olmalıdır. Bir zone **N adet** microzone içerebilir.
- Klinik/hastane/eczane gruplaması = microzone altındaki `AccountTerritoryAssignment` kayıtlarıdır; yeni bir "grup" nesnesi tanımlanmaz.
- MR ataması `exact-territory` kapsamıyla zone **veya** microzone düzeyine yapılabilir.

**Anchor account kararı:**
- `MicroZoneProfile.AnchorAccountId` **opsiyoneldir** ve yalnız `level=microzone` düğümlerde dolu olabilir.
- Anlamı: **planlama merkezi / küme çapası** (ör. merkez hastane). Metadata'dır — bugün hiçbir kural tetiklemez.
- **Rota başlangıcı değildir**, ziyaret sırası üretmez, mesafe hesabı yapmaz.
- Anchor account'un o microzone'a atanmış olması **önerilir** (uyarı üretir), zorunlu değildir — planlama merkezi bazen komşu bir kurum olabilir.

**MOD-0155'e kalanlar (MOD-0151'de KESİNLİKLE yok):**
mesafe hesabı · yakınlık araması (nearby search) · rota sıralama/optimizasyon · ziyaret sıklığı (frequency/cadence) · ziyaret planı ve raporu · MicroTarget · gün planı (daywork/visit mix) · "hastane doktoru → yakın eczane" rota önerisi.

MOD-0151'in MOD-0155'e verdiği tek şey: **coverage okuma API'leri** (microzone'daki account'lar, resource coverage, türetilmiş contact coverage).

---

## 13. Reference Data Proposal

> **Bu görevde hiçbir set oluşturulmadı.** Aşağıdaki liste MOD-0048 authoring template'i için **öneridir** (MOD-0149/MOD-0150 precedent'i: pack → PREREQ authoring template → operator publish). Tüm değerler `lowercase-kebab`. Hardcoded fallback **yasak**; eksik required set → kontrollü 400.

| SetCode | Required? | Example Values | Owner | Runtime Gate? |
|---|---|---|---|---|
| `territory-level` | **Required** | division, country, region, area, zone, microzone | MOD-0048 (tenant-owned; rank metadata ile) | ✅ Node create/update bloklar; **aktivasyonu da bloklar** |
| `territory-model-status` | **Required** | draft, review, approved, active, superseded, archived | MOD-0048 (platform-owned öneri — lifecycle kod ile eşleşmeli) | ✅ Model lifecycle bloklar |
| `territory-node-status` | **Required** | draft, active, inactive, ended | MOD-0048 (platform-owned) | ✅ Node create bloklar |
| `territory-assignment-status` | **Required** | proposed, active, ended, rejected | MOD-0048 (platform-owned) | ✅ Atama bloklar |
| `territory-assignment-source` | **Required** | rule, manual, import, override | MOD-0048 (platform-owned) | ✅ Atama bloklar |
| `territory-resource-role` | **Required** | medical-representative, area-manager, regional-manager, division-manager, product-manager, business-unit-manager, viewer, admin | MOD-0048 (tenant-owned — rol adları sektöre göre değişir) | ✅ Resource assignment bloklar |
| `territory-rule-type` | **Required** | geography, account-list, account-type, product-portfolio, channel, segment, manual, import | MOD-0048 (platform-owned) | ✅ Rule create bloklar |
| `territory-conflict-policy` | **Required** | block, warn, priority, manual-review | MOD-0048 (platform-owned) | ✅ Rule create bloklar |
| `territory-coverage-scope` | **Required** | exact-territory, territory-subtree, business-unit, product-portfolio, model-wide | MOD-0048 (platform-owned) | ✅ Resource assignment bloklar |
| `planning-period-type` | Optional (v1) | annual, quarterly, monthly, custom | MOD-0048 (tenant-owned) | ❌ Planning-only; eksikse `PlanningPeriodRef` boş kalır |
| `business-scope-type` | **Required (BU/portfolio kullanılıyorsa)** | business-unit, product-portfolio, brand-group, channel, segment | MOD-0048 (platform-owned) | ⚠️ Koşullu: `TerritoryBusinessScope` doluysa bloklar |
| `product-portfolio` | Optional (Q3'e bağlı geçici) | (tenant'a özgü: alpha, beta, gamma…) | MOD-0048 (tenant-owned) — **Product master çıkınca MDM'e devredilir** | ❌ Planning-only; geçici çözüm |
| `territory-change-type` | Optional (öneri) | create-model, update-hierarchy, update-assignment-rule, update-account-assignment, update-resource-assignment, activate-model, supersede-model | MOD-0048 (platform-owned) | ❌ v1'de sabit liste de kabul edilebilir; reference tercih edilir |

**Aktivasyonu bloklayan setler:** `territory-level`, `territory-model-status`, `territory-node-status`, `territory-assignment-status`, `territory-assignment-source`, `territory-coverage-scope` — biri eksikse `activate` **fail-closed** olur (§18).

---

## 14. Permission Proposal

> **Hiçbir permission seed edilmedi.** PKS-001: lowercase-dotted, ≥3 segment, her segment `^[a-z][a-z0-9-]*$`. Aşağıdaki 15 anahtarın **tamamı PKS-001 geçerlidir** (kontrol edildi). `view` yerine `read` kullanıldı (PKS-001 canonical).

| Permission | Purpose | Seed Now? | Notes |
|---|---|---|---|
| `crm.territory.read` | Territory yüzeyine genel okuma (menü/landing gate) | ❌ | Menü `<li>` guard'ı bu anahtarla |
| `crm.territory.model.read` | Model listesi/detayı | ❌ | — |
| `crm.territory.model.manage` | Model create/update (yalnız draft) | ❌ | Aktif modeli değiştiremez |
| `crm.territory.model.activate` | Modeli aktive et / supersede et | ❌ | **Yüksek riskli**; Admin'e default verilmez |
| `crm.territory.node.read` | Hiyerarşi okuma | ❌ | Model Viewer |
| `crm.territory.node.manage` | Node create/update (draft model) | ❌ | — |
| `crm.territory.assignment.read` | Account atamaları okuma | ❌ | — |
| `crm.territory.assignment.manage` | Rule + account assignment apply/override | ❌ | Manuel override ayrıca `ChangeReason` ister |
| `crm.territory.resource.read` | Resource atamaları okuma | ❌ | — |
| `crm.territory.resource.manage` | Resource atama/sonlandırma | ❌ | **Yüksek riskli** (saha yetkisi değiştirir) |
| `crm.territory.approval.read` | Change Approval Trace okuma | ❌ | Excel soft page |
| `crm.territory.approval.submit` | Change request oluştur/onaya gönder | ❌ | Onay **verme** MOD-0023'ün `tasks.approve` izniyle olur, burada değil |
| `crm.territory.evidence.export` | Evidence pack üret/indir | ❌ | Excel bundle bileşeni |
| `crm.territory.import` | XLSX import (dry-run + apply) | ❌ | MOD-0150 deseni |
| `crm.territory.export` | XLSX export | ❌ | — |

**Delete izni yok.** Gerekçe: aktif model/atama **asla silinmez** (archive/supersede/end). Draft nesneler için silme gerekirse `crm.territory.model.manage` altında soft-delete olarak kalır — ayrı `delete` anahtarı **önerilmez** (§18).

**Mevcut RBAC planı ile çakışma (pack draft'ında çözülecek):** `crm-rbac-integration-plan.md §3` bugün MOD-0151 için `crm.territory.read/create/update/assign-rep/assign-account` + `crm.micro-zone.manage` listeliyor. Öneri: bu liste **supersede** edilsin ve `crm.micro-zone.manage` **kaldırılsın** (MicroZone ayrı nesne değil, node level'ıdır; ayrı izin yanlış mimari sinyali verir). `§6 ABAC matrisi`'ndeki "MicroZone assignment ayrı izin" satırı da güncellenmeli. **Bu güncelleme bu görevde yapılmadı** — pack draft/onay adımına aittir.

---

## 15. Workflow / Approval Design

### 15.1 Lifecycle

```
draft ──submit-approval──► review ──(MOD-0023 approve)──► approved ──activate──► active
  ▲                          │                                                    │
  └──(reject → reason)───────┘                                    new version ────┤
                                                                                  ▼
                                                              superseded ──► archived
```

- **draft:** serbest düzenlenebilir (node/rule/atama taslakları). Aktivasyon bloklu.
- **review:** change request açık, workflow instance çalışıyor; içerik **kilitli** (düzenleme → 409).
- **approved:** onay tamam, aktivasyon **mümkün** ama otomatik değil (ayrı izin + ayrı komut).
- **active:** **immutable**. Node/rule/atama değişikliği doğrudan yasak → yeni draft versiyon (`BasedOnModelId`) veya `TerritoryChangeRequest`.
- **superseded / archived:** geçmiş korunur; sorgulanabilir, değiştirilemez.

### 15.2 MOD-0023 entegrasyonu (gerçek, sahte değil)

MOD-0023 runtime'da mevcut ve iki mekanizma sunuyor:
1. **Start Instance** — `POST /api/v1/workflow/instances` ile `objectType="TerritoryModel"`, `objectId={modelId}`, `objectRef`, `templateCode`, `candidatePrincipalIds` (`user:` / `position:`), `idempotencyKey`. → `TerritoryChangeRequest.ApprovalWorkflowInstanceId` bu instance'ı saklar.
2. **Transition Gate** — `POST /api/v1/workflow/transitions/evaluate`, salt-okunur. MOD-0151 `activate` komutunu commit etmeden **önce** gate'e sorar: `Allowed / Blocked / NotApplicable`. Blocked ise aktivasyon **422/409** ile reddedilir.

**Kritik sınır (MOD-0023 dokümanı):** iş kaydının gerçek state'ini kaynak modül tutar. Yani `TerritoryModel.Status` **MOD-0151'e aittir**; MOD-0023 yalnız geçiş kapısıdır. `ApprovalStatus` alanı workflow'un yansımasıdır, kaynağı değildir.

**MOD-0023 hazır/konfigüre değilse:** hiçbir sahte onay üretilmez. Model `review`/`approval-pending` durumunda kalır, `activate` **fail-closed** olarak reddedilir ve hata mesajı "workflow template not configured" der. Otomatik onay, bypass flag'i veya "workflow yoksa geç" davranışı **yasaktır**.

### 15.3 Change Approval Trace (Excel soft page) — zorunlu içerik

`ChangeRequestId` · `ChangeType` · `RequestedBy` / `RequestedAt` · `Reason` · **before/after diff** (node ekleme/silme, level değişimi, atama taşımaları — snapshot ref üzerinden) · `ApprovalWorkflowInstanceId` + workflow status · `DecisionBy` / `DecisionAt` / decision reason · `ActivatedAt` · `CorrelationId` · MOD-0021 audit event linkleri.

### 15.4 Aktivasyon = immutable snapshot

`activate` başarılı olduğunda modelin **tam snapshot'ı** (hiyerarşi + rule'lar + aktif atama sayıları + resource atamaları + çakışma durumu) dondurulur ve `TerritoryEvidencePack`'in `ModelSnapshot` alanına referanslanır. Snapshot sonradan yeniden hesaplanmaz.

---

## 16. UI Proposal

Golden Reference **Compact** (MOD-0149/0150 ile aynı). **Offcanvas/quickview create-edit yasak.** Tüm çağrılar Gateway (5000) üzerinden; CrmService portuna doğrudan erişim yok. 7 dil `.resx` + `window.L10n` bridge. Fake/mock UI yok — backend hazır olmadan sayfa açılmaz.

**Excel-zorunlu 3 sayfa:**

1. **Territory Model Viewer** (`/CRM/TerritoryModels/{id}/viewer`)
   Sol: hiyerarşi ağacı (division→…→microzone, level rozetleriyle). Sağ: seçili node detayı — kod/ad/level/geçerlilik, **atanmış account sayısı**, **atanmış resource sayısı**, BU/portfolyo scope'u, kriter özeti (geo/account/portfolio/channel/segment), microzone ise anchor account. Üstte **active ↔ draft karşılaştırma** toggle'ı ve **çakışma göstergeleri** (kırmızı/sarı rozet + çakışma listesi).

2. **Change Approval Trace** (`/CRM/TerritoryModels/{id}/approval-trace`)
   Change request DataTable v2 listesi (type/status/requested-by/date) → satır detayında before/after diff paneli, gerekçe, workflow instance + task durumu, karar geçmişi, aktivasyon izi, correlation id.

3. **Evidence Pack** (`/CRM/TerritoryModels/{id}/evidence-pack`)
   Model metadata · hiyerarşi snapshot · assignment rule'ları · account atama sayıları (level bazında) · resource atamaları · çakışmalar · onaylar · aktivasyon kanıtı · üretim zaman damgası · correlation id · **Export** butonu (`crm.territory.evidence.export`).

**Ek önerilen sayfalar:**

4. **Territory Models List** (`/CRM/TerritoryModels`) — DataTable v2; scope/status/period filtreleri; "Create draft from…" aksiyonu.
5. **Node Detail / Edit** — compact tam sayfa; yalnız draft modelde editable.
6. **Assignment Preview** — kural çalıştır → sonuç tablosu (account, hedef territory, kaynak kural, çakışma) + "Apply" (ayrı izin, ayrı onay akışı).
7. **Resource Assignment** — rol/kapsam/dönem/primary matrisi; exclusivity ihlali inline uyarı.
8. **Account Assignment** — mevcut atamalar + manuel taşıma (gerekçe zorunlu) + geçmiş (ended kayıtlar görünür).
9. **Import / Export** (FU08) — MOD-0150 XLSX template → export → upload → dry-run raporu → apply akışı.

**Menü:** interim olarak `_LayoutTenantShell.cshtml` içinde `crm.territory.read` guard'lı `<li>` (MOD-0149/0150 paritesi); page descriptor MOD-0285 nav migration'a kadar `IsNavigationVisible=false`. **Bu görevde layout değiştirilmedi.**

---

## 17. API / CQRS Proposal

> Öneri; implement edilmedi. Gateway route ekleme yalnız `integration-agent`'a aittir (`ocelot.json` protected). Mevcut konvansiyon: `/api/crm/accounts`, `/api/crm/contacts` → territory için `/api/crm/territory-models` + `/api/crm/territory-models/{everything}` route ailesi önerilir.

| Endpoint / Command | Purpose | Permission | Notes |
|---|---|---|---|
| `GET /api/crm/territory-management/contract` | Modül kontrat/capability descriptor (bundle sürümü, gerekli reference set'ler, workflow readiness) | `crm.territory.read` | MOD-0149/0150 contract endpoint paritesi; UI readiness gösterimi |
| `GET /api/crm/territory-models` | `GetTerritoryModelListQuery` | `crm.territory.model.read` | DataTable v2; scope/status/period filtresi |
| `POST /api/crm/territory-models` | `CreateTerritoryModelCommand` (draft) | `crm.territory.model.manage` | `BasedOnModelId` ile klonlama destekli |
| `GET /api/crm/territory-models/{id}` | `GetTerritoryModelByIdQuery` | `crm.territory.model.read` | Cross-tenant → 404 |
| `PUT /api/crm/territory-models/{id}` | `UpdateTerritoryModelCommand` | `crm.territory.model.manage` | **Yalnız draft**; active → 409 |
| `POST /api/crm/territory-models/{id}/nodes` | `CreateTerritoryNodeCommand` | `crm.territory.node.manage` | Cycle + level-rank + tarih validasyonu |
| `PUT /api/crm/territory-models/{id}/nodes/{nodeId}` | `UpdateTerritoryNodeCommand` | `crm.territory.node.manage` | Yalnız draft |
| `GET /api/crm/territory-models/{id}/nodes` | `GetTerritoryHierarchyQuery` | `crm.territory.node.read` | Model Viewer ağacı |
| `POST /api/crm/territory-models/{id}/rules` | `UpsertTerritoryAssignmentRuleCommand` | `crm.territory.assignment.manage` | Priority + conflict policy |
| `POST /api/crm/territory-models/{id}/preview-assignments` | `PreviewTerritoryAssignmentsCommand` | `crm.territory.assignment.read` | **Yan etkisiz**; hiçbir şey yazmaz |
| `POST /api/crm/territory-models/{id}/account-assignments/apply` | `ApplyAccountTerritoryAssignmentsCommand` | `crm.territory.assignment.manage` | Efektif-tarihli; eski atama `ended`, silinmez; manual/override ezilmez |
| `POST /api/crm/territory-models/{id}/resource-assignments` | `UpsertTerritoryResourceAssignmentCommand` | `crm.territory.resource.manage` | Exclusivity validasyonu |
| `POST /api/crm/territory-models/{id}/submit-approval` | `SubmitTerritoryChangeRequestCommand` | `crm.territory.approval.submit` | MOD-0023 Start Instance çağırır; `idempotencyKey` zorunlu |
| `POST /api/crm/territory-models/{id}/activate` | `ActivateTerritoryModelCommand` | `crm.territory.model.activate` | Transition Gate + çakışma + reference + approval kontrolü; hepsi fail-closed |
| `GET /api/crm/territory-models/{id}/approval-trace` | `GetTerritoryApprovalTraceQuery` | `crm.territory.approval.read` | Change Approval Trace sayfası |
| `GET /api/crm/territory-models/{id}/evidence-pack` | `GetTerritoryEvidencePackQuery` | `crm.territory.evidence.export` | JSON; export dosyası aynı izin |
| `GET /api/crm/accounts/{accountId}/territory-assignments` | `GetAccountTerritoryAssignmentsQuery` | `crm.territory.assignment.read` | **MOD-0149 §3.1 CoverageSummary kontratı** |
| `GET /api/crm/contacts/{contactId}/territory-coverage` | `GetContactTerritoryCoverageQuery` (derived) | `crm.territory.assignment.read` | MOD-0150 link üzerinden türetilir; contact'a alan eklenmez |
| `GET /api/crm/territory-models/{id}/coverage-rollup` | `GetTerritoryCoverageRollupQuery` | `crm.territory.model.read` | §L performance readiness read-model (FU09) |
| `POST /api/crm/territory-models/{id}/import` · `GET .../export` | XLSX dry-run/apply + template export | `crm.territory.import` / `crm.territory.export` | FU08; MOD-0150 deseni |

---

## 18. Validation Rules

| Rule | Severity | Why |
|---|---|---|
| Hiyerarşide döngü yasak (parent zinciri kendine dönemez) | **Block (400)** | MOD-0149 `ParentAccountId` cycle guard precedent'i; sonsuz döngü/roll-up bozulması |
| `TerritoryCode` model içinde unique | **Block (409)** | İnsan-okunur kimlik; import/export eşleşmesi |
| Level sequence geçerli (child rank > parent rank) | **Block (400)** | Zone'un altında Region olamaz; atlamak serbest |
| Tenant + scope + effective period başına **tek aktif model** | **Block (409)** | İki aktif model = belirsiz coverage |
| `ValidFrom <= ValidTo` (tüm entity'ler) | **Block (400)** | Temel tarih tutarlılığı |
| Child node tarihleri parent/model tarih aralığı içinde | **Block (400)** | Parent bittiğinde child yaşayamaz |
| Atama tarihleri model tarih aralığı içinde | **Block (400)** | Model dışı atama izlenemez |
| Aynı (model + business scope + level) için örtüşen dönemde **tek aktif primary** account ataması | **Block (409)** | Çift sahiplik = çift sayım, hedef çakışması |
| Farklı BU/portfolyolarda örtüşen dönemde **tek aktif primary MR** (override hariç) | **Block (409), override ile Warn** | §10 exclusivity kararı |
| Aynı BU içinde MR'ın çoklu zone ataması | **Warn** | İzinli ama iş yükü/coverage riski |
| `CoverageScope` ↔ `TerritoryId` tutarlılığı (subtree/exact → zorunlu; BU/portfolio/model-wide → null) | **Block (400)** | Anlamsız atama önlenir |
| `AnchorAccountId` yalnız `level=microzone` düğümde dolu olabilir | **Block (400)** | Value object koşulu |
| Anchor account o microzone'a atanmamışsa | **Warn** | Meşru istisna olabilir |
| Manuel/override atama `ChangeReason` olmadan yazılamaz | **Block (400)** | "Controlled changes" Excel çıktısı |
| Çözülmemiş çakışma varken aktivasyon | **Block (422)** | Bozuk model canlıya çıkamaz |
| Zorunlu MOD-0048 reference set eksikken aktivasyon | **Block (422/400)** | Fail-closed; hardcoded fallback yasak |
| Gerekli onay yokken (workflow Blocked/NotApplicable/instance yok) aktivasyon | **Block (409/422)** | Sahte onay yasağı |
| Aktif model üzerinde doğrudan node/rule/atama mutasyonu | **Block (409)** | Immutability; değişiklik = yeni draft/change request |
| Aktif model silme | **Block (403/409)** | Yıkıcı işlem yasağı |
| Model/atama sonlandırma = `Status=ended` + `ValidTo`, **DELETE değil** | **Block (destructive update reddi)** | Geçmiş veri koruması |
| Soft-delete yalnız `draft`/`inactive` nesnelerde | **Block (409)** | Aktif kayıt kaybı önlenir |
| Olmayan/soft-deleted Account'a atama | **Block (400)** | MOD-0149 SoR bütünlüğü |
| Cross-tenant ID erişimi | **404** | Platform standardı (metadata sızıntısı yok) |
| Payload'da `TenantId` gönderimi | **Yok sayılır / 400** | TenantId her zaman JWT'den server-side |

---

## 19. Integration Boundaries

| Module | Relationship | What MOD-0151 must not own |
|---|---|---|
| **MOD-0149** Customer 360 | Hard consume: AccountId doğrulama, account lookup/arama, geo/adres girdisi. MOD-0151 → MOD-0149'a `CoverageSummary` read-only projection sağlar (§3.1 borcu) | Account/WorkPlace master, AccountCode üretimi, account hiyerarşisi, adres/geo persistence |
| **MOD-0150** Contact & Relationship | Consume: `AccountContactLink` üzerinden türetilmiş contact coverage | Contact master, Contact↔Account link, Account↔Account relationship, consent |
| **MOD-0048** Reference Data | Consume-only: `published-values?scope_key={tenant}`; eksik required set → kontrollü 400 | Reference set/value tanımı, CRM-local seed, hardcoded fallback listesi |
| **MOD-0018** / AuthService | Consume-only: `[HasPermission("crm.territory.*")]`, JWT claim'leri, (varsa) data-scope | Yeni RBAC/permission engine, rol tanımı, permission seed |
| **MOD-0023** Workflow Designer | Consume: Start Instance + Transition Gate. `TerritoryModel.Status` MOD-0151'de kalır | Onay motoru, task/SLA/escalation, approval task state'i |
| **MOD-0021** Audit Trail | Consume: audit event append (model create/activate, atama değişimi, override, evidence export) | Audit store, retention, redaction |
| **MOD-0031** Evidence Linking | Seam (bugün runtime yok). MOD-0151 kendi **territory evidence pack composition + export**'unu sahiplenir (Excel bundle gereği); MOD-0031 geldiğinde evidence link/provenance oraya bağlanır → Q4 | Genel evidence object store, evidence provenance, cross-module evidence linking |
| **MOD-0288** Organization/Person/Position | Consume: PersonRef/Position/OrgUnit; olası BusinessUnit master | Employee/person master, position master, org unit master, reporting chain |
| **MDM / Product** | Seam: portfolio/brand kodu referansı; gerçek Product master yok (Q3) | Product/Brand/SKU master, portfolio↔brand mapping (owner kararı verilene kadar) |
| **MOD-0154** Forecasting & Quotas | Provide: roll-up boyutları ve coverage read-model'leri | Quota, forecast, hedef hesaplama, quota approval |
| **MOD-0155** Field Sales / Visit Planning | Provide: microzone account listesi, resource coverage, derived contact coverage | Visit plan, visit, MicroTarget, rota, mesafe, frequency/cadence, daywork |
| **MOD-0167** Segmentation | Consume: segment kodu kriter olarak | Segment tanımı/değerlendirmesi |
| **MOD-0285** Navigation | Consume: page descriptor / menü (interim static `<li>`) | Navigation loader/engine |

---

## 20. FU Breakdown

| FU | Scope | Depends On | Out-of-Scope |
|---|---|---|---|
| **FU00** Source reconciliation / pack approval | Excel + CRM doc + legacy mapping mutabakatı; Q1–Q7 kararları; Division/Area/BusinessUnit/ProductPortfolio final kararı; MOD-0048 authoring template (PREREQ, MOD-0149/0150 paritesi); RBAC planı §3/§6 supersede önerisi | — | Kod, seed, registry |
| **FU01** Contract + core backend | `TerritoryModel` + `TerritoryNode` aggregate'leri, CRUD, level/cycle/tarih validasyonu, reference validator, permission'lar, contract endpoint, testler | MOD-0149, MOD-0048, MOD-0018 | **Aktivasyon yok**, atama apply yok, rule yok, UI yok |
| **FU02** Hierarchy UI / Territory Model Viewer | Models list + node detail + ağaç görünümü (division→microzone), draft model düzenleme, menü/page descriptor, 7 dil resx | FU01 | Approval, evidence, atama ekranları |
| **FU03** Assignment rules + preview | `TerritoryAssignmentRule`, geography/account-list/account-type/product-portfolio/channel/segment kuralları, `preview-assignments` (yan etkisiz), çakışma tespiti, Assignment Preview ekranı | FU01, FU02 | Apply, aktivasyon, resource |
| **FU04** Resource assignments | `TerritoryResourceAssignment`, roller, coverage scope, exclusivity kuralları, MOD-0288 PersonRef seam, Resource Assignment ekranı | FU01, MOD-0288 | Employee master, data-scope enforcement (Q5) |
| **FU05** Account assignment apply + history | Efektif-tarihli `AccountTerritoryAssignment`, apply komutu, eski atama `ended` (silinmez), manuel taşıma + gerekçe, geçmiş görünümü, MOD-0149 `CoverageSummary` projection | FU03 | Aktivasyon, onay |
| **FU06** Workflow approval + activation + Change Approval Trace | `TerritoryChangeRequest`, MOD-0023 Start Instance + Transition Gate, lifecycle state machine, immutable snapshot, before/after diff, Change Approval Trace sayfası | FU01–FU05, **MOD-0023** | Sahte onay, bypass flag |
| **FU07** Evidence Pack + audit export | `TerritoryEvidencePack` üretimi, Evidence Pack sayfası, export (dosya), MOD-0021 audit event wiring, correlation id | FU06, MOD-0021 | MOD-0031 genel evidence store |
| **FU08** Import/export hardening | XLSX template + export + upload + **dry-run** + safe apply (MOD-0150 deseni), satır bazlı hata raporu | FU05, FU07 | Yeni import framework |
| **FU09** MOD-0155 readiness APIs | Account territory coverage, microzone account listesi, resource coverage, derived contact coverage, coverage roll-up read-model | FU05, FU07 | Forecast (MOD-0154), visit/route (MOD-0155) |

**Not:** FU00 tamamlanmadan `runtime_code_allowed: false`. FU01 açılırken `runtime_code_scope` MOD-0149/0150 paritesiyle daraltılmalı (örn. `FU01-territory-model-node-backend-only`).

---

## 21. Open Questions and Recommended Answers

| # | Question | Recommended Answer | Needs User Decision? |
|---|---|---|---|
| **Q1** | Alpha / Beta / Gamma nedir — kalıcı satış organizasyonu / ürün portfolyosu mu, yoksa dönemsel kampanya grubu mu? | **Kalıcı BusinessUnit/ProductPortfolio** olarak ele al; dönemsellik `TerritoryModel` versiyonu + `PlanningPeriod`'a taşınsın. Master MOD-0288 `OrganizationUnit` (unitType gerekir) veya geçici olarak MOD-0048 tenant-owned set | ✅ **Evet** — modelin tamamını etkiler |
| **Q2** | "Production Admin" bir business unit mi, admin grubu mu, operasyonel rol mü? | **Business unit değil.** `territory-resource-role=admin` veya tamamen CRM dışı bir MOD-0018 rolü. BU listesine konmasın | ✅ **Evet** |
| **Q3** | Portfolio ↔ Brand mapping'in sahibi kim? Bugün MDM'de yalnız `LegalEntity` var, gerçek Product/Brand master yok | v1'de **MOD-0151 sahiplenmesin**. Geçici: MOD-0048 tenant-owned `product-portfolio` set; portfolio↔brand bağı Product master modülü (CAND-CAP adayı) çıkana kadar **ertelensin** | ✅ **Evet** (erteleme onayı) |
| **Q4** | Territory Evidence Pack'i MOD-0151 mi üretir, MOD-0031 Evidence Linking mi? | **MOD-0151 üretir** — Excel soft page + CRM-TERRITORY-BUNDLE açıkça öyle diyor; MOD-0031'in pack'i/runtime'ı yok. MOD-0031 geldiğinde evidence **link/provenance** oraya devredilir, composition MOD-0151'de kalır | ⚠️ Önerilen; EA teyidi iyi olur |
| **Q5** | Territory bazlı data-scope enforcement nasıl olacak? FU15 `OrgDataScopeResolver` çalışıyor ama `EntitlementDataScopeKind`'da **Territory yok** (`Region=10` var, kullanılmıyor) | v1'de MOD-0151 **kendi coverage filtresini** CrmService içinde uygular (`TerritoryResourceAssignment` ∩ sorgu). Platform enum'una `Territory` eklemek **MOD-0018'in işi** → ayrı follow-up. MOD-0151 platform enum'unu değiştirmez | ✅ **Evet** — MOD-0018 sahibi onayı gerekir |
| **Q6** | AI Assist (Excel: Tier=Assist, 5 HARD AI gate) v1'de açılsın mı? | **Hayır.** v1 **AI-OFF** teslim edilsin; böylece MOD-0066/0067/0068/0069/0041 hard gate'leri devreye girmez. AI istenirse ayrı FU + gate karşılama | ⚠️ Önerilen; sessiz kabul edilebilir |
| **Q7** | Blueprint `Build/Buy/Partner = Buy/Partner` diyor; repo in-house build ediyor (MOD-0149/0150 precedent) | In-house build devam etsin; pack'e açık **"Blueprint Buy/Partner ile bilinçli sapma, EA notu"** yazılsın | ⚠️ Governance notu; blocker değil |
| Q8 | `crm-rbac-integration-plan.md`'deki eski MOD-0151 anahtarları (`crm.micro-zone.manage` vb.) supersede edilsin mi? | **Evet** — §14 listesi geçerli olsun, `crm.micro-zone.manage` kaldırılsın (MicroZone ayrı nesne değil). Güncelleme **pack draft aşamasında** yapılır | ⚠️ Önerilen |

---

## 22. Final Recommendation

### `NEEDS_USER_DECISION`

Tasarım Excel ile tam hizalı ve teknik olarak uygulanabilir: bağımlılıklar (MOD-0149 ✅ runtime, MOD-0150 ✅ runtime, MOD-0023 ✅ runtime, MOD-0048 ✅ %90, MOD-0018 ✅, MOD-0021 ✅ %85, MOD-0288 ✅ %85) **karşılanmış durumda** — yani `BLOCKED_BY_DEPENDENCY` değil. Ancak pack draft'ının veri modelini kilitleyebilmesi için **Q1, Q2, Q3, Q5** kullanıcı/EA kararı gerektiriyor; bunlar `TerritoryBusinessScope`, `ResourceRole`, portfolio/brand seam'i ve scope enforcement mimarisini doğrudan belirliyor. Q4/Q6/Q7/Q8 için önerilen cevaplar sessiz onayla ilerleyebilir.

## 23. Next Recommended Prompt

Q1–Q3 ve Q5 cevaplandıktan sonra:

> **MOD-0151 Territory Management Module Pack Draft**
> Bu dokümandaki (§7 target model, §13 reference proposal, §14 permission proposal, §17 API proposal, §18 validation rules, §20 FU breakdown) kararları ve Q1–Q8 yanıtlarını kullanarak
> `execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md` draft pack'ini üret.
> Status `content-ready` (ready-for-dev **değil**), `runtime_code_allowed: false`.
> DCP-002 canonical gate: `MOD-0151` / `Territory Management`.
> Ayrıca `crm-rbac-integration-plan.md §3/§6` ve `crm-sor-boundary.md` için önerilen güncellemeleri **ayrı bir follow-up listesi** olarak çıkar; bu dosyaları pack draft task'ında değiştirme.
> Runtime kod, seed, migration, reference set, registry update **üretme**.
