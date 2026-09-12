# WORK PACKAGE — WP-SCMM-11-AUD · AudienceProfile çok-eksen + Subject-bound (SCMM-11 blocker)

> **Control Tower kaydı (SoR).** SCMM-11 (eligibility) **önkoşulu** — HTML'in "tek gerçek blocker"ı, DEC-SCMM-03 **AUD** kararı. Bağlı: SCMM-03 ✅ (AUD frozen). Owner: content/domain lead. Module: **MOD-0162** (knowledge taxonomy — H'de retained).
> **Boundary:** MOD-0162 AudienceProfile extend. Eligibility motoru (SCMM-11) DEĞİL — bu yalnız veri modelini açar.

## Metadata
```text
WP ID:            WP-SCMM-11-AUD
Prompt ID:        P-SCMM-11-AUD · v1.0
Task Class:       Domain extend (aggregate 3 tüketici tarafından by-Id refli) — state-changing
Golden-Flow Profile: B (backend); UI = WP-SCMM-11-AUD-UI follow
Risk Class:       MEDIUM (additive; backward-compat SubjectId/ProfileType kritik)
Agent Lane:       AL-SCMM-AUD (DEV) · Target Agent: backend-architect
Branch:           feature/structured-content-messaging · Expected HEAD: 056d774a
Persistence:      L3 (EntityBase.Version optimistic concurrency)
```

## Ölçülmüş girdi (SCMM-02 + bu tur)
- `AudienceProfile.cs`: ProfileCode/Name/Description/**ProfileType (tek-eksen, 8 sabit)**/Status/SortOrder/Effective/Alias/ExternalReferences/audit. **SubjectId YOK** (tenant-global).
- Tüketiciler **by-Id**: `KnowledgeContent.AudienceProfileId`, `KnowledgePath.AudienceProfileId`, `ContentEngagementJourney.AudienceProfileId` (hepsi `Guid?`). → iç yapı zenginleştirme **additive-safe** (tüketici tek Guid ref taşımaya devam eder).
- DEC-SCMM-03 AUD: **çok-eksen + Subject-scoped new-build; vokabüler sektör-nötr.**

## Kapsam
1. **Subject-bound:** additive `SubjectId` (Guid?). **Backward-compat:** nullable — legacy profil (SubjectId null) tenant-global kalır; yeni profil Subject-scoped. (Zorunlu yapmak legacy'yi kırar; migration kararı: nullable + ileride opsiyonel backfill.)
2. **Çok-eksen:** additive `Dimensions` yapısı — `List<AudienceDimensionAssignment>` = `{ AxisCode (string, açık/config — sektör-nötr, hardcoded enum YOK), Values (List<string>) }`. Bir profil birden çok eksende değer taşır (specialty/seniority/setting/channel gibi — ama isimler config, kodda sabit değil).
3. **Backward-compat ProfileType:** `ProfileType` alanı **KALIR** (legacy). Yeni model Dimensions kullanır; ProfileType tek-eksen legacy view olarak korunur (veya read-time bir dimension'a yansıtılır — ama alan silinmez).
4. CRUD + validasyon (AxisCode boş değil, Values non-empty, duplicate-axis red) + Subject-scope guard + **audit** (SCMM-09 IKnowledgeConceptAuditPublisher deseni / uygun publisher, SourceModule MOD-0162) + class-map (embedded AudienceDimensionAssignment).

## Frozen model uyumu (DEC-SCMM-03)
- **AUD:** çok-eksen + Subject-scoped ✓; **sektör-nötr** — AxisCode/Values sabit enum DEĞİL, config-driven string (medical-specific değerler koda gömülmez).
- **RM3:** usage-scope/audience dimensions deseniyle tutarlı.
- **D8:** yalnız veri modeli — eligibility/membership hesaplaması YOK (o SCMM-11).

## Acceptance
- E2: build temiz; unit — Dimensions persist/round-trip; duplicate-axis 400; AxisCode/Values validasyon; SubjectId nullable (legacy null round-trip); ProfileType korunur (backward-compat); audit event SourceModule MOD-0162. **Tüketici by-Id ref bozulmaz** (KnowledgeContent/Path/Journey).
- **Regresyon:** tam CrmService.Application.Tests yeşil (aggregate + audit değişimi).
- E4 (fleet): canonical authz 200/403; multi-axis create/reload; Cold → NOT MEASURED (K10).
- Kapsam: yalnız AudienceProfile (+audit). Eligibility motoru YOK; başka aggregate/modül YOK.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SCMM-11-AUD · Prompt P-SCMM-11-AUD v1.0  (AudienceProfile çok-eksen + Subject-bound — MOD-0162)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/structured-content-messaging · Expected HEAD: 056d774a · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/WP-SCMM-11-AUD-audienceprofile-multiaxis.md (bu WP)
2. docs/decisions/DEC-SCMM-03-model-policy-freeze.md (AUD kararı, RM3, D8, sektör-nötr)
3. services/Diten.CrmService/.../Domain/Entities/AudienceProfile.cs +
   .../Features/Knowledge/AudienceProfile/Commands + Handlers + Queries +
   .../Api/Controllers/CRM/KnowledgeAudienceProfilesController.cs
4. services/Diten.CrmService/.../Features/Knowledge/Concept/IKnowledgeConceptAuditPublisher.cs (audit deseni)

NE:
 1) SubjectId additive (Guid?, NULLABLE — legacy tenant-global bozulmaz; yeni profil Subject-scoped).
 2) Çok-eksen: additive Dimensions = List<AudienceDimensionAssignment>{ AxisCode (string, config/açık — SEKTÖR-NÖTR,
    hardcoded enum YOK), Values (List<string>) }. Profil birden çok eksende değer taşır.
 3) ProfileType alanı KALIR (legacy backward-compat) — silme; yeni model Dimensions kullanır.
 4) CRUD + validasyon (AxisCode boş değil, Values non-empty, duplicate-axis red) + Subject-scope guard + audit
    (SourceModule "MOD-0162") + embedded AudienceDimensionAssignment class-map (GUID/string trap dikkat).
NEDEN: HTML "tek gerçek blocker" — eligibility (SCMM-11) çok-eksen+Subject-bound audience gerektirir; DEC-SCMM-03 AUD.
NASIL: L3 + EntityBase.Version; mevcut AudienceProfile desenine sadık; tüketiciler (KnowledgeContent/Path/Journey)
       by-Id ref taşımaya DEVAM eder — onlara DOKUNMA (additive-safe).
YAPMA: ProfileType SİLME; SubjectId'yi zorunlu yapma (legacy kırılır); sektöre özgü değerleri koda GÖMME (config-driven);
       eligibility/membership HESAPLAMA (D8 — o SCMM-11); tüketici aggregate'leri değiştirme; başka modül; global serializer.
DOĞRULA (E2 + E4 fleet):
 - build temiz; unit: Dimensions persist/round-trip · duplicate-axis 400 · AxisCode/Values validasyon · SubjectId null
   round-trip · ProfileType korunur · audit MOD-0162 · tüketici by-Id ref bozulmaz.
 - TAM CrmService.Application.Tests YEŞİL (regresyon).
 - E4 (fleet): authz 200/403 · multi-axis create/reload. Cold → NOT MEASURED (K10).
Ayrı commit(ler). §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13).

Durma koşulları: backward-compat (legacy profil / tüketici by-Id) riske girerse · sektör-nötr ihlali gerekiyorsa · kapsam AudienceProfile dışına taşarsa. DUR + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-08) → **ACCEPTED (E2)**
```text
Commits: 82676119 (multi-axis+subject) · 7b2c1a30 (audit)
Agent: PASS · Verification: PASS (CT scope+sektör-nötr+regresyon teyit) · CT: ACCEPTED · Evidence: E2 (E4 authenticated pending)
```
- ✅ Scope: yalnız AudienceProfile + paylaşılan Knowledge request/DTO/mapper (AudienceProfile-only satırlar) + audit — **tüketici (KnowledgeContent/Path/Journey) record'ları DOKUNULMAMIŞ** (by-Id ref korundu).
- ✅ **Sektör-nötr:** AxisCode "open, config-driven, not an enum" (`AudienceProfile.cs:63`); legacy ProfileType 8 değeri korundu.
- ✅ SubjectId nullable (legacy backward-compat); Dimensions additive; duplicate-axis 400; audit MOD-0162.
- ✅ AUD testleri izole **45/45**; build 0 Hata.
- ⚠️ **Tam suite: 1 FAILED / 1630** — `ContactLocationPiiHardeningTests.PiiMasking_...` **PRE-EXISTING flake** (kanıt: pre-AUD `056d774a`'te DE 1 fail; test izole 18/18 geçer; AUD PII/redaction'a dokunmaz). **AUD regresyonu DEĞİL** → ayrı defect (aşağıda).
- ⏳ **E4 authenticated (200 multi-axis create) NOT MEASURED** — token yok; authz denial **401 MEASURED** (fleet WARM). Operatör tokenıyla tamamlanır.

## Ayrı bulgu (AUD dışı, kaydedildi)
🟠 **Pre-existing test-izolasyon flake:** `ContactLocationPiiHardeningTests` (MOD-0150) tam-paralel koşuda intermittent 1-fail, izole geçer → paylaşılan static state / parallelization. SCMM ile ilgisiz; test-infra/MOD-0150 owner işi. Bir sonraki temiz-yeşil regresyon iddiasından önce çözülmeli.

## Kalan (bu WP dışı)
- **WP-SCMM-11-AUD-UI** (frontend): AudienceProfiles Slim yüzeyine multi-axis dimension editörü + Subject seçici.
- **SCMM-11 eligibility motoru** — bu WP açtı.
- **E4 authenticated turu** (fleet WARM — operatör tokenı gerek).
