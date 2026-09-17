# WORK PACKAGE — WP-SEG-DETAILS8 · Territory node reverse lookup (Details ismi + Edit cascade)

> **CT (SoR).** MOD-0167-FU02 Segments. Branch `feature/scmm-content-studio` (`824a5cdf` üstü). **Backend (CrmService territory) + frontend (Details.cshtml + form.js).** Owner: Details'te "Territory is TR — Marmara" (node ismi) + Edit'te kayıtlı territory node için model+node **seçili gelmesi**. İkisi de aynı köke bağlı: node id → {ad, parent model} reverse lookup YOK.

## Fizibilite (CT ölçümü)
- `territory_nodes` (DitenERP_Dev, 1063 doküman): `ModelId` (parent model) + `Name` + `TerritoryCode` + `TenantId` **ZATEN var** → reverse lookup Mongo'da mümkün, ekstra veri yok.
- CrmService'te node-by-ids / node-by-id endpoint **YOK** (yalnız `TerritoryModelsController {id}/nodes` ileri yön). → yeni bulk read gerekli.
- SegmentsController proxy: `territory-models` + `territory-models/{model}/nodes` (ileri). node-by-ids proxy eklenecek.
- Details criteria server-render (DETAILS7 human cümle GUID value ATLIYOR). form.js territory-node = `cascade-select` (model context ister; edit'te kayıtlı node var, model yok → boş).

## Kapsam
**1) Backend (CrmService, additive read — territory_nodes reuse):**
- **Bulk read endpoint** `GET /api/crm/territory-nodes?ids=guid,guid` → `[{id, name, modelId, code}]` (tenant-scoped, `territory_nodes` read; `crm.territory.read` / ReadFallback). Query handler + controller action; territory aggregate/write DEĞİŞMEZ (yalnız projeksiyon read).
- **Segment detail enrich:** GetSegmentByIdHandler criteria node'larındaki **entity-picker (territory-node) value GUID'lerini** node name'e çöz (bulk, fail-closed) → node'a **value-label** ekle (SEG-D/F deseni; DTO additive). Auth/territory erişilemezse null (GUID gizli kalır). Reference-set/enum değerler DEĞİŞMEZ (zaten okunur).
**2) Frontend Details (human value):** DETAILS7 human cümle, territory-node value için backend value-label'ı kullansın → "Territory is any of TR — Marmara" (GUID atlamak yerine isim). Backend value-label yoksa mevcut davranış (atla).
**3) Frontend Edit (form.js cascade restore):** territory-node kayıtlı value'lardan **node-by-ids proxy** ile modelId çöz → model context set → node listesi yükle → node seçili gelsin (cascade restore). SegmentsController'a `/api/territory-nodes` proxy ekle.

## KORU / YAPMA
- Territory aggregate/write/model endpoint'leri DEĞİŞMEZ (yalnız yeni bulk read). buildNodes blok→tree **payload byte-identical** (territory value GUID gönderilir; label UI/görünüm). Segment detail mevcut DTO (additive value-label). Resolution/preview/lifecycle/scope/tema/app-card/DETAILS2-7 davranışı DEĞİŞMEZ. reference-set/enum value DEĞİŞMEZ. Fail-closed (lookup yok → GUID gizli, uydurma isim yok). Ekstra per-node read YOK (bulk). segment-create.css/segment-details.css DOKUNMA (yalnız markup/js/backend). Başka modül.

## Acceptance
- **E2:** CrmService.Application.Tests + Diten.Web.Tests baseline-diff sıfır-yeni-fail. node-by-ids bulk read (tenant-scoped, fail-closed); segment detail territory value-label (additive); Details criteria "Territory is any of <node adı>"; Edit'te kayıtlı territory node model+node seçili (cascade restore); buildNodes payload byte-identical; per-node read yok. git diff: CrmService territory read + SegmentsController proxy + Details.cshtml + form.js (+ gerekirse detail handler/DTO).
- **E4:** Details "Territory is any of TR — Marmara"; Edit → territory koşulu model+node dolu seçili (GUID chip değil).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-SEG-DETAILS8 · Territory node reverse lookup (Details ismi + Edit cascade) (MOD-0167-FU02, backend+frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <dispatch anındaki HEAD (824a5cdf üstü)> · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-SEG-DETAILS8-territory-node-lookup.md · services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/TerritoryModelsController.cs ({id}/nodes ileri okuma + territory read handler deseni) · territory_nodes projeksiyonu (ModelId/Name/TerritoryCode) · Segmentation/Handlers/QueryHandlers/GetSegmentByIdHandler.cs (SEG-D/F value-label deseni) + SegmentModels (criteria node DTO) · frontend/Diten.Web/Controllers/CRM/SegmentsController.cs (territory-models proxy'leri ~300-309) · frontend/Diten.Web/wwwroot/assets/js/CRM/Segments/form.js (loadEntityOptions territory-node cascade-select + hydrate/edit restore) · frontend/Diten.Web/Views/CRM/Segments/Details.cshtml (DETAILS7 human cümle @functions, territory value).

NE:
 Backend (additive read, territory_nodes reuse):
 - GET /api/crm/territory-nodes?ids=guid,guid → [{id,name,modelId,code}] (tenant-scoped, territory_nodes read, crm.territory.read/ReadFallback). Query handler + controller action; territory write/aggregate DEĞİŞMEZ.
 - GetSegmentByIdHandler: criteria entity-picker (territory-node) value GUID'leri → node name (bulk, fail-closed) → criteria node DTO'ya additive value-label. reference-set/enum value değişmez.
 Frontend:
 - SegmentsController: /api/territory-nodes proxy (→ /api/crm/territory-nodes).
 - Details.cshtml (DETAILS7 human): territory-node value için backend value-label kullan ("Territory is any of TR — Marmara"); yoksa mevcut (atla).
 - form.js: territory-node edit restore — kayıtlı value'lardan node-by-ids ile modelId çöz → model context set → node yükle → seçili (cascade restore).
KORU/YAPMA: territory aggregate/write/model endpoint değişmez (yalnız yeni bulk read); buildNodes payload byte-identical (territory value GUID gönderilir, label görünüm); segment detail additive value-label; resolution/preview/lifecycle/scope/tema/DETAILS2-7 değişmez; reference-set/enum value değişmez; fail-closed (lookup yok→GUID gizli, uydurma yok); per-node read YOK (bulk); segment-create.css/segment-details.css DOKUNMA; başka modül.
DOĞRULA (E2): TAM CrmService.Application.Tests + Diten.Web.Tests baseline-diff sıfır-yeni-fail; node-by-ids bulk read tenant-scoped+fail-closed; segment detail territory value-label additive; Details "Territory is any of <ad>"; Edit cascade restore (model+node seçili); buildNodes payload byte-identical; per-node read yok. Ayrı commit. §22 TÜRKÇE. K13.
Durma: territory_nodes read handler deseni yoksa/kurulamıyorsa; node→modelId reverse çözülemiyorsa; payload byte-identical bozuluyorsa; cascade restore yapılamıyorsa; kapsam (CrmService territory read + Segmentation detail + SegmentsController + form.js + Details) dışına taşarsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama → (agent sonrası, dispatch owner'da)
```text
Commit: <agent> · Agent: <PASS/FAIL> · CT: <PENDING>
```
- İzole worktree → CrmService.Application.Tests + Diten.Web.Tests baseline-diff; node-by-ids bulk read (tenant-scoped, fail-closed, territory write dokunulmadı); segment detail territory value-label additive; Details "Territory is any of <ad>"; Edit cascade restore; buildNodes payload byte-identical; per-node read yok; reference-set/enum value değişmedi.

## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: 8b89f467 · Agent: PASS · CT: ACCEPTED E2 (gerçek build) · izole /c/tmp/ct-det8-verify @8b89f467
```
- ✅ Scope: CrmService territory read (GetTerritoryNodesByIdsHandler + Queries/Dtos + ITerritoryNodeRepository.ListByIdsAsync + repo) + Segmentation detail (GetSegmentByIdHandler + SegmentModels ValueLabels) + TerritoryModelsController(nodes/by-ids) + SegmentsController proxy + Details.cshtml + form.js + SegmentViewModels + 2 test. Territory write/aggregate DOKUNULMADI.
- ✅ **Gateway route (ocelot değişmedi):** `nodes/by-ids` action TerritoryModelsController'da; mevcut `/api/crm/territory-models/{everything}` wildcard altında. `[HttpGet("nodes/by-ids")]` literal, `{id:guid}/nodes`'ten önce — GUID olmayan "by-ids" `{id:guid}` ile çakışmaz (FU08 deseni).
- ✅ **buildNodes payload byte-identical:** md5 eşit (824a5cdf==8b89f467); territoryModelId/valueLabels salt UI-state, hidden input'a girmez.
- ✅ **Fail-closed + tenant-scoped + bulk:** ListByIdsAsync boş-id→sorgu yok; criteria territory GUID→node name tek toplu okuma; erişilemez/cross-tenant→ValueLabels null (GUID gizli). +2 guard (value-label additive; cross-tenant→etiket yok). reference-set/enum value değişmedi.
- ✅ Details: territory-node value ValueLabels'tan isim ("Territory is any of <ad>"); yoksa GUID gizli. Edit: restoreTerritoryNodeContext node→modelId toplu çöz → cascade select model+node seçili.
- ✅ Build+test (CT izole, Release, GERÇEK): CrmService.Application.Tests **1745/0/5** + Diten.Web.Tests **137/0**.
- ⏳ E4: Details "Territory is any of TR — Marmara"; Edit territory model+node dolu-seçili.

**Segment Create/Edit + Details TAM KOMPLE — tüm açık noktalar kapandı.**
