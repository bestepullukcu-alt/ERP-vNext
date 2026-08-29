---
id: MOD-0155-FU01
name: Visit Planning / Planned Visit
parent: MOD-0155
parent_name: Field Sales / Visit Planning
siblings: MOD-0155-FU02 (Visit Report), MOD-0155-FU03 (Route Planning), MOD-0155-FU04 (Visit Content Sequence Execution), MOD-0155-FU05 (MicroTarget)
domain: commercial-suite
service: Diten.CrmService + frontend/Diten.Web
shell: tenant
golden_reference: compact
entity_base: EntityBase
status: draft
runtime_code_allowed: false
runtime_code_scope: "NONE (draft). Bu pack preservation/hazırlık dokümanıdır; `ready-for-dev` + `runtime_code_allowed: true` flip'i AYRI bir kullanıcı kararıdır (build-lane: pack erken, impl geç). Flip sonrası kapsam: `PlannedVisit` aggregate runtime (CRUD-minus-delete + archive + plan penceresi effective-dating + in-domain vokabüler + contract + read-only frequency/consent/journey provenance) `Diten.CrmService` içinde VE CRM → Field Sales → Planned Visits TEK Compact sayfası `frontend/Diten.Web` içinde. Schedule engine, route optimizer, otomatik plan üretimi, visit execution/check-in/GPS, visit report, MicroTarget, content sequence execution, MOD-0151/0149/0150/0162/0164/0165 aggregate mutation, MDM write, Gateway config yazımı, RBAC seed/grant, MOD-0048 publish, registry write ve Mongo hand-edit YASAKTIR."
owner: module-pack-author
branch: feature/crm/mod-0155-fu01-visit-planning-planned-visit
started: 2026-08-26
target: TBD (kullanıcı onayı + ready-for-dev flip sonrası)
form_field_count: 18
dependencies:
  - MOD-0155 (parent — Field Sales / Visit Planning; SoR = "visit plans")
  - MOD-0149 (read-only — Account / WorkPlace master; "kim/nerede". Mutate YOK)
  - MOD-0150 (read-only — Contact + AccountContactLink + ContactAvailability; "kim / ne zaman müsait". Mutate YOK)
  - MOD-0151 (read-only — Territory/MicroZone tanımı + FU09A/B route-candidate readiness projeksiyonu. MicroZone TANIMLANMAZ, tüketilir)
  - MOD-0165-FU03 (read-only — IVisitFrequencyPolicyResolver; "ne sıklıkla". SHIPPED. İmza DEĞİŞMEZ)
  - MOD-0164-FU02 (read-only — IConsentPreferenceEvaluator; "temas edilebilir mi". SHIPPED. FilterApplied guard'ı onurlandırılır)
  - MOD-0162-FU05 (read-only, OPSİYONEL — IContentEngagementJourneyReader; "hangi aşama". Seam yayınlandı, tüketim burada ilk kez açılır)
  - MOD-0048 (reference data — D-VOCAB=A: runtime ön koşulu DEĞİL; set publish ayrı operatör işi → F-RD)
  - MOD-0018 (RBAC — yalnız tüketim; seed/grant bu pack'te YOK → F-RBAC)
  - MOD-0018-FU15 (BLOKE — Real DataScopeResolver planned/reserved; field-force ABAC scoping bu FU'da AÇILAMAZ, §8.6)
  - MOD-0288 (boundary — Person/Position master; ResourceId string referanstır, Guid FK AÇILMAZ, §4.3/D4)
  - DEV-0001 (Golden Reference Compact — tek yüzey, tek klasör)
---

# MOD-0155-FU01 — Visit Planning / Planned Visit

> **⛔ DRAFT — KOD YETKİSİ YOKTUR (`runtime_code_allowed: false`).**
> Bu pack, MOD-0155 (Field Sales / Visit Planning) ailesinin **foundation FU**'sudur ve build-lane
> `crm-field-sales-extension`'ın **"pack erken / impl geç"** önceliği gereği bir **preservation + hazırlık**
> dokümanıdır. `status: ready-for-dev` + `runtime_code_allowed: true` flip'i **AYRI bir kullanıcı kararıdır**;
> bu pack o kararı vermez ve `@orchestrator` bu pack ile kod yazamaz.
>
> **DCP-002 kimlik kapısı — PASS (2026-08-26):**
> `py .antigravity/scripts/verify_module_id.py . --check-id MOD-0155-FU01 --name "Visit Planning / Planned Visit" --parent MOD-0155`
> → `OK  MOD-0155-FU01: proven against Blueprint/registry.` (**exit 0**).
> Parent `MOD-0155 | Field Sales / Visit Planning` registry'de canonical (`module-id-registry.md` satır 226,
> `reserved / planned`). **Registry satırı bu pack tarafından EKLENMEZ** (MOD-0165-FU01 emsali) → §20/F-REG.
>
> **Neden şimdi — bloklayan EA sorusu KAPANDI.** 2026-07-31 CRM capability review'ı MOD-0155 pack'ini açıkça
> bloklamıştı: *"Frequency veri kaynağı EA-TBD açık soru — legacy tablo mu, yeni cadence config mi? **Karar
> verilmeden MOD-0155 pack'i yazılamaz**"* (`crm-capability-progress-review-2026-07-31.md` satır 294). Bu soru o
> tarihten sonra **kapandı**: MOD-0165-FU01 frequency sahipliğini `VisitFrequencyPolicy` aggregate'ine verdi ve
> **MOD-0165-FU03 resolver'ı canlıya aldı** (`IVisitFrequencyPolicyResolver` — kod üzerinden doğrulandı). Kalan
> iki EA sorusu (**Daywork/VisitMix**, **HCP identity SoR**) bu FU'yu **bloklamaz**; gerekçesi §19.2'de satır
> satır yazılıdır.
>
> **Legacy değeri.** MOD-0155, legacy pharma sisteminin **en olgun ve kurala en zengin** alanıdır
> ([legacy-value-preservation.md](../legacy-value-preservation.md)). Bu pack'in **§21**'i legacy iş kurallarını
> **"legacy business rules (frozen reference)"** olarak kayda geçirir. Kod/controller/view **taşınmaz**; yalnız
> kural çıkarılır.
>
> Otorite sırası: **Blueprint Excel** > bu pack > [Domain Config](../domain-config.md) >
> [crm-sor-boundary.md](../crm-sor-boundary.md) > `AGENTS.md` > `.antigravity/rules/`.

---

## 1. Module Summary

MOD-0155-FU01, saha ekibinin **planlama** çekirdeğini kurar: **`PlannedVisit`** aggregate'i — *"**kim** ziyaret
edilecek, **ne zaman**, **hangi amaçla**, **kim tarafından**, **hangi tenant'ta**?"*

Bu FU **yalnız planlama foundation'ıdır**. Bir plan satırı; hedefini (account / contact / account-contact-link),
tarihini ve saat penceresini, amacını, atanan saha kaynağını ve — **yalnız provenance olarak** — o an geçerli
frequency policy'sini, consent verdict'ini ve (opsiyonel) içerik yolculuğu aşamasını taşır.

**Hedef kullanıcı:** saha yöneticisi (plan kuran/onaylayan), saha temsilcisi (kendi planını gören), CRM admin.

**Kapasite özeti:** `PlannedVisit` CRUD-minus-delete + archive + `confirm`/`cancel` geçişleri, plan penceresi
üzerinden effective-dating (D1), in-domain fail-closed vokabüler (D2), read-only contract yüzeyi, legacy'den
yakalanan **çakışma** ve **aynı-gün-aynı-tip** kurallarının handler-level guard'ı (§21/L5–L6) ve tek Compact
yönetim konsolu.

**Bu FU bir MOTOR DEĞİLDİR (D8).** Plan **üretmez**, sıralamaz, optimize etmez, rota çıkarmaz, ziyaret
gerçekleştirmez, rapor toplamaz. Frequency'yi **okur** (ne sıklıkla), consent'i **sorar** (temas edilebilir mi),
journey aşamasını **bağlar** (opsiyonel) — ama hiçbirini **hesaplamaz** ve hiçbirinin verisini **kopyalamaz**.

---

## 2. Ownership and Boundaries

### 2.1 Kapsam kararı ve beyan edilen varsayım

| Kapsam | Karar |
|---|---|
| **In-scope** | `PlannedVisit` aggregate + repository + CQRS + persistence + 6 API endpoint + contract yüzeyi (`Diten.CrmService`) **ve** CRM → Field Sales → Planned Visits **tek** Compact konsolu (`frontend/Diten.Web`) |
| **Out-of-scope (bu FU'da AÇIKÇA ERTELENİR)** | Route Planning (**FU03**) · Visit Report / ActivityReport (**FU02**) · Visit Content Sequence Execution (**FU04**) · MicroTarget (**FU05**) · schedule/route **engine** · otomatik plan üretimi · visit execution / check-in / GPS · digital detailing · survey · efor/time-entry (MOD-0280 SoR) |

> **Beyan edilen varsayım (kullanıcı onayına açık).** Kullanıcı kapsam cümlesi backend için açıkça
> `Diten.CrmService` dedi; kalite şartı ise `golden_reference`'ın **kullanıcı-form alanından türetilip
> gösterilmesini** ve §11'de **dosya setinin enumere edilmesini** istedi. Bu ikisi ancak bir UI yüzeyi
> sözleşmeye bağlanırsa anlamlıdır. Bu yüzden UI yüzeyi burada **sözleşme olarak** tanımlandı (§9/§11).
> Backend-only teslim tercih edilirse §9+§11 **olduğu gibi** bir `MOD-0155-FU01-UI` pack'ine taşınır ve
> aggregate sözleşmesi (§4) hiç değişmez. Bu, `ready-for-dev` flip'inde verilecek bir kapsam kararıdır.

### 2.2 SoR sınırı — sahiplenilen vs. yalnız tüketilen

`crm-sor-boundary.md` *"Visit Plan / MicroTarget / Visit / Visit Report / route plan → **MOD-0155**"* der ve
*"MicroZone'u **tüketir**, tanımlamaz"* diye ekler. Bu FU o satırın **yalnız "Visit Plan"** kısmını açar.

| Nesne | Sahip | Bu FU'da |
|---|---|---|
| `PlannedVisit` (plan satırı) | **MOD-0155** | **AÇILIR** — bu FU'nun tek aggregate'i |
| `Visit` (gerçekleşen ziyaret) / check-in / GPS | MOD-0155 | **AÇILMAZ** — FU02 |
| `VisitReport` / ActivityReport | MOD-0155 | **AÇILMAZ** — FU02 |
| `RoutePlan` / rota sırası / geo-proximity öneri | MOD-0155 | **AÇILMAZ** — FU03 |
| `MicroTarget` | MOD-0155 | **AÇILMAZ** — FU05 |
| Visit content sequence execution | MOD-0155 | **AÇILMAZ** — FU04 (tanım MOD-0162-FU01A/FU04'te) |
| `Account` / `WorkPlace` / hiyerarşi | MOD-0149 | **read-only** |
| `Contact` / `AccountContactLink` / `ContactAvailability` | MOD-0150 | **read-only** |
| `TerritoryNode` / `MicroZoneProfile` / resource assignment / readiness | MOD-0151 | **read-only** |
| `VisitFrequencyPolicy` + resolve | MOD-0165 (FU03) | **read-only provider çağrısı** |
| `ConsentRecord` / `PreferenceRecord` + evaluate | MOD-0164 (FU02) | **read-only provider çağrısı** |
| `ContentEngagementJourney` + stage | MOD-0162 (FU05) | **read-only reader çağrısı, OPSİYONEL** |
| `Campaign` / `CampaignTarget` | MOD-0165 | **yalnız `CampaignId` bağlam anahtarı** (§4.5) |
| Segment / TargetCustomer / UCLN | MOD-0167 | **dokunulmaz** |
| Person / Position master | MOD-0288 | **dokunulmaz** — `ResourceId` string referans (D4) |

### 2.3 Kalıcı yasaklar (bu pack bunları kayda geçirir)

```text
Account.PlannedVisit*            ❌  plan asla Account/Contact üstünde düz alan değildir
Contact.NextVisitDate            ❌  (MOD-0165-FU01/D2 ile aynı gerekçe: çoklu bağlam düz alana sığmaz)
AccountContactLink.VisitPlan     ❌
PlannedVisit.VisitFrequency      ❌  frequency KOPYALANMAZ; policy id + provenance saklanır (D5)
PlannedVisit.ConsentStatus       ❌  consent kaydı KOPYALANMAZ; verdict + matched id saklanır (D5/D6)
PlannedVisit.MicroZoneDefinition ❌  MicroZone MOD-0151'de tanımlanır; burada yalnız anahtarla referans verilir
PlannedVisit.RouteOrder          ❌  rota sırası FU03'e aittir; foundation'a sızdırılmaz
PlannedVisit.ActualStartTime     ❌  gerçekleşme FU02'ye aittir; plan ile execution tek dokümana karıştırılmaz
```

---

## 3. Owned Objects

| Katman | Nesne |
|---|---|
| **Entity** | `PlannedVisit` (aggregate root) + gömülü `PlannedVisitResourceRef` · `PlannedVisitFrequencyProvenance` · `PlannedVisitConsentProvenance` |
| **Repository** | `IPlannedVisitRepository` (+ Mongo implementasyonu; class-map kaydı **ZORUNLU** — §19/1) |
| **Commands** | `CreatePlannedVisitCommand` · `UpdatePlannedVisitCommand` · `ArchivePlannedVisitCommand` · `ConfirmPlannedVisitCommand` · `CancelPlannedVisitCommand` |
| **Queries** | `ListPlannedVisitsQuery` · `GetPlannedVisitByIdQuery` · `GetPlannedVisitContractQuery` |
| **DTOs** | `PlannedVisitDto` · `PlannedVisitListDto` · `PlannedVisitContractDto` · provenance DTO'ları (`PlannedVisitModels.cs`) |
| **API endpoints** | §15 tablosu — 6 endpoint, hepsi `/api/crm/planned-visits*` |
| **Frontend route** | `/CRM/PlannedVisits` (tenant shell) |
| **Permissions** | `crm.planned-visit.read` · `crm.planned-visit.manage` · `crm.planned-visit.confirm` (§14) |
| **Vokabüler (in-domain)** | `PlannedVisitTargetType` · `PlannedVisitPurpose` · `PlannedVisitType` · `PlannedVisitStatus` · `PlannedVisitSource` · `PlannedVisitReasonCodes` |
| **AÇIKÇA sahiplenilmeyen** | `Visit` · `VisitReport` · `RoutePlan` · `MicroTarget` · content-sequence execution · schedule engine · plan üreteci |

---

## 4. Entity Fields

`entity_base: EntityBase` (tenant-owned; `TenantId` **JWT claim'inden** server-side çözülür ve request
payload'ında **asla** bulunmaz; soft-delete `IsDeleted`/`DeletedAt`; `Version` **teknik concurrency token**'dır,
iş alanı değildir — `module-pack-standard.md` §14 naming rule).

### 4.1 Kimlik ve hedef (WHO)

| # | Alan | Tip | Zorunlu | Form? | Kural / Not |
|---|---|---|---|---|---|
| 1 | `Id` (PlannedVisitId) | Guid | Evet | ✗ | `EntityBase` |
| 2 | `TenantId` | Guid | Evet | ✗ | Server-resolved; cross-tenant erişim **404** |
| 3 | `VisitCode` | string | Evet | ✓ | Stabil iş anahtarı; tenant içinde **arşivlenmemişler arasında** unique; **rename edilmez** |
| 4 | `TargetType` | string | Evet | ✓ | In-domain: `account` · `contact` · `account-contact-link` (§4.7) |
| 5 | `TargetId` | Guid | Evet | ✓ | `TargetType`'a göre çözümlenir; `Guid.Empty` **yasak** |
| 6 | `AccountId` | Guid? | Koşullu | ✗ **türetilir** | `account` → `TargetId`; `account-contact-link` → link'in AccountId'si (`ContactAvailability` emsali: *"navigation copy, derived, never client-supplied"*) |
| 7 | `ContactId` | Guid? | Koşullu | ✗ **türetilir** | `contact` → `TargetId`; `account-contact-link` → link'in ContactId'si |
| 8 | `AccountContactLinkId` | Guid? | Koşullu | ✗ **türetilir** | Yalnız `account-contact-link` hedefinde dolu (= `TargetId`) |

> **`account-contact-link` en spesifik saha hedefidir** ("Dr. Ayşe + Medicana Beylikdüzü") — MOD-0165-FU01 §6
> spesifiklik sırasıyla birebir hizalıdır. Bir contact birden çok account'a bağlıysa bu **çakışma değil,
> union'dır** (2026-07-31 review, satır 132); plan hangi link üzerinden kurulduğunu **kaybetmez**.

### 4.2 Zaman (WHEN) — plan penceresi = effective window (D1)

| # | Alan | Tip | Zorunlu | Form? | Kural / Not |
|---|---|---|---|---|---|
| 9 | `PlannedDate` | DateOnly | Evet | ✓ | Planın günü. **Bu FU'nun tek zaman eksenidir** (D1) |
| 10 | `PlannedStartTime` | string? | Hayır | ✓ (tek kontrol) | `"HH:mm"` **yerel duvar saati** — `ContactAvailability` emsali; ayrı timezone alanı **açılmaz** |
| 11 | `PlannedEndTime` | string? | Hayır | ✗ (aynı kontrol) | `> PlannedStartTime`; ikisi birlikte verilir ya da ikisi de boş kalır |
| 12 | `PlannedDurationMinutes` | int? | Hayır | ✓ | `> 0`; verilmezse MOD-0150 `AverageVisitDurationMinutes` **gösterilebilir** ama **kopyalanmaz** |

### 4.3 Kaynak (WHO VISITS) — gömülü `PlannedVisitResourceRef` (D4)

| # | Alan | Tip | Zorunlu | Form? | Kural / Not |
|---|---|---|---|---|---|
| 13 | `Resource.ResourceId` | **string** | Evet | ✓ (tek seçici) | **Guid DEĞİL.** MOD-0151 `TerritoryResourceRef.ResourceId` şekliyle birebir aynı — sahibi MOD-0288 / MOD-0018 / HCM'dir |
| 14 | `Resource.ResourceType` | string | Evet | ✗ (seçiciden gelir) | Hangi master'a ait (`person` / `user` / `employee`) — ileride doğru çözümlenebilsin diye saklanır |
| 15 | `Resource.DisplayName` | string | Hayır | ✗ (snapshot) | **Yalnız gösterim** snapshot'ı; **asla** sorgu/eşleşme anahtarı değil |
| 16 | `PositionCode` | string? | Hayır | ✗ (snapshot) | Kapsayan Position'ın kodu — audit/gösterim; Position master **kopyalanmaz** |
| 17 | `PositionId` | Guid? | Hayır | ✗ (snapshot) | MOD-0151 `TerritoryPositionRef` emsali; bu FU'da **doğrulanmaz**, yalnız taşınır |

> **SAHTE-FK YASAĞI (D4).** `ResourceId` bir **string**'tir, çünkü ERP-vNext'te CRM'in doğrulayabileceği bir
> Person/Employee master'ı **yoktur** (MOD-0288 `reserved / planned`). Buraya `Guid ResourceId` açmak, hiçbir
> koleksiyona bağlanmayan ve ileride migrate edilmesi pahalı bir **sahte FK** yaratırdı. MOD-0151 bu tuzağı
> zaten string + snapshot deseniyle çözdü (*"Id in the owning master (MOD-0288 Person / MOD-0018 User / HCM
> Employee)"*); FU01 aynı deseni **birebir** devralır. Aynı gerekçeyle `BrandId` / `ProductId` / `SegmentId`
> alanları bu FU'da **hiç açılmaz** (§20/F-CONTEXT).

### 4.4 Amaç (WHY)

| # | Alan | Tip | Zorunlu | Form? | Kural / Not |
|---|---|---|---|---|---|
| 18 | `VisitPurpose` | string | Evet | ✓ | In-domain (§4.7). Consent sorusunun `Purpose`'una **deterministik** eşlenir |
| 19 | `VisitType` | string | Evet | ✓ | In-domain: `field-visit` · `remote-visit` · `phone` · `digital-detailing` · `event` |
| 20 | `Objective` | string? | Hayır | ✓ | Serbest metin, max 1000 |
| 21 | `Notes` | string? | Hayır | ✓ | Serbest metin, max 2000 |

### 4.5 Bağlam anahtarları (WHERE / WHICH) — **referans-only, mutate YOK**

| # | Alan | Tip | Zorunlu | Form? | Kural / Not |
|---|---|---|---|---|---|
| 22 | `BusinessUnit` | string? | Hayır | ✓ | MOD-0151 BU scope sözlüğüyle **aynı** vokabüler; frequency resolve'a aynen geçirilir |
| 23 | `TerritoryNodeId` | Guid? | Hayır | ✓ | MOD-0151 sahipliğinde **okunur**; düğüm tanımı kopyalanmaz |
| 24 | `TerritoryModelId` | Guid? | Hayır | ✗ **türetilir** | Seçilen düğümün modelinden alınır (gösterim/audit) |
| 25 | `CampaignId` | Guid? | Hayır | ✓ | **Yalnız bağlam anahtarı** — campaign target CRUD'u, cycle hesabı ve kampanya sonucu bu FU'da **YOK** |
| 26 | `ContentEngagementJourneyId` | Guid? | Hayır | ✓ | **Opsiyonel.** Yalnız `published` + effective journey seçilebilir (MOD-0162-FU05 reader kuralı) |
| 27 | `ContentEngagementJourneyStageId` | Guid? | Hayır | ✓ | Yalnız seçilen journey'in **aktif** aşamalarından; journey boşsa **zorunlu boş** |

### 4.6 Yaşam döngüsü ve provenance

| # | Alan | Tip | Zorunlu | Form? | Kural / Not |
|---|---|---|---|---|---|
| 28 | `PlanStatus` | string | Evet | ✓ | `draft` · `planned` · `confirmed` · `cancelled` · `archived` (§12.2 state machine) |
| 29 | `Source` | string | Evet | ✓ | `manual` (FU01'de **tek üreticisi olan** değer) · `campaign` · `route-plan` · `import` · `migration` (**rezerve** — üreticileri FU03 / F-IMPORT / F-MIG) |
| 30 | `CancellationReason` | string? | Koşullu | ✗ (cancel diyaloğu) | `cancelled` geçişinde **zorunlu**; create/edit formunda **değildir** |
| 31 | `ArchivedAt` / `ArchivedBy` | DateTimeOffset? / string? | Hayır | ✗ | Archive aksiyonu doldurur |
| 32 | `CreatedBy` / `UpdatedBy` | string? | Evet / Hayır | ✗ | Standart audit |
| 33 | `CreatedAt` / `UpdatedAt` / `IsDeleted` / `DeletedAt` / `Version` | — | — | ✗ | `EntityBase` |

**Frequency provenance — gömülü `PlannedVisitFrequencyProvenance` (türetilir, ASLA authored):**

| Alan | Tip | Kaynak (`VisitFrequencyResolveResult`) |
|---|---|---|
| `FrequencyStatus` | string | `resolved` / `unknown` / `conflict` / `not_applicable` |
| `SelectedFrequencyPolicyId` | Guid? | `SelectedFrequencyPolicyId` |
| `SelectedPolicyCode` / `SelectedPolicyName` | string? | idem |
| `FrequencyType` / `RequiredVisitCount` / `PeriodType` | string? / int? / string? | idem |
| `SelectionReason` | string? | idem |
| `ReasonCodes` | string[] | `FrequencyReasonCodes` sabitleri |
| `ResolvedAt` | DateTimeOffset | Çözümleme anı |

**Consent provenance — gömülü `PlannedVisitConsentProvenance` (türetilir, ASLA authored):**

| Alan | Tip | Kaynak (`ConsentEvaluationResult`) |
|---|---|---|
| `FilterApplied` | bool | D6 guard'ı — `false` ise **hiçbir uygunluk çıkarımı yapılamaz** |
| `EligibilityStatus` | string | `allowed` / `blocked` / `unknown` / `not_applicable` |
| `Decision` | string | `ConsentDecision` |
| `Channel` / `Purpose` | string | Sorulan soru; kanal **daima** `visit` (§4.7) |
| `MatchedConsentId` | Guid? | idem |
| `MatchedPreferenceIds` | Guid[] | idem |
| `ReasonCodes` | string[] | `ConsentReasonCodes` sabitleri |
| `SelectionReason` | string | idem |
| `EvaluatorVersion` | string | `ConsentEvaluationResult.CurrentEvaluatorVersion` (bugün `mod-0164-fu02.v1`) |
| `EvaluatedAt` | DateTimeOffset | idem |

> **KOPYALAMA YASAĞI (D5).** Yukarıdaki iki blok **provenance**'tır: karar + eşleşen id + evaluator/resolver
> sürümü + zaman. `ConsentStatus`, `LegalBasis`, `PreferenceValue`, policy'nin `EffectiveFrom/To`'su gibi
> **kayıt payload'ı asla kopyalanmaz** — MOD-0165-FU04 `CampaignTarget` emsali (*"Provenance ONLY … No
> ConsentStatus, no PreferenceStatus, no record payload is copied out of MOD-0164"*).

### 4.7 In-domain vokabüler (D2 — fail-closed)

```text
PlannedVisitTargetType : account · contact · account-contact-link
PlannedVisitPurpose    : medical-visit · product-information · training · follow-up ·
                         campaign · service · compliance · other
PlannedVisitType       : field-visit · remote-visit · phone · digital-detailing · event
PlannedVisitStatus     : draft · planned · confirmed · cancelled · archived
PlannedVisitSource     : manual · campaign · route-plan · import · migration
```

Vokabüler `Domain/Entities/PlannedVisit.cs` içinde `static class` olarak yaşar (FU02/FU03/FU04/FU05 emsali);
**set dışı değer → 400**; **hardcoded fallback listesi yasaktır** — tüm dropdown'lar `contract` endpoint'inden
beslenir.

`VisitPurpose` → MOD-0164 `Purpose` **deterministik eşleme** (kanal **daima** `ConsentChannel.Visit = "visit"`):

| `VisitPurpose` | MOD-0164 `Purpose` |
|---|---|
| `medical-visit` · `follow-up` | `medical-visit` |
| `product-information` | `product-information` |
| `training` | `training` |
| `campaign` | `campaign` |
| `service` | `service` |
| `compliance` | `compliance` |
| `other` | `other` |

`TargetType` → MOD-0164 `SubjectType` eşlemesi `ConsentSubjectType` sabitleriyle **birebir aynıdır**
(`account` · `contact` · `account-contact-link` — üçü de MOD-0164'te mevcut; kod üzerinden doğrulandı).

### 4.8 MongoDB index ihtiyacı

| Index | Alanlar | Not |
|---|---|---|
| Tenant + kod | `TenantId` + `VisitCode` | Unique **partial** (arşivlenmemiş + silinmemiş). **`$ne` KULLANILMAZ** — `Filter.Type` / `$lt` ile ifade edilir (§19/6) |
| Liste | `TenantId` + `PlannedDate` + `PlanStatus` | Ana DataTable sorgusu |
| Kaynak günü | `TenantId` + `Resource.ResourceId` + `PlannedDate` | Çakışma ve aynı-gün-aynı-tip guard'larının pre-check'i (§21/L5–L6) |
| Hedef | `TenantId` + `TargetType` + `TargetId` | Hedef bazlı plan geçmişi |
| **YASAK** | iki `DateTimeOffset` alanının **birlikte** index'lenmesi/sort'u | CRM parallel-arrays 500 dersi (§19/7). `PlannedDate` bu yüzden `DateOnly`'dir |

---

## 5. Repo Scope

**Backend — `services/Diten.CrmService/`:**

```text
src/Diten.CrmService.Domain/Entities/PlannedVisit.cs                  (aggregate + gömülü tipler + vokabüler)
src/Diten.CrmService.Domain/Repositories/IPlannedVisitRepository.cs
src/Diten.CrmService.Application/Features/PlannedVisit/**             (§10 klasör sözleşmesi)
src/Diten.CrmService.Infrastructure/Persistence/PlannedVisitRepository.cs
src/Diten.CrmService.Infrastructure/Persistence/DependencyInjection.cs   (YALNIZ class-map + index kaydı eklenir)
src/Diten.CrmService.Api/Controllers/CRM/PlannedVisitsController.cs
src/Diten.CrmService.Api/Models/CRM/PlannedVisitRequests.cs
tests/Diten.CrmService.Application.Tests/PlannedVisit/**
```

**Frontend — `frontend/Diten.Web/`:**

```text
Controllers/CrmPlannedVisitsController.cs                             (same-origin proxy)
Views/CRM/PlannedVisits/**                                            (§11.2 — 9 dosya)
wwwroot/assets/js/CRM/PlannedVisits/**                                (3 dosya)
Resources/Views/CRM/PlannedVisits/PlannedVisitsIndex.{ar,en,es,fr,ru,tr,zh}.resx
Resources/SharedResource.{ar,en,es,fr,ru,tr,zh}.resx                  (YALNIZ PlannedVisitsMenu anahtarı eklenir)
Views/Shared/_LayoutTenantShell.cshtml                                (YALNIZ permission-guard'lı tek <li>)
```

**Bu pack (bugün geçerli olan tek yazma alanı):**

```text
execution/domains/commercial-suite/module-packs/MOD-0155-FU01-visit-planning-planned-visit.md
```

---

## 6. Protected Paths

- `.antigravity/**` (global engineering system)
- `gateway/Diten.ApiGateway/**/ocelot.json` — **integration-agent owned**; §15 ayrı task olarak yürütülür
- `frontend/Diten.Web/Views/Shared/_Layout.cshtml` (**FROZEN**)
- `frontend/Diten.Web/Controllers/Archive/**`, `Views/Archive/**` (**FROZEN** — legacy CRM buradan taşınmaz)
- **Tüketilen CRM yüzeyleri — okunur, DEĞİŞTİRİLMEZ:**
  - `Features/VisitFrequencyPolicy/**` (özellikle `IVisitFrequencyPolicyResolver` **imzası**)
  - `Features/ConsentPreference/**` (özellikle `IConsentPreferenceEvaluator` **imzası**)
  - `Features/Knowledge/ContentEngagementJourney/**` (`IContentEngagementJourneyReader` **imzası**)
  - `Features/Territory/**` (readiness projeksiyonu, resource assignment, MicroZone)
  - `Features/Account/**`, `Features/Contact/**`, `Features/AccountContact/**`, `Features/ContactAvailability/**`
  - `Domain/Entities/{Account,Contact,AccountContactLink,ContactAvailability,TerritoryNode,MicroZoneProfile,VisitFrequencyPolicy,ConsentRecord,ContentEngagementJourney}.cs`
- Diğer domain servisleri: `services/Diten.AuthService/**` · `Diten.Platform/**` · `Diten.MdmService/**` ·
  `Diten.HcmService/**` · `Diten.EnterpriseStrategyService/**` · `Diten.DevEnablementService/**`
- RBAC katalog/seed dosyaları ve `rolePermissions` koleksiyonu (**F-RBAC** — bu pack seed yazmaz)
- `execution/registries/**` (**F-REG / F-STATUS** — registry yazımı pack yetkisi dışıdır)
- Mongo hand-edit (**yasak** — GUID subtype hatası tüm login'leri kırar)

---

## 7. Dependencies

| Bağımlılık | Tür | Durum (kod üzerinden doğrulandı) | Bu FU ne yapar |
|---|---|---|---|
| **MOD-0165-FU03** `IVisitFrequencyPolicyResolver` | **read-only, in-process** | **SHIPPED** — `Features/VisitFrequencyPolicy/Resolve/` | `ResolveVisitFrequencyPolicyQuery` ile **çağırır**; imzayı **genişletmez**; HTTP self-call **yapmaz** |
| **MOD-0164-FU02** `IConsentPreferenceEvaluator` | **read-only, in-process** | **SHIPPED** — `Features/ConsentPreference/Evaluation/` | `ConsentEvaluationRequest` ile **sorar**; `FilterApplied` guard'ını **onurlandırır** (D6) |
| **MOD-0162-FU05** `IContentEngagementJourneyReader` | **read-only, OPSİYONEL** | **Seam yayınlandı** (FU05 §8.4) | Yalnız `published` + effective journey/aşama **çözer**; ilerletme/branch değerlendirme **yok** |
| **MOD-0151** readiness + resource assignment | **read-only** | **SHIPPED** — `TerritoryRouteCandidateReadModel` | Aday listesini **gösterir**; `LastVisitDate`/`DueStatus` placeholder'ları FU01 **doldurmaz** (§8.5) |
| **MOD-0149** Account | **read-only** | SHIPPED | Hedef/lokasyon doğrulaması (var mı, aktif mi) |
| **MOD-0150** Contact + `AccountContactLink` + `ContactAvailability` | **read-only** | SHIPPED | Hedef doğrulaması + `AccountId`/`ContactId` türetimi + müsaitlik uyarısı |
| **MOD-0048** Reference Data | **gevşek** | — | **D-VOCAB=A**: runtime ön koşulu **DEĞİL**; set publish ayrı operatör işi (**F-RD**) |
| **MOD-0018** RBAC | **tüketim** | SHIPPED | `[HasPermission]` guard'ları; **seed/grant yok** (**F-RBAC**) |
| **MOD-0018-FU15** DataScopeResolver | **BLOKE** | `planned / reserved` | Field-force ABAC scoping **açılamaz** — §8.6'daki explicit-filtre kararı |
| **MOD-0288** Person/Position | **boundary** | `reserved / planned` | `ResourceId` **string** referans; Guid FK **açılmaz** (D4) |
| **DEV-0001** Golden Compact | **şablon** | SHIPPED | §10/§11 birebir taklit |

---

## 8. Runtime Constraints

**8.1 Persistence.** MongoDB tek instance, logical multi-tenancy. `TenantId` **zorunlu**, server-resolved;
cross-tenant erişim **404** (bulunamadı — "yetkisiz" bilgisi bile sızdırılmaz).

**8.2 Soft delete / hard delete yok.** `Delete` **endpoint'i yoktur**. Bir plan `cancelled` edilir (sebep
zorunlu) ve/veya `archived` edilir; geçmiş **okunabilir kalır**. `BulkDelete` de **yoktur** — bu, Golden
Reference'ın `BulkDelete{Module}Command` beklentisinden **bilinçli ve beyan edilmiş** bir sapmadır (§10 uyarı).

**8.3 Concurrency.** `EntityBase.Version` optimistic concurrency token'ıdır. Update/confirm/cancel/archive
**beklenen `Version`** ile replace eder; uyuşmazlık **409** döner. Sessiz overwrite **yasak**.

**8.4 Transaction gerekmez.** Bu FU'da çok-doküman atomiklik **yoktur** (tek aggregate, tek doküman).
`SupportsTransactionsAsync` guard'ı ve compensation **yazılmaz** — dev ortamındaki standalone Mongo tuzağına
girmemek için de doğru olan budur.

**8.5 MOD-0151 placeholder'ları FU01 tarafından doldurulmaz.** FU09A/B readiness projeksiyonu bugün
`LastVisitDate = null` ve `DueStatus = unknown` döndürür ve bu **kasıtlıdır**. `LastVisitDate` **gerçekleşen
ziyaretten** türer → **FU02**; `DueStatus` last-visit + frequency birleşiminden türer → **FU02 sonrası**. FU01
plan satırı üretir, **ziyaret üretmez**; bu yüzden bu iki alan **bu FU'da da unknown/null kalır** ve FU01
onları **hesaplıyormuş gibi** göstermez.

**8.6 Data scope (ABAC) — bilinçli sınır.** MOD-0018-FU15 `planned / reserved` olduğu için "temsilci yalnız
kendi planlarını görür" **ambient** kuralı **uygulanamaz**. FU01 bunu **taklit etmez**: liste sorgusu
`resourceId` **açık query parametresiyle** daraltılır ve tenant izolasyonu tek gerçek güvenlik sınırıdır.
Sahte bir scope hissi yaratmak, FU15 geldiğinde geri alınması pahalı bir yanlış güvenlik olurdu (**F-ABAC**).

**8.7 Motor yok (D8).** Bu servis: plan **üretmez** (frequency'den otomatik plan çıkarmaz), plan **sıralamaz**,
mesafe/süre **hesaplamaz**, aşama **ilerletmez**, ziyaret **kapatmaz**. Frequency/consent/journey çağrıları
**karar desteği ve provenance** içindir.

**8.8 API/Gateway.** Frontend **Gateway 5000** üzerinden çağırır; browser JS servis portuna (5059/5057/…)
**doğrudan gitmez**. Tarayıcı tarafı **same-origin proxy** (`/CRM/PlannedVisits/api/...`) kullanır; DataTable JS
HttpOnly cookie okumaz ve Bearer token kurmaz.

**8.9 Localization.** 7 dil (`ar,en,es,fr,ru,tr,zh`) + `window.L10n` köprüsü. `.resx` değişiklikleri **tam fleet
restart** ister.

---

## 9. Layout & Shell Contract

- `shell: tenant` → **tüm** `Views/CRM/PlannedVisits/*.cshtml` dosyalarında Razor layout **AÇIKÇA** yazılır:

```cshtml
@{
    ViewData["Title"] = Localizer["PageTitle"];
    Layout = "_LayoutTenantShell";   // shell: tenant — AÇIKÇA, _ViewStart varsayılanına GÜVENİLMEZ
}
```

- View klasörü: `Views/CRM/PlannedVisits/`
- Frontend route: `/CRM/PlannedVisits`
- Menü: `_LayoutTenantShell.cshtml` içine **tek** `<li>`, `@if (Perms.Has("crm.planned-visit.read"))` guard'ıyla
  (how-to-add-a-module Adım 9). `_Layout.cshtml` **FROZEN**, dokunulmaz.
- Bu madde **AC-UI-1** olarak test edilir (§16).

---

## 10. Backend File Convention

Golden Reference Compact (DEV-0001) **naming**'i birebir:

```text
services/Diten.CrmService/src/Diten.CrmService.Application/Features/PlannedVisit/
├── Commands/
│   ├── CreatePlannedVisitCommand.cs        (sealed record, IRequest<Response<Guid>>)
│   ├── UpdatePlannedVisitCommand.cs        (sealed record, IRequest<Response<NoContent>>)
│   ├── ArchivePlannedVisitCommand.cs
│   ├── ConfirmPlannedVisitCommand.cs
│   └── CancelPlannedVisitCommand.cs
├── Queries/
│   ├── ListPlannedVisitsQuery.cs           (sealed record)
│   ├── GetPlannedVisitByIdQuery.cs
│   └── GetPlannedVisitContractQuery.cs
├── Handlers/
│   ├── CommandHandlers/                    ← AYRI klasör (ZORUNLU)
│   │   ├── CreatePlannedVisitHandler.cs    (sealed class, Command/Query suffix YOK)
│   │   ├── UpdatePlannedVisitHandler.cs
│   │   ├── ArchivePlannedVisitHandler.cs
│   │   ├── ConfirmPlannedVisitHandler.cs
│   │   └── CancelPlannedVisitHandler.cs
│   └── QueryHandlers/                      ← AYRI klasör (ZORUNLU)
│       ├── ListPlannedVisitsHandler.cs
│       ├── GetPlannedVisitByIdHandler.cs
│       └── GetPlannedVisitContractHandler.cs
├── Validators/
│   ├── CreatePlannedVisitValidator.cs      (Command suffix YOK)
│   └── UpdatePlannedVisitValidator.cs
├── Provenance/
│   ├── PlannedVisitFrequencyProbe.cs       (MOD-0165 resolver çağrısı — read-only sarmalayıcı)
│   ├── PlannedVisitConsentProbe.cs         (MOD-0164 evaluator çağrısı — read-only sarmalayıcı)
│   └── PlannedVisitJourneyProbe.cs         (MOD-0162-FU05 reader çağrısı — read-only, opsiyonel)
├── PlannedVisitPermissions.cs
├── PlannedVisitValidation.cs               (paylaşılan guard'lar — TEK yer, iki kopya YASAK)
└── PlannedVisitModels.cs                   ← TEK dosyada tüm DTO/ViewModel'ler
```

**Naming (tartışmasız):** Command = `{Verb}PlannedVisitCommand` (record) · Query =
`{Get|List}PlannedVisit{Qualifier}Query` (record) · Handler = `{Verb}PlannedVisitHandler` (class, **suffix YOK**) ·
Validator = `{Verb}PlannedVisitValidator` (**suffix YOK**).

> **⚠️ BEYAN EDİLEN İKİ SAPMA (gizlenmiyor):**
> 1. **`DeletePlannedVisitCommand` ve `BulkDeletePlannedVisitCommand` YOKTUR.** Golden Reference DataTable
>    modülünde bunları bekler; bu modül **CRUD-minus-delete**'tir (§8.2). Sonuç: `verify_datatable_page.py`
>    bulk-delete ile ilgili kontrolleri **N/A** verecektir — bu **beklenen** durumdur (MOD-0162-FU02 emsali:
>    *"archive-only ⇒ 6 bulk-delete verifier check'i EXPECTED N/A"*). §17'de bu sayı **önceden** ilan edilir.
> 2. **`Provenance/` alt klasörü** Golden Reference'ta yoktur. Üç read-only probe'u `Handlers/` içine gömmek
>    handler'ları iki sorumluluğa sokardı; ayrı klasör `handler-design.md` sınırıyla uyumludur ve **F-FILE**
>    olarak Knowledge ailesinin klasör düzeltmesiyle **birlikte** gözden geçirilir.

---

## 11. Frontend File Contract

### 11.1 Golden reference kararı — kullanıcı-form alanı türetmesi (GÖSTERİLİR)

Sayım kuralı (`module-pack-standard.md` §3): yalnız kullanıcının create/edit formunda **doldurduğu** modül
alanları sayılır. `Id`, `TenantId`, audit alanları, **türetilmiş** alanlar, snapshot alanları ve DataTable
checkbox/action kolonları **sayılmaz**. Bir mantıksal aralık **tek kontrol** olarak render ediliyorsa **1**
sayılır (FU03/FU04/FU05 yöntemi).

**Golden-reference yüzeyi (TEK) — `PlannedVisit`:** §4'te 33 numaralı alan + 2 provenance bloğu; form-dışı
olanlar düşüldükten sonra kalan **18**:

| # | Kullanıcı-form alanı | # | Kullanıcı-form alanı |
|---|---|---|---|
| 1 | `VisitCode` | 10 | `Objective` |
| 2 | `TargetType` | 11 | `Notes` |
| 3 | `TargetId` (hedef seçici) | 12 | `BusinessUnit` |
| 4 | `Resource.ResourceId` (kaynak seçici) | 13 | `TerritoryNodeId` |
| 5 | `PlannedDate` | 14 | `CampaignId` |
| 6 | `PlannedStartTime` (+`PlannedEndTime` **aynı kontrol**) | 15 | `ContentEngagementJourneyId` |
| 7 | `PlannedDurationMinutes` | 16 | `ContentEngagementJourneyStageId` |
| 8 | `VisitPurpose` | 17 | `PlanStatus` |
| 9 | `VisitType` | 18 | `Source` |

*Form-dışı (17 + türetilmişler):* `Id` · `TenantId` · `PlannedEndTime` (aynı kontrol) · `AccountId` ·
`ContactId` · `AccountContactLinkId` (**türetilir**) · `Resource.ResourceType` · `Resource.DisplayName` ·
`PositionCode` · `PositionId` (**snapshot**) · `TerritoryModelId` (**türetilir**) · `CancellationReason`
(**cancel diyaloğu**, form değil) · `ArchivedAt/By` · `CreatedAt/By` · `UpdatedAt/By` · `IsDeleted/DeletedAt` ·
`EntityBase.Version` · **tüm** frequency ve consent provenance alanları (**türetilir, ASLA authored**).

→ **18 > 8 ⇒ `golden_reference: compact`** (frontmatter `form_field_count: 18`).

Karar eşiğe **yakın değildir**: en agresif budama (bağlam anahtarlarının 5'i de düşse) bile 13 > 8 kalır. Bu
yüzden Compact kararı, kapsam küçük ayarlamalarına karşı **dayanıklıdır**.

**İkinci bir golden-reference yüzeyi YOKTUR.** Modülde tek aggregate, tek liste, tek form vardır; gömülü
`Resource` / provenance blokları ayrı sayfa/DataTable **değildir** → **tek verifier koşusu**.

### 11.2 Dosya seti — TEK klasör, kanonik Compact 9 dosya (TEK TEK enumerasyon)

**`Views/CRM/PlannedVisits/` (DEV-0001 Compact — tam ve tek set):**

| # | Dosya | Rol |
|---|---|---|
| 1 | `Index.cshtml` | Liste kabuğu; `Layout = "_LayoutTenantShell"` **açıkça**; bölüm sırası ① Filter → ② BulkActionBar → ③ DataTable |
| 2 | `Create.cshtml` | **Compact-özel** sayfa kabuğu + `_Form` |
| 3 | `Edit.cshtml` | **Compact-özel** sayfa kabuğu + `_Form` |
| 4 | `Details.cshtml` | **Compact-özel** detay sayfası; salt-okunur **frequency provenance** + **consent provenance** panelleri + `confirm` / `cancel` / `archive` aksiyonları |
| 5 | `_Form.cshtml` | Create/Edit ortak formu — §11.1'deki **18 alan**; hedef seçici (`TargetType` → `TargetId` zincirli), kaynak seçici, journey → stage zincirli seçici |
| 6 | `_Filter.cshtml` | Inline collapsible filter: `plannedDateFrom` / `plannedDateTo` · `resourceId` · `targetType` · `targetId` · `planStatus` · `visitPurpose` · `territoryNodeId` · `campaignId` · `includeArchived` |
| 7 | `_DataTable.cshtml` | `data-dt-standard="v2"` + skeleton loader; **TEK** DataTable; kolonlar: kod · hedef · tarih/pencere · kaynak · amaç · statü · **consent rozeti** · **frequency rozeti** |
| 8 | `_IndexL10n.cshtml` | JSON payload bridge |
| 9 | `PlannedVisitsIndex.cs` | Marker class (RESX kökü) |

**JS (Golden Compact seti — 3 dosya):**

```text
wwwroot/assets/js/CRM/PlannedVisits/index.js       → DataTable (DtDefaults + v2), filtre, archive/cancel aksiyonları
wwwroot/assets/js/CRM/PlannedVisits/index.l10n.js  → camelCase→PascalCase L10n köprüsü
wwwroot/assets/js/CRM/PlannedVisits/form.js        → hedef seçici + kaynak seçici + journey→stage zinciri
```

`index.l10n.js` **camelCase→PascalCase** dönüşümünü atlamaz (aksi hâlde `window.L10n` anahtarları `undefined`
döner ve toast `"(undefined: <corrId>)"` olur). API profili **`proxy`** (same-origin). Sayfada **tek**
DataTable vardır → `updateVisualState` global selector çakışması **yapısal olarak yoktur**; ikinci bir filtre
host'u da olmadığı için `dt-inline-filter-host` sınıfı gerekmez.

**RESX (tek klasör × 7 dil + shared):**

```text
Resources/Views/CRM/PlannedVisits/PlannedVisitsIndex.{ar,en,es,fr,ru,tr,zh}.resx
Resources/SharedResource.{ar,en,es,fr,ru,tr,zh}.resx        → PlannedVisitsMenu
```

**YASAK dosyalar:** `_CreateEditOffcanvas.cshtml` · `_DetailsQuickView.cshtml` (**Compact yasağı**) ·
`Views/CRM/PlannedVisitRoutes/**` · `Views/CRM/VisitReports/**` (FU02/FU03) · Index içinde create/edit
offcanvas · **hardcoded vokabüler listesi** (tüm dropdown'lar `contract`'tan beslenir).

**Kullanılan mevcut yüzeyler (yeni dosya değil):** hedef seçici MOD-0149/0150'nin
`/api/crm/accounts`, `/api/crm/contacts` endpoint'lerini; kaynak seçici MOD-0151'in `/api/crm/resources/...`
yüzeyini; journey seçici MOD-0162-FU05'in journey endpoint'ini **proxy üzerinden okur** — bu modüllerin
view/JS/controller dosyalarına **dokunulmaz** (§6).

---

## 12. Validation Rules

### 12.1 Alan bazlı

| # | Field | Required | Format / Rule | DB-level | Pre-check |
|---|---|---|---|---|---|
| V1 | `VisitCode` | Evet | Trim, max 64, `^[A-Za-z0-9._-]+$` | Unique **partial** index (arşivlenmemiş) | `ExistsByCodeAsync` |
| V2 | `TargetType` | Evet | `PlannedVisitTargetType` içinde | — | in-domain set (400) |
| V3 | `TargetId` | Evet | `!= Guid.Empty`; **hedefin varlığı + tenant'ı doğrulanır** | — | `Account`/`Contact`/`AccountContactLink` repo lookup |
| V4 | `AccountId`/`ContactId`/`AccountContactLinkId` | Türetilir | Client payload'da gelirse **yok sayılır** (400 değil, **ignore**) | — | `ContactAvailability` emsali |
| V5 | `Resource.ResourceId` | Evet | Trim, max 128, boş olamaz | — | **Doğrulanmaz** (master yok) — D4; `ResourceType` ile birlikte saklanır |
| V6 | `Resource.ResourceType` | Evet | `person` / `user` / `employee` | — | in-domain set (400) |
| V7 | `PlannedDate` | Evet | Geçerli tarih | — | Create'te `>= bugün` (**uyarı değil, 400**); Update'te geçmiş tarih yalnız `draft` statüsünde serbest |
| V8 | `PlannedStartTime` / `PlannedEndTime` | Koşullu | `"HH:mm"` regex; ikisi birlikte ya da ikisi de boş; `End > Start` | — | — |
| V9 | `PlannedDurationMinutes` | Hayır | `> 0`, `<= 1440` | — | Pencere verilmişse pencereyi **aşamaz** |
| V10 | `VisitPurpose` | Evet | `PlannedVisitPurpose` içinde | — | in-domain set (400) |
| V11 | `VisitType` | Evet | `PlannedVisitType` içinde | — | in-domain set (400) |
| V12 | `Objective` | Hayır | Trim, max 1000 | — | — |
| V13 | `Notes` | Hayır | Trim, max 2000 | — | — |
| V14 | `BusinessUnit` | Hayır | MOD-0151 BU sözlüğü | — | Set dışı → 400 |
| V15 | `TerritoryNodeId` | Hayır | Varsa **aktif bir `TerritoryModel`** altında var olmalı | — | Territory repo lookup |
| V16 | `CampaignId` | Hayır | Varsa `Campaign` var olmalı (**yalnız varlık**, target/cycle kontrolü YOK) | — | Campaign repo lookup |
| V17 | `ContentEngagementJourneyId` | Hayır | Varsa **`published` + effective** olmalı | — | `IContentEngagementJourneyReader.ResolvePublishedJourneysAsync` |
| V18 | `ContentEngagementJourneyStageId` | Koşullu | Journey doluysa o journey'in **aktif** aşamalarından biri; journey boşsa **boş olmalı** | — | `GetOrderedStagesAsync` |
| V19 | `PlanStatus` | Evet | `PlannedVisitStatus` içinde + §12.2 geçişine uygun | — | in-domain set (400) |
| V20 | `Source` | Evet | `PlannedVisitSource` içinde; **FU01'de yalnız `manual`** yazılabilir | — | Diğerleri 400 (**rezerve**) |
| V21 | `CancellationReason` | Koşullu | `cancelled` geçişinde zorunlu, trim, max 500 | — | — |

### 12.2 Statü geçiş kuralları (state machine)

```text
draft ──────► planned ──────► confirmed
  │              │                │
  │              ├────────────────┤
  │              ▼                ▼
  └──────────► cancelled  ◄───────┘        (CancellationReason ZORUNLU)
                   │
                   ▼
draft/planned/confirmed/cancelled ──► archived   (terminal; geri dönüş YOK)
```

- `archived` **terminaldir**: unarchive **yoktur** (MOD-0162-FU03 emsali — arşiv satırı UI'da **salt-okunur**).
- `confirmed` → `planned` **geri alınamaz**; yanlışsa `cancelled` + yeni plan.
- Arşivlenmiş satır **hiçbir** update/confirm/cancel kabul etmez → **409**.

### 12.3 Consent guard (D6) — fail-closed, `FilterApplied` onurlandırılır

| Durum | `PlanStatus` = `draft` / `planned` | `PlanStatus` = `confirmed` |
|---|---|---|
| `allowed` | Serbest | **Serbest** |
| `blocked` | Serbest **ama** provenance'a yazılır + UI'da kırmızı rozet | **409** `plan_blocked_by_consent` |
| `unknown` | Serbest **ama** provenance'a yazılır + UI'da sarı rozet | **409** `plan_consent_unknown` (*unknown ASLA allowed değildir*) |
| `not_applicable` | Serbest | Serbest — ama satır **`FilterApplied`** ile birlikte saklanır |
| `FilterApplied = false` | Serbest | **409** `consent_filter_not_applied` — filtre çalışmadıysa **hiçbir uygunluk çıkarımı yapılamaz** |

**Gerekçe:** MOD-0164 sözleşmesi *"unknown is not allowed"* ve *"the provider reports, it does not enforce"*
der. Zorlamayı **tüketici** yapar; FU01 zorlamayı **tek bir yerde**, `confirm` geçişinde uygular. `draft`/
`planned` aşamasında engellemek, saha ekibinin consent eksiğini **görüp düzeltmesini** imkânsız kılardı; satır
silinmez, **sebebiyle** saklanır (MOD-0165-FU04 *"blocked ⇒ excluded-not-dropped"* emsali). Override
(gerekçeli izin) bu FU'da **yoktur** → **F-OVERRIDE**.

### 12.4 Legacy'den yakalanan planlama guard'ları (§21/L5–L6)

| # | Kural | Sonuç |
|---|---|---|
| V22 | **Çakışma (overlap):** aynı `Resource.ResourceId` + aynı `PlannedDate` üzerinde saat pencereleri kesişen ikinci bir **aktif** (`planned`/`confirmed`) plan | **409** `planned_visit_overlap` + çakışan planın kodu döner |
| V23 | Saat penceresi **verilmemiş** planlar | Çakışma kontrolüne **girmez** (gün bazlı çakışma **kural değildir**) — belirsiz veriyle yanlış engelleme yapılmaz |
| V24 | **Aynı gün aynı tip:** aynı `TargetId` + aynı `PlannedDate` + aynı `VisitType` üzerinde ikinci bir **aktif** plan | **409** `planned_visit_duplicate_same_day_type` |
| V25 | `cancelled` / `archived` satırlar | V22 ve V24'te **aday değildir** (iptal edilmiş plan yer tutmaz) |

### 12.5 Müsaitlik (MOD-0150) — **uyarı, engel değil**

`ContactAvailability` (weekday + `StartTime`/`EndTime` + `AppointmentRequired`) plan penceresiyle
çelişiyorsa: **400 DEĞİL**, provenance-siz bir **UI uyarısı** + `reasonCode` döner
(`outside_preferred_window` / `appointment_required` / `contact_not_available_on_day` — MOD-0151
`TerritoryReadinessReasonCodes` ile **aynı** sözcükler). Gerekçe: müsaitlik **tavsiyedir**; randevu telefonla
alınmış olabilir. Bunu sert engele çevirmek sahayı sisteme yalan söylemeye iter.

---

## 13. Failure Path to Verify

- **Duplicate `VisitCode`** → **409** + field-level hata + kayıt **oluşmaz** + reload sonrası temiz state
- **Missing `PlannedDate` / `TargetId` / `Resource.ResourceId`** → **400** + validator mesajı + save engellenir
- **Set dışı vokabüler** (`VisitPurpose = "xyz"`) → **400** `unsupported_vocabulary_value`; **fallback listesi yok**
- **Concurrency conflict** (eski `Version` ile update) → **409** + UI "veri değişti, yeniden yükleyin"; **sessiz overwrite YOK**
- **Unauthorized actor** (`crm.planned-visit.manage` yok) → **403** + UI aksiyonu disabled
- **Cross-tenant erişim** (başka tenant'ın `PlannedVisitId`'si) → **404** (yetki bilgisi sızdırılmaz)
- **Var olmayan hedef** (`TargetId` hiçbir Account/Contact/Link'e karşılık gelmiyor) → **400** `target_not_found`
- **Hedef tipi uyuşmazlığı** (`TargetType=contact` ama id bir Account) → **400** `target_type_mismatch`
- **Consent `blocked`/`unknown` iken `confirm`** → **409** + `reasonCodes` + plan `planned` kalır (§12.3)
- **`FilterApplied=false` iken `confirm`** → **409** `consent_filter_not_applied`
- **Çakışan plan** (V22) → **409** + çakışan planın `VisitCode`'u
- **Aynı gün aynı tip** (V24) → **409** + mevcut planın `VisitCode`'u
- **Arşivlenmiş satırda update/confirm/cancel** → **409** `planned_visit_archived`
- **`published` olmayan journey seçimi** → **400** `journey_not_published`
- **Journey'e ait olmayan `StageId`** → **400** `stage_not_in_journey`
- **MOD-0164 evaluator iç hatası** → plan **500 vermez**; `unknown` + `consent_evaluation_error` provenance'ı
  yazılır ve `confirm` **409** ile durur (*controlled degradation*)
- **MOD-0165 resolver hiçbir policy bulamaz** → `FrequencyStatus = unknown`; **varsayılan sıklık uydurulmaz**;
  plan yine de kurulabilir (frequency bir **engel değildir**)

---

## 14. Authorization Convention

```text
Policy:     [Authorize]                                   // shell: tenant
Permission: [HasPermission("crm.planned-visit.{action}")] // PKS-001: lowercase-dotted, >= 3 segment
Actor type: tenant_user (platform_admin otomatik geçer)
```

| Permission | Kapsadığı endpoint'ler |
|---|---|
| `crm.planned-visit.read` | list · get-by-id · contract |
| `crm.planned-visit.manage` | create · update · cancel · archive |
| `crm.planned-visit.confirm` | confirm (**ayrı anahtar** — planı kuran ile onaylayanı ayırabilmek için) |

**Dev fallback (FU03 emsali, geçici):** RBAC kataloğu `crm.planned-visit.*` anahtarlarını **taşımıyor**. Katalog
hizalanana kadar endpoint'ler **belgelenmiş fallback** ile çalışır — okuma/contract için `crm.territory.read`,
yazma/confirm için `crm.territory.model.manage`. Fallback **hiçbir guard'ı gevşetmez** (§12/§13'ün tamamı
çalışır), **ancak** `manage` ile `confirm` aynı anahtara düştüğü için **SoD uygulanamaz** — bu bilinen boşluk
**F-RBAC** ile kapanır ve o zamana kadar `confirm`'ün ayrı anahtar olduğu **yalnız kod seviyesinde** doğrudur.

**Bu pack hiçbir permission seed etmez, hiçbir role grant yazmaz.**

---

## 15. Gateway / API Routing Decision

**Karar: Gateway değişikliği GEREKLİDİR.** (FU05'ten farklı — orada Knowledge wildcard'ı vardı.)

`ocelot.json` **doğrulandı**: CRM route'ları **kaynak bazlı explicit çiftler** hâlinde tanımlı
(`accounts`, `contacts`, `territory-management`, `territory-models`, `resources`, `visit-frequency-policies`,
`consents`, `preferences`, `campaigns`, `knowledge` — satır ~1940–2260) ve **`/api/crm/{everything}` catch-all
YOKTUR**. Dolayısıyla `/api/crm/planned-visits` Gateway'e **eklenmeden 404** alır.

```text
Gerekli route çifti (integration-agent task'ı — bu pack ocelot.json'a YAZMAZ):
  /api/crm/planned-visits                → Diten.CrmService
  /api/crm/planned-visits/{everything}   → Diten.CrmService
  Metotlar: GET, POST, PUT, PATCH, OPTIONS   (DELETE YOK — §8.2)
```

**Endpoint yüzeyi (6):**

| Metot | Route | Permission |
|---|---|---|
| GET | `/api/crm/planned-visits/contract` | `read` |
| GET | `/api/crm/planned-visits` | `read` |
| GET | `/api/crm/planned-visits/{id}` | `read` |
| POST | `/api/crm/planned-visits` | `manage` |
| PUT | `/api/crm/planned-visits/{id}` | `manage` |
| POST | `/api/crm/planned-visits/{id}/{confirm\|cancel\|archive}` | `confirm` / `manage` / `manage` |

**DELETE endpoint'i yoktur** ve Gateway'de de **açılmaz**.

---

## 16. Acceptance Criteria

> Her madde §17'de **bir teste** eşlenir. Belirsiz ifade (`iyi çalışıyor`, `düzgün`) **yoktur**.

**AC-CORE — aggregate ve yaşam döngüsü**

- [ ] **AC-CORE-1** `POST /api/crm/planned-visits` geçerli payload ile **201** döner; yanıt `PlannedVisitId`
      taşır; `TenantId` **payload'da gönderilse bile** JWT claim'inden çözülür ve gönderilen değer yok sayılır.
- [ ] **AC-CORE-2** Aynı `VisitCode` ile ikinci create **409** döner ve **ikinci doküman oluşmaz**.
- [ ] **AC-CORE-3** `DELETE /api/crm/planned-visits/{id}` **route olarak mevcut değildir** (405/404) ve
      `BulkDelete` komutu kod tabanında **hiç yoktur**.
- [ ] **AC-CORE-4** `archive` sonrası satır listede `includeArchived=true` ile **görünür**, `false` ile
      **görünmez**; arşivlenmiş satırda update/confirm/cancel **409** verir; **unarchive endpoint'i yoktur**.
- [ ] **AC-CORE-5** Eski `Version` ile update **409** döner ve doküman **değişmez**.
- [ ] **AC-CORE-6** `cancel` `CancellationReason` olmadan **400**; sebeple **200** ve satır silinmez.
- [ ] **AC-CORE-7** Başka tenant'ın id'siyle her endpoint **404** döner (403 değil).

**AC-TARGET — hedef çözümlemesi**

- [ ] **AC-TARGET-1** `TargetType=account-contact-link` ile create edilen planda `AccountId` ve `ContactId`
      **link'ten türetilmiş** gelir; aynı alanlar payload'da farklı gönderilse **yok sayılır**.
- [ ] **AC-TARGET-2** Var olmayan `TargetId` → **400** `target_not_found`; tip uyuşmazlığı → **400**
      `target_type_mismatch`.
- [ ] **AC-TARGET-3** `Resource.ResourceId` **string** olarak saklanır; `Guid.Parse` denenmez; doğrulanmaya
      çalışılmaz (D4) — boş string ise **400**.

**AC-TIME — plan penceresi (D1)**

- [ ] **AC-TIME-1** Entity'de `EffectiveFrom` / `EffectiveTo` **alanları yoktur**; effective-dating
      `PlannedDate` (+ opsiyonel saat penceresi) üzerinden `IsEffectiveOn(DateOnly)` ile ifade edilir.
- [ ] **AC-TIME-2** `PlannedEndTime <= PlannedStartTime` → **400**; yalnız biri verilirse → **400**.
- [ ] **AC-TIME-3** Create'te geçmiş `PlannedDate` → **400**; `draft` satırda update ile geçmiş tarih **serbest**.

**AC-FREQ — MOD-0165 tüketimi (read-only)**

- [ ] **AC-FREQ-1** Create/update sırasında `IVisitFrequencyPolicyResolver` **in-process** çağrılır; Gateway
      üzerinden **HTTP self-call yapılmaz**.
- [ ] **AC-FREQ-2** Hiç policy yoksa `FrequencyStatus = unknown` yazılır, plan **yine de kurulur** ve
      **varsayılan bir sıklık uydurulmaz**.
- [ ] **AC-FREQ-3** `conflict` durumunda seçilen policy **deterministik** gelir ve `ReasonCodes`
      `policy_conflict` içerir.
- [ ] **AC-FREQ-4** Provenance'ta policy'nin `EffectiveFrom/To`, `Priority` gibi **kayıt payload'ı
      kopyalanmaz**; yalnız §4.6 tablosundaki alanlar bulunur.
- [ ] **AC-FREQ-5** MOD-0165 yüzeyinde (`Features/VisitFrequencyPolicy/**`) **hiçbir dosya değişmez** — git diff ∅.

**AC-CONSENT — MOD-0164 tüketimi (fail-closed, D6)**

- [ ] **AC-CONSENT-1** Consent sorusu **daima** `Channel = "visit"` ile sorulur ve `Purpose`, §4.7 eşleme
      tablosuna göre türetilir.
- [ ] **AC-CONSENT-2** `blocked` → `confirm` **409** `plan_blocked_by_consent`; satır **silinmez**, `planned`
      kalır ve sebep provenance'ta durur.
- [ ] **AC-CONSENT-3** `unknown` → `confirm` **409**; **unknown hiçbir yolda `allowed` gibi ele alınmaz**.
- [ ] **AC-CONSENT-4** `FilterApplied = false` → `confirm` **409** `consent_filter_not_applied`; bu satırdan
      hiçbir uygunluk çıkarımı yapılamaz.
- [ ] **AC-CONSENT-5** Evaluator iç hatası **500'e dönüşmez**; `unknown` + `consent_evaluation_error` yazılır.
- [ ] **AC-CONSENT-6** MOD-0164 yüzeyinde (`Features/ConsentPreference/**`) **hiçbir dosya değişmez** — git diff ∅.

**AC-JOURNEY — MOD-0162-FU05 tüketimi (opsiyonel)**

- [ ] **AC-JOURNEY-1** Yalnız `published` + effective journey seçilebilir; `draft`/`archived` → **400**.
- [ ] **AC-JOURNEY-2** `StageId` seçilen journey'in **aktif** aşamalarından değilse **400**.
- [ ] **AC-JOURNEY-3** Journey alanı **boş bırakılabilir** ve plan tam işlevlidir (opsiyonellik gerçek).
- [ ] **AC-JOURNEY-4** Bu FU aşama **ilerletmez**, branch **değerlendirmez**, `IKnowledgePathReader` /
      `IContentEngagementJourneyReader` imzalarını **genişletmez** — git diff ∅.

**AC-LEGACY — legacy kural koruması (§21)**

- [ ] **AC-LEGACY-1 (L5)** Saat pencereleri kesişen ikinci aktif plan → **409** `planned_visit_overlap`,
      yanıt çakışan planın `VisitCode`'unu taşır.
- [ ] **AC-LEGACY-2 (L5)** Saat penceresi **olmayan** planlar çakışma kontrolüne girmez (gün bazlı engelleme yok).
- [ ] **AC-LEGACY-3 (L6)** Aynı `TargetId` + aynı gün + aynı `VisitType` ikinci aktif plan → **409**
      `planned_visit_duplicate_same_day_type`.
- [ ] **AC-LEGACY-4** `cancelled` / `archived` satırlar L5 ve L6 kontrollerinde **aday değildir**.
- [ ] **AC-LEGACY-5** §21 tablosundaki **her** legacy kuralı ya bu FU'da bir V-kuralına, ya da adıyla bir
      FU'ya (FU02/FU03/FU04/FU05) veya bir follow-up'a **eşlenmiştir** — eşlenmemiş satır **yoktur**.

**AC-BOUNDARY — motor yok / sınır disiplini**

- [ ] **AC-BOUNDARY-1** Kod tabanında rota/mesafe/optimizasyon/otomatik-plan-üretimi **yoktur**
      (`RouteOrder`, `Distance`, `TravelTime`, `GeneratePlans` gibi bir sembol **hiç geçmez**).
- [ ] **AC-BOUNDARY-2** `LastVisitDate` ve `DueStatus` bu FU'da **hesaplanmaz**; MOD-0151 readiness'inin
      placeholder'ları **olduğu gibi** kalır (§8.5).
- [ ] **AC-BOUNDARY-3** `MicroZone` tanımı **yazılmaz/değiştirilmez**; yalnız anahtarla referans verilir.
- [ ] **AC-BOUNDARY-4** `BrandId` / `ProductId` / `SegmentId` alanları entity'de **hiç yoktur** (D4/sahte-FK yasağı).

**AC-UI — Compact konsol**

- [ ] **AC-UI-1** `Views/CRM/PlannedVisits/*.cshtml` **tümünde** `Layout = "_LayoutTenantShell"` **açıkça** yazılı.
- [ ] **AC-UI-2** Klasörde **tam olarak** §11.2'deki 9 dosya var; `_CreateEditOffcanvas.cshtml` ve
      `_DetailsQuickView.cshtml` **yok**.
- [ ] **AC-UI-3** `_DataTable.cshtml` `data-dt-standard="v2"` + skeleton loader taşır; sayfada **tek** DataTable var.
- [ ] **AC-UI-4** Tüm dropdown değerleri `/api/crm/planned-visits/contract`'tan gelir; **hardcoded liste yok**.
- [ ] **AC-UI-5** Details sayfası frequency ve consent provenance'ını **salt-okunur** gösterir ve `blocked`/
      `unknown` durumunu **görünür bir rozetle** ayırt eder.
- [ ] **AC-UI-6** Browser JS servis portunu (5059/5057) **çağırmaz**; yalnız same-origin proxy kullanır.
- [ ] **AC-UI-7** 7 dilin **hepsinde** `.resx` parite tam; `window.L10n` anahtarları `undefined` dönmez.

---

## 17. Test Expectations

**17.1 Backend unit/integration (`tests/Diten.CrmService.Application.Tests/PlannedVisit/`) — hedef ≥ 40 test**

| Küme | Kapsam |
|---|---|
| 1. Validation | V1–V21'in her biri için pozitif + negatif |
| 2. Hedef çözümlemesi | 3 `TargetType` × (bulundu / bulunamadı / tip uyuşmazlığı) + türetilen alanlar |
| 3. State machine | §12.2'deki her geçiş + her **yasak** geçiş |
| 4. Concurrency | Doğru `Version` → 200; eski `Version` → 409, doküman değişmez |
| 5. Tenant izolasyonu | Cross-tenant get/update/archive → 404 |
| 6. Frequency provenance | `resolved` / `unknown` / `conflict` / `not_applicable` dördü + kopyalama yasağı assertion'ı |
| 7. Consent guard | `allowed` / `blocked` / `unknown` / `not_applicable` / `FilterApplied=false` × (`draft` vs `confirm`) |
| 8. Evaluator hata yolu | Fırlatan sahte evaluator → 500 değil, `unknown` + `consent_evaluation_error` |
| 9. Journey seçimi | published / draft / archived journey + journey'e ait olmayan stage |
| 10. Legacy guard'lar | L5 (çakışan / bitişik / pencereli-penceresiz) + L6 (aynı gün aynı tip / farklı tip) + iptal edilmiş satırın aday olmaması |
| 11. Soft delete | Archive sonrası list davranışı; hard-delete yolunun **yokluğu** |

**17.2 Frontend / smoke**

- Authenticated smoke script (**≥ 30 adım**): create → list → filter → detail → confirm (allowed) →
  confirm (blocked ⇒ 409) → cancel → archive → arşiv görünürlüğü → cross-tenant 404.
- **PowerShell 5.1 tuzağı:** `@(... | Where-Object ...).Count` sarmalaması **zorunlu**; `Add-Result`
  çağrıları script yazıldıktan sonra **gerçekten çalıştırılarak** doğrulanır (MOD-0162-FU04 dersi: 19 bozuk
  `Add-Result` hiç çalıştırılmadan "PASS" raporlanmıştı).
- **Orkestratör self-report'una güvenilmez:** verifier/test sayıları **kendi koşumdan** okunur.

**17.3 Quality gates**

| Gate | Beklenti |
|---|---|
| Build | `Diten.CrmService` + `Diten.Web` + gateway **PASS** |
| `verify_datatable_page.py` | Compact yüzeyi için **tek** koşu; **bulk-delete ile ilgili kontroller EXPECTED N/A** (§10 sapma 1) — beklenen sayı **koşumdan önce** pack'e yazılır, sonradan rasyonalize edilmez |
| `quality-gate-datatable` | PASS |
| RESX parite | 7 dil × `PlannedVisitsIndex` + `SharedResource.PlannedVisitsMenu` **tam** |
| Boundary diff | `Features/{VisitFrequencyPolicy,ConsentPreference,Knowledge,Territory,Account,Contact,AccountContact,ContactAvailability}/**` → **git diff ∅** |
| Gateway | `/api/crm/planned-visits` **200** (route eklendikten sonra); eklenmeden **404** beklenir |

**17.4 Endpoint'ler fleet restart'a kadar 404'tür** — yeni controller servis yeniden başlamadan görünmez;
`.resx` değişiklikleri **tam restart** ister.

---

## 18. Ready-for-dev Checklist

- [x] DCP-002 kimlik kapısı **PASS** (exit 0, 2026-08-26) — komut ve çıktı §başlıkta
- [x] Module registry kontrol edildi: `MOD-0155` canonical, deprecated alias **değil**, replacement ID **yok**
- [x] Zorunlu bağlam okundu: `AGENTS.md` · `domain-config.md` · `crm-sor-boundary.md` ·
      `legacy-value-preservation.md` · `crm-build-lanes.md` · `module-pack-standard.md` · `master-development-plan.md`
- [x] Golden Reference (DEV-0001 **Compact**) referans alındı; alan sayımı **gösterildi** (§11.1 — 18 > 8)
- [x] Frontend dosya seti **tek tek** enumere edildi (§11.2 — tek klasör, 9 dosya + 3 JS + 7 RESX)
- [x] Frontmatter zorunlu alanların tümü dolu (`service`, `shell`, `golden_reference`, `entity_base`,
      `form_field_count`)
- [x] Layout & Shell Contract'ta Razor `Layout` **açıkça** yazıldı ve **AC-UI-1**'de test edilebilir madde oldu
- [x] Backend File Convention Golden Reference naming'iyle birebir; **iki sapma açıkça beyan edildi** (§10)
- [x] Validation Rules her alan için yazıldı (§12 — 25 kural + state machine + consent guard tablosu)
- [x] Failure Path ≥ 4 senaryo (§13 — **18 senaryo**)
- [x] Authorization Convention: 3 anahtar + policy + actor + **fallback'in SoD boşluğu** açıkça yazıldı
- [x] Gateway kararı **açık ve doğrulandı**: değişiklik **GEREKLİ** (catch-all yok, `ocelot.json` ~1940–2260)
- [x] Acceptance Criteria konsolide + test edilebilir; her madde §17'de bir teste eşlendi
- [x] Test Expectations build + verifier + 7 dil RESX + smoke + **boundary diff ∅** kapsıyor
- [x] Protected Paths eksiksiz (§6) — tüketilen 8 CRM yüzeyi, diğer servisler, ocelot, RBAC, registry, Mongo
- [x] **Sahte-FK yasağı** uygulandı: `ResourceId` string; `BrandId`/`ProductId`/`SegmentId` **hiç açılmadı** (D4)
- [x] Legacy business rules **frozen reference** olarak kayda geçirildi (§21) ve her satır bir hedefe eşlendi
- [ ] 🔶 **Kapsam kararı** — UI bu pack'te mi kalsın, ayrı `FU01-UI` pack'ine mi çıksın (§2.1 varsayımı) — **AÇIK**
- [ ] 🔶 **D1 onayı** — "effective dating" plan penceresi olarak modellendi (§19.1/D1); policy-tipi ikinci
      zaman ekseni **açılmadı** — kullanıcı teyidi **AÇIK**
- [ ] `status: ready-for-dev` + `runtime_code_allowed: true` — **AÇIK**; build-lane gereği ("pack erken, impl
      geç") pack `draft` bırakıldı, flip **ayrı kullanıcı aksiyonudur**

---

## 19. Implementation Notes

### 19.1 Kararlar (D1–D8)

| # | Karar | Gerekçe / reddedilen alternatif |
|---|---|---|
| **D1** | **Tek zaman ekseni: plan penceresi = effective window.** `PlannedDate` (+ opsiyonel `PlannedStartTime`/`EndTime`) aggregate'in effective aralığıdır; `IsEffectiveOn(DateOnly)` bunun üzerine yazılır. **Policy-tipi `EffectiveFrom`/`EffectiveTo` çifti AÇILMAZ.** | Bir `PlannedVisit`, `VisitFrequencyPolicy` gibi bir **kural** değil, **tarihli bir örnektir**. İkinci bir zaman ekseni eklemek "plan 3 Mart'a kurulu ama 1–5 Mart arası geçerli" gibi anlamsız bir durum yaratır, iki farklı "geçerli mi?" cevabı doğurur ve iki `DateTimeOffset` alanı birlikte sort/index edilirse bilinen **parallel-arrays 500** tuzağına düşer. Reddedilen alternatif: her ikisini birden tutmak. **Kullanıcı teyidine açık** (§18) |
| **D2** | **Vokabüler in-domain, fail-closed.** Setler `PlannedVisit.cs` içinde static class; set dışı değer **400**; MOD-0048 publish'i **runtime ön koşulu değildir**. | FU02/FU03/FU04/FU05'in tamamının benimsediği `D-VOCAB=A` emsali. Setler aynı sözcüklerle ayrı operatör işi olarak yayınlanır (**F-RD**). Hardcoded **fallback listesi** ise ayrı bir şeydir ve **yasaktır** |
| **D3** | **Hedef üçlüsü** `account` / `contact` / `account-contact-link`; `account-contact-link` **en spesifik**. | MOD-0165-FU01 §6 spesifiklik sırası + MOD-0164 `ConsentSubjectType` ile **birebir** aynı sözcükler. Ayrı bir "HCP" hedef tipi **açılmadı** — HCP identity SoR hâlâ EA-TBD (§19.2) |
| **D4** | **Sahte-FK yasağı.** `ResourceId` **string** + `ResourceType` + display snapshot; `BrandId`/`ProductId`/`SegmentId` **hiç yok**. | Doğrulanamayan `Guid` açmak, hiçbir koleksiyona bağlanmayan ölü FK üretir. MOD-0151 `TerritoryResourceRef` bu deseni zaten kanıtladı. Brand/Product master (MOD-0290) ve MOD-0167 kurulduğunda **additive** olarak açılır (**F-CONTEXT**) |
| **D5** | **Provenance-only tüketim.** Frequency/consent/journey verisi **kopyalanmaz**; karar + id + sürüm + zaman saklanır. | MOD-0165-FU04 `CampaignTarget` emsali. Kopyalanan veri **bayatlar** ve iki gerçek doğurur; provenance ise "o an ne bilindiği"ni **denetlenebilir** kılar |
| **D6** | **Consent zorlaması tek noktada: `confirm`.** `draft`/`planned` serbest + görünür rozet; `confirm` fail-closed. | MOD-0164 *"provider reports, it does not enforce"* der — zorlama tüketicinindir. Create'te sert engel, saha ekibinin eksiği **görüp düzeltmesini** engellerdi; satır **silinmez, sebebiyle saklanır** |
| **D7** | **Legacy guard'lar (çakışma + aynı-gün-aynı-tip) FU01'e ait.** | İkisi de **plan kurma** kuralıdır, rota veya rapor kuralı değil. FU03'e ertelenirse legacy'nin en somut iki kuralı **kayıt dışı** kalırdı — bu pack'in preservation amacına aykırı |
| **D8** | **Motor yok.** Plan üretimi, sıralama, mesafe/süre, aşama ilerletme, due/overdue **yok**. | FU03/FU04/FU05'in "no engine" disiplini. Foundation'a motor sızarsa FU02/FU03 sınırları geri alınamaz biçimde bulanır |

### 19.2 Üç EA sorusunun bugünkü durumu

| EA sorusu | Durum | Bu FU'ya etkisi |
|---|---|---|
| **Frequency veri kaynağı** (legacy tablo mu, yeni cadence config mi) | ✅ **KAPANDI** | MOD-0165-FU01 sahipliği + **FU03 resolver'ı canlı**. 2026-07-31 review'ının *"karar verilmeden MOD-0155 pack'i yazılamaz"* bloğu **kalktı** |
| **Daywork / VisitMix** kaynakları | 🔶 **AÇIK** | **Bloklamaz.** İkisi de **gün doldurma / ziyaret karması** kavramlarıdır ve **schedule/route** alanına aittir → **FU03**. FU01 tek tek plan satırı kurar; günün nasıl dolduğuna karışmaz |
| **HCP identity SoR** (doktor/eczacı kimliği CRM mi MDM mi) | 🔶 **AÇIK** | **Bloklamaz.** FU01 **hiçbir HCP master'ı tanımlamaz**; yalnız MOD-0149/0150 id'lerine referans verir. Karar MDM lehine çıkarsa değişecek olan `TargetId`'nin **anlamı**dır, `PlannedVisit` **şeması değil** (**F-HCP**) |
| **MOD-0018-FU15** Real DataScopeResolver | 🔴 **planned/reserved** | Field-force ABAC scoping **açılamaz**; §8.6'daki explicit-`resourceId` kararı bu yüzden alındı (**F-ABAC**) |

### 19.3 Repo'dan çıkarılmış, bu FU'yu doğrudan vuran tuzaklar

1. **RegisterClassMaps** — `PlannedVisit` **ve gömülü tipler** (`PlannedVisitResourceRef`,
   `…FrequencyProvenance`, `…ConsentProvenance`) `Persistence/DependencyInjection.cs`'e eklenmezse `Guid`
   alanları binary yazılır, filtreler **sessizce boş döner** (MOD-0151 FU05 / `AccountTerritoryAssignment` dersi:
   *"Assigned To" sürekli "—" gösteriyordu*).
2. **`DateOnly` tercihi bilinçlidir** — `PlannedDate` bir `DateTimeOffset` olsaydı BSON'da `[ticks, offset]`
   dizisi olarak saklanır, gün karşılaştırmaları `.Date` tuzağına düşer ve ikinci bir `DateTimeOffset` alanıyla
   birlikte sort edildiğinde **"cannot sort with keys that are parallel arrays"** 500'ü gelirdi.
3. **Partial index `$ne` yasak** — `VisitCode` unique partial index'i `Filter.Ne(x, null)` içerirse servis
   başlangıçta **crash-loop**'a girer; `Filter.Type(...)` / `$lt` kullanılır (Platform 5057 dersi).
4. **Transaction guard yazma** — tek doküman yazımı var; `SupportsTransactionsAsync` + compensation gereksizdir
   ve dev standalone Mongo'da 500 üretir.
5. **In-process çağrı, HTTP self-call değil** — `IVisitFrequencyPolicyResolver` ve `IConsentPreferenceEvaluator`
   **DI ile** çağrılır. Gateway üzerinden kendi servisine HTTP atmak token/tenant bağlamını kaybettirir ve
   MOD-0165-FU03'ün *"no consumer re-implements the engine, and there is no HTTP self-call"* kuralını çiğner.
6. **Motor sızıntısı UI'da da** — frequency/consent rozetleri kullanıcıya **bilgi** olarak sunulur; UI hiçbir
   yerde "sistem sizin için planladı" izlenimi vermez (FU05 *"beyan, motor değil"* dersi).
7. **L10n bridge** — `index.l10n.js` camelCase→PascalCase dönüşümü atlanırsa `window.L10n` anahtarları
   `undefined` döner (toast `"(undefined: corrId)"`).
8. **Menü görünürlük zinciri** — modül sidebar'da çıkmıyorsa 4 kapıya bakılır (entitlement → module code →
   permission → `<li>` guard); legacy-vs-live module code drift'i plan entitlement'ını **sessizce** öldürür.
9. **RBAC grant'i el ile yapılmaz** — `crm.planned-visit.*` kataloğa girene kadar §14 fallback'i kullanılır;
   `rolePermissions` koleksiyonuna el ile GUID yazmak (yanlış subtype ile) **tüm tenant login'lerini kırar**.

---

## 20. Follow-up Items

| # | Follow-up | Neden |
|---|---|---|
| 🔶 **D-SCOPE** | **Kullanıcı kararı (AÇIK)** — UI bu pack'te mi kalır, ayrı `MOD-0155-FU01-UI`'ye mi çıkar | §2.1 beyan edilen varsayım; `ready-for-dev` flip'inin ön koşulu |
| 🔶 **D-DATE** | **Kullanıcı teyidi (AÇIK)** — D1: "effective dating" plan penceresi olarak modellendi | §19.1/D1; şema kararı, sonradan değiştirmek pahalı |
| **F-REG** | `execution/registries/module-id-registry.md`'ye `MOD-0155-FU01` satırı | Registry yazımı **pack yetkisi dışıdır** (MOD-0165-FU01 emsali) |
| **F-GW** | `ocelot.json`'a `/api/crm/planned-visits` + `/{everything}` route çifti (OPTIONS dâhil) | §15 — catch-all yok; **integration-agent** task'ı |
| **F-RBAC** | `crm.planned-visit.{read,manage,confirm}` katalog + grant; §14 fallback'inin kaldırılması | Fallback altında **SoD uygulanamıyor** (`manage` ve `confirm` aynı anahtara düşüyor) |
| **F-RD** | MOD-0048 set publish: `planned-visit-target-type` · `-purpose` · `-type` · `-status` · `-source` | D2 gereği **blocker değil**; vokabüler hizası için yine de yayınlanmalı |
| **F-ABAC** | MOD-0018-FU15 sonrası "temsilci yalnız kendi planını görür" ambient scope | §8.6 — bugün **kasıtlı olarak** explicit filtre |
| **F-OVERRIDE** | Consent `blocked`/`unknown` için **gerekçeli, yetkili** confirm override'ı | §12.3'te kasıtlı yok; compliance kararı gerektirir |
| **F-CONTEXT** | `BrandId` / `ProductId` / `SegmentId` bağlarının **doğrulanmış** biçimde açılması | D4 — bugün sahte FK yaratmamak için hiç açılmadı; MOD-0290 + MOD-0167 kurulunca **additive** |
| **F-HCP** | HCP identity SoR kararı (CRM vs MDM) sonrası `TargetId` semantiğinin gözden geçirilmesi | §19.2 — şema değişmez, **anlam** değişir |
| **F-FU02** | **MOD-0155-FU02 Visit Report** — gerçekleşen ziyaret + rapor + `LastVisitDate` + `DueStatus` | §8.5'teki iki placeholder ancak orada dolar |
| **F-FU03** | **MOD-0155-FU03 Route Planning** — schedule engine, geo-proximity rota, **Daywork/VisitMix** | §21/L9–L11 buraya taşındı |
| **F-FU04** | **MOD-0155-FU04 Visit Content Sequence Execution** — MOD-0162-FU05 tüketiminin **yürütme** tarafı | Bu FU yalnız aşama **bağlar**, göstermez/ilerletmez |
| **F-FU05** | **MOD-0155-FU05 MicroTarget** | §21/L1 |
| **F-IMPORT** | Plan import/export (MOD-0151 FU08 lane emsali) | `Source = import` bugün **rezerve**, üreticisi yok |
| **F-MIG** | Legacy plan/aktivite crosswalk'u | Bu pack yalnız **greenfield authoring** açar; §21/L12'ye bağlı |
| **F-LEGACY** | **Legacy CRM2 kaynak erişimi** — §21'in "kanıt" sütunu bugün yalnız iç dokümanlara dayanıyor | §21 uyarı kutusu; daha derin kural çıkarımı ancak legacy DB/kod erişimiyle mümkün |
| **F-FILE** | `Provenance/` klasörünün Golden Reference hizasına alınması | §10 sapma 2 — Knowledge ailesinin **F-FILE**'ıyla birlikte |
| **F-STATUS** | Closeout'ta `execution/registries/module-implementation-status.md` satırı | Kod-izli modül durum takibi — **yalnız kullanıcı onayıyla** |

---

## 21. Legacy Business Rules (Frozen Reference)

> **EK BÖLÜM** (zorunlu 20 bölümün üstüne). MOD-0155 **en yüksek legacy değere** sahip alandır ve
> `legacy-value-preservation.md` yöntemi nettir: **kod taşınmaz, kural çıkarılır.**
>
> ### ⚠️ Kanıt seviyesi — dürüst beyan
> **Legacy CRM2 kaynak kodu bu repoda YOKTUR.** `frontend/Diten.Web/Controllers/Archive/` ve `Views/Archive/`
> yolları `domain-config.md`'de FROZEN olarak korunmakla birlikte **repoda mevcut değildir**; repo genelinde
> `MicroTarget` / `ActivityReport` / `Daywork` / `VisitMix` sembollerinin **hiçbir kod/şema karşılığı yoktur**
> (yalnız governance dokümanlarında geçer). Dolayısıyla aşağıdaki tablo **ikinci-el kural hafızasıdır** —
> kaynağı `legacy-value-preservation.md`, `crm-sor-boundary.md` ve 2026-07-31 CRM capability review'ıdır,
> legacy kodun kendisi değildir. Alan formülleri, statü kolonları, eşik değerleri gibi **ince kurallar burada
> yoktur ve uydurulmamıştır**. Daha derin çıkarım için legacy DB/kod erişimi gerekir → **F-LEGACY**.
> 2026-07-31 review'ının uyarısı hâlâ geçerlidir: *"Legacy'nin en değerli bilgisi ve şu an yalnız tek bir
> matris satırında yaşıyor. Kural çıkarımı ertelendikçe kaybolma riski artıyor."* Bu bölüm o riski en azından
> **pack seviyesinde dondurur**.

| # | Legacy kural / varlık | Kaynak (frozen) | Bu FU'daki karşılığı | Do-not-migrate notu |
|---|---|---|---|---|
| **L1** | **MicroTarget** — hedefleme cadence'i ve atama | Legacy pharma field-force | **FU05** (`MicroTarget`) — bu FU'da **yok** | Controller/view taşınmaz |
| **L2** | **Activity / Visit** — ziyaret varlığının referans şeması | Legacy pharma | **Bölünmüş:** *plan* tarafı **FU01** (`PlannedVisit`), *gerçekleşme* tarafı **FU02** | Legacy statü kolonları **birebir kopyalanmaz** |
| **L3** | **ActivityReport** — rapor zorunluluk kuralları | Legacy pharma | **FU02** | Legacy rapor formu taşınmaz |
| **L4** | **Visit status lifecycle** — state machine | Legacy pharma | **Bölünmüş:** plan statüleri **§12.2** (`draft`→`planned`→`confirmed`→`cancelled`→`archived`), execution statüleri **FU02** | Lifecycle **greenfield** modellendi; legacy kolon adları alınmadı |
| **L5** | **Ziyaret çakışma kontrolü** (overlap validation) | Legacy pharma | ✅ **FU01 — V22/V23** + **AC-LEGACY-1/2** | Saat penceresi olmayan planlar kapsam dışı (belirsiz veriyle engelleme yok) |
| **L6** | **Aynı gün aynı activity type engeli** (dedup) | Legacy pharma | ✅ **FU01 — V24/V25** + **AC-LEGACY-3/4** | `cancelled`/`archived` satırlar yer tutmaz |
| **L7** | **Frequency / cadence** | Legacy pharma | ✅ **ÇÖZÜLDÜ, DIŞARIDA** — SoR **MOD-0165** (`VisitFrequencyPolicy`), FU01 yalnız **resolver'ı okur** (§4.6) | Frequency **asla** düz alan değildir (MOD-0165-FU01/D2); legacy tablo **taşınmaz** |
| **L8** | **MR zone / micro-zone yetkisi** | Legacy pharma | **Bölünmüş:** tanım **MOD-0151**, tüketim **FU01** (`TerritoryNodeId`), **yetki** → MOD-0018-FU15 (**F-ABAC**) | Legacy yetki tablosu taşınmaz |
| **L9** | **Schedule engine** — planlama algoritması | Legacy pharma | **FU03** (**F-FU03**) — FU01 **motor değildir** (D8) | Kod taşınmaz; algoritma **yeniden yazılır** |
| **L10** | **"Hastane doktorları → yakın eczane rota önerisi"** (geo-proximity) | Legacy pharma | **FU03** | Geo veri MOD-0048/MDM'den; legacy kopya **değil** |
| **L11** | **Daywork / VisitMix** — gün doldurma / ziyaret karması | Legacy pharma (**varlığı EA-TBD**) | **FU03** — kaynağı hâlâ **açık soru** (§19.2) | Kaynak doğrulanmadan şema **açılmaz** |
| **L12** | **Client / WorkPlace / Property / PropertyList / ClientCategory** | DitenCRM | **MOD-0149 / MOD-0150** (zaten shipped); FU01 yalnız **referans verir** | Ürün (`Property`) SoR = **MDM**, kopyalanmaz |

**Bu FU'nun legacy'den fiilen dondurduğu iki kural L5 ve L6'dır** — ikisi de **plan kurma** kuralı olduğu için
foundation'a aittir (D7) ve ikisi de birer 409 + reason code + test olarak **yürütülebilir** hâle getirilmiştir.
Kalan satırlar adıyla bir FU'ya veya follow-up'a **eşlenmiştir**; eşlenmemiş legacy satırı **yoktur**
(**AC-LEGACY-5**).

---

## Handoff

Module pack **`draft`** olarak hazır. Lütfen inceleyip gerekli alan/scope düzeltmelerini yapın ve **§18'deki iki
açık kararı** kapatın:

1. **D-SCOPE** — UI yüzeyi bu pack'te mi kalsın, yoksa ayrı bir `MOD-0155-FU01-UI` pack'ine mi çıksın (§2.1)?
2. **D-DATE** — "effective dating" plan penceresi olarak modellendi; policy-tipi ikinci bir zaman ekseni
   **açılmadı** (§19.1/D1). Teyit edilsin mi?

Geliştirme için status `approved` veya `ready-for-dev` olmalı **ve** `runtime_code_allowed: true` yapılmalıdır;
sonra `@orchestrator MOD-0155-FU01-visit-planning-planned-visit` çağrılır. Build-lane
(`crm-field-sales-extension` — *"pack erken, impl geç"*) gereği bu flip **ayrı bir karardır**; bu pack onu
vermez.

Hazırlık sırasında **Golden Reference Compact (DEV-0001)** şablon olarak alındı — naming'de sapma yok; iki
yapısal sapma (`Delete`/`BulkDelete` yokluğu, `Provenance/` klasörü) §10'da **açıkça beyan edildi**.
