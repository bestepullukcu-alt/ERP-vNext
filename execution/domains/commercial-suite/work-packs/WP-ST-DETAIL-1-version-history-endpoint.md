# WORK PACKAGE — WP-ST-DETAIL-1 · StrategyTemplate sürüm-geçmişi read endpoint (backend, CrmService)

> **CT (SoR).** MOD-0167-FU04, Faz 3 Detay (backend yarısı). Branch `feature/crm-scmm-studio` (`5f7d8d4d` üstü — WP-ST-EDIT-X §37 sonrası). **Kullanıcı kararı:** "Tam Faz 3 (backend sürüm geçmişi dahil)". Detay sayfasının "Sürüm geçmişi" paneli için **salt-okuma** endpoint: bir oyunun lineage'indeki tüm sürümleri listele. **CrmService (Application + Api); domain/aggregate/write-side DEĞİŞMEZ** (mevcut `ListByLineageAsync` kullanılır, yeni repo metodu YOK). DETAIL-2 (frontend Detay) bunu tüketir.

## Kanıt
- **Repo hazır:** `IStrategyTemplateRepository.ListByLineageAsync(Guid tenantId, Guid versionLineageId, ct)` (IStrategyTemplateRepository.cs:20) — lineage'deki tüm sürümler (arşiv dahil). Yeni repo metodu GEREKMEZ.
- **DTO alanları mevcut** (StrategyTemplateModels.cs): TemplateVersion(59), TemplateStatus(58), VersionLineageId(60), SupersededByTemplateId(62), ActivatedAt(79), ArchivedAt(81), CreatedAt(85). `StrategyTemplateByIdHandler` id→detay (VersionLineageId + tenant burada çözülür).
- **Query/handler deseni:** `Features/StrategyTemplate/Queries/GetStrategyTemplateByIdQuery.cs` + `Handlers/QueryHandlers/GetStrategyTemplateByIdHandler.cs` (tenant context + Response<T> zarfı) — aynala.
- **Controller:** `Api/Controllers/CRM/StrategyTemplatesController.cs` — GET `{templateId:guid}` (59) + `{templateId:guid}/bindings` (122) mevcut; ReadPermission. `{templateId}/versions` aynı desende eklenir.
- **Gateway:** `/api/crm/strategy-templates/{everything}` route'u (ocelot.json:3100) `/{id}/versions`'ı KAPSAR → **ocelot değişmez**.

## NE (CrmService; domain/write-side DEĞİŞMEZ)
1. **DTO (StrategyTemplateModels.cs):** yeni `StrategyTemplateVersionDto` = TemplateId(Guid) + TemplateVersion(int) + TemplateStatus(string) + ActivatedAt(DateTimeOffset?) + ArchivedAt(DateTimeOffset?) + CreatedAt(DateTimeOffset) + IsCurrent(bool). İsteğe bağlı sarmalayıcı `StrategyTemplateVersionsDto`(IReadOnlyList<StrategyTemplateVersionDto> Versions).
2. **Query + Handler:** `GetStrategyTemplateVersionsQuery(Guid TemplateId)` + `GetStrategyTemplateVersionsHandler` — tenant context'ten TenantId; `GetByIdAsync(templateId)` (yoksa 404/NotFound Response) → onun `VersionLineageId`'si ile `ListByLineageAsync(tenantId, lineageId, ct)`; map → `StrategyTemplateVersionDto`, **TemplateVersion'a göre azalan** sırala; `IsCurrent` = istenen templateId (veya süperseded olmayan aktif — istenen id işaretlensin yeterli). `Response<StrategyTemplateVersionsDto>.Success(...)`.
3. **Controller endpoint:** `[HttpGet("api/crm/strategy-templates/{templateId:guid}/versions")]` + ReadPermission (Get/bindings ile aynı) → `_mediator.Send(new GetStrategyTemplateVersionsQuery(templateId), ct)` → CreateActionResultInstance/mevcut Response deseni. Tenant-scoped.

## KORU / YAPMA
- **Domain/aggregate/write-side (Create/Update/Activate/Archive/NewVersion) DEĞİŞMEZ.** Yeni repo metodu YOK (mevcut `ListByLineageAsync`). Salt-okuma; hiçbir şey persist etmez. TenantId sunucudan (payload'dan değil). Diğer query/handler'lar (ById/bindings/list/contract) DOKUNMA. Frontend/Web/Details DOKUNMA (DETAIL-2). Gateway/ocelot DOKUNMA. MdmService DOKUNMA.
- **DUR:** `ListByLineageAsync` beklenmeyen şekilde boş/lineage çözülemiyorsa (ById VersionLineageId vermiyorsa) → DUR+raporla.

## Acceptance
- **E2:** `dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Diten.CrmService.Application.Tests.csproj -c Release --nologo` yeşil (baseline 1830/0/5; PII order-flake pre-existing). git diff: StrategyTemplateModels.cs + yeni Query + yeni Handler + StrategyTemplatesController.cs. **Domain/repo/write-handler/frontend/gateway diff YOK.** İsteğe bağlı: handler için +birim test (lineage sıralama + tenant izolasyon + 404).
- **E4:** `GET /api/crm/strategy-templates/{id}/versions` (X-Tenant-Id) → o oyunun tüm sürümleri (version desc, status + tarihler + IsCurrent); başka tenant'ın lineage'i sızmaz; bilinmeyen id→404.

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-ST-DETAIL-1 · StrategyTemplate sürüm-geçmişi read endpoint (MOD-0167-FU04 Faz3 backend; CrmService)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Expected HEAD: 5f7d8d4d üstü · Worktree: ana checkout

Salt-okuma endpoint: bir oyunun lineage'indeki tüm sürümleri listele (Detay "Sürüm geçmişi" paneli için). Mevcut ListByLineageAsync kullanılır; domain/write-side DEĞİŞMEZ.

Önce oku: execution/domains/commercial-suite/work-packs/WP-ST-DETAIL-1-version-history-endpoint.md · services/Diten.CrmService/src/Diten.CrmService.Domain/Repositories/IStrategyTemplateRepository.cs (ListByLineageAsync :20) · services/Diten.CrmService/src/Diten.CrmService.Application/Features/StrategyTemplate/Queries/GetStrategyTemplateByIdQuery.cs + Handlers/QueryHandlers/GetStrategyTemplateByIdHandler.cs (desen: tenant context + GetByIdAsync + Response<T>) · StrategyTemplateModels.cs (StrategyTemplateDetailDto alanları 53-85) · services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/StrategyTemplatesController.cs (GET {templateId} :59 / {templateId}/bindings :122, ReadPermission).

NE (CrmService; domain/write-side DEĞİŞMEZ):
 1) StrategyTemplateModels.cs: record StrategyTemplateVersionDto(Guid TemplateId, int TemplateVersion, string TemplateStatus, DateTimeOffset? ActivatedAt, DateTimeOffset? ArchivedAt, DateTimeOffset CreatedAt, bool IsCurrent) + record StrategyTemplateVersionsDto(IReadOnlyList<StrategyTemplateVersionDto> Versions).
 2) GetStrategyTemplateVersionsQuery(Guid TemplateId) + GetStrategyTemplateVersionsHandler: tenant context TenantId; GetByIdAsync(TemplateId) yoksa NotFound Response; onun VersionLineageId'si ile ListByLineageAsync(tenantId, lineageId, ct); map→StrategyTemplateVersionDto, TemplateVersion DESC sırala; IsCurrent = (v.TemplateId==istenen TemplateId); Response<StrategyTemplateVersionsDto>.Success.
 3) Controller: [HttpGet("api/crm/strategy-templates/{templateId:guid}/versions")] + ReadPermission → _mediator.Send(new GetStrategyTemplateVersionsQuery(templateId), ct) → mevcut Response→ActionResult deseni.
KORU/YAPMA: domain/aggregate/write-side (Create/Update/Activate/Archive/NewVersion) DEĞİŞMEZ; yeni repo metodu YOK (mevcut ListByLineageAsync); salt-okuma, persist yok; TenantId sunucudan; diğer query/handler (ById/bindings/list/contract) DOKUNMA; frontend/Web/Details/gateway/MdmService DOKUNMA.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Diten.CrmService.Application.Tests.csproj -c Release --nologo → yeşil (baseline 1830/0/5, PII order-flake pre-existing); git diff StrategyTemplateModels+Query+Handler+Controller; domain/repo/write-handler/frontend/gateway diff yok. Mümkünse handler birim testi (lineage desc + tenant izolasyon + 404). Ayrı commit ("feat(strategy): WP-ST-DETAIL-1 — StrategyTemplate sürüm-geçmişi read endpoint (MOD-0167-FU04)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: ListByLineageAsync boş/lineage çözülemiyorsa → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-22) → **ACCEPTED (E2)**
```
Commit: 2f3c20a0 · Agent: PASS · CT: ACCEPTED E2 (izole temiz build) · /c/tmp/ct-stdetail1-verify @2f3c20a0
```
- ✅ **Kapsam (5 dosya, +190):** StrategyTemplatesController.cs (Api) + GetStrategyTemplateVersionsHandler.cs + GetStrategyTemplateVersionsQuery.cs + StrategyTemplateModels.cs (Application) + StrategyTemplateVersionsTests.cs. **Domain/Repositories/Persistence/write-CommandHandler/frontend/gateway/ocelot/MdmService TEMİZ** ✓.
- ✅ **KORU=0 (salt-okuma):** handler `if (_tenant.TenantId is not {} tenantId) → Fail 400`; `GetByIdAsync(tenantId, id)` yoksa 404 (cross-tenant lineage sızmaz); template'in VersionLineageId'si ile **mevcut `ListByLineageAsync`** (yeni repo metodu YOK); map → TemplateVersion DESC; `IsCurrent = v.Id==istenen`. Hiçbir write/persist yok; domain/write-side dokunulmadı.
- ✅ **Endpoint:** `[HttpGet("api/crm/strategy-templates/{templateId:guid}/versions")]` + ReadFallback izin (Get/bindings ile aynı); gateway `/{everything}` kapsıyor → ocelot değişmedi.
- ✅ **Birim testleri (+3):** gerçek 2-sürüm lineage (Create→Activate→NewVersion): newest-first+IsCurrent / tenant izolasyon→404 / bilinmeyen id→404.
- ✅ **Build+test (CT izole, Release):** Diten.CrmService.Application.Tests **1833/0/5** (baseline 1830 + 3); Diten.CrmService.Api derleme **0 hata**.
- ⏳ E4: `GET /{id}/versions` canlı (X-Tenant-Id) → DETAIL-2 tüketecek.

**WP-ST-DETAIL-1 KOMPLE (E2). Sonraki: WP-ST-DETAIL-2 (Detay frontend restyle + sürüm-geçmişi paneli + Kampanyada kullan placeholder).**
