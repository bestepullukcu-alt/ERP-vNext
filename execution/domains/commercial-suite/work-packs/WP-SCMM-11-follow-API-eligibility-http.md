# WORK PACKAGE — WP-SCMM-11-follow-API · Eligibility HTTP yüzeyi + RBAC (policy CRUD + evaluate)

> **Control Tower kaydı (SoR).** SCMM R1. Module: **CAND-CAP-0011**, CrmService. Branch: `feature/scmm-content-studio` (HEAD 10ac6735). Bağ: SCMM-11-core ✅ (EligibilityPolicy + evaluator + port). **Yalnız HTTP adapter + RBAC** — logic hazır, DOKUNMA.
> **Bağlam:** SCMM-11-core backend'i (policy aggregate + ResolveEligibilityQuery/port) main'de/branch'te AMA **HTTP controller YOK** (SCMM-12/14 ile aynı desen). Bu WP ucu açar → policy authoring UI (SCMM-11-UI) + ContentSet apply-eligibility gerçek policy'lerle çalışır.

## Ölçülmüş girdi (CT)
- **EligibilityPolicy CQRS HAZIR:** `CreateEligibilityPolicyCommand` · `UpdateEligibilityPolicyCommand` · `ArchiveEligibilityPolicyCommand(id)` · `ListEligibilityPoliciesQuery` · `GetEligibilityPolicyQuery(id)` + DTOs/Mapper + repo. **DOKUNMA.**
- **Evaluate HAZIR:** `ResolveEligibilityQuery` + `ResolveEligibilityHandler` + `IEligibilityEvaluationPort` (disjoint Eligible/Blocked/Unresolved, fail-closed) — evaluate endpoint bunu dispatch eder.
- **RBAC:** `EligibilityPermissions` = `crm.eligibility.read/manage/evaluate` (tanımlı; seed/grant eksik).
- **HTTP controller YOK** (ölçüldü).
- **Mirror:** `ClaimsController` (CRUD + [HasPermission] + Response<T> + DTO map) + effectiveness:batch/citations:resolve deseni (evaluate endpoint için request-DTO + 400 doğrulama). RBAC seed = SCMM-12-API/SCMM-14 DataSeeder deseni.

## Kapsam
1. **`EligibilityPoliciesController`** (Api/Controllers/CRM/, ClaimsController aynası), route `api/crm/content-composition/eligibility-policies`:
   - `GET` `[Read]` → ListEligibilityPoliciesQuery · `GET /{id}` `[Read]` → GetEligibilityPolicyQuery
   - `POST` `[Manage]` → CreateEligibilityPolicyCommand · `PUT /{id}` `[Manage]` → UpdateEligibilityPolicyCommand · `POST /{id}/archive` `[Manage]` → ArchiveEligibilityPolicyCommand
   - `POST` `api/crm/content-composition/eligibility:evaluate` `[Evaluate]` → request DTO (context: audience/product/market/channel/language/period + pinnedSelections) → ResolveEligibilityQuery dispatch; geçersiz→400.
   - `[Authorize]` + `_mediator.Send` + Response<T> + request DTO map.
2. **Request DTO'ları** (Create/Update/Evaluate) → command/query map (ClaimRequests deseni).
3. **RBAC seed/grant:** `crm.eligibility.read/manage/evaluate` AuthService + 97c5 grant (SCMM-12-API/SCMM-14 deseni).

## YAPMA
- EligibilityPolicy command/query/handler/port/aggregate DEĞİŞTİRME. Başka controller DEĞİŞTİRME. Yeni eligibility logic (yalnız HTTP adapter+DTO+RBAC). Silent default (context açık). Frontend (SCMM-11-UI ayrı). Gateway route (content-composition/{everything} zaten var). Başka modül.

## Acceptance
- **E2:** build temiz; unit — controller route/verb/permission (Read/Manage/Evaluate); Create/Update/Evaluate DTO→command/query map; doğru dispatch; evaluate geçersiz-context→400; [Authorize].
- **Regresyon:** tam CrmService.Application.Tests — bilinen PII flake HARİÇ yeni fail YOK (baseline-diff).
- **E4 (CT, standalone/fleet + dev-token):** 97c5 grant + authenticated policy create→list→evaluate (disjoint sonuç) →archive; yetkisiz→403.
- Kapsam: yalnız EligibilityPoliciesController + DTO + RBAC. Logic/frontend DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SCMM-11-follow-API · Prompt P-SCMM-11-follow-API v1.0  (Eligibility HTTP yüzeyi + RBAC — CAND-CAP-0011)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: 10ac6735 · Worktree: ana checkout

Önce oku (mirror):
1. execution/domains/commercial-suite/work-packs/WP-SCMM-11-follow-API-eligibility-http.md (bu WP)
2. services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/ClaimsController.cs (CRUD/route/HasPermission/DTO map aynası) + ContentSetsController.cs (ek-aksiyon deseni)
3. services/Diten.CrmService/.../Features/ContentComposition/Eligibility/* (EligibilityPolicyCommands/Queries/Dtos/Mapper/Permissions + ResolveEligibilityQuery/Handler/IEligibilityEvaluationPort — HAZIR, DOKUNMA)
4. RBAC seed: SCMM-12-API + SCMM-14 DataSeeder (crm.* AuthService seed + 97c5 grant)

NE:
 1) EligibilityPoliciesController (ClaimsController aynası), route api/crm/content-composition/eligibility-policies:
    GET [Read]→ListEligibilityPoliciesQuery · GET/{id} [Read]→GetEligibilityPolicyQuery · POST [Manage]→CreateEligibilityPolicyCommand ·
    PUT/{id} [Manage]→UpdateEligibilityPolicyCommand · POST/{id}/archive [Manage]→ArchiveEligibilityPolicyCommand.
 2) POST api/crm/content-composition/eligibility:evaluate [Evaluate] → request DTO (context: audience/product/market/channel/language/period + pinnedSelections)
    → ResolveEligibilityQuery dispatch; geçersiz/eksik-zorunlu context→400 (silent default YOK).
 3) Request DTO'ları (Create/Update/Evaluate) → command/query map (ClaimRequests deseni). [Authorize]+_mediator.Send+Response<T>.
 4) RBAC seed crm.eligibility.read/manage/evaluate AuthService + 97c5 grant (SCMM-12-API/SCMM-14 deseni; GUID subtype-4).
NASIL: ClaimsController + effectiveness/citations request-DTO+400 desenini birebir izle. EligibilityPolicy CQRS + ResolveEligibilityQuery HAZIR — yalnız HTTP adapter.
YAPMA: EligibilityPolicy command/query/handler/port/aggregate DEĞİŞTİR; başka controller; yeni logic; silent default; frontend(SCMM-11-UI); gateway route(zaten var); başka modül.
DOĞRULA (E2+E4):
 - build temiz; unit: route/verb/permission (Read/Manage/Evaluate), DTO→command/query map, doğru dispatch, evaluate geçersiz→400, [Authorize].
 - TAM CrmService.Application.Tests: bilinen ContactLocationPii flake HARİÇ yeni fail YOK (baseline-diff).
 - E4 (fleet): 97c5 grant + authenticated policy create→list→evaluate→archive; yetkisiz→403.
Ayrı commit. §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT E4'ü doğrular.

Durma koşulları: command/query şekli beklenenden farklıysa · ResolveEligibilityQuery context şekli uymuyorsa · RBAC seed deseni bulunamazsa · kapsam HTTP-adapter dışına taşarsa. DUR + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-14) → **ACCEPTED (E2)** · E4 = son manuel teste katlandı
```text
Commit: cb876c77 (tek) · Agent: PASS · CT: ACCEPTED E2 · Beklenen HEAD 10ac6735 idi; commit b96c7ef2 (WP doc) üzerine bindi
```
- ✅ **Scope (name-set):** 4 dosya — EligibilityPoliciesController + Models/CRM/EligibilityRequests + AuthService DataSeeder + controller test. 412 satır ekleme, **0 silme**. Hiçbir CQRS/handler/port/aggregate dosyası ve başka controller diff'te YOK → gerçekten thin HTTP adapter.
- ✅ **Mantık (CT commit blob'undan okudu):** (1) verb/route/perm = GET/GET[Read] · POST/PUT/POST-archive[Manage] · POST `eligibility:evaluate`[Evaluate], **delete/patch YOK** (kapatma=Archive); (2) evaluate'te dispatch-öncesi **4 açık 400 kapısı** (null/boş PolicyId · null/boş Context · boş dimension-adı/değer) — `Response.Fail(…,400)` ile, **silent default YOK**; boş değerler temizlenir, pinnedSelections+At taşınır; (3) `ResolveEligibilityQuery`'e generic dimension map; (4) RBAC = 3 katalog `crm.eligibility.read/manage/evaluate` (module `crm-content-composition` → tenant scope) + idempotent 97c5 Admin grant, `SeedAsync`'e bağlı.
- ✅ **Regresyon (SoR):** `CrmService.Application.Tests` 1724/0/5 (SCMM-14 sonrası 1710 + 14 yeni); PII flake patlamadı; sıfır yeni fail.
- ⏳ **E4 = son manuel teste katlandı** (fleet authenticated: 97c5 grant + policy create→list→evaluate(disjoint)→archive; yetkisiz→403). SCMM-14-UI deseni gibi — bizim SCMM tarafı bitince tek manuel pass'te. Gateway route (`content-composition/{everything}`) + RBAC seed hazır; fleet restart gerekir.

## Kalan (bu WP dışı)
- **SCMM-11-UI** (eligibility policy authoring console + evaluate/preview — Claims/KnowledgeConcepts aynası, select2, 7-dil, nav) — **bizim SCMM tarafının SON parçası**.
- Sonra **bizim SCMM tarafı TAMAM** → manuel test (SCMM-11-follow E4 dahil, hepsi birlikte) → sync/push/PR.
