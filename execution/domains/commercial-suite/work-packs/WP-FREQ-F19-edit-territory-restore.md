# WORK PACKAGE — WP-FREQ-F19 · Edit'te Saha alanı (Territory Model+Node) doğru geri yüklensin (frontend)

> **CT (SoR).** MOD-0165-FU03. Branch `feature/scmm-content-studio` (**F15 landing sonrası HEAD üstü** — form.js SIRALI). **FONKSİYONEL BUG.** Edit modunda kayıtlı Saha alanı: Model seçili gelmiyor ("Model seçin") + Node ham GUID gösteriyor. Kök neden: politika yalnız `TerritoryNodeId` saklıyor (model id yok); form.js edit-restore node'u yalnız seed ediyor, modeli seçip node options'ı cascade-yüklemiyor + node adını çözmüyor. **Frontend only** (VisitFrequencyPoliciesController proxy + form.js edit-restore). **Backend HAZIR** (`GET /api/crm/territory-models/nodes/by-ids?ids=` → `{id,name,modelId,code}` — GetTerritoryNodesByIdsHandler, TerritoryNodeLookupDto).

## Kapsam
1. **Controller proxy:** `VisitFrequencyPoliciesController`'a ekle: `[HttpGet("api/territory-models/nodes/by-ids")]` → `ProxyGetAsync($"/api/crm/territory-models/nodes/by-ids{Request.QueryString}", ct, "crm.territory.read", ReadFallback)`. (Diğer proxy'lerle aynı RBAC; literal "nodes/by-ids" Gateway {everything} wildcard'ında çalışır — DETAILS8 deseni.)
2. **form.js edit-restore (territory cascade):** politikada `territoryNodeId` varsa (context `vfpTerritoryNode` + gerekiyorsa target picker `vfpTargetTerNode`), edit-load'da:
   - `GET /CRM/VisitFrequencyPolicies/api/territory-models/nodes/by-ids?ids={nodeId}` → `{modelId, name}`.
   - Model select (`vfpTerritoryModel` / target `vfpTargetTerModel`) value = modelId (models listesi zaten yüklü) → **cascade node options yükle** (o model için) → node select value = nodeId (option adıyla). select2 kullanılıyorsa options sonrası rebind (F9 `rebindSelect2`/`syncSelect2` deseni).
   - by-ids çözülemezse: mevcut degrade (raw id seed) korunur (uydurma yok).
3. Aynı düzeltme **target picker territory-node** edit-restore'una da uygulanır (targetType=territory-node kayıt açıldığında Model+Node doğru gelsin). data-role="targetId" node select'te korunur.

## KORU / YAPMA
- buildPayload/id/data-role/cascade/validation DEĞİŞMEZ (yalnız edit-restore doğru model+node seçer). Backend DEĞİŞMEZ (endpoint hazır). Create modu davranışı değişmez. Liste/Detay/Çözümleme/resolve.js/index.js/Segment/diğer bölümler DOKUNMA. F9 select2 köprüsü korunur. Tema-duyarlı. Yalnız VisitFrequencyPoliciesController.cs (1 proxy) + form.js (edit-restore).

## Acceptance
- **E2:** Diten.Web.Tests 137/0. git diff: VisitFrequencyPoliciesController.cs + form.js. Backend/diğer bölüm diff YOK.
- **E4:** Territory-node hedefli VEYA Saha alanı kapsamlı bir politikayı Edit aç → Model **seçili** gelir + Node **adıyla** seçili gelir (ham GUID değil) + cascade çalışır; kaydet payload aynı (territoryNodeId).

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner (F15 landing SONRASI)
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-FREQ-F19 · Edit'te Saha alanı (Territory Model+Node) doğru geri yüklensin (MOD-0165-FU03, frontend, BUG)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/scmm-content-studio · Expected HEAD: <F15 commit> üstü · Worktree: ana checkout

Önce oku: execution/domains/commercial-suite/work-packs/WP-FREQ-F19-edit-territory-restore.md · frontend/Diten.Web/Controllers/CRM/VisitFrequencyPoliciesController.cs (territory proxy'ler) · frontend/Diten.Web/wwwroot/assets/js/CRM/VisitFrequencyPolicies/form.js (edit-restore ~394-401 seedSelect, cascadeNodes ~405, target picker territory ~285-291, loadForEdit/populate ~841 vfpTerritoryNode seed, F9 rebindSelect2/syncSelect2) · services/.../Territory/Nodes/Handlers/GetTerritoryNodesByIdsHandler.cs + Api/.../TerritoryModelsController.cs (by-ids endpoint HAZIR).

NE (frontend; buildPayload/id/data-role KORUNUR; backend DEĞİŞMEZ):
 1) Controller: [HttpGet("api/territory-models/nodes/by-ids")] → ProxyGetAsync("/api/crm/territory-models/nodes/by-ids"+Request.QueryString, ct, "crm.territory.read", ReadFallback).
 2) form.js edit-restore: policy.territoryNodeId varsa (context vfpTerritoryNode + target picker vfpTargetTerNode) → GET api/territory-models/nodes/by-ids?ids={nodeId} → {modelId,name}; model select value=modelId → cascade node options yükle → node select value=nodeId (adıyla). select2 rebind (F9 deseni). Çözülemezse mevcut degrade korunur. Create davranışı değişmez.
KORU/YAPMA: buildPayload/id/data-role/cascade/validation DEĞİŞMEZ; backend DEĞİŞMEZ; liste/Detay/Çözümleme/resolve.js/index.js/Segment/diğer bölümler DOKUNMA; F9 select2 köprüsü korunur; tema-duyarlı; yalnız VisitFrequencyPoliciesController.cs(1 proxy)+form.js(edit-restore).
DOĞRULA (E2): Diten.Web.Tests 137/0; git diff yalnız Controller+form.js; backend/diğer bölüm diff yok; buildPayload/data-role korunmuş. Ayrı commit. §22 TÜRKÇE. K13.
Durma: by-ids proxy bağlanamıyorsa; cascade/select2 restore'u bozuyorsa; buildPayload/data-role korunamıyorsa → DUR+raporla.
```
## §37 CT bağımsız doğrulama (2026-09-17) → **ACCEPTED (E2)**
```
Commit: 9719b113 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-f19-verify @9719b113
```
- ✅ Kapsam: VisitFrequencyPoliciesController.cs (+by-ids proxy) + form.js (edit-restore). backend/liste/detay/resolve.js/index.js/Segment/css/resx = 0.
- ✅ Controller `[HttpGet("api/territory-models/nodes/by-ids")]` proxy (`{modelId:guid}/nodes`'tan önce; literal nodes GUID değil → çakışmaz). Backend endpoint hazırdı (DTO {id,name,modelId,code}).
- ✅ form.js `resolveNodeLookup(nodeId)` + `restoreTerritoryPair(...)`: edit'te nodeId→{modelId,name} → model seç → cascade node yükle → node adıyla seç → select2 rebind (F9). Çözülemezse raw-id degrade. Hem context (vfpTerritoryNode) hem target picker (vfpTargetTerNode, data-role korundu).
- ✅ buildPayload/data-role/cascade/validate silinmemiş (grep 0); CREATE davranışı değişmedi. Diten.Web.Tests 137/0.
- ⏳ E4: territory-node/Saha-alanı politikasını Edit aç → Model seçili + Node adıyla.
```
