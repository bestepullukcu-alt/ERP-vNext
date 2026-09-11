# WORK PACKAGE — WP-SCMM-12-API · Claim HTTP yüzeyi + RBAC (SCMM-12-UI ön koşulu)

> **Control Tower kaydı (SoR).** SCMM R1, gate G2. Module: **CAND-CAP-0011** (Claim). Branch: `feature/scmm-content-studio` (HEAD 11befc07, main tabanlı).
> **Bağlam:** SCMM-12 Claim aggregate + CQRS main'de (PR #95) AMA **HTTP controller'ı YOK** (DM-2a gibi backend-only). SCMM-12-UI (claim authoring + component picker) bu ucu tüketecek → önce API. UI ayrı WP (SCMM-12-UI, follow).

## Ölçülmüş girdi (CT, 2026-09-11)
- **Claim backend HAZIR** (`Features/ContentComposition/Claims/`): `CreateClaimCommand` (ClaimText, Qualifiers?, EvidenceRefs?(opak), **ComponentRefs?(List<Guid>)** = KnowledgeContent reuse, Applicability(ProductRefs/MarketRefs/AudienceRefs), LanguageCode/ClaimCode vb.) · `UpdateClaimCommand` · `ApproveClaimCommand(ClaimId)` (draft→approved) · `ArchiveClaimCommand(ClaimId)` · `ListClaimsQuery(...IncludeArchived=true)` · `GetClaimQuery(ClaimId)` · `ClaimDto`/`ClaimListDto` + `ClaimMapper`. **Bu handler/command/query/mapper'a DOKUNMA — hazır.**
- **HTTP controller YOK** (ölçüldü) → tüketilemez.
- **RBAC:** `ClaimPermissions` = `crm.claim.read` / `crm.claim.manage` / `crm.claim.approve` (tanımlı; seed/grant ayrı).
- **Mirror (birebir):** `Diten.CrmService.Api/Controllers/CRM/KnowledgeConceptChainTemplatesController.cs` — `[Authorize]`, route `api/crm/knowledge/concept-chain-templates`, `List`(GET,Read) · `Get`(GET/{id},Read) · `Create`(POST,Manage) · `Update`(PUT/{id},Manage) · `Archive`(POST/{id}/archive,Manage); `_mediator.Send` + `CreateActionResultInstance`. Create/Update **request DTO → command** map eder.
- **RBAC seed deseni (mirror):** SCMM-05-S1 (concept-foundation `crm.*` key'leri AuthService'e seed + 97c5 grant). Claim için aynı.

## Kapsam
1. **`ClaimsController`** (`Api/Controllers/CRM/`, ChainTemplates aynası), route `api/crm/content-composition/claims`:
   - `GET claims` `[HasPermission(Read)]` → `ListClaimsQuery`
   - `GET claims/{claimId:guid}` `[HasPermission(Read)]` → `GetClaimQuery`
   - `POST claims` `[HasPermission(Manage)]` → `CreateClaimCommand` (request DTO map)
   - `PUT claims/{claimId:guid}` `[HasPermission(Manage)]` → `UpdateClaimCommand`
   - `POST claims/{claimId:guid}/approve` `[HasPermission(Approve)]` → `ApproveClaimCommand`
   - `POST claims/{claimId:guid}/archive` `[HasPermission(Manage)]` → `ArchiveClaimCommand`
   - `[Authorize]` + `_mediator.Send` + `CreateActionResultInstance` + Response<T> deseni birebir.
2. **Request DTO'ları** (Create/Update) — ChainTemplates request DTO deseni; command'a map (ClaimMapper varsa reuse, yoksa controller-level map).
3. **RBAC seed/grant (SCMM-05-S1 deseni):** `crm.claim.read/manage/approve` AuthService'e seed + **97c5 Admin grant** (UI/E4 authenticated için).

## YAPMA
- Claim command/query/handler/mapper/aggregate DEĞİŞTİRME (hazır, main'de). Başka ContentComposition controller'ı DEĞİŞTİRME. Yeni claim logic yazma (yalnız HTTP adapter + DTO + RBAC seed). Assembly/execution (SCMM-14/15) YOK — yalnız Claim CRUD/approve. Global serializer/class-map trap (yeni controller değil aggregate için geçerli; burada yok). Frontend YOK (SCMM-12-UI ayrı WP).

## Acceptance
- **E2:** build temiz; unit — controller route/verb/permission attribution (Read/Manage/Approve doğru eşleşir); Create/Update DTO→command map; doğru query/command dispatch; `[Authorize]`.
- **Regresyon:** tam CrmService.Application.Tests — bilinen `ContactLocationPii` flake HARİÇ yeni fail YOK (baseline-diff).
- **E4 (CT, fleet):** 97c5'e `crm.claim.*` grant → authenticated: `POST claims` (create draft) → `GET claims` (listede) → `POST approve` (draft→approved) → `POST archive`; yetkisiz→403; component picker'ın tüketeceği `GET claims` ComponentRefs döndürür. (Seed/grant sonrası.)
- **Kapsam:** yalnız ClaimsController + request DTO + RBAC seed. Logic/aggregate/frontend DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SCMM-12-API · Prompt P-SCMM-12-API v1.0  (Claim HTTP yüzeyi + RBAC — CAND-CAP-0011)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/scmm-content-studio · Expected HEAD: 11befc07 · Worktree: ana checkout

Önce oku (mirror):
1. execution/domains/commercial-suite/work-packs/WP-SCMM-12-API-claim-http-surface.md (bu WP)
2. services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/KnowledgeConceptChainTemplatesController.cs (BİREBİR ayna: [Authorize], route, List/Get/Create/Update/Archive, [HasPermission], _mediator.Send, CreateActionResultInstance, request DTO→command map)
3. services/Diten.CrmService/.../Features/ContentComposition/Claims/ (ClaimCommands, ClaimQueries, ClaimDtos, ClaimMapper, ClaimPermissions — HAZIR, DOKUNMA)
4. RBAC seed deseni: SCMM-05-S1 kayıtları/kod (crm.* key'leri AuthService'e seed + 97c5 grant) — aynısını crm.claim.* için.

NE:
 1) ClaimsController (Api/Controllers/CRM/, ChainTemplates aynası), route api/crm/content-composition/claims:
    - GET claims [HasPermission(ClaimPermissions.Read)] → ListClaimsQuery
    - GET claims/{claimId:guid} [Read] → GetClaimQuery
    - POST claims [Manage] → CreateClaimCommand (request DTO map)
    - PUT claims/{claimId:guid} [Manage] → UpdateClaimCommand
    - POST claims/{claimId:guid}/approve [Approve] → ApproveClaimCommand
    - POST claims/{claimId:guid}/archive [Manage] → ArchiveClaimCommand
    [Authorize] + _mediator.Send + CreateActionResultInstance + Response<T> birebir.
 2) Create/Update request DTO'ları (ChainTemplates deseni) → command map (ClaimMapper varsa reuse).
 3) RBAC: crm.claim.read/manage/approve AuthService'e seed + 97c5 Admin grant (SCMM-05-S1 deseni; GUID subtype-4/tenant tuzağı).
NEDEN: SCMM-12 Claim logic'i hazır ama HTTP uç yok → SCMM-12-UI (claim authoring + component picker) tüketilemez. Bu WP ucu açar.
NASIL: KnowledgeConceptChainTemplatesController'ı birebir örnek al (aynı route/verb/HasPermission/dispatch deseni). Claim command/query HAZIR — yalnız HTTP adapter + DTO + RBAC seed.
YAPMA: Claim command/query/handler/mapper/aggregate DEĞİŞTİRME; başka controller DEĞİŞTİRME; yeni claim logic; assembly/execution (SCMM-14/15); frontend (SCMM-12-UI ayrı); global serializer.
DOĞRULA (E2 + E4):
 - build temiz; unit: route/verb/permission attribution (Read/Manage/Approve), DTO→command map, doğru dispatch, [Authorize].
 - TAM CrmService.Application.Tests: bilinen ContactLocationPii flake HARİÇ yeni fail YOK (baseline-diff).
 - E4 (fleet): 97c5 grant + authenticated create→list→approve→archive; yetkisiz→403.
Ayrı commit. §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT E4'ü fleet'te authenticated doğrular.

Durma koşulları: command/query şekli beklenenden farklıysa (raporla) · ChainTemplates request-DTO deseni uygulanamıyorsa · RBAC seed deseni (SCMM-05-S1) bulunamazsa · kapsam HTTP-adapter+DTO+RBAC dışına (logic/frontend/assembly) taşarsa. DUR + raporla.
```

## Kalan (bu WP dışı)
- **SCMM-12-UI** (frontend): claim authoring console + component picker (KnowledgeConcepts aynası, 7-dil; `KnowledgeContentsController`'dan component listeler) — API kabul edilince.
- CT E4 (97c5 grant + authenticated CRUD/approve).
- SCMM-13 (iki-dil varyant) · SCMM-14 (assembly ④⑤⑥).
