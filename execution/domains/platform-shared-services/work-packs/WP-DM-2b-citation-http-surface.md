# WORK PACKAGE — WP-DM-2b · Citation port HTTP yüzeyi + RBAC (tüketilebilir uç)

> **Control Tower kaydı (SoR).** DM planı DM-2b. Module: **MOD-0029**. Branch: `feature/dm-document-master-register` (HEAD ff1dc38e).
> **Kritik yol:** TaskCenter repoint'i citation'ı buradan tüketir. DM-2a **logic'i** (Resolve/Search handler + mapping + port) zaten teslim etti (main'de, CT-ACCEPTED); **eksik olan HTTP ucu** — DM-2a'nın bilinçli ertelediği parça. Bu WP onu kapatır.

## Ölçülmüş girdi (CT, 2026-09-10)
- **DM-2a HAZIR (ölçüldü):** `ResolveDocumentCitationHandler` (batch $in `GetByPermanentUidsAsync`/`GetByDocumentCodesAsync`, unresolved omit, fail-closed) + `SearchDocumentCitationHandler` (`GetAllForTenantAsync` + in-memory term filtre uid/code/title, blocked görünür/Citable=false, limit 1-200/def-50) + `DocumentCitationMapping.ToCitation` (Citable=`IsOperationallyEffective`, BlockedReason=lifecycle adı, UID+Code zorunlu). **Bu handler'lara DOKUNMA — çalışıyor.**
- **Query'ler HAZIR:** `ResolveDocumentCitationQuery(IReadOnlyList<string> Identifiers, DocumentIdentifierKind By, string CorrelationId)` · `SearchDocumentCitationQuery(string? Term, int Limit, string CorrelationId)`.
- **HTTP uç YOK · in-process tüketici YOK · citation RBAC key YOK** (ölçüldü) → port erişilemez durumda.
- **Mirror deseni (birebir):** `DocumentManagementMasterRegisterController` (Route `api/v1/document-management`) → `[HttpPost("document-master-register/effectiveness:batch")] [HasPermission(EffectivenessRead)]` → `ResolveDocumentEffectivenessQuery`; request `ResolveEffectivenessApiRequest{By,Identifiers}` + `EffectivenessApiMapper.TryParseIdentifierKind/HasResolvableIdentifier/InvalidRequestReasonCode` (400 doğrulama). Citation bunun zengin kardeşi.
- **Perm sabitleri:** `DocumentMasterRegisterPermissions` = View/Manage/Link/AuditView/**EffectivenessRead** (`…master-register.effectiveness.read`). Citation → yeni `CitationRead`.
- **Perm auto-registration:** controller `[HasPermission]`'leri açılışta AuthService'e otomatik sync olur (E4 log'unda "Catalog permission synced … Permission auto-registration complete" görüldü). Yani perm sabiti + endpoint yeter; **97c5 Admin'e GRANT** ayrı (CT E4'te yapar, S1/manual-grant deseni).

## Kapsam
1. **Perm:** `DocumentMasterRegisterPermissions.CitationRead = "platform.document-management.master-register.citation.read"` (EffectivenessRead mirror).
2. **API request modeli:** `ResolveCitationApiRequest { string? By; IReadOnlyList<string>? Identifiers; }` (ResolveEffectivenessApiRequest mirror). Search query-string alır (term+limit), gövde yok.
3. **Endpoint'ler (MasterRegisterController'a, effectiveness:batch aynası):**
   - `POST document-master-register/citations:resolve` `[HasPermission(CitationRead)]` → doğrula (`EffectivenessApiMapper.TryParseIdentifierKind` + `HasResolvableIdentifier` REUSE; geçersiz→400 `invalid_request`) → `ResolveDocumentCitationQuery(Identifiers, by, CorrelationId)` dispatch. (governing-docs + freezer).
   - `GET document-master-register/citations/search?term={t}&limit={n}` `[HasPermission(CitationRead)]` → `SearchDocumentCitationQuery(term, limit, CorrelationId)` dispatch. (picker).
4. Response<T> + CorrelationId deseni (effectiveness endpoint birebir).

## YAPMA
- DM-2a handler/mapping/query/port'una DOKUNMA (çalışıyor, ACCEPTED). Effectiveness endpoint'ini DEĞİŞTİRME. **Tasks picker/governing-docs'u repoint ETME** (o "to task center" = arkadaşın işi; biz yalnız tüketilebilir uç veriyoruz). Register şema/PlatformCollections/MOD-0262 dokunma. Yeni citation logic yazma (yalnız HTTP adapter + perm). RBAC grant'ı KODA gömme (perm sabiti + endpoint; grant CT/ops).

## Acceptance
- **E2:** build temiz; unit — endpoint doğrulama (geçersiz By/boş Identifiers→400 invalid_request; effectiveness mapper reuse); doğru query dispatch (Resolve→ResolveDocumentCitationQuery, Search→SearchDocumentCitationQuery); `[HasPermission(CitationRead)]` attribution (Mod0029Fu29a/manifest testi eklenebilir); CitationRead perm sabiti mevcut.
- **Regresyon:** tam Platform.Application.Tests — yeni fail YOK (env/Mongo + 14 release-gate hariç; baseline-diff).
- **E4 (CT yapacak, fleet + seed'li 358):** 97c5 Admin'e CitationRead grant → authenticated: `citations/search?term=SOP` gerçek term-filtreli sonuç (blocked görünür, Citable=false) · `citations:resolve` by=uid/code gerçek item · geçersiz→400 · yetkisiz→403 · fail-closed. (Seed'li register şart — brownfield.)
- **Kapsam:** yalnız HTTP adapter + perm + request model + testler. Logic/handler/picker DEĞİŞMEZ.

---

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-DM-2b · Prompt P-DM-2b v1.0  (Citation port HTTP yüzeyi + RBAC — MOD-0029)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: feature/dm-document-master-register · Expected HEAD: ff1dc38e · Worktree: ana checkout

Önce oku (mirror = effectiveness:batch):
1. execution/domains/platform-shared-services/work-packs/WP-DM-2b-citation-http-surface.md (bu WP)
2. services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentManagementMasterRegisterController.cs (effectiveness:batch endpoint ~104-124 — BİREBİR ayna)
3. services/Diten.Platform/src/Diten.Platform.API/Models/DocumentManagement/EffectivenessApiRequests.cs (ResolveEffectivenessApiRequest + EffectivenessApiMapper: TryParseIdentifierKind/HasResolvableIdentifier/InvalidRequestReasonCode — REUSE)
4. services/Diten.Platform/.../MasterRegister/Queries/DocumentCitationQueries.cs + Handlers/QueryHandlers/DocumentCitationHandlers.cs (Resolve/Search query+handler — HAZIR, DOKUNMA) + Models/DocumentCitationModels.cs
5. services/Diten.Platform/.../MasterRegister/…/DocumentMasterRegisterPermissions (View/Manage/Link/AuditView/EffectivenessRead sabitleri)

NE:
 1) Perm sabiti ekle: DocumentMasterRegisterPermissions.CitationRead = "platform.document-management.master-register.citation.read" (EffectivenessRead mirror).
 2) API request modeli: ResolveCitationApiRequest { string? By; IReadOnlyList<string>? Identifiers; } (ResolveEffectivenessApiRequest mirror).
 3) MasterRegisterController'a iki endpoint (effectiveness:batch birebir ayna):
    - [HttpPost("document-master-register/citations:resolve")] [HasPermission(CitationRead)]:
      EffectivenessApiMapper.TryParseIdentifierKind(request?.By,...) false→400 invalid_request; HasResolvableIdentifier(request.Identifiers) false→400;
      else dispatch new ResolveDocumentCitationQuery(request.Identifiers!, by, CorrelationId).
    - [HttpGet("document-master-register/citations/search")] [HasPermission(CitationRead)]:
      [FromQuery] term, limit → dispatch new SearchDocumentCitationQuery(term, limit, CorrelationId).
    Response<T> + CorrelationId deseni effectiveness ile birebir.
NEDEN: DM-2a citation logic'i hazır ama HTTP uç yok → port erişilemez; TaskCenter repoint'i (arkadaş) bu uçları tüketecek. picker=search, governing-docs/freezer=resolve.
NASIL: effectiveness:batch endpoint'ini birebir örnek al (aynı controller, aynı mapper reuse, aynı 400/Response deseni). Citation query/handler HAZIR — yalnız HTTP adapter yaz.
YAPMA: DM-2a handler/mapping/query DEĞİŞTİRME; effectiveness endpoint DEĞİŞTİRME; Tasks picker/governing-docs REPOINT ETME (arkadaşın işi); yeni citation logic; register şema/PlatformCollections/MOD-0262; RBAC grant'ı koda gömme (yalnız perm sabiti+endpoint).
DOĞRULA (E2):
 - build temiz; unit: geçersiz By/boş Identifiers→400; doğru query dispatch (Resolve/Search); [HasPermission(CitationRead)] attribution; CitationRead sabiti.
 - TAM Platform.Application.Tests: yeni fail YOK (env/Mongo + 14 release-gate hariç; baseline-diff).
Ayrı commit. §22 raporu TÜRKÇE. Senin PASS'in kapanış değildir (K13) — CT E4'ü fleet'te (97c5 grant + seed'li 358) authenticated doğrular.

Durma koşulları: citation query şekli beklenenden farklıysa (raporla) · effectiveness mapper reuse edilemiyorsa · perm auto-registration deseni belirsizse · kapsam HTTP-adapter+perm dışına (logic/picker) taşarsa. DUR + raporla.
```

## Kalan (bu WP dışı)
- **CT E4:** 97c5 CitationRead grant + authenticated search/resolve doğrulama (seed'li 358).
- **DM-3** (effectiveness:batch canlı doğrula, reuse) · **DM-4** (gerçek dosya, MOD-0262).
- **TaskCenter repoint** (arkadaş): Tasks picker/governing-docs'u eski DocumentReferenceList'ten bu citation uçlarına geçir.
- DM branch push + PR.
