# MOD-0162-FU02 — Knowledge / Content Taxonomy Runtime + UI — Module Pack Authorization Audit

- **Tarih:** 2026-08-09
- **Task türü:** Module pack authorization / scope definition (implementation DEĞİL)
- **Pack:** [MOD-0162-FU02-knowledge-content-runtime-ui.md](../../execution/domains/commercial-suite/module-packs/MOD-0162-FU02-knowledge-content-runtime-ui.md)
- **Verdict:** **PARTIAL** — pack oluşturuldu, ancak SoT boundary (FU01) `draft` ve Gateway route yok olduğu için
  `ready-for-dev` yerine **`draft` (draft-for-review)** kaldı.

---

## 1. Preflight

- Okunan kaynaklar: `AGENTS.md`, `commercial-suite/domain-config.md`, `.antigravity/rules/module-pack-standard.md`,
  MOD-0162-FU01 / FU01A / FU01C boundary pack'leri, MOD-0165-FU05 (Compact CRM UI precedent), MOD-0165-FU02/FU04/FU05,
  `ocelot.json` (`/api/crm/*` route envanteri), `services/Diten.CrmService` Feature/controller yapısı,
  `module-id-registry.md` (MOD-0162 satırı), `module-implementation-status.md`.
- Agent talimatı: `.antigravity/agents/module-pack-author.md` (kod yazma yok; yalnız execution/module-packs + audit).
- Kural uyumu: bu task `services/`, `frontend/`, `gateway/`, registry, Mongo'ya **dokunmadı**.

## 2. Dependency Confirmation

| # | Ön kabul | Durum | Not |
|---|---|---|---|
| 1 | FU01 boundary PASS | **KISMEN** | Pack mevcut ve içerik-eksiksiz, ancak `status: draft` (PASS değil). Onay bekliyor. |
| 2 | FU01A boundary | Mevcut, `draft` | KnowledgePath — bu FU'da runtime açılmaz |
| 3 | FU01B boundary | Mevcut, `draft` | EngagementJourney — bu FU'da runtime açılmaz |
| 4 | FU01C boundary | Mevcut, `draft` | Concept Graph — yalnız ConceptNodeId format-level referans |
| 5 | Pack'ler draft / runtime_code_allowed=false | **DOĞRULANDI** | Dördü de `runtime_code_allowed: false` |
| 6 | CrmService'te Knowledge runtime yok | **DOĞRULANDI** | `Features/`'te Account/Contact/Territory/Campaign/Consent var; Knowledge yok |
| 7 | Campaign, Knowledge'i ID/format-level taşıyor | **DOĞRULANDI** | MOD-0165-FU05 §4 SubjectId/TopicId/ConceptChainTemplateId format-level |
| 8 | MOD-0155 öncesi gerçek upstream boşluk | **DOĞRULANDI** | "Gidince ne anlatılacak?" runtime karşılığı yok |
| 9 | RBAC en sona; permission eksikliği PARTIAL | **UYGULANDI** | §15 + F-RBAC |

## 3. Scope Confirmation

- **Backend runtime:** KnowledgeContent + Subject + Topic + AudienceProfile aggregate'leri; CRUD-minus-delete + archive
  + effective dating + contract endpoint + Campaign read provider + tests + smoke. FU01C/01A/01B runtime **hariç**.
- **Frontend/UI:** CRM Admin → Knowledge nav + Content List/Detail/Create/Edit/Archive + taxonomy admin + contract-driven
  Gateway-only UI + Golden Compact/Slim + 7 dil RESX + tests/smoke.
- **Yasaklananlar** pack §2/§18'de birebir sıralandı (Campaign/Consent/Brand-Product mutation, MOD-0155, recommendation,
  digital detailing, workflow, MOD-0048 publish, RBAC seed/grant, registry/Mongo, import/export, hard delete, DELETE).

## 4. Module Identity

- `id: MOD-0162-FU02` · `name: Knowledge / Content Taxonomy Runtime + UI` · parent `MOD-0162`.
- **DCP-002 gate:** `OK  MOD-0162-FU02: proven against Blueprint/registry.` (exit 0).
- Follow-up numarası doğrulaması: FU01 §18/F4 + §19 Next Prompt #2 **FU02'yi "Knowledge Content & Taxonomy
  Implementation" olarak zaten rezerve etmiştir** → önerilen `FU02` repo standardıyla **uyumlu**. Sapma yok.
- `service: Diten.CrmService + frontend/Diten.Web` · `shell: tenant` · `golden_reference: compact` ·
  `form_field_count: 18` · `entity_base: EntityBase`.

## 5. Governance Need

- AGENTS.md §7/§10: `approved`/`ready-for-dev` pack olmadan `@orchestrator` implementasyona başlayamaz. FU02 hem yeni
  backend runtime hem geniş UI feature'ı olduğundan authorization zorunludur.
- Domain config tenant-shell navigation'ı korur; pack §13 yalnız Knowledge `<li>` için dar, test edilebilir istisna verir.

## 6. Ownership Decisions

- Knowledge/Content runtime = **MOD-0162 SoT**. Brand/Product SoT = **MOD-0290/MDM** (optional reference tüketici).
- Campaign, Knowledge'i **kopyalamaz**, referans tutar (read provider ile tüketir). MOD-0155 Knowledge **üretmez**, tüketir.
- Brand/Product master Knowledge'e kopyalanmaz; ProductName/BrandName snapshot **future decision** (bu FU'da kopyalama yok).

## 7–12. Runtime / Model / Contract / Gateway / UI

- Backend model: §8 (KnowledgeContent), §9 (Subject/Topic/AudienceProfile) — FU01 §4–§9 sözleşmesi esas alındı.
- **`Version` → `ContentVersion` naming divergence** kayda geçirildi (platform reserved-name kuralı; FU01 metninden
  gerekçeli sapma).
- **Golden `Delete`/`BulkDelete` → `Archive` sapması** kayda geçirildi (hard delete yasağı).
- API contract §10: DELETE yok, TenantId payload yok, archive-only, Response/reasonCode/correlationId zarfı.
- **Gateway kararı: yeni route GEREKLİ** (ocelot'ta `/api/crm/knowledge*` yok — doğrulandı). integration-agent task'ı
  (**F-GW**); pack ocelot'a yazmaz. → ready-for-dev ön koşulu.
- UI §11/§14: Compact (18 alan) + Slim taxonomy canvas + archive/toast; Gateway-only; fake Brand/Product name yasağı.

## 13. Contract Flags

- Pozitif (7): `supportsKnowledgeContentManagement`, `supportsSubjectTaxonomyManagement`, `supportsConceptGraphReference`
  (format-level), `supportsBrandProductReference` (optional), `supportsArchiveLifecycle`, `supportsEffectiveDating`,
  `supportsContractDrivenUi`.
- Yasak (9): `supportsVisitPlanning`, `supportsRoutePlanning`, `supportsRecommendationEngine`,
  `supportsDigitalDetailingRuntime`, `supportsWorkflowApproval`, `supportsCampaignRuntimeMutation`,
  `supportsBrandProductMasterOwnership`, `supportsFileStorage`, `supportsHardDelete` — response'ta **hiç bulunmaz** (test).

## 14. Tests / Smoke

- 15 backend + 13 UI + 12 smoke maddesi pack §17'de tanımlandı; DELETE/TenantId/direct-5061/fake-name guard'ları dahil.

## 15. Explicit Exclusions

- Pack §18 tam liste; audit'te §3'te özetlendi.

## 16. Final Verdict — **PARTIAL**

Task'ın PARTIAL kriterlerinden **ikisi** doğrudan geçerli:

1. **Boundary pack'ler `draft`** → SoT sözleşmesi (FU01) henüz `approved` değil; bu yüzden pack `ready-for-dev` yerine
   **`draft`** kaldı (F-BND).
2. **Gateway route kararı EA/onaya kaldı** → `/api/crm/knowledge*` ocelot'ta yok; integration-agent authorization gerekli (F-GW).

FAIL kriterlerinin **hiçbiri** oluşmadı: implementation yapılmadı; Campaign/Consent/Brand-Product değişmedi; MOD-0155
açılmadı; RBAC seed/grant yapılmadı; registry/Mongo hand-edit yapılmadı; DELETE/hard delete yetkilendirilmedi; ownership
net. PASS kriterlerinin çoğu karşılandı (pack oluşturuldu, scope/ownership/gateway/golden/permission net, exclusions net,
MOD-0155 açılmadı, next prompt verildi) — tek eksik `ready-for-dev` durumu iki ön koşula bağlandı.

## 17. Next Recommended Prompt

`ready-for-dev` olması için önce **F-BND** (FU01 approved) + **F-GW** (Gateway route). Ardından:

```text
@orchestrator execution/domains/commercial-suite/module-packs/MOD-0162-FU02-knowledge-content-runtime-ui.md

MOD-0162-FU02 — Knowledge / Content Taxonomy Runtime + UI Implementation
```
