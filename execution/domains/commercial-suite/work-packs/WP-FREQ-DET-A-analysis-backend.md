# WORK PACKAGE — WP-FREQ-DET-A · Frekans Politikası Detay ANALİZ backend (ETKİ + çakışan hesap) (backend+proxy)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (`b2bb0dbe` üstü). Detay sayfası fazlaması — **FAZ 1: hesap mantığı (backend), sayfa/görsel sonra.** Kullanıcı KARARI: ETKİ = **tüm hedef tiplerini say** + ziyaret **çeyreğe normalize**; çakışanlar = **resolve engine reuse**. **Backend (CrmService) + frontend controller proxy** (görsel YOK bu fazda).

## Kapsam
### 1) Analiz query/handler/endpoint (yeni, read-only)
`GetVisitFrequencyPolicyAnalysisQuery(Guid PolicyId)` → `VisitFrequencyPolicyAnalysisDto`:
- **Impact:** `TargetCount:int?`, `TargetCountComputable:bool`, `PlannedVisitsPerQuarter:int?`, `ProjectionNote:string?`.
- **Conflicts:** resolve engine'i politikanın KENDİ hedefi (targetType+targetId + context BU/territory/segment/campaign/brand/product/cycle) için çalıştır → `SelectedPolicyId`, `Verdict`, `Candidates[]` (her biri: policyId, name, frequency özet, selected, reason). **VisitFrequencyResolveEngine.Resolve REUSE** (yeni motor yok).
- Handler: `GetVisitFrequencyPolicyQuery`/repo ile politikayı yükle; conflicts için aktif+silinmemiş adayları çek + resolve; impact hesapla.

### 2) ETKİ hedef sayımı (tüm tipler — feasible + graceful)
Hedef tipine göre `TargetCount`:
- `segment` → **SegmentMembershipResolver** `Outcome.Result.TotalMemberCount` (candidate-cap aşılırsa Computable=false + not).
- `account` / `contact` / `account-contact-link` → **1** (tekil kayıt).
- `campaign-target` → kampanyanın hedef snapshot sayısı (CampaignTarget reader/repo).
- `territory-node` → o node'un coverage sayısı (account_territory_assignments — AccountCurrentCoverageResolver / territory assignment reader).
- `audience-profile` / `concept-node` → **hazır matching sayımı YOKSA** `TargetCount=null, Computable=false, ProjectionNote="bu hedef tipi için sayım henüz yok"` (KOCA yeni matching engine KURMA — DUR+raporla eğer büyük iş gerekiyorsa). Mevcut hafif bir reader varsa kullan.
- **PlannedVisitsPerQuarter** = `TargetCount × requiredVisitCount(çeyreğe normalize)`: periodType month→×3, week→×13, quarter→×1, day→×~91 (13 hafta), cycle/campaign-period→normalize edilemez → ham `count×requiredVisitCount` + ProjectionNote("dönem bazlı, çeyreğe normalize edilemedi"). Computable=false ise null.

### 3) Endpoint + proxy
- Backend: `[HttpGet("{policyId:guid}/analysis")]` `VisitFrequencyPoliciesController` (CrmService) → RBAC read (crm.visit-frequency-policy.read / territory.read fallback). Gateway: policies route zaten var; `{id}/analysis` mevcut wildcard'da çalışır (teyit; ocelot değişikliği gerekiyorsa DUR+raporla).
- Frontend proxy: `VisitFrequencyPoliciesController` (Web) `[HttpGet("api/visit-frequency-policies/{policyId:guid}/analysis")]` → ProxyGetAsync.
### 4) Tests
CrmService.Application.Tests: analysis handler — segment count (mock resolver), tekil=1, campaign-target/territory-node count, audience/concept→computable=false, projeksiyon normalize (month×3 vb.), conflicts resolve reuse (selected+candidates+reason). Baseline sıfır-yeni-fail.

## KORU / YAPMA
- resolve/CRUD/archive/soft-delete/validation DEĞİŞMEZ (analiz YENİ read-only query; motor reuse). Segment membership resolver / territory coverage / campaign reader'ları **reuse** (davranış değiştirme). Audience/concept için büyük yeni matching engine KURMA (null+flag; gerekiyorsa DUR+raporla). Frontend görsel/sayfa YOK (sonraki faz). Liste/Detay quick-view/editör/Segment/başka modül DOKUNMA (yalnız yeni analysis query/handler/dto/endpoint + Web proxy + test). Tenant izolasyonu (her sorgu TenantId).

## Acceptance
- **E2:** CrmService.Application.Tests baseline-diff sıfır-yeni-fail + Diten.Web.Tests 137/0. `GET api/crm/visit-frequency-policies/{id}/analysis` → impact (per-type count + projection) + conflicts (resolve reuse). git diff: yeni analysis Query/Handler/Dto + CrmService controller endpoint + Web proxy + test. resolve/CRUD handler davranışı değişmedi.
- **E4:** (sonraki görsel fazda) — bu fazda API cevabı doğru (impact + conflicts).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-FREQ-DET-A · Detay analiz backend (ETKİ tüm-tip sayım + çakışan resolve reuse) (MOD-0165-FU03, backend+proxy)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: b2bb0dbe üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-DET-A-analysis-backend.md · services/.../VisitFrequencyPolicy/{Queries,Handlers/VisitFrequencyPolicyQueryHandlers,Resolve/VisitFrequencyResolveEngine + IVisitFrequencyPolicyResolver + Resolve/VisitFrequencyResolveContracts, VisitFrequencyPolicyDtos} · services/.../Segmentation/Resolution/SegmentMembershipResolver.cs (Outcome.TotalMemberCount) · services/.../Territory/AccountAssignments/AccountCurrentCoverageResolver.cs (node coverage) · services/.../Campaign/* (CampaignTarget snapshot count) · Api/Controllers/CRM/VisitFrequencyPoliciesController.cs + frontend/Diten.Web/Controllers/CRM/VisitFrequencyPoliciesController.cs (proxy deseni).

NE (backend read-only + Web proxy; resolve/CRUD DEĞİŞMEZ):
 GetVisitFrequencyPolicyAnalysisQuery(PolicyId) + handler + VisitFrequencyPolicyAnalysisDto {Impact{TargetCount:int?,TargetCountComputable:bool,PlannedVisitsPerQuarter:int?,ProjectionNote:string?}, Conflicts{SelectedPolicyId,Verdict,Candidates[]}}.
 Impact sayım (tip bazlı): segment→SegmentMembershipResolver TotalMemberCount (cap aşılırsa computable=false); account/contact/account-contact-link→1; campaign-target→kampanya hedef snapshot sayısı; territory-node→node coverage sayısı (account_territory_assignments/AccountCurrentCoverageResolver); audience-profile/concept-node→hazır sayım yoksa computable=false+not (KOCA yeni matching engine KURMA → gerekiyorsa DUR+raporla). PlannedVisitsPerQuarter = count × requiredVisitCount çeyreğe normalize (month×3/week×13/quarter×1/day×~91; cycle/campaign-period→ham+not); computable=false→null.
 Conflicts: VisitFrequencyResolveEngine.Resolve'u politikanın KENDİ hedefi+context'i için çalıştır (aktif+silinmemiş adaylar) → selected+candidates+reason REUSE (yeni motor yok).
 Endpoint: CrmService VisitFrequencyPoliciesController [HttpGet("{policyId:guid}/analysis")] RBAC read; Web proxy [HttpGet("api/visit-frequency-policies/{policyId:guid}/analysis")]. Gateway {id}/analysis wildcard'da çalışır (değilse DUR+raporla). Tenant izolasyonu.
 Tests: CrmService.Application.Tests analysis handler (per-type count + projection normalize + conflicts reuse).
KORU/YAPMA: resolve/CRUD/archive/soft-delete/validation DEĞİŞMEZ; resolver/coverage/campaign reader reuse (davranış değiştirme); audience/concept büyük matching engine YOK (null+flag/DUR); frontend görsel/sayfa YOK; liste/detay-quickview/editör/Segment/başka modül DOKUNMA; TenantId her sorgu.
DOĞRULA (E2): CrmService.Application.Tests baseline-diff sıfır-yeni-fail + Diten.Web.Tests 137/0 (Release); GET {id}/analysis impact+conflicts; git diff yeni analysis + endpoint + proxy + test; resolve/CRUD diff yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: bir hedef tipi sayımı KOCA yeni cross-service matching gerektiriyorsa (audience/concept); gateway {id}/analysis route eklenmesi gerekiyorsa; resolve reuse edilemiyorsa; kapsam VisitFrequencyPolicy dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama → (agent sonrası)
```text
Commit: <agent> · Agent: <PASS/FAIL> · CT: <PENDING>
```
- İzole worktree → CrmService.Application.Tests + Diten.Web.Tests baseline-diff; analysis query/handler/dto/endpoint/proxy; per-type count (segment/tekil/campaign/territory feasible, audience/concept computable=false), projeksiyon çeyrek-normalize, conflicts resolve reuse; resolve/CRUD davranışı değişmedi; tenant izolasyonu; git diff kapsam içi.
```
