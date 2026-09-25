# WORK PACKAGE — WP-MOB-B02 · `GET /api/crm/resources/me` (interim: user-as-resource) (backend, CrmService)

> **CT (SoR).** DitenMultiply (Android) Phase 5.7 blocker B-02. Branch **`feature/mobile-crm-integration` (main'den; chain-template'den DEĞİL)**. **Ürün sahibi kararı (2026-09-25):** User→Resource köprüsü **sonra** eklenecek; **şimdilik giriş yapan kullanıcının kimliği = ResourceId** (ResourceType=`user`). Bu WP `/me` endpoint'ini bu **interim** eşlemeyle kurar; **sözleşme (items[]) sabit** — ileride gerçek `UserResourceAssignment` arkasını değiştirir, Android sözleşmesi bozulmaz. **CrmService (Api + Application).** Android koduna DOKUNMA.

## Kanıt
- **B-02 doğrulandı:** `PlannedVisit.ResourceRef.ResourceId` serbest string + `ResourceType` (person/user/employee, `PlannedVisitResourceTypes`); CRM-doğrulı resource master YOK. `TerritoryResourcesController` (`api/crm/resources`) yalnız `{resourceId}/territory-responsibilities` + `{resourceId}/plan-vs-current` (resourceId=girdi). **`/me` YOK.**
- **Kimlik kaynağı mevcut:** `HttpActorContext` JWT `sub` / `ClaimTypes.NameIdentifier` (→ email/name fallback) okuyor. → **userId = `sub`/NameIdentifier claim** server'dan alınabilir (payload'dan DEĞİL).
- **Gateway:** `api/crm/resources/...` zaten route'lu (mobil territory-responsibilities'i çağırıyor) → `/me` aynı base'e eklenince kapsanır (OPTIONS ile doğrula).
- **Ürün kararı:** owner "şimdilik user bilgisi kullanılsın" (interim user-as-resource) — prompt'un "JWT sub→ResourceId'yi açık ürün kararı olmadan varsayma" şartı **karşılandı** (owner onayı 2026-09-25).

## NE (CrmService; interim)
1. **Endpoint:** `GET /api/crm/resources/me` — `TerritoryResourcesController` (`api/crm/resources`) altına ekle (veya yeni ince `ResourcesController`; aynı route base). **`[Authorize]`** (bearer zorunlu; anon→401). Yeni RBAC izni GEREKMEZ — yalnız **caller'ın kendi** kimliğini döndürür (yetki yükseltme riski yok); yine de bariyer: kimlik yoksa 401.
2. **Kimlik:** userId = JWT `sub`/NameIdentifier (server-derived; payload/route'dan ALMA). TenantId = mevcut tenant context (X-Tenant-Id/JWT).
3. **Yanıt (sabit, versiyonlanabilir; `items[]` — ileride çoklu-resource için):**
   ```json
   { "items": [ { "resourceId": "<userId>", "resourceType": "user", "status": "active" } ] }
   ```
   - Interim: **her zaman tam 1 öğe** = caller'ın user kimliği, `status:"active"`. (Field isimleri PlannedVisit `ResourceId`/`ResourceType` sözleşmesiyle hizalı.)
   - Kod yorumu: interim user-as-resource; gerçek `UserResourceAssignment` sonra (sözleşme aynı kalacak).
4. **Tenant/izolasyon:** yalnız caller'ın tenant'ı; başka tenant/user resource'u ASLA. userId claim yoksa/boşsa → 401 (veya boş `items:[]` — **tercih: 401**, çünkü authenticated ama kimliksiz anlamsız).
5. **Gateway:** `api/crm/resources/me` route kapsandığını doğrula (mevcut resources route deseni); gerekmezse ekleme.

## KORU / YAPMA
- **Mevcut `api/crm/resources/*` endpoint'leri (territory-responsibilities / plan-vs-current) DEĞİŞMEZ.** PlannedVisit/VisitApi contract DEĞİŞMEZ. Kimlik **JWT'den** (payload/route'dan resourceId ALMA). Interim eşleme yalnız `type:"user"`; person/employee döndürme. Android koduna DOKUNMA. B-03 (RBAC) AYRI WP. Yeni master/aggregate YOK (interim). TenantId zorunlu.
- **DUR:** `sub`/NameIdentifier claim yoksa (kimlik çözülemiyorsa) → 401 + raporla; `api/crm/resources` route'u `/me`'yi kapsamıyorsa (gateway) → additive route + raporla.

## Acceptance
- **E2:** `dotnet test services/Diten.CrmService/tests/... -c Release` → yeşil (yeni /me testleri); CrmService.Api 0 hata. git diff: Api (controller + response model) + gerekirse Application (ince query). Mevcut resources/PlannedVisit davranış diff YOK.
- **B-02 güvenlik testleri (prompt listesi, runtime assertion):** (1) authenticated kendi resource'unu görür (resourceId=userId, type=user, active) · (2/3) başka user/tenant resource'u görülemez (yalnız self) · (4) tenant mismatch reddedilir · (5) unauthenticated→401 · (6) kimliksiz/claim-yok→401 · (7) interim: her zaman 1 öğe (no-resource yok) — davranış açık · (8) çoklu-resource=interim'de yok (sözleşme items[] hazır) · (9) inactive=interim'de yok (hep active) · (10) resourceId=userId claim ile birebir.
- **E4 (no-credential):** anon `GET /api/crm/resources/me` → 401. (Credential'lı akış Android/owner E4.)

## Android Contract (backend bitince doldurulacak)
```
B-02:
  endpoint: GET /api/crm/resources/me
  method: GET · auth: Bearer + X-Tenant-Id
  response: { items: [ { resourceId, resourceType:"user", status:"active" } ] }
  resource identity: JWT sub/NameIdentifier (userId) — INTERIM user-as-resource
  multiple resource: sözleşme items[] hazır; interim tek öğe
  inactive: interim yok (hep active)
  401: anon / kimliksiz
```

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-MOB-B02 · GET /api/crm/resources/me (interim user-as-resource) (Diten.CrmService, backend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/mobile-crm-integration (main'DEN; chain-template'den DEĞİL — yoksa `git checkout -b feature/mobile-crm-integration origin/main`)

Amaç: Android B-02 blocker. Ürün kararı: şimdilik giriş yapan kullanıcının kimliği = ResourceId (type=user); gerçek User→Resource köprüsü sonra. /me endpoint'i bu interim eşlemeyle; sözleşme (items[]) sabit. CrmService; Android'e DOKUNMA; B-03 ayrı.

Önce oku: execution/domains/commercial-suite/work-packs/WP-MOB-B02-resources-me.md · services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/TerritoryResourcesController.cs (api/crm/resources) · services/Diten.CrmService/src/Diten.CrmService.Infrastructure/HttpActorContext.cs (sub/NameIdentifier) · services/Diten.CrmService/src/Diten.CrmService.Domain/Entities/PlannedVisit.cs (ResourceId/ResourceType/PlannedVisitResourceTypes).

NE (CrmService; interim):
 1) GET /api/crm/resources/me — TerritoryResourcesController altına (aynı api/crm/resources base) veya yeni ince ResourcesController; [Authorize] (anon→401); yeni RBAC izni GEREKMEZ (yalnız self).
 2) userId = JWT sub/NameIdentifier (server-derived, payload/route'dan ALMA); TenantId = tenant context.
 3) Yanıt: { items:[ { resourceId:"<userId>", resourceType:"user", status:"active" } ] } — interim her zaman 1 öğe; field isimleri PlannedVisit ResourceId/ResourceType ile hizalı; kod yorumu interim user-as-resource (gerçek UserResourceAssignment sonra, sözleşme aynı).
 4) Tenant izolasyon; claim yoksa 401; başka tenant/user ASLA.
 5) Gateway api/crm/resources/me kapsandığını doğrula; gerekmezse additive route.
KORU/YAPMA: mevcut api/crm/resources/* (territory-responsibilities/plan-vs-current) + PlannedVisit/VisitApi DEĞİŞMEZ; kimlik JWT'den (payload'dan resourceId ALMA); yalnız type=user; Android'e DOKUNMA; B-03 ayrı; yeni master YOK; TenantId zorunlu.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Diten.CrmService.Application.Tests.csproj -c Release --nologo → yeşil; CrmService.Api 0 hata; git diff yalnız CrmService (Api + gerekirse Application); mevcut resources/PlannedVisit diff yok. Testler: self→resourceId=userId+type=user+active · anon→401 · claim-yok→401 · başka-tenant/user görülemez · interim tek-öğe. E4 no-cred: anon /me→401. Ayrı commit ("feat(resource): add authenticated resource mapping (GET /api/crm/resources/me, interim user-as-resource) [B-02]" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). Push YAPMA. §22 TÜRKÇE. K13.
Durma: sub/NameIdentifier claim yoksa 401+raporla; gateway /me kapsamıyorsa additive route+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-25) → **ACCEPTED (E2)**
```
Commit: f182ba1b (feature/mobile-crm-integration, main'den) · Agent: PASS (1874/0, sabotaj-kanıtlı) · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-b02 @f182ba1b → 1874/0/5
```
- ✅ **Kapsam (3 YENİ dosya, +256):** ResourcesController · GetMyResourcesQuery · MyResourcesTests. **mevcut TerritoryResourcesController/PlannedVisit/AuthService/frontend diff YOK.** Ayrı controller (TerritoryResources'a dokunmadı), route base `api/crm/resources`.
- ✅ **Kimlik (kod okundu):** `MyResourceIdentity.ResolveUserId` = `sub` ?? `NameIdentifier` — **email/name fallback bilerek YOK** (resourceId stabil olmalı). Controller yalnız `CancellationToken` alır; userId `User` principal'dan (route/query/body'den ASLA). anon→401, tenant-yok→400.
- ✅ **Yanıt:** `items[]` sabit (interim tek öğe: `{resourceId:<sub>, resourceType:"user", status:"active"}`); field isimleri PlannedVisit `ResourceId`/`ResourceType` hizalı; interim yorumu (gerçek `UserResourceAssignment` sonra, sözleşme aynı).
- ✅ **Gateway:** `/api/crm/resources/{everything}` GET `/me`'yi zaten kapsıyor — değişiklik yok.
- ✅ **Sabotaj kanıtı:** email fallback eklenince test kırıldı → geri alındı (guard gerçek). **Build+test:** 1874/0/5. E4 (canlı /me→401) fleet restart bekliyor.
- ⚠ **Android sözleşme notu:** yanıt `Response<T>` zarfında → **`{ data: { items:[...] }, statusCode }`** (çıplak `{items}` değil). İstek **`X-Tenant-Id` zorunlu** (yoksa/uyuşmazsa 400).

**WP-MOB-B02 KOMPLE (E2).** E4 = fleet restart + `curl …/api/crm/resources/me` (anon→401, authed→data.items). Sıradaki: WP-MOB-B03.

