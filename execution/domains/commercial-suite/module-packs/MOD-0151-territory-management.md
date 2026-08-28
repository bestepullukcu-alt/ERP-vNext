---
id: MOD-0151
name: Territory Management
module_id: MOD-0151
module_name: Territory Management
domain: commercial-suite
service: Diten.CrmService
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: ready-for-dev
runtime_code_allowed: true
runtime_code_scope: "FU01-territory-model-node-backend-only; FU02-territory-hierarchy-ui-viewer; FU02A-country-business-unit-scope-selector-hardening; FU02B-lifecycle-computed-expiry-draft-soft-delete; FU03-assignment-rules-and-preview; FU04-resource-assignments; FU04A-resource-assignment-lifecycle-replacement-operational-visibility; FU04B-resource-assignment-plan-vs-current-visibility; FU05-account-assignment-apply-history; FU05A-coverage-summary-model-lifecycle-guard; FU05B-versioned-draft-clone-account-carry-forward; FU08-import-export-hardening; FU09A-visit-route-readiness-boundaries"
owner: module-pack-author
branch: feature/crm/mod-0151-territory-management
started: 2026-07-23
target: TBD (FU01 start gated only by developer availability; live create smoke gated by F10 operator publish)
fu00_closeout: PASS 2026-07-23 — pack approval / source reconciliation gate executed; D1–D7 closed; F1 authoring template completed (publish still pending, F10). See docs/audits/mod-0151-fu00-pack-approval-closeout-2026-07-23.md
ready_for_dev_by: FU00 Pack Approval / Source Reconciliation Closeout (2026-07-23)
wave: W-4
capability_group: CRM Core
placement: Domain App (CRM)
blueprint_bundle: CRM-TERRITORY-BUNDLE
integration_contract: DOMAIN-APP-BASE
sor: territories, assignments, territory change approvals
sor_primary_object: territories
slo_tier: Tier 2
support_model: L1 Service Desk; L2 Domain App Ops; L3 Vendor / Partner
ai_enabled: false
ai_note: Blueprint AI Enablement Tier=Assist / Risk=Medium. v1 ships AI-OFF; MOD-0066/0067/0068/0069/0041 AI hard gates are therefore NOT runtime blockers. AI assist is a separate future FU.
build_buy_partner_note: Blueprint says "Buy/Partner"; repo reality is in-house build (Diten.CrmService), consistent with the MOD-0149 / MOD-0150 precedent. This is a deliberate, documented deviation and requires an EA governance note. Not a blocker.
dependencies:
  - MOD-0149
  - MOD-0150
  - MOD-0023
  - MOD-0048
  - MOD-0018
  - MOD-0021
  - MOD-0288
  - commercial-suite-domain-foundation
---

# MOD-0151 — Territory Management

> **READY-FOR-DEV (FU09A scope update 2026-08-01).** `runtime_code_allowed: true`; runtime yetkisi yalnız
> frontmatter'da adlandırılan **FU01, FU02, FU02A, FU02B, FU03, FU04, FU04A, FU04B, FU05, FU05A, FU05B, FU08 ve FU09A** kapsamları içindir. FU02B; workflow onayı olmadan
> manual lifecycle action'ları, computed expiry, draft soft-delete, single-active-model guard, lifecycle UI
> görünürlüğü, audit event'leri ve test/evidence raporunu açar. **FU03**; `TerritoryAssignmentRule` CRUD'unu ve
> **yan etkisiz** assignment preview'unu (candidate + conflict tespiti, preview UI) açar. **FU04**; `TerritoryResourceAssignment`
> CRUD'unu, position/coverage-scope/business-scope validasyonunu, exclusivity guard'ını ve resource assignment UI'ını açar.
> **FU04A**; draft planning ile active operational responsibility ayrımını, activation transition'ını, active-model
> end/create, atomik replacement/transfer, resource current/history query'lerini ve position-based conflict/metadata
> hardening'ini açar.
> **FU04B**; activation anında alınan **immutable plan baseline snapshot'ını**, plan-vs-current karşılaştırmasını,
> diff type hesaplamasını, üç **read-only** query endpoint'ini ve Resource Assignments sayfasındaki read-only
> Plan vs Current tab/section'ını açar. **FU04B hiçbir resource assignment mutasyonu, workflow approval veya yeni
> bağımsız menü sayfası açmaz** — tek istisnası, activation lifecycle'ına eklenen plan baseline yazımıdır (§22.4).
> **FU05**; `AccountTerritoryAssignment` aggregate'ini, preview sonucunun kullanıcı onayıyla apply edilmesini,
> efektif-tarihli assignment history'yi, conflict/override guard'ını, current/history query'lerini, ayrı
> CoverageSummary read model'ini ve bunların UI/test/evidence-report yüzeylerini açar.
> **FU05A**; FU05 live smoke'ta bulunan CoverageSummary model-lifecycle boşluğunu kapatan **additive read-projection
> guard**'ıdır: current CoverageSummary ve current account-territory coverage query'leri yalnız **operationally valid**
> (active + effective-window içi, arşiv/inactive/superseded olmayan) territory model'e bağlı, açık/effective bir
> `AccountTerritoryAssignment` döner; deactivated/archived/superseded modele bağlı atamalar current'tan düşer ama
> history'de korunur. FU05A **hiçbir mutasyon açmaz** — assignment'ı ended yapmaz, hard delete etmez, Account/Contact
> master'a dokunmaz; yalnız current projection'ı model lifecycle status ile filtreler (§22.2a). Bu, FU09 contact-derived
> coverage için **prerequisite**'tir.
> **FU05B**; aktif/inactive bir modelden yeni editable draft sürüm oluşturulmasını, model metadata + hierarchy +
> assignment rule'ların yeni kimliklerle klonlanmasını ve yeni sürüm aktivasyonunda account assignment continuity'nin
> fail-closed biçimde taşınmasını açar (§22.2b). Carry-forward draft oluşturma anında operational assignment yazmaz;
> aktivasyonda kaynak node → hedef node eşlemesi canonical `TerritoryCode` ile doğrulanır. Eksik/çift node veya
> conflict varsa aktivasyon hiçbir kayıt değiştirmeden 409 döner. Başarılı cutover eski assignment'ları `ended`
> yapar, yeni model altında yeni active kayıtlar açar ve provenance/history'yi korur. Account/Contact master mutate
> edilmez; workflow approval ve genel active-model mutation açılmaz.
> **FU08**; büyük territory modellerinin ve atamalarının elle tek tek girilmeden yönetilebilmesi için **kontrollü
> XLSX import/export**'u açar (§22.5): read-only export (model metadata, node'lar, hiyerarşi, BU scope'ları, assignment
> rule'ları, account assignment current/history, CoverageSummary, resource assignment current/history, Plan vs Current),
> çok-sheet'li import template üretimi, **dry-run-first** satır bazlı validation raporu, guard'lara saygılı safe apply
> ve read-only `TerritoryImportRun` geçmişi. FU08 **hiçbir mevcut guard'ı bypass etmez**: account assignment import'u
> FU05 apply kurallarının (yalnız active model, all-or-nothing, conflict/override + reason, ended-not-deleted)
> **aynısını** kullanır; resource assignment tarafında v1 yalnız export + template + dry-run'dır, **apply ayrı bir
> FU08A yetkisidir**; CoverageSummary ve Plan vs Current **yalnız export**'tur, import edilemez. FU08 workflow/approval,
> visit/route, campaign/frequency, Brand/Product master açmaz; Account/Contact master'ı mutate etmez.
> **FU09A**; MOD-0155 Visit / Route Planning başlamadan **önce** MOD-0151 tarafında hangi read model, endpoint ve
> sahiplik sınırlarının hazır olması gerektiğini yetkilendirir (§22.6): **yalnız-okuma** territory coverage readiness,
> resource/MR responsibility readiness, `AccountContactLink` üzerinden **derived** contact coverage, route candidate
> **readiness** projeksiyonu ve makine-okunur reason code sözleşmesi. FU09A ayrıca üç girdinin **sahipliğini pack'e
> yazar ama implementasyonunu açmaz**: contact availability / working schedule (**MOD-0150**, `AccountContactLink`
> bazlı), visit frequency / call-cycle policy (**campaign/segmentation → MOD-0165 / MOD-0167**, tüketici MOD-0155) ve
> last visit / due-overdue (**MOD-0155**). **FU09A rota üretmez**: günlük route planı, visit planı, route
> optimizasyonu, campaign/frequency engine, GPS check-in/out, visit report ve digital detailing **açılmamıştır** —
> hepsi MOD-0155'e aittir. FU09A hiçbir mutasyon açmaz.
> Bu **genel bir runtime izni değildir**: Account/Contact master mutasyonu, Account veya Contact entity'sine territory
> alanı eklenmesi, FU04A dışında resource assignment mutasyonu (FU08 v1 resource **apply** içermez), workflow approval/MOD-0023 entegrasyonu (FU06), evidence pack
> (FU07), FU09A dışındaki visit/route readiness ve coverage roll-up (FU09), visit/route **planning implementation**
> (MOD-0155), contact availability **master** implementation (MOD-0150-FU), frequency/call-cycle **engine**,
> Brand Scope ve Product/Brand master yetkilendirilmemiştir.
>
> **FU03/FU04/FU04A/FU05 sınırı:** FU03 preview handler'ı yalnız yazma üyesi bulunmayan bir read seam üzerinden Account
> okur; FU04 **kişi** atar, **müşteri** atamaz ve employee/person master MOD-0151'e ait değildir (pack §10 PersonRef
> seam). FU04A yalnız resource responsibility lifecycle'ını harden eder. FU05 müşteri atamasını yalnız ayrı
> `AccountTerritoryAssignment` aggregate'inde kalıcılaştırır; Account ve
> Contact SoR kayıtlarını hiçbir zaman mutate etmez.
> Permission seed/grant, reference set publish ve registry kaydı **hâlâ bu pack'in yetkisi dışındadır**.
> Otorite sırası: **Blueprint Excel** (`docs/System Capability & Implementation Blueprint - master 7.xlsx`,
> `Blueprint_Data`) > Module Pack > [Domain Config](../domain-config.md) > `AGENTS.md` > `.antigravity/rules/`.
> Preflight/tasarım analizi: [mod-0151-territory-management-pack-prep-2026-07-23.md](../../../../docs/audits/mod-0151-territory-management-pack-prep-2026-07-23.md).
>
> **Frontmatter notu:** `id` / `name` alanları MOD-0149 & MOD-0150 pack konvansiyonu (DCP-002 canonical-name gate bu
> alanları okur) ile birebir korunmuştur; `module_id` / `module_name` bunların açık takma adlarıdır.

---

## 1. Executive Summary

MOD-0151, Commercial Suite / CRM Core'un **ticari yapı (commercial structure)** modülüdür. Blueprint'e göre üç
System-of-Record sınıfını sahiplenir: **territories**, **assignments**, **territory change approvals**.

Bu modül "bir zone tablosu" değildir. Teslim ettiği şey **yönetişimli bir bölge planlama sistemi**dir:
versiyonlu territory modeli · konfigüre edilebilir hiyerarşi (division→microzone) · kural tabanlı ve manuel
account atamaları · position-based resource (MR / Area Manager / Regional Manager / Product Manager / HOC /
Commercial Manager / Production Admin PositionRef policy'leri) atamaları · aktif modelin kontrollü değişiklik
sözleşmesi · MOD-0023 üzerinden **gerçek** onay ·
değişiklik izi · kanıt paketi ve performans takibi için roll-up hazırlığı.

Blueprint'in `Delivery Outcome` alanı iki şey ister — **"Controlled territory changes; performance tracking"** — ve
pack'in tüm mimarisi bu ikisinden türer: (a) aktif model node/rule/account kapsamı doğrudan mutate edilemez,
değişiklik change request + approval + versiyon üzerinden gider; FU04A resource responsibility için yalnız auditli
create/end/replace/transfer komutlarını kontrollü istisna yapar; (b) forecast/ziyaret KPI'ları hesaplanmaz ama roll-up boyutları ve coverage
read-model'leri hazırlanır.

MOD-0151 hiçbir master veriyi fork etmez: Account (MOD-0149), Contact (MOD-0150), Person/Position/OrgUnit (MOD-0288),
Product/Brand (MDM future), reference values (MOD-0048), permission engine (MOD-0018), workflow engine (MOD-0023),
audit store (MOD-0021) — hepsi **tüketilir**, sahiplenilmez.

---

## 2. Blueprint Alignment

`Blueprint_Data` MOD-0151 satırı (ham okuma; otorite budur):

| Blueprint Field | Value | Pack'te nasıl karşılandı |
|---|---|---|
| Module ID / Name | MOD-0151 / Territory Management | Frontmatter `id`/`name` birebir (DCP-002 canonical-name gate) |
| Domain / Landscape | 4) Enterprise Application Ecosystem | `domain: commercial-suite` |
| Suite / Platform | Commercial Suite (CRM + O2C) | Domain Config in-scope listesi |
| Capability Group | CRM Core | MOD-0149/0150 ile aynı bounded context → `Diten.CrmService` |
| Aim / Goal | "Maintain customer master and commercial structure with governed integrations and consistent semantics." | Territory = **commercial structure**. "Governed" → §13 approval + §14 evidence + MOD-0021 audit. "Consistent semantics" → §16 MOD-0048 reference-driven; hardcoded enum/fallback **yasak** |
| Wave | W-4 | MOD-0149 (W-1) ve MOD-0150 (W-3) runtime'da → wave ön koşulu karşılanmış |
| Dependency Gate | Customer 360; Workflow Designer | MOD-0149 **hard** (§21), MOD-0023 **hard** (§13). MOD-0023 runtime'da mevcut → fake approval yasak, gerçek seam kuruldu |
| Delivery Outcome / Value | **Controlled territory changes; performance tracking** | §13 (controlled changes) + §15 (performance readiness) |
| Soft Pages | Territory Model Viewer; Change Approval Trace; Evidence Pack | §18'de üçü de **zorunlu** yüzey |
| Placement | Domain App (CRM) | tenant shell, `_LayoutTenantShell`, Golden Reference Compact |
| Bundle | CRM-TERRITORY-BUNDLE (territory model schema, approvals, audit/evidence export) | §7 schema + §13 approvals + §14 evidence export |
| SoR | territories, assignments, territory change approvals | §5 Scope; `SoR_Map` collision count = **0** |
| SoR Primary Object(s) | territories | `TerritoryNode` + `TerritoryModel` |
| Integration Contract | DOMAIN-APP-BASE | tenant isolation + authz + audit + health taban kontratı |
| Deployment Unit | Domain Applications | `Diten.CrmService` (port 5061), Gateway-only erişim |
| Build / Buy / Partner | **Buy/Partner** | ⚠️ §4-D6 governance sapma notu |
| SLO Tier | Tier 2 | Planlama modülü; runtime-critical değil |
| Support Model | L1 Service Desk; L2 Domain App Ops; L3 Vendor / Partner | Frontmatter birebir Excel'den |
| AI Enablement / Risk | Assist / Medium (gate: Prompt Registry, HITL, Model Registry, Eval/Drift, Logging) | ⚠️ §4-D5: v1 **AI-OFF** → 5 hard AI gate runtime blocker değil |

**`Module Pages` sayfası notu:** Excel MOD-0151 için 3 named soft page + ~25 generic CRM soft page (Overview Dashboard,
My Work/Approvals Inbox, Exceptions & Reconciliation Queue, Trace/Audit Viewer, Configuration, Reports & Analytics…)
listeler. **Named 3 sayfa zorunludur**; generic sayfalar opsiyonel/ileri FU olarak değerlendirilir (§18).

**`Dependencies` / `Dependencies_Normalized` notu:** Blueprint_Data'nın `Dependency Gate`'i (Customer 360; Workflow
Designer) dışında normalize edilmiş sayfa `MOD-0068 Prompt Registry`, `MOD-0069 HITL`, `MOD-0066 Model Registry`,
`MOD-0067 Eval/Drift`, `MOD-0041 Logging` bağımlılıklarını **HARD** işaretler. Bunlar **AI Enablement Tier=Assist**
kaynaklıdır ve yalnız AI özelliği açıldığında bağlayıcıdır → §4-D5.

---

## 3. Module Summary

Territory Management, tenant'ın ticari kapsama yapısını (kim, nerede, hangi ürün/iş birimi için sorumlu) versiyonlu ve
onaylı bir plan nesnesi olarak yönetir. Yaşam döngüsü: **draft → review → approved → active → superseded → archived**.

- **Territory model:** tenant + scope (ülke/division/business scope) + effective period için bir plan sürümü.
- **Territory hierarchy:** `TerritoryNode` + `TerritoryLevel` (division/country/region/area/zone/microzone) — tek ağaç,
  konfigüre edilebilir seviyeler, hardcoded sıra yok.
- **Assignments:** account↔territory (kural/manuel/import/override) ve resource↔(territory | business scope) atamaları,
  hepsi efektif-tarihli.
- **Controlled change:** aktif model node/rule/account kapsamı immutable; değişiklik `TerritoryChangeRequest` +
  MOD-0023 onayı + before/after snapshot + correlation id ile yönetilir. FU04A resource responsibility lifecycle'ı
  history koruyan auditli create/end/replace/transfer komutlarıyla sınırlı istisnadır.
- **Evidence:** `TerritoryEvidencePack` — Blueprint bundle bileşeni, opsiyonel değil.
- **Performance readiness:** roll-up read-model'leri ve coverage API'leri (MOD-0154 / MOD-0155 tüketir).

---

## 4. Confirmed Decisions

> Bu kararlar **MOD-0151 Pack Prep** aşamasında alınmış ve kullanıcı tarafından onaylanmıştır. Pack tasarımı bunları
> esas alır; yeniden tartışılmaz.

| ID | Decision | Karar | Gerekçe / Veri kaybı riski |
|---|---|---|---|
| **D1** | Alpha / Beta / Gamma | **Sabit Business Unit / Product Portfolio / Business Scope.** Yıl veya çeyrek bazında yeniden açılmaz. Dönemsellik `TerritoryModel` versiyonuna / `PlanningPeriodRef`'e taşınır | Legacy'de çeyrek/yıl başına BU kopyalanması → BU kimliği kopar, geçmiş performans karşılaştırılamaz. Sabit `ScopeCode` bu kopukluğu önler |
| **D2** | Production Admin | **Satış/product-portfolio business unit'i DEĞİL.** Eski sistemde factory / affiliated company / non-sales yapıların resource planlaması için BU adı gibi kullanılmış. Target: `TerritoryBusinessScope(ScopeType=operational-scope \| non-sales-resource-planning)`, `IsSalesScope=false`, `IncludeInSalesPerformance=false`. Resource assignment ve visibility planlamasında **kullanılabilir**; satış roll-up'ına **otomatik dahil edilmez** | Operasyonel kaynakların satış performansına karışması → portfolyo roll-up'ları ve MR verimlilik metrikleri bozulur |
| **D3** | Product / Brand master | **MOD-0151 sahiplenmez.** Yalnız Product Portfolio / Brand Group / BrandCode **seam/reference**. v1'de gerekiyorsa MOD-0048 tenant-owned `product-portfolio` / `brand-group` geçici set. Portfolio↔Brand mapping MOD-0151'in **kalıcı sahipliği değildir**; Product/Brand master modülü gelince MOD-0151 ona bağlanır | Legacy Property/PropertyList hatası (ürün listesinin CRM'de kopyalanması) tekrarlanmaz; sonradan MDM ile çift kayıt olmaz |
| **D4** | Territory data-scope | **Hedef davranış** (§10): MR kendi zone/microzone'u · Area Manager area/region altı · Regional Manager region/division subtree · Product Manager ilgili BU/portfolyo tamamı · HOC tüm BU + tüm zone · Commercial Manager Area Manager benzeri + ticari scope kısıtı · Production Admin satış roll-up dışında ama resource-planning visibility içinde. **v1'de tam platform-level enforcement zorunlu değil** (HR/HCM/Position/ManagerChain olgunlaşmadı) → MOD-0151 **kendi CrmService coverage read-model/filter mantığını** kurar. Platform-level Territory data-scope **MOD-0018 follow-up**'tır; MOD-0151 `EntitlementDataScopeKind` enum'unu veya platform data-scope engine'ini **değiştirmez** | Platform enum'una MOD-0151'in dokunması = domain boundary ihlali; olgunlaşmamış HCM'e sert bağımlılık = FU01 bloklanır |
| **D5** | AI | **v1 AI-OFF.** Blueprint AI Assist / Medium risk yazsa da AI özellikleri v1 kapsam dışı. AI açılmadığı sürece MOD-0066 / MOD-0067 / MOD-0068 / MOD-0069 / MOD-0041 hard AI gate'leri **runtime blocker yapılmaz**. AI summarize/recommend ayrı FU olarak planlanabilir | 5 AI gate'i gereksiz yere hard blocker yapmak MOD-0151'i W-4'te süresiz bekletir |
| **D6** | Build / Buy / Partner | Blueprint **Buy/Partner** diyor; repo gerçekliği MOD-0149/MOD-0150 gibi **in-house build**. **EA governance notu:** "Blueprint Buy/Partner ile repo in-house build arasında bilinçli sapma vardır; MOD-0149/MOD-0150 precedent'i nedeniyle in-house build önerilmiştir. EA onayı / governance note gereklidir." **Blocker değildir** | Sapmanın kayıt altına alınmaması → sonraki EA denetiminde açıklanamayan mimari fark |
| **D7** | RBAC supersede | [crm-rbac-integration-plan.md](../crm-rbac-integration-plan.md) §3'teki eski MOD-0151 anahtarları (`crm.territory.create/update/assign-rep/assign-account`, **`crm.micro-zone.manage`**) **supersede edilmelidir**. MicroZone ayrı nesne değil, `TerritoryNode(level=microzone)`'dur → ayrı permission önerilmez. §17 yeni liste geçerlidir. **Bu pack hiçbir permission seed etmez ve `crm-rbac-integration-plan.md`'yi değiştirmez** — yalnız §23 follow-up listesine yazar | Ayrı `micro-zone` izni yanlış mimari sinyali verir (MicroZone'un ayrı aggregate olduğu izlenimi) |

---

## 5. Scope

MOD-0151 **sahiplenir (owns):**

1. `TerritoryModel` — versiyonlu plan kabı, lifecycle, approval durumu, aktivasyon, supersede/archive.
2. `TerritoryNode` — hiyerarşi düğümleri (division/country/region/area/zone/microzone) ve kapsama kriterleri.
3. `TerritoryLevel` **kullanımı** (değer seti MOD-0048'de).
4. `TerritoryAssignmentRule` — kural tipleri, kriterler, öncelik, çakışma politikası.
5. `AccountTerritoryAssignment` — account↔territory efektif-tarihli atamalar + kaynak + gerekçe + çakışma durumu.
6. `TerritoryResourceAssignment` — kişi↔(territory | business scope) rol bazlı kapsam atamaları.
7. `TerritoryChangeRequest` — kontrollü değişiklik, before/after snapshot, workflow bağı, karar izi.
8. `TerritoryEvidencePack` — kanıt paketi kompozisyonu + export.
9. `TerritoryBusinessScope` — BU / product portfolio / brand group / operational scope **referans** boyutu (master değil).
10. Coverage read-model'leri — Account 360 `CoverageSummary`, türetilmiş contact coverage, resource coverage, roll-up.

---

## 6. Out of Scope

| Konu | Sahibi |
|---|---|
| Account / WorkPlace master, AccountCode üretimi, account hiyerarşisi, adres/geo persistence | **MOD-0149** |
| Contact master, Contact↔Account link, Account↔Account relationship, consent | **MOD-0150** / MOD-0164 |
| Employee / Person / User / Position / OrgUnit master, reporting chain | **MOD-0288** / MOD-0018 |
| Business Unit **master** kaydı (MOD-0151 yalnız referans tutar) | **MOD-0288** (unitType follow-up) |
| Product / Brand / SKU master, portfolio↔brand kalıcı mapping | **MDM / Product (future)** — D3 |
| country / city / district ve tüm lookup değerleri | **MOD-0048** |
| Permission/RBAC engine, platform data-scope engine, `EntitlementDataScopeKind` | **MOD-0018** — D4 |
| Workflow/approval engine, task, SLA, escalation | **MOD-0023** |
| Audit store, retention, redaction | **MOD-0021** |
| Genel evidence object store / provenance / cross-module evidence linking | **MOD-0031** (future) |
| Quota, forecast, hedef hesaplama | **MOD-0154** |
| Visit plan, visit, MicroTarget, rota, mesafe, nearby search, frequency/cadence, daywork/visit-mix | **MOD-0155** |
| Segment tanımı / değerlendirmesi | **MOD-0167** |
| Navigation loader/engine | **MOD-0285** |
| Gateway global routing policy (`ocelot.json`) | **integration-agent** |
| Hasta/klinik veri, tıbbi kayıt | Kapsam dışı (CRM değil) |
| AI assist / recommend / summarize | v1 kapsam dışı — D5 |

---

## 7. Domain Model

Tüm entity'ler `EntityBase` (tenant-owned) · `TenantId` **zorunlu**, yalnız JWT'den server-side çözülür ·
soft-delete (`IsDeleted`/`DeletedAt`) · cross-tenant erişim **404** · iş alanı olarak `Version` adı **kullanılmaz**
(concurrency için rezerve; MOD-0149 §10 naming kuralı) → `VersionNumber`.

### 7.1 `TerritoryModel` (aggregate root)

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `TerritoryModelId` | Guid | ✅ | — |
| `TenantId` | Guid | ✅ | server-side |
| `ModelCode` | string | ✅ | tenant-scoped unique; trim/normalize |
| `Name` | string | ✅ | max 200 |
| `CountryScope` | string? | ❌ | MOD-0048 `country` referansı |
| `DivisionScope` | string? | ❌ | opsiyonel üst kırılım kodu |
| `BusinessScopes[]` | `TerritoryBusinessScope[]` | ❌ | **çoklu** (bir model N portfolyoyu kapsayabilir) |
| `PlanningPeriod` | `PlanningPeriodRef?` | ❌ | annual/quarterly/monthly/custom |
| `EffectiveFrom` / `EffectiveTo` | DateTimeOffset / ? | ✅ / ❌ | `From <= To` |
| `Status` | string (ref) | ✅ | `territory-model-status`: draft/review/approved/active/superseded/archived |
| `VersionNumber` | int | ✅ | 1'den başlar; klonlamada artar |
| `BasedOnModelId` | Guid? | ❌ | klon/yeni sürüm kaynağı |
| `SupersededByModelId` | Guid? | ❌ | aktivasyon sonrası set edilir |
| `ChangeReason` | string? | ⚠️ | yeni sürüm/aktivasyon değişikliklerinde zorunlu |
| `ApprovalStatus` | string | ✅ | MOD-0023 **yansıması** (kaynağı değil) |
| `ApprovalWorkflowInstanceId` | string? | ❌ | MOD-0023 instance |
| `ApprovedBy` / `ApprovedAt` | string? / DateTimeOffset? | ❌ | workflow varsa |
| `ActivatedAt` / `ActivatedBy` | DateTimeOffset? / string? | ❌ | aktivasyon izi |
| `ActiveSnapshotRef` | string? | ❌ | immutable snapshot referansı (§13.4) |
| `CorrelationId` | string? | ❌ | uçtan uca iz |
| audit metadata | — | ✅ | CreatedAt/By, UpdatedAt/By, IsDeleted/DeletedAt |

### 7.2 `TerritoryNode` (aggregate, model-scoped)

`TerritoryId` · `TenantId` · `ModelId` · `ParentTerritoryId?` · `TerritoryCode` (model içinde unique) · `Name` ·
`TerritoryLevel` (ref: division/country/region/area/zone/microzone) · `CountryCode?` · `DivisionCode?` ·
`RegionCode?` · `AreaCode?` · `ZoneCode?` · `MicroZoneCode?` · `GeoCriteria` (country / city / district / postal code;
**future** polygon/coordinate referansı — v1'de yok) · `AccountCriteria` · `ProductPortfolioCriteria` ·
`ChannelCriteria` · `SegmentCriteria` · `BusinessScopes[]?` · `MicroZoneProfile?` (yalnız level=microzone) ·
`IsLeaf` (türetilmiş) · `Status` (ref: `territory-node-status`) · `EffectiveFrom` / `EffectiveTo` · `SortOrder` ·
audit metadata.

> `AnchorAccountId` node'un düz alanı **değildir** → `MicroZoneProfile` value object'i içindedir (§12).

### 7.3 `TerritoryAssignmentRule` (aggregate, model-scoped)

`RuleId` · `TenantId` · `ModelId` · `TerritoryId` · `RuleType` (ref: geography / account-list / account-type /
product-portfolio / business-scope / channel / segment / manual / import) · `RuleExpression` veya yapılandırılmış
kriter objesi · `Priority` (int) · `ConflictPolicy` (ref: block / warn / priority / manual-review) · `IsActive` ·
`EffectiveFrom` / `EffectiveTo` · audit metadata.

### 7.4 `AccountTerritoryAssignment` (aggregate)

`AssignmentId` · `TenantId` · `ModelId` · `TerritoryId` · `AccountId` (MOD-0149) ·
`AccountCodeSnapshot?` (**yalnız display**; sorgu/eşleşme anahtarı değil, hiçbir zaman SoR değil) ·
`BusinessScope?` · `AssignmentSource` (ref: rule / manual / import / override) · `SourceRuleId?` ·
`ValidFrom` / `ValidTo` · `Status` (ref: proposed / active / ended / rejected) · `IsPrimary` ·
`ConflictStatus` + `ConflictNotes?` · `ChangeReason?` (manual/override'da **zorunlu**) ·
`EvidenceMetadata` (correlationId, requestedBy, changeRequestId?) · audit metadata.

### 7.5 `TerritoryResourceAssignment` (aggregate)

`AssignmentId` · `TenantId` · `ModelId` · `TerritoryId?` (coverage scope'a bağlı) ·
`PersonRef` (MOD-0288) · `UserId?` (MOD-0018) · `EmployeeRef?` (HCM future seam) ·
`PositionRef` (`PositionId?`, `PositionCode`, `PositionTitle`, `PositionType`, `SourceSystem`) ·
`LegacyRoleCode?` (**migration-only, deprecated; yeni write contract'ta kabul edilmez**) ·
`BusinessScope?` (BU / portfolio / operational) ·
`CoverageScope` (ref: exact-territory / territory-subtree / business-unit / product-portfolio / business-scope /
model-wide / all-business-scopes) · `ValidFrom` / `ValidTo` · `Status` · `IsPrimary` · `AssignmentSource` ·
`ChangeReason?` · `EvidenceMetadata` · audit metadata.

### 7.5a `TerritoryResourceAssignmentPlanSnapshot` (aggregate — **immutable**, FU04B)

Activation anındaki proposed resource assignment planının **write-once** baseline'ı. Bir kez yazılır, **asla
update/delete edilmez**; sonraki her plan-vs-current karşılaştırmasının referans noktasıdır.

**Header:** `PlanSnapshotId` · `TenantId` · `TerritoryModelId` · `CapturedAt` · `CapturedBy` ·
`ActivationCorrelationId` · `SnapshotVersion` · audit metadata.

**Line (`PlanSnapshotLine`, snapshot anındaki değerlerin donmuş kopyası):** `TerritoryNodeId` · `TerritoryNodeCode` ·
`TerritoryNodeName` · `BusinessScopes` · `PositionCode` · `PositionTitle` · `PositionType` ·
`ResourceId` / `PersonRef` · `ResourceDisplayName` · `PlannedEffectiveFrom` · `PlannedEffectiveTo` · `IsPrimary` ·
`SourceAssignmentId`.

**Kurallar:**

- Snapshot yalnız `draft → active` activation lifecycle işlemi içinde, proposed kayıtlar `active` yapılmadan **hemen
  önce** ve **aynı işlem sınırında** yazılır. Activation fail-closed olursa snapshot da yazılmaz (all-or-nothing).
- Model başına aktivasyon başına **bir** snapshot. Yeniden aktivasyon (inactive → active) yeni bir
  `SnapshotVersion` üretir; önceki sürüm silinmez.
- `SourceAssignmentId`, snapshot satırını canlı `TerritoryResourceAssignment` zincirine (replacement/transfer
  provenance dahil) bağlayan tek anahtardır.
- Snapshot **display kopyasıdır, SoR değildir**: Person/Position master MOD-0288'e, assignment SoR'u
  `TerritoryResourceAssignment`'a aittir.
- `LegacyRoleCode` snapshot'a **yazılmaz** (§22.4 E).

### 7.5b `TerritoryImportRun` (aggregate — **append-only**, FU08)

Bir import **apply**'ının kalıcı, salt-okunur izi. Yalnız yazılır; **update/delete komutu yoktur**, hard delete
edilmez (§22.5).

**Alanlar:** `ImportRunId` · `TenantId` (server-resolved, payload'dan değil) · `FileName` · `FileHash` ·
`UploadedBy` · `UploadedAt` · `Status` (`applied` / `partially-applied` / `failed` / `blocked`) · `DryRunResult`
(özet + satır raporu) · `AppliedAt` · `AppliedBy` · `CorrelationId` · sheet bazında row counts
(total / created / updated / ended / skipped) · error counts · warning counts.

**Kurallar:**

- Yalnız **apply** bir run kaydı yazar; salt dry-run çağrısı **hiçbir şey persist etmez** (run kaydı dahil).
- Ham dosya **saklanmaz** — yalnız `FileHash` tutulur (PII/dosya saklama yüzeyi açılmaz).
- `ImportRunId` + `CorrelationId`, apply'ın yazdığı/kapattığı her kayda provenance olarak taşınır; aynı dosyanın
  ikinci apply'ı bu anahtarlar üzerinden **duplicate üretmez**.
- Run kaydı bir approval/evidence artefaktı **değildir**; FU06 approval trace ve FU07 evidence pack sahiplikleri
  değişmez.

### 7.6 `TerritoryChangeRequest` (aggregate)

`ChangeRequestId` · `TenantId` · `ModelId` · `ChangeType` (create-model / update-hierarchy / update-assignment-rule /
update-account-assignment / update-resource-assignment / activate-model / supersede-model) · `RequestedBy` /
`RequestedAt` · `Reason` (**zorunlu**) · `BeforeSnapshotRef` · `AfterSnapshotRef` · `ApprovalWorkflowInstanceId?` ·
`ApprovalStatus` · `DecisionBy?` / `DecisionAt?` / `DecisionReason?` · `EvidencePackId?` · `CorrelationId` ·
audit metadata.

### 7.7 `TerritoryEvidencePack` (aggregate, read-heavy)

`EvidencePackId` · `TenantId` · `ModelId` · `GeneratedAt` / `GeneratedBy` · `CorrelationId` · `ModelSnapshot` ·
`HierarchySnapshot` · `BusinessScopes` · `PlanningPeriod` · `AssignmentRules` · `AssignmentCounts` ·
`AccountAssignmentSample` (gerekirse maskeli) · `ResourceAssignments` · `Conflicts` · `ApprovalTrace` ·
`ActivationEvidence` · `AuditEventRefs` · `ExportFileRef?` · `ExportVersion` · audit metadata.

### 7.8 `TerritoryBusinessScope` (value object)

§9'da detaylandırılmıştır. `TerritoryModel`, `TerritoryNode`, `AccountTerritoryAssignment` ve
`TerritoryResourceAssignment` içinde gömülü kullanılır. **Master kaydı değildir.**

### 7.9 `MicroZoneProfile` (value object — yalnız `level=microzone`)

`AnchorAccountId?` · `ClusterNotes?` · `PlanningCenterType?`. Diğer level'larda **null olmak zorundadır** (§20).

### 7.10 `PlanningPeriodRef` (value object)

`PeriodType` (ref: annual / quarterly / monthly / custom) · `PeriodCode` (örn. `2027-Q1`) · `From` / `To`.
İleride ayrı bir Planning Period master modülü çıkarsa bu VO referansa dönüşür.

### 7.11 `TerritoryCoverageReadModel` (query DTO — entity DEĞİL)

Account coverage · derived contact coverage · resource coverage · roll-up projeksiyonları. MOD-0149 (Account 360),
MOD-0154 (roll-up boyutları) ve MOD-0155 (coverage API'leri) tarafından tüketilir. Persist edilmez.

### 7.12 `TerritoryRouteCandidateReadModel` (query DTO — entity DEĞİL, FU09A)

MOD-0155 Visit / Route Planning'in **girdi** olarak okuduğu readiness projeksiyonu. **Yeni aggregate değildir,
persist edilmez, cache'lenmez**; her çağrıda current coverage (FU05A guard'ı) + current resource responsibility
(FU04A/FU04B) üzerinden hesaplanır (§22.6).

**Alanlar:** `AccountId` · `AccountName` · `TerritoryNodeId` · `TerritoryNodeCode` · `BusinessUnit` · `ResourceId` ·
`ResourceDisplayName` · `PositionCode` · `ContactId?` · `ContactName?` · `AccountContactLinkId?` ·
`AvailabilityStatus` · `PreferredVisitWindow?` · `FrequencyStatus` · `LastVisitDate?` · `DueStatus` ·
`LocationReadiness` · `ReasonCodes[]` (makine-okunur, §22.6 reason code sözleşmesi) · `EffectiveAt` ·
`CoverageSource` (hangi account/assignment üzerinden türetildi).

**Kurallar:**

- **Bu DTO bir rota değildir**: sıra, mesafe, süre, gün planı, stop listesi ve optimizasyon skoru **taşımaz**.
- Contact bilgisi yalnız `AccountContactLink` (MOD-0150) üzerinden **türetilir**; Contact'a territory alanı eklenmez.
- `AvailabilityStatus`, `FrequencyStatus`, `LastVisitDate` ve `DueStatus` MOD-0151'in **sahiplendiği veriler
  değildir**; girdi mevcut değilse alan `unknown`/`not-available` döner ve ilgili reason code eklenir — **MOD-0151
  bu değerleri üretmez, tahmin etmez ve persist etmez**.

---

## 8. Territory Hierarchy

**Karar (D-hierarchy):** Division / Country / Region / Area / Zone / MicroZone **ayrı aggregate YAPILMAZ**. Hepsi
`TerritoryNode` + `TerritoryLevel` ile modellenir.

**Gerekçe:** (a) seviye sayısı ülke/tenant'a göre değişir, ayrı aggregate her farklılıkta şema değişikliği gerektirir;
(b) subtree / roll-up / cycle sorguları tek koleksiyonda tutarlı ve basittir; (c) Salesforce `Territory2` ve SAP
Territory Hierarchy benchmark'ı da tek nesne + tip/level yaklaşımını kullanır; (d) yeni seviye ihtiyacı (örn.
`sub-region`) yalnız MOD-0048 set'ine değer eklemekle çözülür.

| Level | Anlam | Zorunlu? | Konfigüre edilebilir? | Not |
|---|---|---|---|---|
| `division` | Satış organizasyonu / coğrafi üst kırılım | Hayır | ✅ | **Business Unit değildir** (§9) |
| `country` | Ülke düzeyi düğüm | Hayır (tek-ülke tenant'ta pratik kök) | ✅ | `CountryCode` MOD-0048 `country` referansı |
| `region` | Bölge | Hayır | ✅ | Regional Manager kapsamı |
| `area` | Alt bölge | Hayır | ✅ | Area Manager kapsamı |
| `zone` | MR'ın birincil sorumluluk birimi | ⚠️ Pratikte (MR ataması buraya bağlanır) | ✅ | Modelde en az bir leaf-benzeri seviye gerekir |
| `microzone` | Zone içi planlama kümesi | Hayır | ✅ | §12; MOD-0155 tüketir |

**Kurallar:**
- Her tenant her level'ı kullanmak **zorunda değildir** (örn. TR: `country > region > zone > microzone`;
  DE: `division > region > area > zone`).
- Level sırası **hardcoded değildir**. `territory-level` reference değerleri metadata olarak **`rank` / `sortOrder`**
  taşır (MOD-0150 `account-relationship-type` direction/inverse metadata precedent'i ile aynı desen).
- Validation: **child rank > parent rank**.
- **Level atlamak serbesttir** (`country → zone` geçerli). **Geri gitmek yasaktır** (`zone → region` geçersiz).
- Hiyerarşide **cycle yasaktır** (MOD-0149 `ParentAccountId` cycle guard precedent'i).
- `TerritoryCode` **model içinde unique**.
- Child `EffectiveFrom/To`, parent ve model tarih aralığı **dışına çıkamaz**.
- MicroZone **ayrı aggregate / ayrı permission / ayrı collection değildir**.

---

## 9. Business Scope / Product Portfolio / Operational Scope

**Business Unit territory level DEĞİLDİR** — kesişen bir **boyuttur**. Business Unit / Product Portfolio /
Operational Scope / Production Admin gibi kavramlar `TerritoryBusinessScope` value object'i ile modellenir.

### 9.1 `TerritoryBusinessScope` alanları

| Alan | Açıklama |
|---|---|
| `ScopeType` | ref `business-scope-type`: business-unit · product-portfolio · brand-group · operational-scope · non-sales-resource-planning · channel · segment |
| `ScopeCode` | Sabit, dönem taşımayan kod (D1) |
| `ScopeDisplayNameSnapshot` | **Yalnız display**; SoR değil |
| `ExternalRef` | Dış sistem/MDM referansı (opsiyonel) |
| `ValidFrom` / `ValidTo` | Valid-dated bağ; brand/BU değişiminde geçmiş kopmaz |
| `Source` | MOD-0288 · MOD-0048 · MDM/Product (future) · external |
| `IsSalesScope` | bool |
| `IncludeInSalesPerformance` | bool |
| `Notes` | opsiyonel |

### 9.2 Scope sınıflandırma kararları

| Kavram | ScopeType | IsSalesScope | IncludeInSalesPerformance | Not |
|---|---|---|---|---|
| **Alpha / Beta / Gamma** | `business-unit` veya `product-portfolio` | **true** | **true** | D1: sabit sales/product scope; yıl/çeyrek bazında yeniden açılmaz |
| **Production Admin** | `non-sales-resource-planning` veya `operational-scope` | **false** | **false (default)** | D2: resource assignment ve visibility **yapılabilir**; satış roll-up'ına **otomatik girmez** |
| **Factory / Affiliated Company** | `operational-scope` veya `non-sales-resource-planning` | **false** | **false** | Satış territory performansına karışmaz; resource planning için atama yapılabilir |
| **Brand grubu** | `brand-group` | duruma göre | duruma göre | D3: yalnız seam/reference; brand master MOD-0151'de değil |
| **Kanal / Segment** | `channel` / `segment` | duruma göre | duruma göre | Segment tanımı MOD-0167'de |

### 9.3 Sahiplik sınırı

- MOD-0151 **Business Unit master kurmaz**. Bugün `OrganizationUnit` (MOD-0288) üzerinde `unitType` alanı **yoktur**
  → ya MOD-0288'e eklenir (**MOD-0288'in işi**, §23 follow-up), ya da MOD-0048 tenant-owned bir set kullanılır.
- MOD-0151 **Product/Brand master kurmaz** (D3). v1'de MOD-0048 tenant-owned `product-portfolio` / `brand-group`
  **geçici** set önerilir; Product/Brand master modülü gelince MOD-0151 ona bağlanır ve geçici set emekliye ayrılır.
- Portfolio ↔ Brand mapping **MOD-0151'in kalıcı sahipliği değildir**.

---

## 10. Resource Assignment

`TerritoryResourceAssignment` MOD-0151'e aittir; **Employee / User / Person / Position master MOD-0151'e ait değildir**
(MOD-0288 / MOD-0018 referansları: `PersonRef`, `UserId`, opsiyonel `EmployeeRef` seam).

> **FU04A canonical karar — position-based:** Resource responsibility kimliği `PositionRef` /
> `PositionCode` üzerinden tanımlanır. Aşağıdaki tarihsel role adları yalnız legacy policy eşlemesi ve migration
> bağlamıdır; yeni entity, command, query, conflict key, UI label veya testlerde `RoleCode` authoritative alan olarak
> kullanılamaz. UI etiketi **Position** olur. `territory-resource-role` compatibility-only/deprecated sözlüktür.

| Legacy role → Position policy mapping | Coverage Scope | Level / Boyut | Exclusivity | Not |
|---|---|---|---|---|
| `medical-representative` | `exact-territory` | zone veya microzone | **Blok:** aynı örtüşen dönemde **farklı business scope'larda** aktif primary MR olamaz (override + gerekçe ile aşılabilir). **Uyarı:** aynı BU içinde çoklu zone → izinli, workload/coverage-conflict warning | Sadece kendi zone/microzone account'larını görmesi hedeflenir (D4) |
| `area-manager` | `territory-subtree` | area veya region altı | (model, node, business scope) başına tek aktif primary | Alt zone/microzone account + resource coverage'ını görür. **Resource hiyerarşisi ≠ territory hiyerarşisi** |
| `regional-manager` | `territory-subtree` | region / division altı | Aynı | — |
| `division-manager` | `territory-subtree` | division altı | Aynı | Division level kullanılmıyorsa atanamaz |
| `product-manager` | `product-portfolio` veya `business-unit` | `TerritoryId` **null olabilir** | Portfolyo başına tek aktif primary (öneri) | İlgili portfolyo/BU'nun tamamını, tüm territory ağacında cross-territory roll-up ile görür |
| `business-unit-manager` | `business-unit` | `TerritoryId` null | BU başına tek aktif primary | BU tamamını görür |
| `hoc` | `model-wide` veya `all-business-scopes` | — | Exclusivity yok | Tüm business unit + tüm zone'lar. Full commercial visibility gerektirebilir → **policy-driven** (§23 follow-up) |
| `commercial-manager` | `territory-subtree` + business scope kısıtı | area/region + belirli business scope | (model, node, scope) başına tek aktif primary | Area Manager benzeri, ancak ticari scope kısıtlı → **policy-driven** |
| `operational-resource` / Production Admin | `business-scope` (`operational-scope` / `non-sales-resource-planning`) | `TerritoryId` opsiyonel | Exclusivity yok | D2: satış roll-up'ına girmez; resource planning/visibility için atanır |
| `admin` | `model-wide` | — | Exclusivity yok | Yönetim yetkisi; operasyonel coverage değil |
| `viewer` | `model-wide` veya `territory-subtree` | — | Exclusivity yok | Read-only; `IsPrimary` her zaman `false` |

**Ortak kurallar:**
- Tüm atamalar **efektif-tarihlidir**. Sonlandırma **hard delete değildir** → `Status=ended` + `ValidTo`.
- `CoverageScope` ↔ `TerritoryId` tutarlılığı zorunlu: `exact-territory` / `territory-subtree` → `TerritoryId`
  **zorunlu**; `business-unit` / `product-portfolio` / `business-scope` / `model-wide` / `all-business-scopes` →
  `TerritoryId` **null**.
- Manual/override atamalarda `ChangeReason` **zorunlu**.
- `IsPrimary=false` atamalar (yedek/vekil/ortak sorumluluk) exclusivity kurallarına **takılmayabilir**.
- HR/HCM olgunlaşana kadar `PersonRef` / `UserId` **seam** ile ilerlenir; MOD-0151 employee master tutmaz.
- Platform-level Territory data-scope **MOD-0018 follow-up**'tır (D4); v1'de MOD-0151 kendi coverage filter'ını kurar.
- FU04A sonrası current responsibility ve exclusivity eşleşme anahtarı deterministic normalize edilmiş
  `PositionCode` / `PositionRef`'tir; `RoleCode` değildir.

---

## 11. Account / Contact Coverage

### 11.1 Account coverage

- `AccountTerritoryAssignment` **MOD-0151'e aittir**; Account master **MOD-0149'a aittir**.
- `AccountId` / `AccountCode` **referanslanır, kopyalanmaz**. Account lokasyonu (Country/City/District/Lat/Lon)
  MOD-0149'dan gelen bir **girdidir**; MOD-0151 adres persist etmez.
- Bir account **aynı modelde** farklı boyutlarda (farklı business scope / product portfolio / channel) **birden fazla
  territory'ye** atanabilir.
- **Aynı model + aynı business scope + aynı level** içinde duplicate **active primary** assignment **yasaktır**.
- Manuel override için `ChangeReason` **zorunludur**.
- Olmayan / soft-deleted account'a atama → **400**.

### 11.2 Contact coverage

- **Direct `ContactTerritoryAssignment` v1'de YOKTUR.**
- Contact territory coverage **türetilir**: `Contact` → `AccountContactLink` (MOD-0150) → `Account` →
  `AccountTerritoryAssignment`.
- Contact master ve Contact↔Account link **MOD-0150'ye aittir**.
- MOD-0151 Contact'a `ZoneId` / `TerritoryId` alanı **eklemez**.
- MR, contact'ı **kapsadığı account üzerinden** görür. Contact birden çok account üzerinden birden çok territory'de
  görünebilir — bu bir çakışma değil, **birleşimdir** (union); ekranda "hangi account üzerinden" gösterilir.

### 11.3 MOD-0149 Account 360 entegrasyonu

- MOD-0149 §3.1'in bıraktığı **CoverageSummary placeholder'ı** MOD-0151 projection'ı ile doldurulur.
- MOD-0149 `Account` entity'sine `ZoneId` / `MicroZoneId` / `TerritoryId` **eklenmez** (mimari kural / pack ihlali).
- MOD-0151 yalnız **read-only coverage projection** sağlar (`GET /api/crm/accounts/{accountId}/territory-assignments`).

### 11.4 Visit / Route readiness input boundary (FU09A)

MOD-0155 Visit / Route Planning'in ihtiyaç duyduğu girdiler **dört ayrı sahiplikten** gelir; MOD-0151 bunların
yalnız **birincisini** sahiplenir:

| Girdi | Sahibi | MOD-0151'in rolü |
|---|---|---|
| Account current territory coverage · resource/MR responsibility | **MOD-0151** | **Sahip** — FU05/FU05A + FU04A/FU04B |
| Contact ↔ Account bağı (`AccountContactLink`), contact availability / working schedule / visit preference | **MOD-0150** | **Tüketici** (read-only); Contact master mutate edilmez |
| Visit frequency / call-cycle / campaign target policy | **MOD-0165 / MOD-0167** (üretici), **MOD-0155** (tüketici) | Yalnız **boundary** — territory coverage ile birleşme kuralını tanımlar |
| Last visit date · visit status · due/overdue hesabı | **MOD-0155** | **Yazmaz**; yalnız input sözleşmesini tanımlar |

Ayrıntı ve policy kararları §22.6'dadır. **MOD-0151 hiçbir koşulda** contact availability master'ı, frequency
policy master'ını veya visit history'yi sahiplenmez.

---

## 12. MicroZone

- MicroZone = `TerritoryNode(TerritoryLevel=microzone)`. **Ayrı aggregate değil · ayrı permission değil ·
  ayrı collection değil.**
- Parent olarak `zone` (veya rank kuralına göre geçerli bir üst seviye) alır. Bir zone içinde **N adet** microzone olabilir.
- MicroZone içindeki account gruplaması `AccountTerritoryAssignment` ile yapılır. **Clinic / hospital / pharmacy
  grouping**, microzone altındaki account assignment'lardır — yeni bir "grup" nesnesi tanımlanmaz.
- MR ataması `exact-territory` kapsamıyla **zone veya microzone** düzeyine yapılabilir.

**`MicroZoneProfile` (opsiyonel value object):** `AnchorAccountId?` · `ClusterNotes?` · `PlanningCenterType?`

**`AnchorAccountId` kararı:** merkez hospital / clinic / pharmacy olabilir. Anlamı **planning center / cluster
anchor**'dır. **Rota başlangıcı değildir.** Bugün hiçbir kural tetiklemez (metadata). Anchor account'un aynı
microzone'a atanmış olması **önerilir** (uyarı üretir), zorunlu değildir — planlama merkezi bazen komşu bir kurum olabilir.

**MOD-0151'de KESİNLİKLE yapılmayacaklar** (hepsi MOD-0155): rota optimizasyonu · mesafe hesabı · nearby pharmacy
search · visit sequencing · MR daily route · MicroTarget / visit cadence · "hastane doktoru → yakın eczane" rota önerisi.

---

## 13. Workflow / Approval

Blueprint `Dependency Gate = Customer 360; Workflow Designer` olduğu için MOD-0151 activation approval'ı **fake olamaz**.
Bu bölüm **FU06 approval-governed target state**'ini tanımlar. FU02B'nin §22.1'deki manual lifecycle geçişleri bu
workflow'u uygulamaz; FU06 açıldığında activation davranışı aşağıdaki gate ve immutable snapshot modeline yükseltilir.

### 13.1 Lifecycle

```
draft ──submit-approval──► review ──(MOD-0023 approve)──► approved ──activate──► active
  ▲                          │                                                     │
  └──(reject + reason)───────┘                             new draft version ──────┤
                                                                                   ▼
                                                               superseded ──► archived
```

- **draft:** serbest düzenlenebilir. Aktivasyon bloklu.
- **review:** change request açık, workflow instance çalışıyor; içerik **kilitli** (düzenleme → 409).
- **approved:** onay tamam; aktivasyon **mümkün ama otomatik değil** (ayrı izin + ayrı komut).
- **active:** Node/rule/account assignment doğrudan mutate **edilemez** → yeni draft sürüm (`BasedOnModelId`) veya
  `TerritoryChangeRequest`. Resource responsibility yalnız FU04A'nın history koruyan, auditli
  create/end/replace/transfer komutlarıyla değişebilir; kritik alan direct overwrite edilemez.
- **superseded / archived:** geçmiş olarak saklanır; sorgulanabilir, değiştirilemez.

### 13.2 MOD-0023 entegrasyonu (gerçek, sahte değil)

1. **Start Instance** — `POST /api/v1/workflow/instances`: `objectType="TerritoryModel"`, `objectId={modelId}`,
   `objectRef`, `templateCode`, `candidatePrincipalIds` (`user:` / `position:`), `idempotencyKey`.
   → `TerritoryChangeRequest.ApprovalWorkflowInstanceId`.
2. **Transition Gate** — `POST /api/v1/workflow/transitions/evaluate` (salt-okunur). MOD-0151, `activate` komutunu
   commit etmeden **önce** gate'e sorar: `Allowed / Blocked / NotApplicable`. Blocked → **422/409**.

**Kritik sınır:** iş kaydının gerçek state'ini **kaynak modül tutar**. `TerritoryModel.Status` **MOD-0151'e aittir**;
MOD-0023 yalnız geçiş kapısıdır. `ApprovalStatus` workflow'un **yansımasıdır**, kaynağı değildir.

**Workflow hazır/konfigüre değilse:** hiçbir sahte onay üretilmez. Model `review` / `approval-pending`'de kalır,
`activate` **fail-closed** reddedilir ("workflow template not configured"). Otomatik onay, **bypass flag'i** veya
"workflow yoksa geç" davranışı **yasaktır**.

### 13.3 `TerritoryChangeRequest` içeriği

change type · requested by · requested at · reason · before snapshot · after snapshot · workflow instance id ·
approval status · decision by · decision at · decision reason · evidence pack id · correlation id.

### 13.4 Aktivasyon = immutable snapshot

`activate` başarılı olduğunda modelin **tam snapshot'ı** (hiyerarşi + rule'lar + aktif atama sayıları + resource
atamaları + business scope'lar + çakışma durumu) dondurulur ve `ActiveSnapshotRef` ile `TerritoryEvidencePack`'e
bağlanır. Snapshot sonradan **yeniden hesaplanmaz**.

---

## 14. Evidence Pack

Blueprint soft page **ve** CRM-TERRITORY-BUNDLE bileşeni olduğu için Evidence Pack **zorunludur**.

**Sahiplik:** MOD-0151 Evidence Pack'i **kendisi üretir**. MOD-0031 Evidence Linking ileride geldiğinde evidence
**link/provenance** oraya bağlanabilir; ancak **territory evidence composition/export MOD-0151'de kalır**.

**İçerik:** model metadata · hierarchy snapshot · business scopes · planning period · assignment rules ·
account assignment counts · account assignment sample (gerekirse maskeli) · resource assignments · conflicts ·
approval trace · activation evidence · audit event references (MOD-0021) · generated by · generated at ·
correlation id · export version.

---

## 15. Performance Tracking Readiness

Blueprint outcome "performance tracking" der. MOD-0151 **forecast/visit KPI hesaplamaz**; roll-up ve read-model sağlar.

Hazırlanacak read-model / seam:
- account count by territory
- account count by level
- contact count derived from accounts
- active resource count by territory
- resource coverage by role
- **sales-scope vs non-sales-scope ayrımı** (D2 — Production Admin satış roll-up'ına girmez)
- business unit / product portfolio coverage
- active / historical assignment counts
- conflict count
- future lead / opportunity / order / visit roll-up **boyutları** (veri değil, boyut)

**Sınır:** MOD-0154 forecast/quota sahibidir — MOD-0151 yalnız territory boyutlarını sağlar. MOD-0155 visit/route/
MicroTarget sahibidir — MOD-0151 yalnız coverage API'lerini sağlar.

**FU09A notu:** coverage roll-up (`coverage-rollup`) hâlâ **FU09** kapsamındadır ve FU09A ile açılmamıştır. FU09A
yalnız **satır bazlı readiness** (account/contact/resource düzeyinde coverage + candidate) verir; agregat KPI,
performans skoru veya cadence compliance oranı **hesaplamaz** (§22.6).

---

## 16. Reference Data Proposal

> **Bu pack hiçbir reference set OLUŞTURMAZ.** Aşağıdaki liste MOD-0048 authoring template'i için **öneridir**
> (MOD-0149 / MOD-0150 precedent'i: pack → PREREQ authoring template → operator publish). Tüm değerler
> `lowercase-kebab`. **Hardcoded fallback yasaktır**; eksik required set → kontrollü 400.

| SetCode | Required? | Owner | Örnek değerler | Runtime gate | Activation gate | Metadata ihtiyacı |
|---|---|---|---|---|---|---|
| `territory-level` | **Required** | MOD-0048 · tenant-owned | division, country, region, area, zone, microzone | ✅ node create/update | ✅ | **`rank` / `sortOrder`** (§8 kuralı buna dayanır) |
| `territory-model-status` | **Required** | MOD-0048 · platform-owned | draft, review, approved, active, **inactive**, superseded, archived | ✅ model lifecycle | ✅ | — (kod lifecycle'ı ile eşleşmeli — **§22.1 VE §13.1**) |
| `territory-node-status` | **Required** | MOD-0048 · platform-owned | draft, active, inactive, ended, **archived** | ✅ node create + model lifecycle | ✅ | — |
| `territory-assignment-status` | **Required** | MOD-0048 · platform-owned | proposed, active, ended, rejected | ✅ atama | ✅ | — |
| `territory-assignment-source` | **Required** | MOD-0048 · platform-owned | rule, manual, import, override | ✅ atama | ✅ | — |
| `territory-resource-role` | **Compatibility-only / deprecated** | MOD-0048 · tenant-owned | legacy role kodları | ❌ yeni write contract | ❌ | Yalnız eski kayıt/migration → Position policy mapping; yeni kod hardcode etmez |
| `territory-resource-position-policy` | **FU04A target metadata; publish ayrı yetki** | MOD-0048 veya canonical Position directory metadata | position code/type policy kayıtları | ✅ policy resolve | ✅ resource transition | `positionCode`, `positionType`, `requiresTerritoryId`, `allowsTerritoryId`, `requiredNodeLevels`, `allowedNodeLevels`, `requiresBusinessScope`, `allowsBusinessScope`, `canBePrimary`, `allowsMultiNode`, `allowsCrossBusinessUnit` |
| `territory-rule-type` | **Required** | MOD-0048 · platform-owned | geography, account-list, account-type, product-portfolio, business-scope, channel, segment, manual, import | ✅ rule create | ⚠️ (rule varsa) | — |
| `territory-conflict-policy` | **Required** | MOD-0048 · platform-owned | block, warn, priority, manual-review | ✅ rule create | ⚠️ | — |
| `territory-coverage-scope` | **Required** | MOD-0048 · platform-owned | exact-territory, territory-subtree, business-unit, product-portfolio, business-scope, model-wide, all-business-scopes | ✅ resource assignment | ✅ | opsiyonel: `requiresTerritoryId` (bool) |
| `business-scope-type` | **Required** (business scope kullanılıyorsa) | MOD-0048 · platform-owned | business-unit, product-portfolio, brand-group, operational-scope, non-sales-resource-planning, channel, segment | ⚠️ koşullu | ⚠️ koşullu | **`isSalesScopeDefault`**, `includeInSalesPerformanceDefault` (D2) |
| `planning-period-type` | Optional | MOD-0048 · tenant-owned | annual, quarterly, monthly, custom | ❌ planning-only | ❌ | — |
| `product-portfolio` | **Optional / temporary** (D3) | MOD-0048 · tenant-owned → **Product master çıkınca devredilir** | tenant'a özgü (örn. alpha, beta, gamma) | ❌ | ❌ | `externalRef` |
| `brand-group` | **Optional / temporary** (D3) | MOD-0048 · tenant-owned → devredilecek | tenant'a özgü | ❌ | ❌ | `externalRef`, valid-dating |
| `territory-change-type` | Optional | MOD-0048 · platform-owned | create-model, update-hierarchy, update-assignment-rule, update-account-assignment, update-resource-assignment, activate-model, supersede-model | ❌ | ❌ | — |
| `commercial-position-scope-policy` | Optional / FU04A target metadata; publish ayrı yetki | MOD-0048 veya Position directory metadata | — | ⚠️ policy resolve | ⚠️ resource transition | HOC / Commercial Manager position scope davranışı; legacy role setine yeni bağımlılık eklemez |

**Aktivasyonu bloklayan setler:** `territory-level`, `territory-model-status`, `territory-node-status`,
`territory-assignment-status`, `territory-assignment-source`, `territory-coverage-scope` (+ business scope
kullanılıyorsa `business-scope-type`). FU04A resource transition varsa ayrıca doğrulanmış Position policy snapshot
veya compatibility policy mapping gerekir. Zorunlu girdilerden biri eksikse `activate` **fail-closed** olur (§20).

#### 16.1 Lifecycle status sözlüğü — FU02B / FU06 sahiplik ayrımı

> **Reconciliation 2026-07-28.** Bu iki set'in ilk authoring'i yalnız §13.1 (FU06 approval lifecycle) baz alınarak
> yapılmıştı; §22.1'de FU02B manual lifecycle'ı eklendiğinde sözlük güncellenmedi. Canlı smoke bunu ortaya çıkardı
> (`deactivate`/`archive` → fail-closed 400). Aşağıdaki tablo **her iki lifecycle'ın birleşimidir** ve iki set artık
> **7** ve **5** value içerir.

| Value | Set | Sahibi | Anlam | Terminal? | Geri dönülebilir? |
|---|---|---|---|---|---|
| `draft` | model + node | FU01/FU02B | Hazırlanan kayıt; tek editlenebilir durum | Hayır | — |
| `review` | model | **FU06** | Change request açık, içerik kilitli | Hayır | reject ile draft |
| `approved` | model | **FU06** | Onay tamam, aktivasyon mümkün | Hayır | — |
| `active` | model + node | FU02B | Operasyonel kullanımda | Hayır | Evet |
| **`inactive`** | model + node | **FU02B** | Geçici olarak kullanım dışı | **Hayır** | **Evet — `inactive → active`** |
| `superseded` | model | **FU06** | Yeni bir versiyon tarafından ikame edildi | **Evet** | Hayır |
| `ended` | node | FU06 / assignment | Tarihsel olarak sonlandırıldı | Evet | Hayır |
| **`archived`** | model + node | **FU02B** | Arşivlenmiş, read-only geçmiş kayıt | Evet | Hayır |

**`inactive` ≠ `superseded`:** §22.1 `inactive → active` geçişine izin verir; `superseded` ise `isTerminal=true`
olduğu için FU06 versiyonlama semantiğini taşır. Deactivate'i `superseded`'e bağlamak hem geri dönüşü kırar hem de
FU06'nın ikame semantiğini bozar.

**`archived` ≠ `ended`:** `ended` node/assignment tarihsel sonlandırmasıdır ve FU06/atama katmanına aittir;
`archived` ise model lifecycle'ının node'lara yansıttığı read-only arşiv durumudur. İkisini birleştirmek FU05/FU06'da
`ended`'i kullanılamaz hâle getirir.

`review`, `approved` ve `superseded` **FU06'ya aittir**; FU02B bu değerleri hiçbir zaman yazmaz. FU02B yalnız
`draft`, `active`, `inactive`, `archived` yazar ve `IsDeleted` ile soft-delete uygular (soft-delete bir status
değeri değildir).

#### 16.2 FU05 reference-data readiness

FU05 apply/history için `territory-assignment-status`, `territory-assignment-source` ve
`territory-conflict-policy` required'dır; business-scope doğrulaması mevcut model scope sözleşmesini kullanır.
`territory-coverage-scope` resource assignment için required kalır fakat account assignment apply payload'ının yeni
bir selector'ı değildir. FU02B canlı closeout'u bu setlerin target tenant'ta publish edildiğini doğrulamıştır.
Implementation yine de runtime'da fail-closed davranır: required set/value eksikse kontrollü 400 döner, hardcoded
fallback veya CRM-local seed kullanmaz. Eksik publish kod geliştirmeyi bloklamaz; ilgili canlı apply smoke adımını
bloklar ve MOD-0048 operator follow-up'ı gerektirir.

---

## 17. Permission Proposal

> **Bu pack hiçbir permission SEED ETMEZ** ve `crm-rbac-integration-plan.md`'yi **değiştirmez** (D7 → §23 follow-up).
> PKS-001: lowercase-dotted, ≥3 segment, her segment `^[a-z][a-z0-9-]*$`. Aşağıdaki anahtarların **tamamı PKS-001
> geçerlidir**. `view` yerine canonical `read` kullanılmıştır.

| Permission | Purpose | Seed now? | Notes |
|---|---|---|---|
| `crm.territory.read` | Territory yüzeyine genel okuma (menü/landing gate) | ❌ | Menü `<li>` guard'ı |
| `crm.territory.model.read` | Model listesi/detayı | ❌ | — |
| `crm.territory.model.manage` | Model create/update | ❌ | **Yalnız draft**; aktif modeli değiştiremez. Draft soft-delete gerekirse bunun altında |
| `crm.territory.model.activate` | Aktivasyon / supersede | ❌ | **Yüksek riskli**; Admin'e default verilmez |
| `crm.territory.node.read` | Hiyerarşi okuma | ❌ | Territory Model Viewer |
| `crm.territory.node.manage` | Node create/update (draft model) | ❌ | MicroZone dahil — **ayrı micro-zone izni yok** (D7) |
| `crm.territory.assignment.read` | Account atamaları + preview okuma | ❌ | — |
| `crm.territory.assignment.manage` | Rule upsert + account assignment apply/override | ❌ | Manuel override ayrıca `ChangeReason` ister |
| `crm.territory.resource.read` | Resource atamaları okuma | ❌ | — |
| `crm.territory.resource.manage` | Resource atama/sonlandırma | ❌ | **Yüksek riskli** (saha yetkisi değiştirir) |
| `crm.territory.approval.read` | Change Approval Trace okuma | ❌ | Blueprint soft page |
| `crm.territory.approval.submit` | Change request oluştur / onaya gönder | ❌ | Onay **verme** MOD-0023 `tasks.approve` iznidir, burada değil |
| `crm.territory.evidence.export` | Evidence pack üret/indir | ❌ | Blueprint bundle bileşeni |
| `crm.territory.import` | XLSX import (dry-run + apply) | ❌ | MOD-0150 deseni |
| `crm.territory.export` | XLSX export | ❌ | — |
| `crm.territory.commercial-scope.read` | *(koşullu öneri)* Commercial Manager ticari scope görünürlüğü | ❌ | Yalnız §10 policy-driven davranış gerekirse |
| `crm.territory.operational-scope.read` | *(koşullu öneri)* non-sales / operational scope görünürlüğü (D2) | ❌ | Yalnız gerekirse |

**FU05 geçici permission kararı:** `crm.territory.assignment.read` ve
`crm.territory.assignment.manage` canonical hedef anahtarlardır. Bu anahtarlar RBAC kataloğunda/grant'lerinde henüz
yoksa FU05 implementation permission seed/grant değiştirmez; ayrı
**`MOD-0151 FU05-RBAC — Assignment RBAC Permission Catalog Alignment`** follow-up'ı açılır. *(Not: bu follow-up FU04A-RBAC
deseniyle hizalı olarak `FU05-RBAC` etiketiyle izlenir; `FU05A` etiketi artık §22.2a CoverageSummary Model Lifecycle
Guard scope'una aittir.)* FU05 bu hizalama tamamlanana
kadar mevcut `crm.territory.model.read` (query/UI) ve `crm.territory.model.manage` (apply/end/override) ile geçici
fallback kullanabilir. Fallback yalnız FU05 endpoint'leri içindir; permission kataloğuna yeni literal eklemez ve
`crm.territory.delete` / `crm.micro-zone.manage` anahtarlarını hiçbir koşulda açmaz.

**FU04A permission kararı:** canonical hedefler `crm.territory.resource.read` ve
`crm.territory.resource.manage` anahtarlarıdır. Katalog/grant hazır değilse FU04A implementation seed/grant
değiştirmez; ayrı **`MOD-0151 FU04A-RBAC — Resource Assignment Permission Catalog Alignment`** follow-up'ı açılır.
Geçici olarak query/UI için `crm.territory.model.read`, create/end/replace/transfer için
`crm.territory.model.manage` kullanılabilir. Bu fallback yalnız FU04A endpoint'leriyle sınırlıdır; yeni permission
literal'i seed etmez ve `crm.territory.delete` / `crm.micro-zone.manage` açmaz.

**FU04B permission kararı:** FU04B **yalnız read**'dir; **yeni permission anahtarı önerilmez ve eklenmez**.
Üç query endpoint'i ve Plan vs Current tab'ı canonical `crm.territory.resource.read` ile korunur; katalog/grant hazır
değilse FU04A ile aynı geçici fallback (`crm.territory.model.read`) kullanılır. FU04B hiçbir `*.manage` anahtarı
talep etmez — plan baseline yazımı ayrı bir endpoint değil, mevcut activation komutunun (`model.activate` /
FU02B fallback `model.manage`) içindeki bir yan etkidir ve ek yetki gerektirmez.

**FU08 permission kararı:** canonical hedefler `crm.territory.export` (export + template) ve `crm.territory.import`
(dry-run + apply) anahtarlarıdır. Katalog/grant hazır değilse FU08 implementation seed/grant **değiştirmez**; ayrı
**`MOD-0151 FU08-RBAC — Import/Export Permission Catalog Alignment`** follow-up'ı açılır ve geçici olarak export/template
için `crm.territory.model.read`, dry-run/apply için `crm.territory.model.manage` fallback'i kullanılır. Fallback yalnız
FU08 endpoint'leriyle sınırlıdır, yeni permission literal'i seed etmez ve `crm.territory.delete` /
`crm.micro-zone.manage` açmaz. Fallback **yetki genişletmez**: dosya account veya resource sheet'i içeriyorsa ilgili
FU05 / FU04A guard'ları yine çalışır (§22.5).

**FU09A permission kararı:** FU09A **yalnız read**'dir; **yeni permission anahtarı önerilmez ve eklenmez** (FU04B
deseniyle aynı). Coverage/candidate readiness endpoint'leri canonical `crm.territory.assignment.read` (account/contact
coverage, route candidate) ve `crm.territory.resource.read` (resource/MR responsibility readiness) ile korunur;
katalog/grant hazır değilse FU05-RBAC / FU04A-RBAC ile aynı geçici `crm.territory.model.read` fallback'i kullanılır.
FU09A hiçbir `*.manage` anahtarı talep etmez, RBAC seed/grant değiştirmez ve `crm.territory.delete` /
`crm.micro-zone.manage` açmaz. Contact readiness satırları için **ek bir Contact permission'ı MOD-0151'de
tanımlanmaz** — contact görünürlüğü MOD-0150'nin kendi permission yüzeyine tabidir ve readiness cevabı Contact master
alanı taşımaz (yalnız `ContactId` / display name / link referansı).

**Önerilmeyen / kaçınılan anahtarlar:**

| Anahtar | Neden önerilmiyor |
|---|---|
| `crm.micro-zone.manage` | **Supersede (D7)** — MicroZone ayrı nesne değil, `TerritoryNode(level=microzone)`. `crm.territory.node.manage` yeterlidir |
| `crm.territory.delete` | Aktif model/atama **asla hard-delete edilmez** (archive/supersede/end). Draft soft-delete `model.manage` altında kalır |
| `crm.territory.create` / `crm.territory.update` / `crm.territory.assign-rep` / `crm.territory.assign-account` | Eski RBAC planı anahtarları; model/node/assignment/resource/approval/evidence ayrımı ile **supersede** edildi (D7) |

---

## 18. UI Surfaces

Golden Reference **Compact** (MOD-0149 / MOD-0150 ile aynı) · **offcanvas/quickview create-edit yasak** ·
**Gateway-only** (tarayıcı 5061'e asla gitmez) · **fake UI / mock data yasak** (backend hazır olmadan sayfa açılmaz) ·
**7 dil `.resx`** + `window.L10n` bridge · DataTable v2 kontratı.

### Blueprint-zorunlu 3 sayfa

**1. Territory Model Viewer** — hierarchy tree (division / country / region / area / zone / microzone) · level badge'leri ·
seçili node detayı · **assigned account count** · **assigned resource count** · business scope · **sales vs non-sales
scope** göstergesi (D2) · microzone ise anchor account · **active ↔ draft karşılaştırma** · **conflict indicators**.

**2. Change Approval Trace** — change request listesi (DataTable v2) · requested by / at · reason ·
**before / after diff** · workflow status · decision history · activation trace · correlation id.

**3. Evidence Pack** — model snapshot · hierarchy snapshot · business scopes · assignment rules ·
account assignment counts · resource assignments · conflicts · approvals · activation evidence · export timestamp ·
correlation id · Export aksiyonu (`crm.territory.evidence.export`).

### Ek yüzeyler

| # | Sayfa | Not |
|---|---|---|
| 4 | Territory Models List | DataTable v2; scope/status/period filtreleri; "create draft from…" |
| 5 | Node Detail / Edit | Compact tam sayfa; **yalnız draft modelde editable** |
| 6 | Assignment Preview | Kural çalıştır → sonuç tablosu (account, hedef territory, kaynak kural, çakışma); Apply ayrı izin |
| 7 | Account Assignment | Mevcut atamalar + manuel taşıma (gerekçe zorunlu) + geçmiş (`ended` kayıtlar görünür) |
| 8 | Resource Assignment | Rol / kapsam / dönem / primary matrisi; exclusivity ihlali inline uyarı |
| 9 | Business Scope view | Gerekirse: scope listesi, sales vs non-sales ayrımı |
| 10 | Import / Export | **FU08** (§22.5); MOD-0150 deseni: XLSX export → çok-sheet template indir → dosya yükle → **dry-run** (satır bazlı, blocking/non-blocking severity) → apply onayı → read-only **Import Run History** listesi. Dry-run **hiçbir şey yazmaz**; CoverageSummary ve Plan vs Current yalnız export'tur; resource assignment v1'de apply içermez |
| 11 | **Plan vs Current** (Resource Assignments sayfasında **read-only compact pill tab/section**) | **FU04B**; **yeni bağımsız sayfa/menü değildir.** Resource Assignments ile aynı sayfada yer alır; Territory Model Details yalnız hierarchy viewer olarak kalır. Kolonlar: Planned Resource · Current Resource · Change Type · Position · Business Unit · Territory Node · Effective Date · Reason · Replacement/Transfer link'leri. Golden Compact DataTable v2 toolbar, inline filter ve shared personalization Save View kontratını uygular. Hiçbir aksiyon butonu (create/end/replace/transfer/apply) içermez. Global "Resource Change Monitor" **future follow-up**'tır ve bu pack'te yetkilendirilmemiştir |

**FU09A notu (§22.6):** FU09A **yeni sayfa, yeni menü öğesi veya yeni ekran açmaz**. Visit/Route readiness bir
**API / read-model** yüzeyidir ve tüketicisi MOD-0155'tir. Territory tarafında görsel bir "route candidate" ekranı
açmak MOD-0155 sahipliğine girer ve bu pack'te yetkilendirilmemiştir; ihtiyaç doğarsa mevcut Account Assignment /
Resource Assignment sayfalarına **read-only** readiness rozeti eklenmesi ayrı bir follow-up olarak değerlendirilir.

**Menü:** interim olarak `_LayoutTenantShell.cshtml` içinde `crm.territory.read` guard'lı `<li>` (MOD-0149/0150
paritesi); page descriptor MOD-0285 nav migration'a kadar `IsNavigationVisible=false`.
**Bu pack layout dosyasını değiştirmez.**

---

## 19. API / CQRS Proposal

> **Öneri; implement edilmez.** `ocelot.json` **protected** — route implementation ileride `integration-agent`
> kapsamındadır. Mevcut konvansiyon: `/api/crm/accounts`, `/api/crm/contacts`.

| Endpoint | Command / Query | Permission | Notes |
|---|---|---|---|
| `GET /api/crm/territory-management/contract` | `GetTerritoryContractQuery` | `crm.territory.read` | Bundle sürümü, gerekli reference set'ler, workflow readiness (MOD-0149/0150 contract paritesi) |
| `GET /api/crm/territory-models` | `GetTerritoryModelListQuery` | `crm.territory.model.read` | DataTable v2; scope/status/period filtresi |
| `POST /api/crm/territory-models` | `CreateTerritoryModelCommand` | `crm.territory.model.manage` | Draft; `BasedOnModelId` ile klonlama |
| `GET /api/crm/territory-models/{id}` | `GetTerritoryModelByIdQuery` | `crm.territory.model.read` | Cross-tenant → 404 |
| `PUT /api/crm/territory-models/{id}` | `UpdateTerritoryModelCommand` | `crm.territory.model.manage` | **Yalnız draft**; active → 409 |
| `POST /api/crm/territory-models/{id}/nodes` | `CreateTerritoryNodeCommand` | `crm.territory.node.manage` | Cycle + level-rank + tarih validasyonu |
| `PUT /api/crm/territory-models/{id}/nodes/{nodeId}` | `UpdateTerritoryNodeCommand` | `crm.territory.node.manage` | Yalnız draft |
| `GET /api/crm/territory-models/{id}/nodes` | `GetTerritoryHierarchyQuery` | `crm.territory.node.read` | Model Viewer ağacı |
| `POST /api/crm/territory-models/{id}/rules` | `UpsertTerritoryAssignmentRuleCommand` | `crm.territory.assignment.manage` | Priority + conflict policy |
| `POST /api/crm/territory-models/{id}/preview-assignments` | `PreviewTerritoryAssignmentsCommand` | `crm.territory.assignment.read` | **Yan etkisiz** — hiçbir şey yazmaz |
| `POST /api/crm/territory-models/{id}/account-assignments/apply` | `ApplyAccountTerritoryAssignmentsCommand` | `crm.territory.assignment.manage` | Efektif-tarihli; eski atama `ended` (silinmez); manual/override **ezilmez** |
| `POST /api/crm/territory-models/{id}/resource-assignments` | `UpsertTerritoryResourceAssignmentCommand` | `crm.territory.resource.manage` | Exclusivity validasyonu |
| `GET /api/crm/territory-models/{modelId}/resource-assignment-plan-snapshot` | `GetTerritoryResourceAssignmentPlanSnapshotQuery` | `crm.territory.resource.read` | **FU04B**; immutable activation baseline. Snapshot yoksa kontrollü boş/`notCaptured` response (404 değil) |
| `GET /api/crm/territory-models/{modelId}/resource-assignment-plan-vs-current` | `GetTerritoryResourceAssignmentPlanVsCurrentQuery` | `crm.territory.resource.read` | **FU04B**; read-time diff. Filtreler: `effectiveAt`, `territoryNodeId`, `businessUnit`, `positionCode`, `resourceId`, `diffType` |
| `GET /api/crm/resources/{resourceId}/resource-assignment-plan-vs-current` | `GetResourcePlanVsCurrentQuery` | `crm.territory.resource.read` | **FU04B**; kişi bazlı bakış ("Ayşe planned nerede, current nerede") — model-cross okuma |
| `POST /api/crm/territory-models/{id}/submit-approval` | `SubmitTerritoryChangeRequestCommand` | `crm.territory.approval.submit` | MOD-0023 Start Instance; `idempotencyKey` zorunlu |
| `POST /api/crm/territory-models/{id}/activate` | `ActivateTerritoryModelCommand` | `crm.territory.model.activate` | **FU06 target state:** Transition Gate + conflict + reference + approval kontrolü — hepsi fail-closed. **FU02B yeni permission açmaz; model manage kullanır.** |
| `GET /api/crm/territory-models/{id}/approval-trace` | `GetTerritoryApprovalTraceQuery` | `crm.territory.approval.read` | Change Approval Trace |
| `GET /api/crm/territory-models/{id}/evidence-pack` | `GetTerritoryEvidencePackQuery` | `crm.territory.evidence.export` | JSON + export |
| `GET /api/crm/accounts/{accountId}/territory-assignments` | `GetAccountTerritoryAssignmentsQuery` | `crm.territory.assignment.read` | **MOD-0149 CoverageSummary kontratı** |
| `GET /api/crm/contacts/{contactId}/territory-coverage` | `GetContactTerritoryCoverageQuery` | `crm.territory.assignment.read` | **Derived** (FU09A, §22.6); `AccountContactLink → Account → current coverage`; contact'a alan eklenmez; çok-account'lu contact **çoklu satır** döner |
| `GET /api/crm/territory-models/{id}/coverage-rollup` | `GetTerritoryCoverageRollupQuery` | `crm.territory.model.read` | §15 performance readiness (FU09) |
| `GET /api/crm/territory-models/{id}/export` | `ExportTerritoryModelQuery` | `crm.territory.export` | FU08 (§22.5); XLSX, çok-sheet, read-only |
| `GET /api/crm/territory-models/import-template` | `GetTerritoryImportTemplateQuery` | `crm.territory.export` | FU08; doldurulabilir XLSX şablon |
| `POST /api/crm/territory-models/{id}/import-file?dryRun=true` | `DryRunTerritoryImportCommand` | `crm.territory.import` | FU08; **varsayılan `dryRun=true`** — kazara yazma mümkün değil; hiçbir şey persist etmez |
| `POST /api/crm/territory-models/{id}/import-file/apply` | `ApplyTerritoryImportCommand` | `crm.territory.import` | FU08; **ayrı rota** (yıkıcı çağrı önizleme isteğiyle tetiklenemez); sheet-level all-or-nothing; account sheet'i FU05 guard'larından geçer |
| `GET /api/crm/territory-models/{id}/import-runs` | `GetTerritoryImportRunsQuery` | `crm.territory.import` | FU08; read-only, append-only run history |
| `GET /api/crm/accounts/{accountId}/coverage-readiness` | `GetAccountCoverageReadinessQuery` | `crm.territory.assignment.read` | **FU09A** (§22.6); current coverage + sorumlu resource + reason code'lar; `effectiveAt` destekli; **read-only** |
| `GET /api/crm/territory-models/{id}/nodes/{nodeId}/coverage-accounts` | `GetTerritoryNodeCoverageAccountsQuery` | `crm.territory.assignment.read` | **FU09A**; node (microzone dahil) altındaki current account listesi; BU scope + `effectiveAt` filtresi |
| `GET /api/crm/resources/{resourceId}/coverage-readiness` | `GetResourceCoverageReadinessQuery` | `crm.territory.resource.read` | **FU09A**; "benim account'larım / benim doktorlarım" seam'i; current responsibility (FU04A) + current coverage (FU05A); replacement/transfer sonrası **current sahip** döner |
| `GET /api/crm/territory-models/{id}/route-candidates` | `GetTerritoryRouteCandidatesQuery` | `crm.territory.assignment.read` | **FU09A**; §7.12 readiness projeksiyonu. **Rota değildir** — sıra/mesafe/gün planı yok; availability/frequency/last-visit girdisi yoksa `unknown` + reason code |

---

## 20. Validation Rules

| Rule | Severity | Why |
|---|---|---|
| Hiyerarşide cycle yasak | **Block 400** | MOD-0149 cycle guard precedent'i; sonsuz döngü / roll-up bozulması |
| `TerritoryCode` model içinde unique | **Block 409** | İnsan-okunur kimlik; import/export eşleşmesi |
| Level sequence geçerli (child rank > parent rank; atlamak serbest, geri gitmek yasak) | **Block 400** | §8 |
| Tenant + scope + period başına **tek active model** | **Block 409** | İki aktif model = belirsiz coverage |
| `ValidFrom <= ValidTo` (tüm entity'ler) | **Block 400** | Tarih tutarlılığı |
| Child node tarihleri parent/model aralığı içinde | **Block 400** | Parent bittiğinde child yaşayamaz |
| Assignment tarihleri model aralığı içinde | **Block 400** | Model dışı atama izlenemez |
| Aynı model + business scope + level'da duplicate **active primary** account assignment | **Block 409** | Çift sahiplik = çift sayım |
| MR exclusivity: örtüşen dönemde **farklı business scope'larda** aktif primary MR | **Block 409** (override + reason ile geçilebilir) | §10 |
| Aynı BU içinde MR'ın çoklu zone ataması | **Warn** | İzinli; workload/coverage riski |
| `CoverageScope` ↔ `TerritoryId` tutarlılığı | **Block 400** | Anlamsız atama önlenir |
| `AnchorAccountId` yalnız `level=microzone` | **Block 400** | Value object koşulu |
| Anchor account aynı microzone'a atanmamış | **Warn** | Meşru istisna olabilir |
| Manual / override assignment `ChangeReason`sız | **Block 400** | "Controlled changes" Blueprint çıktısı |
| Çözülmemiş conflict varken activation | **Block 422** | Bozuk model canlıya çıkamaz |
| Zorunlu MOD-0048 reference set eksikken activation | **Block 422/400** | Fail-closed; hardcoded fallback yasak |
| FU06 approval-governed activation'da workflow approval eksik (Blocked / NotApplicable / instance yok) | **Block 409/422** | Sahte onay yasağı; FU02B manual activation'a uygulanmaz |
| Active model üzerinde doğrudan node/rule/assignment overwrite | **Block 409** | Node/rule immutable; resource için yalnız FU04A'nın auditli create/end/replace/transfer komutları istisnadır |
| Destructive update yerine supersede/archive | **Block** | Geçmiş veri koruması |
| Hard delete (aktif model / atama) | **Block 403/409** | §17 delete permission yok |
| Soft-delete yalnız `draft` model/node üzerinde | **Block 409** | Inactive/expired/archived dahil diğer status'lar silinemez |
| Olmayan / soft-deleted Account'a atama | **Block 400** | MOD-0149 SoR bütünlüğü |
| Cross-tenant ID erişimi | **404** | Platform standardı (metadata sızıntısı yok) |
| `TenantId` payload'dan | **Yok sayılır / 400** | TenantId **yalnız JWT'den** server-side |

---

## 21. Integration Boundaries

| Module | Relationship | MOD-0151 must NOT own |
|---|---|---|
| **MOD-0149** Customer 360 | Hard consume: AccountId/AccountCode referansı, account lookup/arama, geo/adres **girdisi**. **Provide:** `CoverageSummary` read-only projection | Account/WorkPlace master, AccountCode üretimi, account hiyerarşisi, adres/geo persistence. **Account'a `ZoneId`/`MicroZoneId`/`TerritoryId` eklenemez** |
| **MOD-0150** Contact & Relationship | Consume: `AccountContactLink` üzerinden **derived** contact coverage; **FU09A**: `AccountContactLink` bazlı contact availability / visit preference verisinin **read-only** tüketimi (§22.6) | Contact master, Contact↔Account link, Account↔Account relationship, consent, **`ContactAvailability` / `VisitPreference` master**. **Contact'a `TerritoryId` eklenemez**; `ContactTerritoryAssignment` **yoktur** |
| **MOD-0048** Reference Data | Consume-only: `published-values?scope_key={tenant}`; eksik required set → kontrollü 400 | Reference set/value tanımı, CRM-local seed, **hardcoded fallback** |
| **MOD-0018** / AuthService | Consume-only: `[HasPermission("crm.territory.*")]`, JWT claim'leri. **v1'de MOD-0151 kendi CrmService coverage filter'ını kurar**; platform Territory data-scope **follow-up** (D4) | RBAC/permission engine, rol tanımı, permission seed, `EntitlementDataScopeKind` enum'u, platform data-scope engine |
| **MOD-0023** Workflow Designer | Consume: Start Instance + Transition Gate. `TerritoryModel.Status` MOD-0151'de kalır. **Fake approval yasak** | Onay motoru, task/SLA/escalation, approval task state'i |
| **MOD-0021** Audit Trail | Consume: audit event append (model create/activate, atama değişimi, override, evidence export) | Audit store, retention, redaction |
| **MOD-0031** Evidence Linking | **Future seam.** MOD-0151 şimdilik territory evidence **composition + export**'unu sahiplenir (Blueprint bundle gereği); MOD-0031 gelince link/provenance oraya bağlanır | Genel evidence object store, provenance, cross-module evidence linking |
| **MOD-0288** Organization / Person / Position | Consume: `PersonRef` / `PositionRef` / `OrgUnit`; olası BusinessUnit master (unitType follow-up) | Employee/person master, position master, org unit master, reporting chain |
| **MDM / Product (future)** | Seam/reference: `product-portfolio` / `brand-group` / `BrandCode` (D3) | Product / Brand / SKU master, portfolio↔brand kalıcı mapping |
| **MOD-0154** Forecasting & Quotas | **Provide:** territory roll-up boyutları + coverage read-model'leri | Quota, forecast, hedef hesaplama, quota approval |
| **MOD-0155** Field Sales / Visit Planning | **Provide (FU09A, read-only):** microzone/node account listesi, coverage readiness, resource responsibility readiness, derived contact coverage, route **candidate** readiness + reason code'lar (§22.6) | Visit plan, visit, MicroTarget, rota/route optimizasyonu, mesafe, günlük plan, cadence/frequency **engine**, daywork, check-in/out & GPS, visit report, **last visit / visit history / due-overdue engine** |
| **MOD-0165** Campaign / **MOD-0167** Segmentation | **FU09A boundary:** `VisitFrequencyPolicy` / `CallCyclePolicy` **üreticisi** buradadır; MOD-0151 yalnız policy ↔ territory coverage eşleşme anahtarını tanımlar (§22.6) | Campaign/cycle period execution, segment tanımı ve değerlendirmesi, **frequency policy master ve hesaplaması** |
| **MOD-0167** Segmentation | Consume: segment kodunu kriter olarak | Segment tanımı / değerlendirmesi |
| **MOD-0285** Navigation | Consume: page descriptor / menü (interim static `<li>`) | Navigation loader/engine |

---

## 22. FU Breakdown

### 22.1 FU02B — Lifecycle Activation, Computed Expiry and Draft Soft Delete

FU02B, `TerritoryModel.Status` ve `TerritoryNode.Status` alanlarını workflow approval kapsamına girmeden operasyonel
hale getiren kontrollü ara FU'dur. **FU02B runtime code allowed**; FU06'yı iptal etmez veya onun approval sahipliğini
devralmaz.

**Allowed runtime scope:** model lifecycle endpoint'leri; node lifecycle guard'ları; computed expiry read davranışı;
draft soft-delete; deactivate/archive davranışı; single-active-model guard; lifecycle action visibility; audit
event'leri; testler ve evidence report.

**Explicitly out of scope:** full workflow approval; MOD-0023 entegrasyonu; submit/approve/reject; workflow transition
gate; approval trace; evidence pack; assignment rule/preview/apply; resource assignment; import/export; Brand Scope;
Product/Brand master; background scheduler; hard delete; active record delete; RBAC seed/grant değişiklikleri.

#### Lifecycle state machine

| Entity | Allowed transition | Guard / behavior |
|---|---|---|
| Model | `draft → active` | Manual FU02B activation; single-active-model guard zorunlu |
| Model | `active → inactive` | Manual deactivation; uygun active node'lar model ile inactive olur |
| Model | `inactive → active` | Single-active-model guard yeniden çalışır |
| Model | `inactive → archived` | Archived kayıt read-only'dir |
| Model | `computed-expired → archived` | Stored status değişmeden computed expiry üzerinden archive edilebilir |
| Model | `draft → soft-deleted` | Yalnız soft-delete; default listelerden gizlenir, audit/history korunur |
| Node | `draft → active` | Yalnız model activation ile; model active değilken tek başına active olamaz |
| Node | `draft → soft-deleted` | Yalnız draft node; hard delete yok |
| Node | `active → inactive` | Model lifecycle ile |
| Node | `inactive/computed-expired → archived` | Model lifecycle ile; archived read-only |

Model ve node create sırasında stored status `draft` olur. Active model doğrudan archive edilemez; önce inactive
veya computed-expired olmalıdır. Active/inactive/expired/archived model veya node silinemez. Archived kayıt
editlenemez; operasyonel listelerde varsayılan olarak gizlenebilir ve açık filtreyle gösterilebilir.

#### Computed expiry v1

`EffectiveTo` geçmişse DB status otomatik mutate edilmez ve bu FU background scheduler çalıştırmaz. API/UI read model
stored status'u koruyarak `isExpired=true` ve/veya `computedStatus=expired` üretir; UI expired badge gösterir.
Stored status `draft` olan ve tarihi geçmiş kayıt draft kalır, lifecycle action'ı yerine expiry warning gösterir.
Materialized expiry ve scheduler ayrı future hardening'dir.

#### Single active model guard

Aynı **tenant + normalized `CountryScope` + normalized, sırasız `BusinessUnitScope` seti + çakışan effective date
window** için birden fazla active `TerritoryModel` olamaz; ihlal **409** döner. Normalizasyon trim/case normalization,
BU setinde duplicate temizliği ve sıra-bağımsız set karşılaştırması içerir. Bu guard FU02A `BusinessScopes`
persistence sözleşmesine bağlıdır. FU02A tamamlanmadan yalnız CountryScope/legacy DivisionScope ile kontrol eksik
kalır ve FU02B implementation verdict'i **PARTIAL** olur.

#### Contract flags

FU02B sonrası contract capability önerisi:

```json
{
  "supportsLifecycleActions": true,
  "supportsComputedExpiry": true,
  "supportsDraftSoftDelete": true,
  "supportsWorkflowActivation": false,
  "supportsApprovalTrace": false
}
```

`supportsWorkflowActivation=false` kalır. FU02B yalnız mevcut `crm.territory.model.read`,
`crm.territory.model.manage`, `crm.territory.node.read`, `crm.territory.node.manage` permission'larını kullanır.
`crm.territory.delete` ve `crm.micro-zone.manage` eklenmez; seed/grant değişikliği bu FU'nun dışındadır.

#### Lifecycle audit expectations

Event adları: `territory.model.activated`, `territory.model.deactivated`, `territory.model.archived`,
`territory.model.soft_deleted`, `territory.node.soft_deleted`, `territory.model.activation_rejected`,
`territory.model.delete_rejected`.

Payload: `tenantId`, `modelId`, varsa `nodeId`, `previousStatus`, `newStatus`, varsa `computedStatus`, `actor`,
`reason`, `correlationId`, `timestamp`. Tenant kimliği request payload'ından değil server-side auth context'ten
alınır.

#### FU06 boundary

FU06 ayrı future scope olarak **workflow approval + approval-governed activation** sahibidir: MOD-0023 Start
Instance ve Transition Gate, submit for approval, approve/reject, `TerritoryChangeRequest`, approval trace,
evidence-backed activation, approval-based immutable lifecycle, before/after diff ve Change Approval Trace.
FU02B'nin manual lifecycle güvenliği FU06'nın workflow gate'ini veya immutable approved snapshot modelini
uygulamaz.

> **Runtime authorization update (2026-07-25):** FU01, FU02, FU02A ve FU02B frontmatter scope'unda açıktır.
> **FU05 authorization update (2026-07-28):** FU01–FU05 frontmatter scope'unda açıktır. FU06–FU09 yalnız ilgili
> ayrı onaylarla açılabilir; FU06 özellikle future workflow approval scope'udur.
> **FU04A authorization update (2026-07-30):** Position-based resource lifecycle hardening additive olarak açıktır;
> FU05 account assignment sınırı ve FU06–FU09 sahiplikleri değişmez.
> **FU05A authorization update (2026-07-31):** FU05 live smoke PASS sonrası bulunan CoverageSummary model-lifecycle
> guard boşluğu additive read-projection scope'u (`FU05A-coverage-summary-model-lifecycle-guard`) olarak açıktır (§22.2a).
> FU05A yalnız current coverage projection'ını model lifecycle status ile filtreler; assignment mutasyonu,
> Account/Contact mutasyonu, ContactTerritoryAssignment, workflow/approval açmaz ve `supportsWorkflowActivation=false`
> değerini korur. FU06–FU09 sahiplikleri değişmez.
> **FU08 authorization update (2026-08-01):** kontrollü XLSX import/export hardening'i additive runtime scope
> (`FU08-import-export-hardening`) olarak açıktır (§22.5). FU08 **FU06/FU07'den önce** yetkilendirilmiştir; §22 FU
> tablosundaki eski `Depends On: FU05, FU07` girdisi bu yetkilendirmeyle **FU05 + FU05A** olarak düzeltilmiştir —
> evidence pack (FU07) import/export'un **hard prerequisite'i değildir**, yalnız FU07 geldiğinde export yüzeyi evidence
> pack'e girdi olabilir. FU08 hiçbir mevcut guard'ı bypass etmez, `supportsWorkflowActivation=false` değerini korur ve
> FU06/FU07/FU09 sahipliklerini değiştirmez.
> **FU09A authorization update (2026-08-01):** MOD-0155 Visit / Route Planning öncesi hazırlık scope'u
> (`FU09A-visit-route-readiness-boundaries`) additive ve **yalnız-okuma** olarak açıktır (§22.6). FU09A coverage
> readiness, resource/MR responsibility readiness, derived contact coverage, route **candidate readiness** ve
> makine-okunur reason code sözleşmesini yetkilendirir; **rota, günlük route planı, visit planı, optimizasyon,
> campaign/frequency engine, GPS check-in/out, visit report ve visit history açılmamıştır** — hepsi MOD-0155'e aittir.
> FU09A hiçbir mutasyon, hiçbir yeni master aggregate ve hiçbir yeni permission literal'i açmaz; contact availability
> master'ı **MOD-0150**'ye, frequency / call-cycle policy master'ı **MOD-0165 / MOD-0167**'ye, last visit ve
> due/overdue engine'i **MOD-0155**'e bırakılmıştır (bu task yalnız **boundary**'yi yazar).
> **Dependency reconciliation:** §22 FU tablosunda FU09'un `Depends On` alanı `FU05, FU07` yazıyordu; readiness
> API'lerinin gerçek hard prerequisite'i **FU05 + FU05A + FU09A**'dır — evidence pack (FU07) readiness'in ön koşulu
> **değildir** (FU08 için 2026-08-01'de kaydedilen aynı gerekçe). Alan buna göre düzeltildi.
> `supportsWorkflowActivation=false` korunur; FU06/FU07/FU08 sahiplikleri değişmez.

### 22.2 FU05 — Account Assignment Apply + History

FU05, FU03'ün yan etkisiz preview sonucunu kullanıcı onayıyla ayrı, efektif-tarihli
`AccountTerritoryAssignment` kayıtlarına dönüştürür. Account ve Contact SoR aggregate'leri read-only dependency'dir;
atama geçmişi yalnız MOD-0151 collection/aggregate'inde tutulur.

**Allowed runtime scope:**

- `AccountTerritoryAssignment` aggregate/repository/index'leri; tenant isolation ve soft-delete tabanı.
- Preview run/selected account satırlarının apply edilmesi; account, model, node ve business-scope doğrulaması.
- Model-level ve account-level assignment history; assignment detail; `effectiveAt` current-assignment query.
- Conflict detection, controlled 409 ve reason zorunlu override.
- Önceki assignment'ı silmeden `EffectiveTo`/`EndedAt`/`EndedBy` ve ended status ile kapatma; yeni assignment'ı yeni
  kayıt olarak açma.
- Account master'a yazmayan ayrı `TerritoryCoverageSummary` query/read model/projection hazırlığı.
- Preview Apply paneli, assignment history listesi, conflict/override warning ve end/replace action'ı.
- Contract flag'leri: `supportsAccountAssignmentApply=true`, `supportsAssignmentHistory=true`,
  `supportsCoverageSummary=true`, `supportsResourceAssignments=true`, `supportsWorkflowActivation=false`.
- Backend/frontend testleri, Gateway-only smoke ve FU05 implementation evidence report.

**Apply/history policy decisions:**

1. v1 apply yalnız stored status'u `active` olan, soft-delete/arşiv/expiry guard'larını geçen modele yapılır. `draft`
   model planning/proposed apply almaz; böyle bir ihtiyaç ayrı governance kararıdır. `inactive`, `archived`,
   computed-expired veya soft-deleted model apply alamaz.
2. v1 batch davranışı **all-or-nothing**'dir. Seçili satırlardan biri doğrulama/conflict hatası verirse hiçbir
   assignment veya end-date yazılmaz.
3. Aynı tenant + account + model + kesişen business scope + örtüşen efektif tarih aralığındaki assignment default
   olarak controlled **409** üretir.
4. Override için non-empty reason zorunludur. Override eski kaydı hard-delete etmez; yeni başlangıçtan önce
   end-date/status/audit metadata ile kapatır ve yeni kaydı ayrı Id ile açar.
5. Eski, ended ve expired assignment'lar history query'lerinde görünür. Future assignment, `EffectiveFrom`
   gelmeden current kabul edilmez.
6. CoverageSummary Account master'a persist edilmez; ayrı query/projection'dır ve yalnız geçerli assignment'ı döner.

**Explicitly out of scope:** Account/Contact update; Account veya Contact entity'sine `TerritoryId`, `ZoneId` ya da
`MicroZoneId` ekleme; resource assignment mutation; workflow submit/approve/reject ve MOD-0023 entegrasyonu;
approval trace; evidence pack; import/export; visit/route planning veya readiness; Brand Scope; Product/Brand master;
hard delete; Mongo hand-edit; RBAC seed/grant; MOD-0048 publish; `crm.territory.delete`;
`crm.micro-zone.manage`; request payload'ında `TenantId`; direct port 5061 business API çağrısı.

**FU06 boundary:** FU05 assignment apply bir workflow approval değildir. FU06 ayrıca submit/approve/reject,
workflow trace, MOD-0023 integration ve gerekiyorsa approval-governed controlled activation getirir. FU05 bu
endpoint/flag'leri açmaz ve `supportsWorkflowActivation=false` değerini korur.

### 22.2a FU05A — CoverageSummary Model Lifecycle Guard

FU05 live smoke closeout (90/90 PASS,
[kanıt](../../../../docs/audits/mod-0151-fu05-account-assignment-apply-history-live-smoke-closeout-2026-07-31.md))
current coverage'ın **doğru** çalıştığını doğruladı; ancak CoverageSummary'nin bağlı **territory model'in lifecycle
status'unu** current projeksiyonda uygulamadığı bir boşluk kaydedildi. FU05A bu boşluğu additive, **yalnız-okuma**
bir guard ile kapatır: deactivated / inactive / archived / superseded bir modele bağlı `AccountTerritoryAssignment`
artık current coverage'ta görünmez, ama history'de korunur.

**Risk kapsamı (neden şimdi):** yanlış current coverage şu tüketicileri doğrudan etkiler — "bu account şu an hangi
territory'de?", "bu account'tan hangi MR sorumlu?", contact-derived territory coverage, FU09 Visit/Route Readiness
API, MOD-0155 Visit Planning ve MR'ın "benim account'larım / benim doktorlarım" listesi. Bu yüzden guard FU09'dan
**önce** kapanmalıdır.

**Allowed runtime scope:**

1. **CoverageSummary current guard.** CoverageSummary ve current account-territory coverage query'leri yalnız
   *operationally valid* model üzerinden current döner. Bir kayıt current sayılması için tüm şartlar sağlanmalı:
   model `active`; model effective-date window `effectiveAt`'i kapsar; model `archived`/`inactive`/`superseded`
   değil; `AccountTerritoryAssignment` active/open; assignment effective-date window `effectiveAt`'i kapsar; assignment
   soft-delete değil; assignment `ended` değil; tenant claim'den gelir; Account master mutate edilmez.
2. **Historical coverage ayrımı.** Ended / inactive / archived / superseded modele bağlı assignment'lar history
   query'lerinde görünmeye **devam eder**; yalnız current CoverageSummary projeksiyonundan düşer. History = geçmiş
   kayıtlar; Current CoverageSummary = yalnız active model + active assignment.
3. **`effectiveAt` davranışı.** CoverageSummary/coverage query `effectiveAt` destekliyorsa: geçmiş tarih sorulduğunda
   o tarihte active/effective olan model ve assignment dikkate alınır; bugün sorulduğunda yalnız bugün active olan
   model ve assignment dikkate alınır; deactivated/archived model bugün current görünmez.
4. **Deactivation/archive sonrası davranış.** Model deactivated/inactive/archived/superseded olduğunda current
   CoverageSummary o modele bağlı account assignment'larını current göstermez; history silinmez; assignment hard
   delete edilmez. Karar: **assignment status otomatik `ended` yapılmaz** — current projeksiyon model lifecycle guard
   ile filtrelenir, assignment tarihçesi olduğu gibi korunur.
5. **Account master boundary.** Account master'a `TerritoryId`/`ZoneId`/`MRId` yazılmaz; CoverageSummary ayrı
   read model/query olarak kalır.
6. **Contact-derived coverage readiness.** Contact için doğrudan `TerritoryAssignment` yapılmaz kararı korunur.
   Contact coverage ileride (FU09) `AccountContactLink → Account → current AccountTerritoryAssignment / CoverageSummary`
   üzerinden türetilecektir; bu nedenle FU05A model lifecycle guard'ı **contact-derived coverage için prerequisite**
   kabul edilir.

**Policy decisions:**

1. CoverageSummary current sayılması için territory model `active` olmalı mı? **Evet.**
2. Archived/inactive/superseded modele bağlı assignment'lar current coverage döner mi? **Hayır.**
3. Bu assignment'lar history'de görünür mü? **Evet.**
4. Model deactivation assignment'ları otomatik `ended` yapar mı? **Hayır** — bu FU05A'nın konusu değildir; current
   projeksiyon model lifecycle guard ile filtrelenir, tarihçe korunur.
5. `effectiveAt` geçmiş tarih sorgusunda ne olur? O tarihte active/effective olan model ve assignment dikkate alınır;
   model o tarihte active değilse current coverage dönmez.
6. Bu guard contact-derived coverage için prerequisite mi? **Evet** — FU09'dan önce kapanmalı.

**Contract flags (FU05A sonrası öneri):**

```json
{
  "supportsCoverageSummary": true,
  "supportsCoverageSummaryModelLifecycleGuard": true
}
```

Mevcut flag'ler korunur: `supportsAccountAssignmentApply`, `supportsAssignmentHistory`, `supportsCoverageSummary`,
`supportsResourceAssignmentPlanVsCurrent`. Özellikle `supportsWorkflowActivation=false` **kalır**; workflow
readiness/approval flag'i eklenmez.

**Explicitly out of scope:** workflow approval; controlled activation; ChangeRequest / Change Approval Trace;
MOD-0023 integration; lifecycle guard dışında `AccountTerritoryAssignment` apply davranışını değiştirmek; assignment
rule / preview davranışını değiştirmek; resource assignment davranışını değiştirmek; FU04A replacement/transfer
davranışını değiştirmek; FU04B Plan vs Current davranışını değiştirmek; Account master mutasyonu; Contact mutasyonu;
`ContactTerritoryAssignment` eklemek; evidence pack; import/export; visit/route planning implementation; Brand Scope;
Product/Brand master; hard delete; Mongo hand-edit; RBAC seed/grant (ayrıca yetkilendirilmedikçe); MOD-0048 publish
(ayrıca yetkilendirilmedikçe); `crm.territory.delete`; `crm.micro-zone.manage`.

### 22.2b FU05B — Versioned Draft Clone + Account Assignment Carry-forward

FU05B, active model immutability'yi bozmadan assignment rule değişikliğine izin veren canonical yolu tamamlar:
operational model doğrudan mutate edilmez; ondan yeni bir draft version üretilir, draft düzenlenir ve kontrollü
cutover ile aktive edilir.

**Allowed runtime scope:**

1. `POST /api/crm/territory-models/{sourceId}/clone-draft`: aynı tenant'taki active/inactive kaynak modelden yeni
   model metadata'sı, hierarchy node'ları ve assignment rule'ları yeni aggregate kimlikleriyle klonlanır.
2. Yeni model `draft`, `BasedOnModelId=sourceId`, `VersionNumber=source.VersionNumber+1` olur; code/name/effective
   window kullanıcı girdisidir. Node parent ve rule target-node referansları yeni node kimliklerine remap edilir.
3. Draft oluşturma sırasında `AccountTerritoryAssignment` kopyalanmaz ve current coverage değişmez.
4. Yeni draft activation'ında kaynak modelin open/effective account assignment'ları canonical `TerritoryCode`
   üzerinden yeni node'lara fail-closed remap edilir. Eksik/duplicate mapping, invalid window veya conflict tüm
   activation batch'ini 409 ile bloklar.
5. Başarılı cutover tek transaction sınırında yeni modeli/nodeları aktive eder, kaynak modeli `inactive` yapar,
   kaynak assignment'ları cutover anında `ended` yapar ve hedef model altında yeni active assignment kayıtları açar.
6. Yeni assignment `MigratedFromAssignmentId`, `MigratedFromModelId`, correlation/reason ve applied rule provenance
   taşır. Eski kayıt hard-delete edilmez; history query'lerinde kalır.
7. Account ve Contact SoR kayıtları mutate edilmez. Resource assignment carry-forward, workflow approval/FU06,
   ChangeRequest ve evidence pack bu FU'nun dışındadır.

**Acceptance criteria:**

- Active modelde rule create/update/delete hâlâ 409; UI bunun yerine `Create draft version` aksiyonunu sunar.
- Clone sonrası model, hierarchy ve rule sayıları kaynakla eşleşir; tüm yeni aggregate Id'leri farklıdır ve parent /
  target-node referansları yeni modele aittir.
- Kaynak account assignment'ları draft oluşturulduğunda current kalır; hedef draft altında operational assignment yoktur.
- Activation öncesi unmapped target node testi 409 üretir ve model/assignment state'inde kısmi yazım olmaz.
- Başarılı activation sonrası her taşınabilir kaynak assignment için bir ended source + bir active target bulunur;
  current CoverageSummary yalnız yeni modeli döndürür, history iki kaydı da gösterir.
- Cross-tenant source 404; duplicate code 409; permission ve Gateway-only kuralları korunur.

### 22.3 FU04A — Resource Assignment Lifecycle, Replacement and Operational Visibility Hardening

FU04A, tarihsel FU04 CRUD yüzeyini position-based, efektif-tarihli ve operasyonel olarak tüketilebilir resource
responsibility sözleşmesine yükseltir. FU04A kişi/resource atar; FU05'in Account assignment aggregate veya apply
akışına dokunmaz.

**Allowed runtime scope:**

- Draft model resource assignment'larının planning-only `proposed`; active model kayıtlarının operational `active`
  olarak ayrılması.
- Model `draft → active` geçişinde geçerli proposed resource assignment'ların auditli biçimde `active` yapılması.
- Active modelde reason + effective date ile resource assignment create ve end.
- Eski kaydı ended/effective-to ile kapatıp yeni kaydı ayrı Id ile açan atomik replacement.
- Source assignment'ı kapatıp target assignment'ı ayrı Id ile açan atomik transfer.
- Node-, resource/person-, position- ve model-level efektif-tarihli history query'leri.
- Active model + active assignment + effective-at + primary + BU scope + PositionRef filtresiyle current
  responsibility query/read contract'ı.
- Position-based exclusivity/override engine, activation preflight, audit/provenance ve controlled conflict response.
- Planned / Active / Ended badge'leri; planning-only uyarısı; End / Replace / Transfer; effective-date + reason
  modalı; current responsibility görünümü; conflict warning; history drawer/table; Position selector/PositionRef.
- Backend/frontend testleri, contract flag/limitation hizalaması, Gateway-only authenticated smoke ve implementation
  evidence report.

**Draft / active ve mutation policy kararları:**

1. Draft resource assignment yalnız `proposed` ve **planning-only**'dir; current responsibility, visit/route veya
   downstream operational coverage query'sinde dönmez.
2. Model activation, valid proposed kayıtları aynı kontrollü işlem içinde `active` yapar. Blocking conflict, eksik
   required position policy veya geçersiz effective window varsa activation **fail-closed** olur; hiçbir assignment
   kısmen active yapılmaz. Advisory warning tek başına activation'ı bloklamaz ve auditlenir.
3. Active modelde yeni resource assignment create ve assignment end; non-empty reason, effective date, tenant/model
   guard ve position policy validation ile izinlidir.
4. Active assignment'ın `ResourceRef`, `PositionRef`, TerritoryId, business scope, primary flag veya effective window
   gibi kritik alanları direct overwrite edilemez. Bu değişiklikler End / Replace / Transfer komutlarıyla yeni history
   kaydı üretir. Yalnız display snapshot/email gibi minor metadata düzeltmesi ayrı, auditli patch ile yapılabilir.
5. Archived, inactive, superseded, computed-expired veya soft-deleted model resource mutasyonu alamaz.
6. Hard delete yoktur. Ended kayıtlar history'de kalır; proposed soft-delete yalnız draft planning hatasını geri
   almak içindir ve audit metadata'sını korur.

**Replacement policy:**

- `oldAssignmentId`, `effectiveDate`, `replacementReason`, yeni `ResourceRef`, yeni `PositionRef` ve `correlationId`
  zorunlu girdilerdir.
- Eski kayıt silinmez; `Status=ended`, `EffectiveTo/ValidTo=effectiveDate` ve replacement provenance alır.
- Yeni kayıt ayrı Id ile `active` açılır; `ReplacedAssignmentId`, `ReplacementReason`, `CorrelationId`,
  `PreviousPositionCode`, `NewPositionCode` korunur.
- Eski end + yeni create **all-or-nothing**'dir. Validation/conflict/concurrency hatasında iki kayıt da değişmez.

**Transfer policy:**

- `transferFromAssignmentId`, target TerritoryId/BU scope, `effectiveDate`, `transferReason`, `PositionRef` ve
  `correlationId` zorunludur.
- Source kayıt ended olur; target kayıt ayrı Id ile active açılır.
- `TransferFromAssignmentId`, `TransferToAssignmentId`, `TransferReason`, `EffectiveDate` ve `PositionCode`
  çift yönlü provenance/audit contract'ında korunur.
- Source end + target create **all-or-nothing**'dir; transfer person/resource kimliğini sessizce değiştiremez.

**Current responsibility contract:**

“Belirli effective-at tarihinde bu node + BU + position için kim sorumlu?” query'si yalnız şu koşulların tamamını
sağlayan kaydı current döndürür: tenant eşleşmesi; stored model status `active`; assignment status `active`;
`ValidFrom <= effectiveAt` ve (`ValidTo` null veya `effectiveAt < ValidTo`); `IsDeleted=false`; ended/rejected değil;
business scope eşleşmesi; `IsPrimary=true`; normalize `PositionCode` / `PositionRef` eşleşmesi. Sıfır sonuç kontrollü
boş response, birden fazla sonuç integrity conflict üretir; proposed kayıt hiçbir zaman current değildir.

**Position validation ve compatibility kararları:**

1. Canonical value object `PositionRef` alanları: `PositionId?`, zorunlu normalize `PositionCode`, zorunlu
   `PositionTitle`, zorunlu `PositionType`, zorunlu `SourceSystem`. Display snapshot SoR değildir.
2. Canonical Position directory erişilebiliyorsa backend `PositionId` + code/title/type eşleşmesini doğrular; client
   snapshot'ına kör güvenmez.
3. Directory geçici olarak erişilemiyorsa eksiksiz PositionRef snapshot planning create'i bloklamaz; kayıt
   `snapshot` validation mode/dependency warning ve audit bilgisi taşır. Operational activation için directory'den
   önceden doğrulanmış policy snapshot veya compatibility policy mapping bulunmalıdır; ikisi de yoksa fail-closed.
4. Node/coverage policy hardcoded role switch'iyle değil Position metadata/policy resolver ile belirlenir:
   Medical Representative position → zone/microzone; Area Manager → area (region yalnız metadata açıkça izin verirse);
   Regional Manager → region (division yalnız metadata ile); Product Manager → node'suz BU/product-portfolio;
   HOC/Commercial Manager → policy metadata izin verirse model-wide/wider scope.
5. `territory-resource-role` yeni write/read contract'ın kaynağı değildir. Migration döneminde yalnız legacy
   RoleCode → Position policy mapping için compatibility-only kullanılır; sonra deprecated edilir. Yeni testler
   position beklentileriyle yazılır.

**Conflict / override policy:**

- Aynı node + normalize PositionCode + aynı BU scope + çakışan dönem için iki primary: **409**, override ile
  aşılamaz.
- Aynı resource/person + aynı PositionCode + aynı BU + çakışan dönemde birden fazla primary node: izinli fakat
  `multi-node-coverage` warning ve audit zorunludur.
- Aynı resource/person + aynı PositionCode + farklı BU + çakışan dönemde primary: default **409**; yalnız
  `source=override` + non-empty reason + yetkili manage actor ile izinlidir.
- Override yalnız açıkça override edilebilir cross-BU policy'yi aşar; duplicate slot, invalid date, tenant,
  terminal-state veya missing-position-policy guard'ını aşamaz.
- `IsPrimary=false` backup/shared atamalar duplicate-primary ve cross-BU exclusivity'den muaftır; effective-date,
  PositionRef, coverage, tenant ve model-state validation'ları yine zorunludur.

**Reference / metadata kararı:**

- `territory-coverage-scope`, `territory-assignment-status` ve `territory-assignment-source` mevcut owner'larından
  consume edilir.
- Position policy için hedef metadata: `positionCode`, `positionType`, `requiresTerritoryId`,
  `allowsTerritoryId`, `requiredNodeLevels`, `allowedNodeLevels`, `requiresBusinessScope`,
  `allowsBusinessScope`, `canBePrimary`, `allowsMultiNode`, `allowsCrossBusinessUnit`, `requiresReason`,
  `allowsMutation`, `isOperationalStatus`, `isPlanningStatus`, `isTerminal`.
- FU04A bu task içinde MOD-0048 publish yapmaz. Eksik metadata/publish ayrı operator veya reference-data follow-up'ıdır.

**FU04A acceptance criteria:**

- Draft modelde oluşturulan assignment `proposed` ve `planningOnly=true` görünür; current responsibility query'sine
  hiçbir effective date için girmez.
- Model activation, tüm valid proposed resource assignment'ları aynı işlem sınırında active yapar; tek blocking
  conflict veya çözülemeyen required Position policy varsa model ve assignment statülerinde kısmi yazma olmadan
  controlled 409/422 döner.
- Active modelde create/end endpoint'leri reason + effective date ister; archived/inactive/superseded/expired model
  409 döner.
- Replacement eski kaydı ended tutar, yeni kaydı ayrı Id ile active açar ve replacement provenance alanlarını
  doldurur; iki write concurrency/validation hatasında birlikte rollback olur.
- Transfer source kaydı ended, target kaydı ayrı Id ile active yapar ve iki yönlü transfer provenance taşır; işlem
  all-or-nothing'dir.
- Current responsibility query yalnız active model + active/effective/primary/not-deleted assignment + BU +
  PositionRef eşleşmesini döndürür; duplicate current sonucu integrity conflict olarak raporlar.
- History query'leri ended kayıtları node/resource/position/model filtreleriyle döndürür; kritik update geçmişi
  overwrite etmez.
- Duplicate slot 409; same-BU multi-node warning; cross-BU same resource/position default 409; yalnız yetkili
  override + reason cross-BU policy'yi aşar; non-primary muafiyet açık test edilir.
- Directory hazırsa PositionRef backend'de doğrulanır; hazır değilse eksiksiz snapshot + dependency warning ile
  planning kayıt açılabilir, fakat policy snapshot/mapping olmadan operational activation fail-closed olur.
- UI Planned / Active / Ended badge, planning-only uyarısı, current view, End/Replace/Transfer, effective-date +
  reason modalı, conflict warning, history drawer/table ve Position label/selector gösterir.
- UI ve API hiçbir yerde yeni authoritative `RoleCode` üretmez; legacy alan yalnız migration read/mapper testinde
  kullanılır.
- Contract resource lifecycle/current/history capability'lerini doğru bildirir; workflow/evidence/import-export/
  visit-route flag'leri açılmaz.
- Frontend bütün business çağrılarını Gateway/same-origin proxy üzerinden yapar; direct `:5061` çağrısı ve payload
  `TenantId` alanı yoktur.
- Tenant isolation cross-tenant ID'lerde 404; Account/Contact master mutation yolu yok; hard delete yoktur.

**FU04A test expectations:**

- Unit: activation transition/rollback; proposed planning-only; active create/end; replacement/transfer atomikliği;
  effective-date sınırları; current/history filtreleri; PositionRef validation modes; duplicate/override/warning/
  non-primary policy; concurrency; terminal model guard; tenant isolation.
- Guard: Account/Contact aggregate veya endpoint mutasyonu yok; FU05/FU06–FU09 scope'u açılmamış; hard delete,
  `crm.territory.delete`, `crm.micro-zone.manage`, direct port ve request `TenantId` yok; yeni RoleCode write yok.
- Frontend: draft/active/ended badge, planning-only notice, End/Replace/Transfer reason modalı, conflict görünümü,
  history/current görünümü, Position label ve unavailable-directory dependency warning.
- Contract/build: CrmService API + Application test suite, Diten.Web build, JavaScript syntax, 7-dil RESX parity,
  Compact DataTable v2 verifier.
- Authenticated Gateway-only smoke: activation transition; active end; replacement; transfer; history; current
  responsibility; duplicate 409; override + reason; archived block; cross-tenant 404; Account/Contact unchanged.

**Explicitly out of scope:** `AccountTerritoryAssignment` apply/history değişikliği; Account veya Contact mutasyonu;
workflow approval, submit/approve/reject veya MOD-0023 entegrasyonu; approval trace; evidence pack; import/export;
visit/route planning implementation; Brand Scope; Product/Brand master; hard delete; Mongo hand-edit; RBAC
seed/grant; MOD-0048 publish; `crm.territory.delete`; `crm.micro-zone.manage`; request payload'ında `TenantId`;
direct port 5061 business API çağrısı.

**FU05–FU09 boundary:** FU04A resource responsibility lifecycle sahibidir. FU05 Account assignment sahibidir; FU06
workflow approval sahibidir; FU07 evidence pack; FU08 import/export; FU09 yalnız MOD-0155 readiness/coverage API
sahibidir. FU04A visit/route planı üretmez ve bu sınırları genişletmez.

---

### 22.4 FU04B — Resource Assignment Plan vs Current Visibility

FU04A, planlanan (`proposed`) ve operasyonel (`active`) resource responsibility'yi ayırdı ve replacement/transfer
provenance'ını üretti. Ancak kullanıcı bugün **"başta plan neydi, şimdi ne?"** sorusunu tek ekranda soramıyor:
plan bilgisi activation anında `proposed` kayıtların `active` olmasıyla **üzerine yazılıyor** ve geriye yalnız
history satırları kalıyor.

FU04B bu boşluğu **additive ve read-only** olarak kapatır: activation anında immutable bir plan baseline yakalar ve
bu baseline ile o anki current responsibility'yi karşılaştıran bir okuma yüzeyi sağlar.

**Örnek senaryo (pack kaydı):**

```text
Plan (activation anı)   : Edirne/Keşan Zone + Alpha BU + Medical Representative → Ayşe
Sonraki operasyon       : Ayşe → Tekirdağ/Süleymanpaşa Zone transfer; Keşan Zone'a Mehmet atanır
Beklenen FU04B çıktısı  :
  Keşan Zone  | Planned: Ayşe | Current: Mehmet | ChangeType: Replaced          | reason + changedAt/By
  S.paşa Zone | Planned: —    | Current: Ayşe   | ChangeType: TransferredIn     | transfer link + reason
  (Ayşe satırı) Keşan → ChangeType: TransferredOut, transferToAssignmentId ile bağlı
```

**FU04B, FU04A üzerine additive bir visibility/read-model follow-up'ıdır. Workflow approval değildir; resource
assignment mutasyonu değildir.**

#### Allowed runtime scope

1. **Plan baseline capture.** `TerritoryResourceAssignmentPlanSnapshot` (§7.5a) aggregate'i ve onu yazan
   activation-time capture adımı. Snapshot alanları §7.5a'da tanımlıdır (`TerritoryModelId`, `CapturedAt`,
   `CapturedBy`, `ActivationCorrelationId`, `TerritoryNodeId/Code/Name`, `BusinessScopes`, `PositionCode`,
   `PositionTitle`, `PositionType`, `ResourceId`/`PersonRef`, `ResourceDisplayName`, `PlannedEffectiveFrom`,
   `PlannedEffectiveTo`, `IsPrimary`, `SourceAssignmentId`).
   **Bu, FU04B'nin tek yazma yetkisidir** ve yalnız mevcut activation lifecycle işleminin içinde çalışır; ayrı bir
   "snapshot al" endpoint'i veya kullanıcı aksiyonu **açılmaz**.
2. **Plan vs Current comparison.** `Plan` = activation anındaki proposed snapshot; `Current` = active modelde
   verilen `effectiveAt` tarihindeki current responsibility; `Diff` = read-time hesaplanan fark.
3. **Diff type hesaplama.** Minimum diff type kümesi: `Unchanged` · `Replaced` · `TransferredOut` ·
   `TransferredIn` · `AddedAfterActivation` · `EndedAfterActivation` · `MissingCurrent` · `DateChanged` ·
   `ScopeChanged` · `PositionChanged`.
4. **Üç read-only query endpoint'i** (§19'a eklendi) ve query filtreleri: `effectiveAt` · `territoryNodeId` ·
   `businessUnit` scope · `positionCode` · `resourceId` · `diffType`/`changeType`.
5. **UI:** Resource Assignments sayfasında **read-only compact pill tab/section** (§18 satır 11); Territory Model
   Details yalnız hierarchy viewer olarak kalır. Kolonlar: Planned Resource ·
   Current Resource · Change Type · Position · Business Unit · Territory Node · Effective Date · Reason ·
   Replacement/Transfer link'leri. 7 dil RESX parity, DataTable v2, Gateway-only.
6. **Audit / provenance visibility.** Mevcut FU04A alanlarının **okunup gösterilmesi**: `replacementReason` ·
   `transferReason` · `replacedAssignmentId` · `transferFromAssignmentId` · `transferToAssignmentId` · `changedAt` ·
   `changedBy` · `correlationId`. FU04B bu alanları **üretmez veya değiştirmez**, yalnız görünür kılar.
7. Backend/frontend testleri, contract flag hizalaması (`supportsResourceAssignmentPlanBaseline`,
   `supportsResourcePlanVsCurrent`), Gateway-only authenticated smoke ve implementation evidence report.

#### Diff type semantiği (normatif)

| Diff type | Koşul | Not |
|---|---|---|
| `Unchanged` | Aynı node + position + BU için planlı resource, `effectiveAt`'te hâlâ current | Baseline satırı ile current satırı `SourceAssignmentId` üzerinden aynı zincirde |
| `Replaced` | Planlı slot current'ta **başka bir resource** tarafından dolduruluyor ve zincirde replacement provenance var | `replacedAssignmentId` / `replacementReason` gösterilir |
| `TransferredOut` | Planlı resource bu node'da artık current değil ve `transferToAssignmentId` başka node'a işaret ediyor | Hedef node link'i gösterilir |
| `TransferredIn` | Current resource bu node'a başka bir node'dan transfer ile gelmiş | `transferFromAssignmentId` link'i gösterilir |
| `AddedAfterActivation` | Current'ta var, baseline'da karşılığı yok, replacement/transfer zinciriyle de bağlı değil | Activation sonrası yeni açılmış assignment |
| `EndedAfterActivation` | Baseline'da var, current'ta yok, yerine geçen kayıt da yok | Slot boşalmış |
| `MissingCurrent` | Baseline satırının `SourceAssignmentId` zinciri çözülemiyor / current sorgusu sonuç vermiyor | **Veri bütünlüğü sinyali**; hata değil, açıkça işaretlenir |
| `DateChanged` | Aynı resource + position + node, fakat effective window baseline'dan farklı | `PlannedEffectiveFrom/To` ↔ current `ValidFrom/To` |
| `ScopeChanged` | Aynı resource + position + node, fakat `BusinessScopes` veya `IsPrimary` farklı | — |
| `PositionChanged` | Aynı resource + node, fakat normalize `PositionCode` farklı | Position canonical olduğu için ayrı tip |

Bir satır birden fazla koşulu sağlıyorsa **öncelik sırası** yukarıdan aşağıdır (`Replaced` > `TransferredOut/In` >
`AddedAfterActivation`/`EndedAfterActivation` > `MissingCurrent` > `DateChanged` > `ScopeChanged` >
`PositionChanged` > `Unchanged`); ikincil farklar satırın detay alanında listelenir.

#### Policy kararları (D-FU04B-1…7)

| # | Karar | Sonuç |
|---|---|---|
| **D-FU04B-1** | **Plan baseline ne zaman yakalanır?** | `TerritoryModel` activation sırasında, proposed resource assignment'lar `active` yapılmadan **hemen önce**, **aynı lifecycle işlem sınırında**. Activation fail-closed olursa snapshot da yazılmaz |
| **D-FU04B-2** | **Snapshot immutable mı?** | **Evet.** Write-once; update/delete yok. Yeniden aktivasyon yeni `SnapshotVersion` üretir, öncekini silmez |
| **D-FU04B-3** | **Current hangi kaynaktan?** | **FU04A current responsibility query'si** (veya onunla **birebir aynı** deterministic current assignment policy). FU04B paralel/ikinci bir current tanımı üretmez |
| **D-FU04B-4** | **Runtime mutation yapar mı?** | **Hayır.** Read-only projection/query. Tek istisna D-FU04B-1'deki activation-time baseline yazımıdır ve bu bir kullanıcı aksiyonu değildir |
| **D-FU04B-5** | **Draft modelde görünür mü?** | Draft modelde yalnız **"planning preview"** gösterilebilir (proposed listesi, current sütunu boş, açık "not yet activated" uyarısı). Gerçek Plan vs Current **ancak activation snapshot'ından sonra** anlamlıdır |
| **D-FU04B-6** | **Archived modelde görünür mü?** | **Evet**, read-only historical comparison olarak. Archived modelde hiçbir aksiyon sunulmaz |
| **D-FU04B-7** | **Diff saklanır mı, runtime'da mı hesaplanır?** | Plan snapshot **immutable saklanır**; current state **runtime okunur**; diff **read-time hesaplanır**. Projection cache ileride eklenebilir fakat FU04B'de **zorunlu değildir** ve bu FU'da yetkilendirilmemiştir |

#### Position-based zorunluluk

FU04B **tamamen Position tabanlıdır.** Snapshot, current eşleştirmesi, diff hesaplaması, query filtreleri ve UI
kolonları yalnız `PositionCode` (normalize) · `PositionTitle` · `PositionRef` · `PositionType` alanlarını kullanır.

- **`RoleCode` / `LegacyRoleCode` yeni query, diff veya snapshot kaynağı olamaz.** Snapshot'a yazılmaz.
- Legacy `RoleCode` yalnız migration/backward-compatibility amacıyla, açıkça "legacy" etiketiyle **gösterilebilir**;
  eşleştirme anahtarı olarak kullanılamaz.
- Baseline ile current arasındaki slot eşleştirme anahtarı: `TerritoryNodeId` + normalize `PositionCode` +
  `BusinessScopes` + (`ResourceId`/`PersonRef` zincir takibi için `SourceAssignmentId`).

#### Explicit exclusions

Aşağıdakiler FU04B kapsamında **kesinlikle yasaktır**:

- Resource assignment create / update / end / replace / transfer **davranışını değiştirmek** (yeni endpoint,
  değişen validation, değişen conflict/override politikası dahil)
- `AccountTerritoryAssignment` apply · account assignment history değiştirmek
- Account master mutasyonu · Contact mutasyonu
- Workflow approval · submit/approve/reject · MOD-0023 entegrasyonu
- Evidence pack (FU07) · import/export (FU08) · visit/route planning implementation (FU09/MOD-0155)
- Brand Scope · Product/Brand master
- Hard delete · Mongo hand-edit
- RBAC seed/grant (ayrıca yetkilendirilmedikçe) · MOD-0048 publish (ayrıca yetkilendirilmedikçe)
- `crm.territory.delete` · `crm.micro-zone.manage` · request payload'ında `TenantId` · direct port 5061 çağrısı
- **Yeni bağımsız ana menü sayfası** — global Resource Change Monitor future follow-up'tır
- Diff projection cache / materialized read model (D-FU04B-7)

#### FU04B acceptance criteria

- Model activation, proposed kayıtlar `active` yapılmadan önce aynı işlem sınırında immutable snapshot yazar;
  activation fail-closed olduğunda snapshot yazılmaz ve kısmi snapshot oluşmaz.
- Snapshot yazıldıktan sonra hiçbir yol (endpoint, handler, UI) onu update veya delete edemez; yeniden aktivasyon
  yeni `SnapshotVersion` üretir.
- Üç query endpoint'i read-only'dir; hiçbiri yazma yolu tetiklemez ve hepsi `crm.territory.resource.read`
  (veya FU04A fallback'i) ile korunur.
- Diff type'lar §22.4 tablosundaki semantiğe ve öncelik sırasına göre deterministik üretilir; aynı girdi aynı çıktıyı
  verir.
- Replacement/transfer provenance ve reason alanları ekranda görünür; hiçbiri FU04B tarafından üretilmez/değiştirilmez.
- Draft modelde yalnız planning preview + "not yet activated" uyarısı gösterilir; archived modelde read-only
  historical comparison gösterilir ve aksiyon sunulmaz.
- UI Resource Assignments sayfasında **compact pill tab/section**'dır; yeni route/menü/page descriptor eklenmez ve hiçbir
  create/end/replace/transfer/apply aksiyonu içermez.
- Hiçbir yüzey `RoleCode`'u eşleştirme/diff anahtarı olarak kullanmaz.
- Contract, plan baseline ve plan-vs-current capability'lerini doğru bildirir; workflow/evidence/import-export/
  visit-route flag'leri **açılmaz**.
- Tenant isolation cross-tenant ID'lerde 404; Account/Contact master değişmez; hard delete yoktur.

#### FU04B test expectations

- **Unit:** activation-time snapshot capture ve fail-closed rollback; snapshot immutability; yeniden aktivasyonda
  versiyonlama; 10 diff type'ın her biri için deterministik senaryo; diff öncelik sırası; `MissingCurrent` bütünlük
  sinyali; `effectiveAt`/node/BU/position/resource/diffType filtreleri; draft planning preview davranışı; archived
  read-only davranışı; position-based eşleştirme (RoleCode kullanılmadığının negatif testi); tenant isolation.
- **Guard:** resource assignment mutation endpoint'lerinde davranış değişikliği yok; Account/Contact mutasyonu yok;
  FU05–FU09 scope'u açılmamış; hard delete/`crm.territory.delete`/`crm.micro-zone.manage`/direct port/request
  `TenantId` yok; yeni menü page descriptor'ı yok.
- **Frontend:** Plan vs Current tab render'ı, kolon seti, provenance link'leri, boş/`notCaptured` durumu, draft
  uyarısı, 7 dil RESX parity, Compact DataTable v2 verifier.
- **Authenticated Gateway-only smoke:** senaryo — draft'ta plan (Keşan/Alpha/MR → Ayşe) → activate → snapshot oluştu
  → Ayşe'yi Süleymanpaşa'ya transfer → Keşan'a Mehmet replacement → Plan vs Current tab `Replaced` +
  `TransferredOut` + `TransferredIn` satırlarını reason/provenance ile gösteriyor → resource bazlı endpoint Ayşe için
  planned/current ayrımını döndürüyor.

#### FU04B boundary

FU04B **görünürlük** sahibidir; **lifecycle sahibi değildir**. FU04A resource responsibility lifecycle'ın
sahibi olarak kalır — FU04B onun ürettiği veriyi okur, kurallarını değiştirmez. FU05 Account assignment, FU06
workflow approval, FU07 evidence pack, FU08 import/export, FU09 MOD-0155 readiness sahipliğinde hiçbir değişiklik
yoktur. FU04B'nin ürettiği plan baseline, ileride **FU06'nın before/after diff'i** için doğal girdi olabilir; bu
bağlantı FU06 pack authorization'ında ele alınır, FU04B'de **açılmaz**.

### 22.5 FU08 — Import/Export Hardening

FU08, MOD-0151'i "elle tek tek girilen" bir modülden **toplu yönetilebilir** bir modüle çıkarır. Gerçek bir territory
planı yüzlerce node ve binlerce account ataması içerir; bunları ekrandan tek tek girmek operasyonel olarak
sürdürülemez. FU08 bunun için **MOD-0150 Contact Import/Export desenini** (template → export → upload → **dry-run** →
safe apply → run history) MOD-0151 nesnelerine uygular.

FU08'in **temel mimari kuralı**: import bir **taşıma yolu**dur, **ikinci bir iş kuralı motoru değildir**. Her satır,
UI'dan girilmiş gibi mevcut FU03/FU04A/FU05/FU05A guard'larından geçer. Import hiçbir guard'ı gevşetemez, atlayamaz
veya kendi paralel validasyonunu koyamaz. **Yeni bir import framework yazılmaz**; `Diten.CrmService` içinde MOD-0149/
MOD-0150 için zaten çalışan XLSX parse / dry-run / apply altyapısı yeniden kullanılır.

**Allowed runtime scope:**

1. **Export (read-only).** Territory Model metadata · Territory Node'lar · hiyerarşi (parent/level/sort) · Business
   Unit scope'ları · Assignment Rule'lar · Account Assignment current + history · CoverageSummary · Resource
   Assignment current + history · Plan vs Current. Format **XLSX**, satır bazlı açık kolonlar. Tenant **claim'den**
   okunur; export payload'ında/çıktısında `TenantId` **yer almaz**; çağrılar Gateway üzerinden yapılır, direct 5061
   business API çağrısı yoktur.
2. **Import template generation.** Sistem, doldurulabilir çok-sheet'li bir XLSX şablon üretir: `Model` · `Nodes` ·
   `AssignmentRules` · `AccountAssignments` · `ResourceAssignments` · `ReferenceValues` (lookup) · `ValidationNotes`
   (Instructions). Şablon required kolonları, kabul edilen değerleri, reference-data ipuçlarını, örnek satırları ve
   validation kurallarının açıklamasını taşır.
3. **Dry-run validation (zorunlu ilk adım).** Import **hiçbir koşulda** doğrudan yazmaz; önce dry-run çalışır ve
   **hiçbir şey persist etmez**.
4. **Safe apply.** Yalnız dry-run sonucu üzerinden, aşağıdaki sheet-level policy ile çalışır.
5. **Import run history.** Read-only `TerritoryImportRun` kaydı (append-only sidecar aggregate; update/delete komutu
   yoktur).
6. **UI.** §18 #10 Import / Export sayfası: export butonları, template indirme, dosya yükleme, dry-run sonuç tablosu
   (satır bazlı, severity renkli), apply onayı ve import run history listesi. 7 dil RESX paritesi.
7. Backend/frontend testleri, contract flag/limitation hizalaması, Gateway-only authenticated smoke ve FU08
   implementation evidence report.

#### Import / export object scope

| Nesne | Export | Template | Dry-run | Apply | Zorunlu guard |
|---|---|---|---|---|---|
| Territory Model metadata | ✅ | ✅ | ✅ | ✅ | Yalnız `draft` model editable (FU01/FU02B); active/archived model **import ile değiştirilemez** |
| Territory Nodes | ✅ | ✅ | ✅ | ✅ | **Hiyerarşi validasyonu zorunlu**: duplicate code, geçersiz parent, cycle, geçersiz `TerritoryLevel`, level sırası, tarih penceresi containment |
| Territory hierarchy (parent/level/sort) | ✅ | ✅ | ✅ | ✅ | Node ile aynı guard; ayrı bir hiyerarşi yazma yolu **yoktur** |
| Business Unit scopes | ✅ | ✅ | ✅ | ✅ | FU02A normalized `BusinessScopes` sözleşmesi; yalnız `business-unit` scope type; MOD-0048 published değer |
| Assignment Rules | ✅ | ✅ | ✅ | ✅ | FU03 rule type / conflict policy / scope validasyonu; **preview yan etkisizliği korunur** — import bir rule'u çalıştırmaz, yalnız tanımını yazar |
| Account Assignments | ✅ (current + history) | ✅ | ✅ | ✅ **(FU05 guard'larıyla)** | FU05 apply kurallarının **aynısı** (aşağıya bakınız) |
| Resource Assignments | ✅ (current + history) | ✅ | ✅ | ❌ **FU08 v1'de YOK → FU08A** | FU04A lifecycle bypass riski nedeniyle apply ayrı yetkilendirilir |
| CoverageSummary | ✅ | ❌ | ❌ | ❌ | **Read model** — import edilemez |
| Plan vs Current | ✅ | ❌ | ❌ | ❌ | **Snapshot/diff read model** — import edilemez |

#### Dry-run validation policy

Dry-run **hiçbir kayıt yazmaz** (import run history satırı dahil değil — bkz. run history policy). En az şu kontroller
çalışır:

| Kategori | Kontrol |
|---|---|
| Yapısal | required kolonlar; bilinmeyen/duplicate kolon; veri tipi; tamamen boş satır; dosya-seviyesi hata (bozuk/parolalı dosya, eksik zorunlu sheet) |
| Node/hiyerarşi | duplicate node code; geçersiz parent (yok / farklı model / soft-deleted); **cycle riski**; geçersiz `TerritoryLevel`; level sırası ihlali |
| Scope | geçersiz business-unit scope; model scope'unu aşan satır scope'u; geçersiz country scope |
| Rule | geçersiz `RuleType`; geçersiz `ConflictPolicy`; geçersiz hedef node |
| Account | geçersiz `AccountId`; çözülemeyen account external reference; cross-tenant account |
| Resource | geçersiz/policy'siz position code; geçersiz resource ref; snapshot alanı tutarsızlığı *(yalnız dry-run — apply FU08A)* |
| Tarih | effective window containment (assignment ⊆ node ⊆ model); geçersiz `EffectiveTo < EffectiveFrom` |
| Lifecycle | **active model overlap riski** (single-active-model guard); active/archived model'e yazma denemesi |
| Reference data | ilgili MOD-0048 setleri **published mı** → değilse **fail-closed** |
| İzolasyon | tenant isolation; dosyadaki her satır çağıran tenant claim'ine bağlanır |

**Dry-run sonuç satırı sözleşmesi:** `Sheet` · `RowNumber` (gerçek Excel satırı) · `Severity` · `ErrorCode` (stabil,
makine-okunur) · `Message` (lokalize) · `SuggestedFix` · `Blocking` (bool) · `Operation` · `EntityType` ·
`ResolvedKey` · `ChangedFields`. Özet sayaçları: creates · updates · ends · skips · errors · conflicts · warnings.
**Blocking / non-blocking ayrımı zorunludur**: blocking satır hiçbir koşulda apply edilmez; non-blocking warning
apply'ı tek başına bloklamaz ama raporlanır ve run history'de sayılır.

#### Safe apply policy

| Karar | Sonuç |
|---|---|
| Import doğrudan apply yapabilir mi? | **Hayır.** Dry-run zorunlu ilk adımdır; apply ayrı endpoint/rotadır ki yıkıcı çağrı bir "önizleme" isteğiyle kazara tetiklenemesin |
| Apply nasıl çalışır? | **Sheet-level policy** (aşağıdaki tablo). Genel motor "validate-all, then apply" — kullanıcının onayladığı plan ile çalışan plan aynı doğrulamadan geçer |
| `Model` / `Nodes` / `AssignmentRules` | **Sheet-level all-or-nothing**: sheet'te blocking hata varsa o sheet'ten **hiçbir satır** yazılmaz. Hiyerarşi kısmi yazılırsa yetim/kopuk ağaç oluşur — bu yüzden kısmi apply yasaktır |
| `AccountAssignments` | **Batch-level all-or-nothing** — FU05'in kendi apply sözleşmesiyle **birebir aynı** (§22.2 policy #2) |
| `ResourceAssignments` | Apply **yok** (FU08A) |
| Strict mode | Operatör isterse **dosya-seviyesi all-or-nothing**: herhangi bir sheet'te tek blocking hata varsa hiçbir şey yazılmaz |
| Partial apply olursa | Sheet bazında **açıkça raporlanır** (hangi sheet uygulandı, hangisi atlandı, neden) ve import run history'ye yazılır. Sessiz partial apply yasaktır |
| Sheet sırası | `Model` → `Nodes` → `AssignmentRules` → `AccountAssignments`; aynı dosyada oluşturulan node'a rule/assignment bağlanabilsin diye. Önceki sheet blocking hata aldıysa ona bağlı satırlar `skipped_dependency` olur |
| Hard delete | **Yok.** `delete` operasyonu desteklenmez → controlled `unsupported_operation`. Kapatma yalnız `end` semantiğiyle (FU05 `ended` / FU04A `ended`) yapılır |
| Update overwrite riski | Boş hücre = **değiştirme**; açık `<CLEAR>` token'ı = temizle (zorunlu alanlar temizlenemez); id/immutable alan değiştirme denemesi controlled hata. Sessiz toplu overwrite yasaktır |
| Idempotency | Aynı dosyanın ikinci apply'ı **duplicate üretmez**: eşleşen kayıt `no_change` skip olur veya controlled conflict döner. Eşleştirme anahtarı doğal anahtardır (model+code / model+account+scope+window), e-posta/serbest metin değil |
| Provenance | Her yazılan/kapatılan kayıtta `ImportRunId` + `CorrelationId` taşınır; source file **hash**'i run kaydında tutulur |
| Reference set eksik | **Fail-closed** — apply bloklanır (dry-run zaten blocking hata üretir) |
| Uygulanacak satır yok | Apply bloklanır ("uygulanacak bir şey yok"); yanıltıcı "başarılı" gösterilmez |
| Hata oranı eşiği | Blocking hata oranı yüksekse (yanlış dosya/şablon sinyali) apply bloklanır; eşik implementation'da sabitlenir ve raporlanır |
| `TenantId` | Excel'de **yer almaz**; tenant claim'den gelir. Dosyada `TenantId` kolonu varsa yok sayılır ve uyarı üretir |
| Atomiklik notu | Mongo multi-document transaction **zorunlu kılınmaz**; standalone dev sunucusunda compensation fallback'i (MOD-0151 FU05 deseni) kullanılır ve partial durum açıkça raporlanır |

#### Import run history policy

Read-only, **append-only** `TerritoryImportRun` sidecar aggregate'i: `ImportRunId` · `TenantId` (server-resolved) ·
`FileName` · `FileHash` · `UploadedBy` · `UploadedAt` · `Status` (`dry-run` / `applied` / `partially-applied` /
`failed` / `blocked`) · `DryRunResult` (özet + satır raporu referansı) · `AppliedAt` · `AppliedBy` · `CorrelationId` ·
row counts (per sheet: total / created / updated / ended / skipped) · error counts · warning counts.

Kurallar: yalnız **apply** bir run kaydı **yazar**; salt dry-run çağrısı kalıcı kayıt bırakmaz (MOD-0150 "dry-run
hiçbir şey yazmaz" kuralıyla tutarlı). Run kaydı **güncellenmez ve silinmez**; hard delete yoktur. Ham dosyanın
kendisi saklanmaz — yalnız hash tutulur (PII/dosya saklama yüzeyi açılmaz).

#### Account / resource assignment import policy

- **Account Assignments import edilebilir mi? Evet — ama FU05'i bypass ederek değil.** Import satırı, FU05
  `ApplyAccountTerritoryAssignments` ile **aynı** guard setinden geçer: yalnız stored status'u `active` olan model;
  batch all-or-nothing; kesişen scope + örtüşen window'da controlled **409**; override yalnız non-empty reason ile;
  eski kayıt **silinmez**, `ended` + `EffectiveTo`/`EndedAt` ile kapatılır; assignment window ⊆ node window ⊆ model
  window. Import, preview/rule sürecini "atlayan" ayrı bir yazma yolu **değildir**; rule kaynaklı satırlar
  `AppliedRuleId`/`AppliedRuleCode` provenance'ını taşır, manuel satırlar `AssignmentSource=import` olarak işaretlenir.
  FU05A current-coverage guard'ı okuma tarafındadır ve import'tan etkilenmez.
- **Resource Assignments import edilebilir mi? v1'de yalnız export + template + dry-run.** Apply **FU08A**'ya
  ertelenmiştir. Gerekçe: FU04A `proposed` (planning) ile `active` (operational) ayrımını, activation transition'ını,
  atomik replacement/transfer'ı, reason/provenance zorunluluğunu ve position exclusivity guard'ını taşır; bunları bir
  import satırından güvenle ifade etmenin sözleşmesi (özellikle "bu satır replacement mı, transfer mı, yeni atama mı?")
  ayrı bir tasarım kararıdır. FU08A açılırsa **proposed/active ayrımı ve reason/provenance korunmak zorundadır**;
  import ile doğrudan `active` operational responsibility yaratmak veya replacement/transfer'ı bypass etmek
  yetkilendirilmemiştir.
- **CoverageSummary ve Plan vs Current import edilemez.** İkisi de türetilmiş read model'dir; import edilmeleri
  kaynağı ile projeksiyonu çelişkiye düşürürdü.

#### Permission decision

Canonical hedefler §17'deki `crm.territory.export` (export + template) ve `crm.territory.import` (dry-run + apply)
anahtarlarıdır. Katalog/grant hazır değilse FU08 implementation **seed/grant değiştirmez**; ayrı
**`MOD-0151 FU08-RBAC — Import/Export Permission Catalog Alignment`** follow-up'ı açılır ve geçici olarak export/template
için `crm.territory.model.read`, dry-run/apply için `crm.territory.model.manage` fallback'i kullanılır (FU04A-RBAC /
FU05-RBAC deseniyle aynı). Fallback yalnız FU08 endpoint'leri içindir; yeni permission literal'i seed etmez ve
`crm.territory.delete` / `crm.micro-zone.manage` anahtarlarını hiçbir koşulda açmaz. Fallback **yetki genişletmez**:
dosya account/resource sheet'i içeriyorsa ilgili FU05/FU04A guard'ları yine çalışır.

#### Contract flags

FU08 sonrası contract capability önerisi:

```json
{
  "supportsTerritoryExport": true,
  "supportsTerritoryImportExport": true,
  "supportsTerritoryImportDryRun": true,
  "supportsTerritoryImportApply": true,
  "supportsResourceAssignmentImportApply": false,
  "supportsWorkflowActivation": false
}
```

Mevcut flag'ler **korunur**: `supportsAssignmentRules`, `supportsAssignmentPreview`, `supportsResourceAssignments`,
`supportsResourceAssignmentPlanVsCurrent`, `supportsAccountAssignmentApply`, `supportsAssignmentHistory`,
`supportsCoverageSummary`, `supportsCoverageSummaryModelLifecycleGuard`. `supportsWorkflowActivation=false`
**kalır**; workflow/approval readiness flag'i eklenmez. `supportsResourceAssignmentImportApply=false`, FU08A sınırını
contract yüzeyinde de görünür kılar.

#### FU08 test expectations

- **Unit:** template üretimi (sheet seti, required kolonlar, örnek satır); parser (normalize header eşleşmesi, hücre
  tipi koruma, duplicate kolon, bozuk dosya); her dry-run kontrolü için ayrı negatif senaryo (duplicate node code,
  invalid parent, cycle, invalid level, invalid BU scope, invalid rule type, invalid conflict policy, invalid account
  ref, window containment, active-model overlap, unpublished reference set, cross-tenant satır); **dry-run hiçbir şey
  yazmaz** (her sheet için ayrı ayrı kanıtlanır); sheet-level all-or-nothing; account assignment batch
  all-or-nothing; idempotency (aynı dosya iki kez → duplicate yok); `delete` → `unsupported_operation`; `TenantId`
  kolonu yok sayılır + uyarı.
- **Guard:** Account/Contact master mutasyonu yok; `ContactTerritoryAssignment` yok; CoverageSummary/Plan vs Current
  import endpoint'i **yok** (derlenebilir bir yolu bulunmamalı); resource assignment apply yolu **yok**; hard delete
  yok; workflow/approval/ChangeRequest yok; `crm.territory.delete` / `crm.micro-zone.manage` yok; direct 5061 yok;
  request payload'ında `TenantId` yok.
- **Frontend:** Import/Export sayfası render'ı, dry-run sonuç tablosu (severity/blocking ayrımı), apply onay akışı,
  run history listesi, 7 dil RESX parity, Compact DataTable v2 verifier.
- **Authenticated Gateway-only smoke:** export → template indir → hatalı dosya yükle (dry-run blocking hata satırları
  doğru satır numarasıyla) → düzelt → dry-run temiz → apply → node/rule/account assignment yazıldı → aynı dosyayı
  tekrar apply → duplicate yok → run history iki kaydı da gösteriyor → Account master değişmedi.

#### FU08 boundary

FU08 **taşıma** sahibidir; **iş kuralı sahibi değildir**. FU01/FU02A node ve scope kurallarının, FU02B lifecycle'ın,
FU03 rule/preview'un, FU04A resource responsibility lifecycle'ın, FU05 account apply sözleşmesinin ve FU05A current
coverage guard'ının sahipliği **değişmez** — FU08 onların üstünden yazar, kurallarını değiştirmez. FU06 workflow
approval, FU07 evidence pack ve FU09 MOD-0155 readiness sahipliğinde hiçbir değişiklik yoktur. FU07 geldiğinde FU08
export yüzeyi evidence pack'e **girdi** olabilir; bu bağlantı FU07 authorization'ında ele alınır, FU08'de açılmaz.

**Explicitly out of scope:** workflow approval; controlled activation; ChangeRequest / Change Approval Trace; MOD-0023
integration; visit/route planning implementation; campaign / frequency / call-cycle implementation; digital detailing;
survey; GPS check-in/out; Brand Scope; Product/Brand master; Account master mutasyonu; Contact mutasyonu;
`ContactTerritoryAssignment` eklemek; **CoverageSummary import**; **Plan vs Current import**; **resource assignment
apply (FU08A)**; yeni import framework yazmak; hard delete; Mongo hand-edit; RBAC seed/grant (ayrıca
yetkilendirilmedikçe); MOD-0048 publish (ayrıca yetkilendirilmedikçe); `crm.territory.delete`;
`crm.micro-zone.manage`; request payload'ında `TenantId`; direct port 5061 business API çağrısı.

### 22.6 FU09A — Visit/Route Readiness: Coverage, Contact Availability and Frequency Input Boundaries

FU09A, **MOD-0155 Field Sales / Visit Planning başlamadan önce** MOD-0151 tarafında hangi read model'lerin,
endpoint'lerin ve **sahiplik sınırlarının** hazır olması gerektiğini yetkilendirir. Amaç, eski CRM / Campaign
tarafındaki saha bilgisinin (ziyaret sıklığı, doktorun hangi kurumda hangi gün/saat bulunduğu, son ziyaret,
due/overdue) **yeni mimaride kaybolmamasını** sağlamak; ama bu bilgiyi yanlış modüle gömmemektir.

**FU09A'nın temel mimari kuralı:** *readiness bir **girdi yüzeyi**dir, bir planlayıcı değildir.* FU09A rota üretmez,
günlük plan kurmaz, optimizasyon yapmaz, cadence hesaplamaz ve ziyaret kaydı tutmaz. Ürettiği tek şey, MOD-0155'in
"bu MR bugün kimi ziyaret edebilir?" sorusuna başlayabilmesi için gereken **doğru, current ve gerekçelendirilmiş
aday kümesidir**.

**Legacy bulgusu (neden şimdi):** [legacy-value-preservation.md](../legacy-value-preservation.md) "Frequency /
cadence" satırını MOD-0155 (+ MOD-0167) hedefine bağlar, fakat **"Frequency verisi nereden beslenecek?"** sorusunu
açık EA-TBD olarak bırakır. Aynı dosya MicroTarget, visit lifecycle, ziyaret çakışma kontrolü ve "hastane doktoru →
yakın eczane rota önerisi" kurallarını da MOD-0155'e verir. FU09A bu açık soruyu **MOD-0151 tarafında** kapatmaz —
kapatılması gereken yeri (üretici: campaign/segmentation, tüketici: MOD-0155, birleşim kuralı: territory coverage)
**yazılı hâle getirir** ve MOD-0151'in yanlışlıkla frequency/visit sahibi olmasını engeller.

**Allowed runtime scope (hepsi read-only):**

1. **Territory coverage readiness.** "Bu account şu an hangi node'da / hangi active model kapsamında / hangi BU
   scope'unda / hangi position-resource sorumluluğunda?" sorularının tek, tutarlı cevabı. FU05A guard'ı **zorunlu**:
   operationally valid olmayan modele bağlı coverage current sayılmaz ve route candidate'a **giremez**.
2. **Resource / MR responsibility readiness.** FU04A current responsibility + FU04B plan-vs-current farkının
   **read-only** tüketimi: resource hangi node'lardan, hangi BU scope'unda, hangi position code ile sorumlu; bu
   sorumluluk current mı; replacement/transfer sonrası current sahip kim. **Route planning bu bilgiyi kullanır,
   değiştirmez.**
3. **Contact derived coverage.** `Contact → AccountContactLink (MOD-0150) → Account → current
   AccountTerritoryAssignment / CoverageSummary` zinciri üzerinden türetilmiş contact coverage; her satır **hangi
   account üzerinden** türediğini gösterir.
4. **Route candidate readiness projeksiyonu.** §7.12 `TerritoryRouteCandidateReadModel`: uygun/uygun değil kararı
   **değil**, uygunluk **sinyali** + makine-okunur reason code'lar.
5. **Reason code sözleşmesi.** Bir hedefin neden readiness dışında kaldığının stabil, lokalize edilebilir, UI'dan
   bağımsız ifadesi.
6. **Boundary kayıtları (implementation değil):** contact availability / working schedule, visit frequency /
   call-cycle policy ve last visit / due-overdue girdilerinin **sahiplik ve alan sözleşmesi**.
7. Backend testleri, contract readiness flag hizalaması, Gateway-only authenticated smoke ve FU09A evidence report.

#### Coverage readiness policy

| Soru | Karar |
|---|---|
| Account şu an hangi territory node'da? | Current coverage üzerinden; **FU05A operationally-valid-model guard'ı zorunlu** |
| Hangi active model kapsamında? | Yalnız stored status `active` + effective window içi model; archived/inactive/superseded düşer |
| Hangi BU scope altında? | FU02A normalized `BusinessScopes`; BU filtresi readiness sorgusunda **desteklenir** |
| Bu account'tan kim sorumlu? | FU04A **current** resource responsibility (position code ile); `proposed` planning satırı **current sayılmaz** |
| Coverage current değilse ne olur? | Route candidate'a **girmez**; `coverage_not_current` reason code'u ile raporlanır — sessizce düşürülmez |
| Geçmiş/gelecek tarih sorulabilir mi? | Evet, `effectiveAt` desteklenir; o tarihte active olan model + assignment dikkate alınır (FU05A §22.2a policy #3) |
| Readiness cache'lenir mi? | **Hayır** — türetilmiş projeksiyondur, persist/cache edilmez; kaynak ile projeksiyon çelişemez |
| Readiness mutasyon yapabilir mi? | **Hayır** — hiçbir endpoint yazma yapmaz; assignment `ended` etmez, coverage düzeltmez |

#### Contact derived coverage boundary

- **`ContactTerritoryAssignment` EKLENMEZ** (§11.2 kararı korunur). Contact'a `TerritoryId` / `ZoneId` / `MRId`
  alanı **eklenmez**.
- Coverage yolu tektir: `Contact → AccountContactLink → Account → current AccountTerritoryAssignment /
  CoverageSummary`.
- **Sahiplik:** MOD-0151 account coverage'ın **current doğruluğundan**, MOD-0150 Contact ve `AccountContactLink`
  ilişkisinin **master'ından** sorumludur. Contact derived coverage endpoint'i MOD-0151'de (veya ileride cross-module
  read model olarak) **okunabilir**; her iki durumda da **Contact master mutate edilmez**.
- Bir contact birden fazla account'a bağlıysa coverage **çoklu döner** — bu bir çakışma değil, birleşimdir (union).
  **Tek coverage varsayımı yapılamaz.**
- `AccountContactLink.IsPrimary` varsa ilgili satır **default** olarak işaretlenebilir; bu bir filtre değil, bir
  **görüntüleme tercihi**dir ve diğer satırları gizlemez.
- Her derived coverage satırı **provenance** taşır: hangi `AccountContactLinkId` / `AccountId` / assignment üzerinden
  türetildi.
- Link `inactive` / süresi geçmiş ise satır readiness dışıdır → `contact_not_linked_to_account`.

#### Contact availability / working schedule boundary

Route planning için "doktor hangi lokasyonda, hangi gün, hangi saat aralığında ziyaret edilebilir?" bilgisi
**zorunludur**. Bu veri Contact üzerine **tek bir düz alan olarak gömülemez**: aynı doktor birden fazla
hastane/klinik/eczanede çalışabilir ve müsaitliği **lokasyona göre değişir**.

**Sahiplik kararı:**

| Katman | Sorumluluk |
|---|---|
| **MOD-0150** Contact / Relationship | `AccountContactLink` bazlı `ContactAvailability` / `VisitPreference` **master data** (implementation ayrı yetkilendirmeyle) |
| **MOD-0151** (FU09A) | Yalnız **boundary tanımı** + route readiness için **read-only** tüketim; master **açılmaz** |
| **MOD-0155** Visit Planning | Bu availability verisini kullanarak visit plan / route plan **üretir** |

**Boundary'ye yazılan alan sözleşmesi** (MOD-0151'de implement **edilmez**): `AccountContactLinkId` · `ContactId` ·
`AccountId` · `Weekday` · `StartTime` · `EndTime` · `PreferredStartTime` · `PreferredEndTime` · `AvoidStartTime` ·
`AvoidEndTime` · `AppointmentRequired` · `AverageVisitDurationMinutes` · `AvailabilityType` · `EffectiveFrom` ·
`EffectiveTo` · `Notes` · `Source`.

Örnek (kayıt altına alınan gerçek ihtiyaç): *Dr. Ayşe — Medicana Beylikdüzü · Pazartesi 09:00–13:00 · Çarşamba
14:00–17:00 · Preferred 10:00–12:00 · AppointmentRequired: true.*

**Kurallar:** availability **link bazlıdır** (contact bazlı değil) — aynı contact farklı account'ta farklı gün/saat
taşıyabilir; "ziyaret edilmemesi gereken saat" (`Avoid*`) **preferred'ın tersi değildir**, ayrı ve daha güçlü bir
kısıttır; availability verisi yoksa readiness `AvailabilityStatus=unknown` döner ve **candidate'ı sessizce
düşürmez** (`contact_not_available_on_day` yalnız veri **varsa ve uymuyorsa** üretilir). MOD-0151 bu veriyi
**yazmaz, türetmez ve persist etmez**. Follow-up: **`MOD-0150-FU — Contact Availability and Visit Preference`**
(alternatif etiket: `MOD-0155-PREREQ — Contact Availability and Visit Preference Readiness`).

#### Frequency / call-cycle / campaign target boundary

Ziyaret sıklığı (haftada 1, ayda 1, iki haftada bir, campaign dönemine göre ayda 2) Visit Planning'in **zorunlu**
girdisidir; ancak Contact üzerine düz bir alan olarak **gömülemez** — aynı hedef farklı campaign, farklı BU, farklı
dönem ve farklı segment altında **farklı frequency** taşıyabilir.

**Model kararı (boundary):** ayrı bir **`VisitFrequencyPolicy` / `CallCyclePolicy`** nesnesi. Alan sözleşmesi:
`PolicyId` · `TenantId` · `TargetType` (`account` / `contact` / `account-contact-link`) · `TargetId` ·
`BusinessUnit` · `TerritoryNodeId?` · `CampaignId?` · `BrandId?` / `ProductId?` *(future)* · `FrequencyType`
(`weekly` / `monthly` / `biweekly` / `cycle-based` / `custom`) · `RequiredVisitCount` · `PeriodType`
(`week` / `month` / `cycle`) · `EffectiveFrom` · `EffectiveTo` · `Priority` · `Source`
(`campaign` / `manual` / `segmentation` / `legacy-import`) · `Notes`.

Örnekler: *Dr. Ali → haftada 1 · Dr. Ayşe → ayda 1 · A segment doktor → ayda 4 · Büyükşehir Eczanesi → iki haftada 1
· Campaign "Almiba Q1" → hedef doktorlara ayda 2.*

**Sahiplik:**

| Katman | Sorumluluk |
|---|---|
| **MOD-0165** Campaign / **MOD-0167** Segmentation | Frequency policy **üretir** (campaign hedefi, segment kuralı) |
| **MOD-0155** Visit Planning | Policy'yi **tüketir**, cadence compliance ve plan üretir |
| **MOD-0151** (FU09A) | Yalnız **birleşim boundary'si**: policy ile territory coverage'ın hangi anahtar üzerinden eşleşeceği (`TargetType/TargetId` + `BusinessUnit` + `TerritoryNodeId`) ve çakışma önceliği (`Priority`) sözleşmesi |

**Kurallar:** frequency **implementation'ı bu task'ta yapılmaz**; MOD-0151 policy **yazmaz, hesaplamaz, saklamaz**.
Aynı hedef için birden fazla policy varsa çözüm `Priority` + `EffectiveFrom/To` ile yapılır ve seçilen policy
readiness cevabında **görünür** olmalıdır (sessiz seçim yasak). Policy yoksa `FrequencyStatus=unknown` döner;
MOD-0151 **varsayılan bir sıklık uydurmaz**. Bu bölüm, `legacy-value-preservation.md` "Frequency verisi nereden
beslenecek?" EA-TBD sorusunun MOD-0151 tarafındaki **cevabıdır**: *kaynağı campaign/segmentation, tüketicisi
MOD-0155; MOD-0151 yalnız territory eşleşme anahtarını verir.*

#### Last visit / due-overdue boundary

**Sahiplik:** last visit tarihi, visit status ve visit history **MOD-0155** (Activity / Visit) tarafındadır.
**MOD-0151 last visit yazmaz, saklamaz ve türetmez.**

Due/overdue hesabının gerektirdiği girdiler (yalnız sözleşme olarak kaydedildi): target (`account` /
`account-contact-link`) · frequency policy · last completed visit date · visit status · effective date ·
availability window · **current coverage (MOD-0151)** · **current resource responsibility (MOD-0151)**.

**Karar:** FU09A implementation'ı açılırsa due/overdue yalnız **placeholder / readiness contract** olabilir —
alanlar (`LastVisitDate`, `DueStatus`) response şemasında **yer alır**, ancak girdi sağlanmadıkça `unknown` döner.
**Gerçek due/overdue engine MOD-0155'e aittir**; MOD-0151 içinde cadence compliance hesaplamak scope ihlalidir.

#### Route candidate readiness policy

Bir hedefin route candidate sayılabilmesi için gereken şartlar (**karar değil, sinyal**; her ihlal bir reason code
üretir):

| # | Şart | Kaynak |
|---|---|---|
| 1 | Account **current coverage** içinde | MOD-0151 FU05 + **FU05A guard** |
| 2 | Account **active** (soft-delete/pasif değil) | MOD-0149 (read-only girdi) |
| 3 | Account lokasyon bilgisi (adres veya lat/lon) mevcut | MOD-0149 (**MOD-0151 adres persist etmez**) |
| 4 | Contact varsa `AccountContactLink` **active** | MOD-0150 |
| 5 | Contact ilgili gün/saatte o lokasyonda müsait | MOD-0150 availability (**boundary**) |
| 6 | Frequency policy due/overdue sinyali veriyor | MOD-0165/0167 → MOD-0155 (**boundary**) |
| 7 | Son ziyaret bilgisi uygun | MOD-0155 (**boundary**) |
| 8 | Resource/MR o territory'den **current** sorumlu | MOD-0151 FU04A/FU04B |
| 9 | BU scope uyumlu | MOD-0151 FU02A |

**Response sözleşmesi:** §7.12 (`AccountId` · `AccountName` · `TerritoryNodeId` · `TerritoryNodeCode` ·
`BusinessUnit` · `ResourceId` · `ResourceDisplayName` · `ContactId?` · `ContactName?` · `AccountContactLinkId?` ·
`AvailabilityStatus` · `PreferredVisitWindow?` · `FrequencyStatus` · `LastVisitDate?` · `DueStatus` ·
`LocationReadiness` · `ReasonCodes[]`).

**Sınırlar:** response **sıra/mesafe/süre/gün planı/stop listesi/optimizasyon skoru içermez**; "en iyi rota" veya
"önerilen sıra" alanı **eklenemez**; readiness çağrısı **hiçbir kayıt yazmaz** (candidate log dahil); eksik girdi
(availability/frequency/last visit) candidate'ı sessizce düşürmez — `unknown` + reason code ile **görünür** kalır.

#### Reason code policy

Reason code'lar **stabil, lowercase-snake, makine-okunur** ve lokalize mesajdan **bağımsızdır**; UI metni değişse de
kod değişmez. Minimum küme:

| Reason code | Anlam |
|---|---|
| `readiness_ok` | Bilinen tüm şartlar sağlandı |
| `coverage_not_current` | Coverage FU05A operationally-valid-model guard'ından geçmiyor |
| `account_inactive` | Account pasif / soft-deleted |
| `account_missing_location` | Adres veya lat/lon yok (MOD-0149 girdisi) |
| `contact_not_linked_to_account` | `AccountContactLink` yok / inactive / süresi geçmiş |
| `contact_inactive` | Contact pasif |
| `contact_not_available_on_day` | Availability verisi **var** ve istenen gün/saat kapsamıyor |
| `outside_preferred_window` | Tercih edilen saat aralığı dışında (**non-blocking uyarı**) |
| `frequency_not_due` | Policy'ye göre henüz zamanı gelmemiş |
| `frequency_overdue` | Gereken ziyaret sayısı dönem içinde tamamlanmamış |
| `no_last_visit` | Son ziyaret bilgisi yok/erişilemiyor |
| `resource_not_current_owner` | Sorulan resource o node'un current sorumlusu değil |
| `business_scope_mismatch` | BU scope uyuşmuyor |

**Kurallar:** bir satır **birden fazla** reason code taşıyabilir; `readiness_ok` diğerleriyle birlikte dönemez;
`outside_preferred_window` gibi **uyarı** nitelikli kodlar candidate'ı tek başına elemez (blocking / non-blocking
ayrımı FU08 dry-run deseniyle aynı ruhtadır); girdi eksikliği (`unknown`) ile **kural ihlali** birbirinden ayrı
kodlarla ifade edilir — "veri yok" asla "uygun değil" olarak raporlanmaz.

#### Permission decision

FU09A **yalnız read**'dir ve **yeni permission anahtarı önermez** (§17). Endpoint'ler canonical
`crm.territory.assignment.read` (account/contact coverage, route candidate) ve `crm.territory.resource.read`
(resource responsibility readiness) ile korunur; katalog/grant hazır değilse FU05-RBAC / FU04A-RBAC ile aynı geçici
`crm.territory.model.read` fallback'i kullanılır. Hiçbir `*.manage` anahtarı talep edilmez, RBAC seed/grant
değiştirilmez ve `crm.territory.delete` / `crm.micro-zone.manage` açılmaz.

#### Contract flags

FU09A sonrası contract capability önerisi:

```json
{
  "supportsVisitRouteReadiness": true,
  "supportsContactDerivedCoverageReadiness": true,
  "supportsRouteCandidateReadiness": true,
  "supportsContactAvailabilityInputBoundary": true,
  "supportsVisitFrequencyInputBoundary": true,
  "supportsWorkflowActivation": false
}
```

Mevcut flag'ler **korunur**: `supportsCoverageSummary`, `supportsCoverageSummaryModelLifecycleGuard`,
`supportsResourceAssignments`, `supportsResourceAssignmentPlanVsCurrent`, `supportsAccountAssignmentApply`,
`supportsAssignmentHistory`, `supportsAssignmentRules`, `supportsAssignmentPreview`, `supportsTerritoryExport`,
`supportsTerritoryImportExport`, `supportsTerritoryImportDryRun`, `supportsTerritoryImportApply`,
`supportsResourceAssignmentImportApply=false`. `supportsWorkflowActivation=false` **kalır**.
**`supportsVisitRoutePlanning` gibi bir flag eklenmez** — MOD-0151 route planlamaz; `*InputBoundary` flag'leri
"bu girdinin sözleşmesi tanımlı" demektir, "bu veri MOD-0151'de vardır" demek **değildir**.

#### FU09A test expectations

- **Unit:** coverage readiness yalnız operationally valid model döner (FU05A guard'ı readiness yolunda da çalışır);
  `effectiveAt` geçmiş/gelecek tarih davranışı; deactivated model → `coverage_not_current`; resource readiness
  `proposed` satırı current saymaz ve replacement/transfer sonrası current sahibi döner; derived contact coverage
  çok-account'lu contact'ta **çoklu satır** + provenance döner; `IsPrimary` default işaretlenir ama diğer satırları
  gizlemez; inactive link → `contact_not_linked_to_account`; availability/frequency/last-visit girdisi yokken
  `unknown` + doğru reason code (candidate **sessizce düşmez**); reason code kümesi stabil ve `readiness_ok` yalnız
  tek başına döner.
- **Guard:** readiness endpoint'lerinin hiçbiri yazma yapmaz (mutasyon yolu **derlenebilir biçimde bulunmamalı**);
  `ContactTerritoryAssignment` yok; Contact/Account master mutasyonu yok; `ContactAvailability` /
  `VisitFrequencyPolicy` / visit / route / visit-history **aggregate'i MOD-0151'de yok**; response'ta rota
  sırası/mesafe/optimizasyon alanı yok; cadence compliance hesabı yok; hard delete yok; `crm.territory.delete` /
  `crm.micro-zone.manage` yok; request payload'ında `TenantId` yok; direct 5061 yok.
- **Authenticated Gateway-only smoke:** active model + account assignment mevcut → account coverage readiness doğru
  node/resource döner → model deactivate edilir → aynı account `coverage_not_current` ile candidate dışına düşer ve
  history bozulmaz → çok-account'lu contact iki coverage satırı döner → resource readiness current sahibi gösterir →
  Account/Contact master **değişmemiştir**.

#### FU09A boundary

FU09A **girdi yüzeyi** sahibidir; **planlayıcı değildir**. FU05/FU05A account coverage, FU04A/FU04B resource
responsibility sahipliği **değişmez** — FU09A onları okur, kurallarını değiştirmez. MOD-0150 Contact /
`AccountContactLink` / (gelecekte) contact availability master'ının sahibidir; MOD-0165/MOD-0167 frequency policy'nin
üreticisi, MOD-0155 visit/route/last-visit/due-overdue'nun sahibidir. FU06 workflow approval, FU07 evidence pack ve
FU08 import/export sahipliklerinde **hiçbir değişiklik yoktur**; FU09'un coverage roll-up ve MOD-0155 entegrasyon
teslimi **açılmamıştır**.

**Explicitly out of scope:** route optimization algoritması; günlük rota oluşturma; visit plan oluşturma; visit
execution; check-in / check-out; GPS validation; visit report; digital detailing; survey; campaign engine;
frequency / call-cycle **engine** implementation; contact availability **master** implementation (ayrıca
yetkilendirilmedikçe); MOD-0150 Contact master mutasyonu; Account master mutasyonu; `ContactTerritoryAssignment`;
hasta (patient) verisi; workflow approval; ChangeRequest; MOD-0023 entegrasyonu; evidence pack; import/export yeni
scope; Brand/Product master; coverage roll-up (FU09); hard delete; Mongo hand-edit; RBAC seed/grant (ayrıca
yetkilendirilmedikçe); MOD-0048 publish (ayrıca yetkilendirilmedikçe); `crm.territory.delete`;
`crm.micro-zone.manage`; request payload'ında `TenantId`; direct port 5061 business API çağrısı.

---

| FU | Name | Scope | Depends On | Out-of-Scope |
|---|---|---|---|---|
| **FU00** | Source reconciliation / pack approval | Excel + CRM docs + legacy mapping mutabakatı; §4 kararlarının kayda geçmesi; **MOD-0048 authoring template önerisi**; RBAC supersede follow-up | — | **Runtime kod yok**, seed yok, registry yok |
| **FU01** | Contract + core TerritoryModel/TerritoryNode backend | Contract endpoint; `TerritoryModel` + `TerritoryNode` aggregate'leri; level/cycle/tarih validasyonu; reference validator; permission tanımları | MOD-0149, MOD-0048, MOD-0018 | **Aktivasyon yok**, assignment apply yok, rule yok, **UI yok** |
| **FU02** | Territory hierarchy UI / Territory Model Viewer | Models list; hierarchy tree; node detail; draft editing; level badges; compact UI; 7 dil resx; menü/page descriptor | FU01 | Approval, evidence, atama ekranları |
| **FU02A** | Country & Business Unit Scope selector hardening | Country reference single-select; `business-unit` reference multi-select; normalized `BusinessScopes` persistence; no hardcoded fallback | FU01, FU02, MOD-0048 | Brand Scope, Product/Brand master, assignment scope |
| **FU02B** | Lifecycle Activation, Computed Expiry and Draft Soft Delete | Manual activate/deactivate/archive; node lifecycle guards; computed expiry; draft-only soft-delete; single-active-model guard; lifecycle UI/audit/tests | FU01, FU02, **FU02A** | Workflow approval/MOD-0023, hard delete, scheduler, assignment/resource/evidence/import-export |
| **FU03** | Assignment rules + preview | geography / account-list / account-type / product-portfolio / business-scope rule'ları; **preview only**; conflict tespiti; Assignment Preview ekranı | FU01, FU02 | Apply, aktivasyon, resource, **hiçbir yan etki** |
| **FU04** | Resource assignments | Tarihsel temel CRUD; PositionRef/PersonRef seam; coverage scope; draft planning UI; temel duplicate-primary guard | FU01, MOD-0288 | Active lifecycle, replacement/transfer, operational current/history, Employee/Position master |
| **FU04A** | Resource assignment lifecycle, replacement and operational visibility hardening | Position-based planning/operational ayrımı; activation transition; active create/end; atomik replace/transfer; current/history; position policy + conflict/override hardening; UI/test/smoke | FU04, FU02B, MOD-0288 | Account/Contact/FU05; workflow/FU06; evidence/import-export/FU07–FU08; visit/route/FU09; hard delete; seed/publish |
| **FU04B** | Resource assignment plan vs current visibility | Activation-time **immutable** plan baseline snapshot (§7.5a); plan-vs-current karşılaştırması; 10 diff type; 3 **read-only** query endpoint'i + filtreler; Resource Assignments sayfasında read-only compact pill Plan vs Current tab + Golden Compact DataTable v2 Save View; replacement/transfer reason & provenance görünürlüğü; position-based eşleştirme | FU04A, FU02B | **Resource assignment mutasyon davranışının değişmesi**; FU05 account apply/history; Account/Contact mutasyonu; workflow/FU06; evidence/FU07; import-export/FU08; visit-route/FU09; Brand/Product master; hard delete; seed/publish; **yeni bağımsız menü sayfası**; diff projection cache |
| **FU05** | Account assignment apply + history | `AccountTerritoryAssignment`; active-model-only, all-or-nothing apply; effective dating; eski atama `ended` (**silinmez**); reason zorunlu override; model/account history; current query; **ayrı MOD-0149 CoverageSummary projection**; apply/history UI | FU03, FU02B | Account/Contact/resource mutasyonu; workflow/aktivasyon; evidence/import-export; visit/route; Brand/Product master |
| **FU05A** | CoverageSummary model lifecycle guard | Current CoverageSummary/coverage query yalnız operationally valid model (active + effective-window + arşiv/inactive/superseded değil) üzerinden döner; deactivated/archived modele bağlı atamalar current'tan düşer, history'de korunur; `effectiveAt` = o tarihte active model+assignment; **yalnız read projection filtresi**, mutasyon yok; contact-derived coverage prerequisite'i | FU05 | Assignment `ended`/mutasyon; Account/Contact mutasyonu; ContactTerritoryAssignment; workflow/approval/FU06; hard delete; seed/publish; evidence/import-export; visit/route |
| **FU05B** | Versioned draft clone + account carry-forward | Active/inactive modelden metadata + hierarchy + rule draft clone; activation-time fail-closed node remap; atomic source end + target create; provenance/history continuity; Create draft version UI | FU02B, FU03, FU05, FU05A | Active rule mutation; Account/Contact master mutation; resource carry-forward; workflow approval/FU06; hard delete |
| **FU06** | Workflow approval + approval-governed activation + Change Approval Trace | MOD-0023 Start Instance; submit/approve/reject; Transition Gate; `TerritoryChangeRequest`; approval trace; evidence-backed activation; **approval-based immutable lifecycle**; before/after diff; Change Approval Trace sayfası | FU01–FU05, **FU02B**, **MOD-0023** | FU02B manual lifecycle, fake approval, bypass flag |
| **FU07** | Evidence Pack + audit/evidence export | `TerritoryEvidencePack` üretimi; Evidence Pack sayfası; export; MOD-0021 audit event wiring; correlation id | FU06, MOD-0021 | MOD-0031 genel evidence store |
| **FU08** | Import/export hardening | XLSX export (model/node/hiyerarşi/BU scope/rule/account assignment current+history/CoverageSummary/resource current+history/Plan vs Current); çok-sheet import template; **dry-run-first** satır bazlı validation raporu (blocking/non-blocking); sheet-level safe apply; idempotency + file hash + `ImportRunId` provenance; read-only `TerritoryImportRun` history; Import/Export sayfası (MOD-0150 deseni) | FU05, **FU05A** *(FU07 hard prerequisite **değil** — 2026-08-01 reconciliation, §22.1)* | **Yeni import framework**; CoverageSummary import; Plan vs Current import; **resource assignment apply (FU08A)**; FU03/FU04A/FU05 guard bypass'ı; workflow/FU06; evidence/FU07; visit-route/FU09; campaign/frequency; Brand/Product master; hard delete; seed/publish |
| **FU09A** | Visit/route readiness: coverage, contact availability and frequency input boundaries | **Yalnız-okuma** readiness: account coverage readiness (FU05A guard'lı); node coverage accounts; resource/MR responsibility readiness; `AccountContactLink` üzerinden derived contact coverage (çoklu satır + provenance); §7.12 route **candidate** readiness projeksiyonu; stabil reason code sözleşmesi; contact availability (MOD-0150), frequency/call-cycle policy (MOD-0165/0167 → MOD-0155) ve last-visit/due-overdue (MOD-0155) **sahiplik boundary'leri**; contract readiness flag'leri | FU05, **FU05A**, FU04A/FU04B, MOD-0150 (`AccountContactLink`) | **Rota/route optimizasyonu; günlük route planı; visit plan/execution; check-in-out/GPS; visit report; digital detailing; survey; campaign engine; frequency engine; contact availability master implementation; visit history/last-visit yazımı; cadence compliance; coverage roll-up (FU09)**; Account/Contact mutasyonu; `ContactTerritoryAssignment`; patient data; workflow/FU06; evidence/FU07; hard delete; seed/publish; yeni permission literal'i; yeni sayfa/menü |
| **FU09** | MOD-0155 readiness APIs (kalan kapsam) | Coverage roll-up (§15 boyutları); microzone account listesi genişletmeleri; MOD-0155 entegrasyon teslimi | FU05, **FU05A**, **FU09A** *(FU07 hard prerequisite **değil** — 2026-08-01 reconciliation, §22.1)* | Forecast (MOD-0154), visit/route planning implementation (MOD-0155) |
| *(future)* | AI Assist FU | Territory önerisi / conflict summarize / recommend | MOD-0066/0067/0068/0069/0041 **hard gate** | D5: v1 kapsam dışı |

---

## 23. Risks / Open Follow-ups

| # | Follow-up | Owner | Neden | FU01'i bloklar mı? |
|---|---|---|---|---|
| F1 | **MOD-0048 Territory Reference Set Authoring Template** (§16 setleri) — ✅ **COMPLETED 2026-07-23 (template); publish pending → F10.** Çıktılar: [authoring template md](../reference-data/mod-0151-territory-required-reference-authoring-template.md) · [json](../reference-data/mod-0151-territory-required-reference-authoring-template.json) · [operator checklist](../reference-data/mod-0151-territory-reference-operator-checklist.md) | commercial-suite / MOD-0048 operator | MOD-0149/0150 precedent'i: pack → authoring template → operator publish | **Hayır (artık)** — template tamam; FU01 kod yazımını bloklamaz |
| F10 | **MOD-0048 Territory Reference Set Publish Operator Runbook** — gerçek publish operator aksiyonudur | MOD-0048 operator | 10 required set publish edilmeden **canlı** create/activate çalışmaz | **Kısmen:** FU01 **kod + fail-closed testleri** için hayır; FU01 **canlı create smoke** için **evet** |
| F11 | **Registry drift düzeltmesi** — `module-implementation-status.md` satır 75 "MOD-0023 Workflow + MOD-0024 Task — not built (0%)" diyor; oysa MOD-0023 runtime **mevcut** (WorkflowDefinitionsController, StartWorkflowInstanceCommand, IWorkflowTransitionGate, gateway `/api/v1/workflow/{everything}`). Bu satır FU06'yı yanlışlıkla "bloklu" gösterir | registry / governance owner | FU00 source reconciliation bulgusu; bu task registry'yi **değiştirmez** | Hayır (FU06 planlamasını etkiler) |
| F2 | `crm-rbac-integration-plan.md` **supersede update** — `crm.micro-zone.manage` kaldır; `assign-rep`/`assign-account` stilindeki eski anahtarları model/node/assignment/resource/approval/evidence anahtarlarıyla değiştir; §6 ABAC matrisindeki "MicroZone assignment ayrı izin" satırını güncelle | commercial-suite governance | D7; yanlış mimari sinyali | Hayır (pack §17 zaten canonical) |
| F3 | `crm-sor-boundary.md` **update** — Production Admin = non-sales resource-planning scope; **BusinessScope ↔ Territory ayrımı**; MOD-0151'in `CoverageSummary` projection borcu | commercial-suite governance | D2 + §9 ayrımı SoR matrisinde yok | Hayır |
| F4 | **MOD-0018 follow-up** — platform Territory data-scope desteği; `EntitlementDataScopeKind` territory extension (`Region=10` bugün kullanılmıyor, `Territory` yok) | MOD-0018 / Platform | D4: v1 CrmService-level filter, kalıcı çözüm platform tarafında | Hayır (v1 tasarımı bunu gerektirmiyor) |
| F5 | **MOD-0288 follow-up** — `OrganizationUnit.unitType` / business scope sınıflandırması; `PersonRef` / `PositionRef` readiness | MOD-0288 / Platform | §9: BU master MOD-0151'de olamaz; bugün `unitType` yok | Hayır (FU04'te seam yeterli); **FU04 için önemli** |
| F6 | **Product / Brand master follow-up** — `CAND-CAP` Product / Brand / ProductPortfolio master capability candidate | EA / MDM | D3: portfolio↔brand mapping MOD-0151'in kalıcı sahipliği değil | Hayır |
| F7 | **HOC / Commercial Manager scope policy** tanımı (policy-driven davranışın kesin kuralı) | commercial-suite / EA | §10: bu iki rolün exact rule'u policy-driven bırakıldı | Hayır (FU04'te netleşmeli) |
| F8 | **EA governance note** — Blueprint `Buy/Partner` vs in-house build sapması | EA | D6 | Hayır |
| F9 | **MOD-0151 FU01 implementation prompt** — ✅ completed | module-pack-author / orchestrator | FU01 backend ve live-smoke raporları PASS | Hayır |
| F12 | **FU02A BusinessScopes dependency** — normalized, unordered BU scope setinin persist ve compare sözleşmesi | MOD-0151 FU02A | Single-active-model guard'ın tam anahtarıdır; eksikse FU02B PARTIAL | **FU02B'yi bloklar** |
| F13 | **FU02B lifecycle hardening** — manual lifecycle, computed expiry, draft soft-delete, audit ve test/evidence | MOD-0151 | Status alanlarını workflow öncesinde operasyonel hale getirir | Yetkilendirildi |
| F14 | **FU06 workflow approval + activation** | MOD-0151 + MOD-0023 | Submit/approve/reject, transition gate, approval trace ve immutable approved lifecycle ayrı kalır | Future |
| F15 | **Background expiry scheduler** | MOD-0151 future hardening | v1 computed expiry; DB mutation/scheduler yok | Future |
| F16 | **Brand Scope + Product/Brand master integration** | Brand/Marketing + MDM future capability | Brand master oluşmadan MOD-0151'e erken sahiplik verilmez | Future |
| F17 | **MOD-0155 visit/route readiness** | MOD-0155 / FU09 | Coverage/readiness entegrasyonu lifecycle FU'sunun dışında | **Kısmen kapatıldı** — readiness/boundary kısmı FU09A ile yetkilendirildi (§22.6, F21); coverage roll-up + MOD-0155 entegrasyon teslimi hâlâ future |
| F18 | **Evidence Pack** | MOD-0151 FU07 + MOD-0021 | FU02B audit event'i üretir; evidence pack üretmez | Future |
| F19 | **FU05A CoverageSummary model lifecycle guard** — FU05 live smoke'ta bulunan boşluk; current coverage yalnız operationally valid model üzerinden dönmeli (§22.2a). Ayrıca §17'deki RBAC katalog follow-up'ı çakışmayı önlemek için `FU05A → FU05-RBAC` olarak yeniden etiketlendi | MOD-0151 FU05A | Yanlış current coverage FU09/MOD-0155 ve MR account/doktor listelerini etkiler; guard FU09'dan önce kapanmalı | Yetkilendirildi (contact-derived coverage prerequisite) |
| F20 | **FU08 Import/Export hardening** — büyük territory modelleri ve atamaları elle girilemez; MOD-0150 template → export → **dry-run** → safe apply deseni MOD-0151 nesnelerine uygulanır (§22.5). İki alt follow-up doğurur: **FU08-RBAC** (`crm.territory.import` / `crm.territory.export` katalog hizalaması) ve **FU08A** (resource assignment import **apply**, FU04A lifecycle sözleşmesiyle) | MOD-0151 FU08 | Toplu veri girişi olmadan modül operasyonel ölçeğe çıkamaz; FU08 hiçbir FU03/FU04A/FU05 guard'ını bypass etmez | Yetkilendirildi (FU06/FU07'den bağımsız) |
| F21 | **FU09A Visit/Route readiness boundaries** — MOD-0155 öncesi coverage/resource/derived-contact readiness + route **candidate** projeksiyonu + reason code sözleşmesi (§22.6). Üç bağlı follow-up doğurur: **`MOD-0150-FU — Contact Availability and Visit Preference`** (`AccountContactLink` bazlı availability master; alternatif etiket `MOD-0155-PREREQ — Contact Availability and Visit Preference Readiness`), **`VisitFrequencyPolicy / CallCyclePolicy` sahiplik kararı** (üretici MOD-0165/MOD-0167, tüketici MOD-0155) ve **MOD-0155 last-visit / due-overdue engine** | MOD-0151 FU09A + MOD-0150 + MOD-0155 | Eski CRM/Campaign'in frequency, çalışma günü/saati ve son ziyaret bilgisi yeni mimaride **sahipsiz kalırsa** visit planning kurulamaz; `legacy-value-preservation.md` "Frequency verisi nereden beslenecek?" EA-TBD sorusunun territory tarafındaki cevabıdır | Yetkilendirildi (yalnız boundary + read-only readiness; MOD-0155 implementasyonundan bağımsız). **Frequency alt follow-up'ı kapatıldı 2026-08-02:** `VisitFrequencyPolicy / CallCyclePolicy` sahipliği [MOD-0165-FU01](MOD-0165-FU01-visit-frequency-call-cycle-policy.md) (SoR/custodian) + [MOD-0167-FU01](MOD-0167-FU01-segment-sourced-frequency-policy-authoring.md) (co-author) pack'lerinde yetkilendirildi; MOD-0151 tarafında **hiçbir değişiklik gerekmez** — provider gelene kadar `FrequencyStatus=unknown` korunur. **MOD-0155 last-visit / due-overdue engine sahipliği hâlâ açık.** |

**Riskler:**
- **R1 — Reference set gecikmesi:** F1 tamamlanmazsa FU01 create/update validation'ı kontrollü 400 döner (MOD-0149/0150
  ile aynı davranış). Kod blocker'ı değil, **runtime validation prereq'i**.
- **R2 — MOD-0023 template konfigürasyonu:** Workflow template tenant'ta tanımlı değilse FU06 aktivasyonu fail-closed
  kalır. Bu **doğru davranıştır**; bypass eklenmemelidir.
- **R3 — HCM olgunluğu:** Position/ManagerChain olgunlaşmadan FU04 resource hiyerarşisi tam doğrulanamaz → seam ile
  ilerlenir (D4/F5).
- **R4 — Business scope master boşluğu:** F5/F6 gecikirse `TerritoryBusinessScope` geçici MOD-0048 setlerine dayanır;
  bu **kabul edilmiş geçici çözümdür**, kalıcı sahiplik değildir.
- **R5 — FU02A dependency drift:** `BusinessScopes` persist/normalize sözleşmesi hazır değilse scope eşitliği eksik
  hesaplanır; FU02B implementasyonu single-active-model kriterini tam karşılayamaz ve verdict **PARTIAL** olur.
- **R6 — Computed/stored status karışması:** v1 expiry yalnız read-time computed state'tir. Scheduler veya sessiz DB
  status mutation eklemek scope ihlalidir.
- **R7 — Import ile guard bypass'ı:** import'un "hızlı yol" olarak ikinci bir yazma kanalına dönüşmesi en büyük FU08
  riskidir (örn. active modele node basmak, FU05 conflict guard'ını atlamak, resource assignment'ı doğrudan `active`
  yaratmak). Kontrol: import bir **taşıma yolu**dur; her satır mevcut guard'lardan geçer, resource apply v1'de
  **kapalıdır** (FU08A) ve dry-run blocking/non-blocking ayrımı zorunludur (§22.5).
- **R8 — Sessiz toplu overwrite:** boş hücrenin "temizle" sayılması veya boş `Operation`'ın "update" sayılması bir
  dosyayla tüm planı bozabilir. Kontrol: boş hücre = **değiştirme**, açık `<CLEAR>` token'ı = temizle, boş
  `Operation` = **skip**; hard delete yok; idempotency doğal anahtar üzerinden.
- **R9 — Readiness'in gizli route planner'a dönüşmesi:** "candidate listesine bir sıralama/skor eklesek" talebi
  MOD-0155'in sahipliğini sessizce MOD-0151'e taşır ve iki yerde iki farklı planlama mantığı doğurur. Kontrol:
  response'ta sıra/mesafe/süre/gün planı/optimizasyon skoru **yok**; readiness yalnız sinyal + reason code verir
  (§22.6); guard testi bu alanların yokluğunu doğrular.
- **R10 — Availability/frequency'nin yanlış yere gömülmesi:** contact availability'yi Contact üzerine tek alan
  olarak, frequency'yi Contact/Account üzerine düz alan olarak yazmak en olası kısayoldur; ikisi de **çok-account'lu
  doktor** ve **campaign dönemli hedef** gerçeğini kaybeder ve geri dönüşü pahalıdır. Kontrol: availability
  `AccountContactLink` bazlı ve **MOD-0150**'ye, frequency ayrı `VisitFrequencyPolicy` olarak
  **MOD-0165/MOD-0167**'ye aittir; MOD-0151 hiçbirini persist etmez (§22.6).
- **R11 — "Veri yok" ile "uygun değil"in karıştırılması:** availability/frequency/last-visit girdisi henüz yokken
  hedefin candidate dışına düşürülmesi, MOD-0155 başlamadan saha kapsamının **sessizce daraldığı** izlenimi yaratır.
  Kontrol: eksik girdi `unknown` + ayrı reason code ile **görünür** kalır; varsayılan sıklık veya varsayılan
  müsaitlik **uydurulmaz**.

---

## 24. Acceptance Criteria for Pack Approval

> **GATE EXECUTED — FU00 Pack Approval / Source Reconciliation Closeout, 2026-07-23: PASS.**
> Kanıt: [mod-0151-fu00-pack-approval-closeout-2026-07-23.md](../../../../docs/audits/mod-0151-fu00-pack-approval-closeout-2026-07-23.md)

- [x] §4 kararları (D1–D7) reviewer tarafından **onaylandı** (pack prep'te alınmıştı; pack'te kayıt altına alındı).
- [x] Blueprint alignment doğrulandı: MOD-0151 / Territory Management / CRM Core / W-4 / CRM-TERRITORY-BUNDLE /
      3 named soft page / SoR üçlüsü (DCP-002 canonical-name gate PASS).
- [x] §7 domain modeli ve §8 hiyerarşi kararı (tek `TerritoryNode` + `TerritoryLevel`) kabul edildi.
- [x] §9 business scope ayrımı (Alpha/Beta/Gamma sales, Production Admin non-sales) kabul edildi.
- [x] §16 reference set listesi kabul edildi ve **F1 authoring template** tamamlandı (10 required set / 62 value).
- [x] §17 permission listesi kabul edildi; `crm.micro-zone.manage` ve `crm.territory.delete` **önerilmediği** teyit edildi.
- [x] §13 workflow tasarımı kabul edildi; **fake approval / bypass yasağı** teyit edildi.
- [x] §21 integration boundary'leri (özellikle Account/Contact'a territory alanı eklenmemesi) teyit edildi.
- [x] §22 FU sırası kabul edildi; FU01, FU02, FU02A ve FU02B `runtime_code_scope` frontmatter'a yazıldı.
- [x] FU02B lifecycle/computed-expiry/draft-soft-delete scope'u FU06 workflow approval sınırından ayrıldı.
- [x] FU03, FU04 ve **FU05-account-assignment-apply-history** additive runtime scope'ları yetkilendirildi; FU05
      Account/Contact SoR mutasyonu yapmadan apply/history/CoverageSummary sağlar ve FU06–FU09 sınırlarını korur.
- [x] **FU04A-resource-assignment-lifecycle-replacement-operational-visibility** additive runtime scope'u
      yetkilendirildi; position-based lifecycle/current/history kararları ve FU05–FU09 sınırları kayda geçti.
- [x] **FU05A-coverage-summary-model-lifecycle-guard** additive runtime scope'u yetkilendirildi (§22.2a); FU05 live
      smoke'ta bulunan CoverageSummary model-lifecycle boşluğu yalnız-okuma current-projection guard'ı ile kapatılır;
      history/current ayrımı + `effectiveAt` policy netleşti; contact-derived coverage prerequisite'i kayda geçti;
      mutasyon/workflow açılmadı ve `supportsWorkflowActivation=false` korundu.
- [x] **FU08-import-export-hardening** additive runtime scope'u yetkilendirildi (§22.5); import/export object scope'u
      (neyin export/template/dry-run/apply edileceği) nesne bazında netleşti; **dry-run-first** ve sheet-level safe
      apply policy'si yazıldı; account assignment import'unun FU05 guard'larını, resource assignment tarafının FU04A
      lifecycle'ını **bypass edemeyeceği** kayda geçti (resource apply → FU08A); CoverageSummary ve Plan vs Current
      **import dışı** bırakıldı; read-only `TerritoryImportRun` history sözleşmesi tanımlandı; FU08-RBAC follow-up'ı
      açıldı; workflow/visit-route/campaign açılmadı ve `supportsWorkflowActivation=false` korundu.
- [x] **FU09A-visit-route-readiness-boundaries** additive runtime scope'u yetkilendirildi (§22.6); MOD-0155 öncesi
      **yalnız-okuma** coverage/resource/derived-contact readiness ve route **candidate** readiness projeksiyonu
      (§7.12) tanımlandı; stabil reason code sözleşmesi yazıldı; contact availability (**MOD-0150**,
      `AccountContactLink` bazlı), visit frequency / call-cycle policy (**MOD-0165/MOD-0167** üretir, **MOD-0155**
      tüketir) ve last visit / due-overdue (**MOD-0155**) sahiplik sınırları kayda geçti; **rota / route optimizasyonu
      / visit plan / campaign-frequency engine / contact availability master açılmadı**; Account/Contact mutasyonu,
      `ContactTerritoryAssignment` ve patient data açılmadı; yeni permission literal'i ve yeni sayfa/menü eklenmedi;
      `supportsWorkflowActivation=false` korundu.
- [x] F1 (MOD-0048 authoring template) tamamlandı → `runtime_code_allowed` **`true`** (adlandırılmış FU scope'larıyla).
- [ ] `form_field_count` FU01 authoring sırasında hesaplanır (TerritoryModel ≈ 12, TerritoryNode ≈ 16) →
      Golden Reference **Compact** teyidi. *(FU01-time item; FU00'ı bloklamaz.)*
- [ ] **F10 operator publish** — 10 required set canlıya alınmadan FU01 **canlı create smoke** çalıştırılamaz.
      *(FU01 kod yazımını ve fail-closed testlerini bloklamaz.)*

---

## 25. Next Recommended Prompt

1. **`@orchestrator MOD-0151 FU04A — Resource Assignment Lifecycle, Replacement and Operational Visibility Hardening`**:
   yalnız §22.3 allowed scope; position-based lifecycle; active create/end; atomik replace/transfer;
   current/history; conflict/override hardening; Account/Contact/FU05 ve FU06–FU09 sınırları korunur.
2. **`@orchestrator MOD-0151 FU05A — CoverageSummary Model Lifecycle Guard`**: yalnız §22.2a allowed scope;
   current CoverageSummary/coverage query'lerini model lifecycle status ile filtrele (active + effective-window,
   arşiv/inactive/superseded değil); history'yi koru; `effectiveAt` policy'sini uygula; mutasyon/Account/Contact/
   ContactTerritoryAssignment/workflow açma; `supportsWorkflowActivation=false` korunur.
3. **`@orchestrator MOD-0151 FU08 — Import/Export Hardening`**: yalnız §22.5 allowed scope; XLSX export + çok-sheet
   template + **dry-run-first** satır bazlı validation + sheet-level safe apply + read-only `TerritoryImportRun`
   history + Import/Export sayfası. Account assignment import'u FU05 guard'larının aynısını kullanır; resource
   assignment v1'de yalnız export/template/dry-run'dır (apply → FU08A); CoverageSummary ve Plan vs Current import
   edilmez; yeni import framework yazılmaz; `supportsWorkflowActivation=false` korunur.
4. **`@orchestrator MOD-0151 FU09A — Visit/Route Readiness: Coverage, Contact Availability and Frequency Input
   Boundaries`**: yalnız §22.6 allowed scope; **read-only** coverage readiness (FU05A guard'lı) + resource/MR
   responsibility readiness + `AccountContactLink` üzerinden derived contact coverage + §7.12 route **candidate**
   readiness + reason code sözleşmesi. **Rota/route optimizasyonu, günlük plan, visit plan/execution, GPS,
   visit report, campaign/frequency engine, contact availability master ve visit history açılmaz**; hiçbir mutasyon,
   yeni master aggregate, yeni permission literal'i veya yeni sayfa eklenmez; `supportsWorkflowActivation=false`
   korunur.
5. **`MOD-0150-FU — Contact Availability and Visit Preference Pack Authorization`**: `AccountContactLink` bazlı
   availability / visit preference master'ı MOD-0150'de yetkilendir (alternatif etiket
   `MOD-0155-PREREQ — Contact Availability and Visit Preference Readiness`). **✅ Tamamlandı 2026-08-01** (MOD-0150 §20).
   Aynı zincirde `VisitFrequencyPolicy / CallCyclePolicy` sahipliği (MOD-0165 / MOD-0167) **✅ yetkilendirildi
   2026-08-02** — [MOD-0165-FU01](MOD-0165-FU01-visit-frequency-call-cycle-policy.md) (aggregate SoR + provider
   contract) ve [MOD-0167-FU01](MOD-0167-FU01-segment-sourced-frequency-policy-authoring.md) (segment co-author);
   MOD-0155 last-visit / due-overdue engine sahipliği **hâlâ ayrı bir authorization olarak açıktır** (§22.6, F21).
6. Permission katalog hizalaması gerekiyorsa ayrı
   **`MOD-0151 FU04A-RBAC — Resource Assignment Permission Catalog Alignment`**,
   **`MOD-0151 FU05-RBAC — Assignment RBAC Permission Catalog Alignment`** ve
   **`MOD-0151 FU08-RBAC — Import/Export Permission Catalog Alignment`** follow-up'larını hazırla.
5. Resource assignment import **apply**'ı gerekiyorsa ayrı
   **`MOD-0151 FU08A — Resource Assignment Import Apply`** authorization'ını hazırla (FU04A proposed/active ayrımı ve
   reason/provenance zorunluluğu korunmak şartıyla).
6. FU06 workflow approval + approval-governed activation için ayrı pack/runtime gate'i future olarak koru.
