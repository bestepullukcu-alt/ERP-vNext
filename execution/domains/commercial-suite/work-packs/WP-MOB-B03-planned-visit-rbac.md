# WORK PACKAGE — WP-MOB-B03 · Planned Visit canonical RBAC (permission + wiring + seed + grant) (backend, CrmService + AuthService)

> **CT (SoR).** DitenMultiply (Android) Phase 5.7 blocker B-03. Branch **`feature/mobile-crm-integration`** (WP-MOB-B02 ile aynı; ayrı commit). Planned Visit endpoint'leri **DEV-ONLY territory fallback** kullanıyor (`crm.territory.read` / `crm.territory.model.manage`); canonical `crm.planned-visit.read/manage/confirm` yalnız yorumda, **tanımlı-bağlı + seed'li DEĞİL**; **confirm manage'e çöküyor** (SoD yok). Bu WP canonical izinleri kurar/bağlar/seed'ler/grant'lar. **CrmService (Api + Application) + AuthService (catalog/seed).** Android route'ları DEĞİŞMEZ (yalnız izin enforce edilir).

## Kanıt
- **`PlannedVisitsController`** tüm aksiyon: `Perms.ReadFallback`(=`crm.territory.read`) / `Perms.ManageFallback`(=`crm.territory.model.manage`); **confirm→ManageFallback** (SoD çöküyor). (Fallback const deseni: `CampaignPermissions.cs:22-23`.)
- Canonical `crm.planned-visit.*` **AuthService kataloğunda YOK** (`PpmPermissionCatalog`/`DataSeeder` territory seed'liyor; planned-visit seed testi yok).
- **Yaygın borç:** aynı territory-fallback Campaign/Consent/CycleCapacity/CyclePeriod'da da var (F-RBAC) — bu WP **yalnız planned-visit** dilimini kapatır; VisitPlanning/VisitReport aynı fallback'i paylaşır (follow).
- **Ürün kararı (2026-09-25):** confirm ayrı canonical izin (SoD-yetenekli); şimdilik saha-rep rolüne read+manage+confirm ver (mobil çalışsın); confirm ileride supervisor'a taşınabilir.

## NE
1. **Canonical izin sabitleri** (`PlannedVisitPermissions` — CrmService.Application): `crm.planned-visit.read` · `crm.planned-visit.manage` · `crm.planned-visit.confirm`.
2. **Controller wiring** (`PlannedVisitsController`): GET list/detail/contract → **read** · POST create / PUT update / cancel / archive → **manage** · **confirm → confirm** (ayrı). **DEV-ONLY territory fallback KALDIR** (Perms.*Fallback → canonical). (Calendar endpoint varsa → read.)
3. **AuthService katalog + seed** (`PpmPermissionCatalog` + `DataSeeder`): 3 izni CRM permission kataloğuna ekle (territory deseni) + `PlannedVisitPermissionSeedTests` (TerritoryPermissionSeedTests deseni: 3 anahtar seed'li + doğru tier).
4. **97c5 grant:** saha-rep rolüne read+manage+confirm (mobil için); py+pymongo subtype-4 ([[mongo-guid-subtype-write-recipe]]) veya seed; **kullanıcı çalıştırır** (DB write auto-mode'da bloklu — script hazırla, marker `manual-grant-planned-visit-rbac`).
5. **Backward-compat:** Android route/istek/yanıt DEĞİŞMEZ — yalnız enforce edilen izin territory→planned-visit'e döner; grant sonrası mevcut akış aynı çalışır (grant yoksa 403 = beklenen).

## KORU / YAPMA
- **Planned Visit route/istek/yanıt sözleşmesi DEĞİŞMEZ** (yalnız `[HasPermission]` attribute anahtarları). PlannedVisit domain/handler DOKUNMA. **VisitPlanning/VisitReport bu WP'de DEĞİL** (aynı fallback ama kapsam dışı — F-RBAC follow). AuthService seed'de mevcut izinleri bozma (yalnız additive 3 anahtar) — [[jwt-clockskew-guard-red]]/seed testleri kırılmasın. TenantId zorunlu. confirm manage'den ayrı kalır (SoD-yetenekli). Android'e DOKUNMA. B-02 ayrı.
- **DUR:** canonical izinleri eklemek mevcut territory seed testini kırıyorsa (additive olmalı); confirm'ı ayırmak mevcut bir testi bozuyorsa → DUR+raporla.

## Acceptance
- **E2:** `dotnet test` (CrmService.Application.Tests + AuthService.Application.Tests) → yeşil (mevcut + yeni PlannedVisit RBAC/seed testleri); CrmService.Api + AuthService derleme 0 hata. git diff: CrmService (PlannedVisitPermissions + PlannedVisitsController attribute) + AuthService (catalog + seed + test). Route/istek/yanıt diff YOK.
- **B-03 rol testleri (runtime):** izin var→200/201 · izin yok→403 · tenant mismatch→reddedilir · unauthenticated→401 · **read/manage/confirm ayrımı** (yalnız read'i olan create edemez; yalnız manage'i olan confirm edemez). Seed testi: 3 anahtar katalogda+doğru tier.
- **E4:** grant sonrası 97c5 rep → planned-visit list/create/confirm 200/201; grant'sız kullanıcı → 403; anon → 401.

## Android Contract (backend bitince)
```
B-03:
  endpoint → permission:
    GET list/detail/contract/calendar → crm.planned-visit.read
    POST create / PUT update / POST cancel / POST archive → crm.planned-visit.manage
    POST confirm → crm.planned-visit.confirm
  role: saha-rep = read+manage+confirm (97c5, interim); confirm SoD ileride ayrı
  tenant: X-Tenant-Id zorunlu
  401: anon · 403: izin yok
  route/request/response: DEĞİŞMEDİ (backward-compatible)
```

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/backend-architect.md]
WP: WP-MOB-B03 · Planned Visit canonical RBAC (permission+wiring+seed+grant) (Diten.CrmService + Diten.AuthService, backend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/mobile-crm-integration (WP-MOB-B02 ile aynı; ayrı commit)

Amaç: Android B-03. Planned Visit DEV-ONLY territory fallback yerine canonical crm.planned-visit.read/manage/confirm; confirm ayrı (SoD-yetenekli); catalog+seed+97c5 grant. Route/istek/yanıt DEĞİŞMEZ (backward-compat). CrmService+AuthService; Android'e DOKUNMA; VisitPlanning/VisitReport kapsam dışı (F-RBAC follow); B-02 ayrı.

Önce oku: execution/domains/commercial-suite/work-packs/WP-MOB-B03-planned-visit-rbac.md · services/Diten.CrmService/src/Diten.CrmService.Api/Controllers/CRM/PlannedVisitsController.cs (Perms.ReadFallback/ManageFallback, confirm→manage) · services/Diten.CrmService/src/Diten.CrmService.Application/Features/Campaign/CampaignPermissions.cs (fallback const deseni) · services/Diten.AuthService/src/Diten.AuthService.Application/Common/Authorization/PpmPermissionCatalog.cs + services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs (territory seed deseni) · services/Diten.AuthService/tests/.../Authorization/TerritoryPermissionSeedTests.cs (seed testi deseni) · memory mongo-guid-subtype-write-recipe.

NE:
 1) PlannedVisitPermissions (CrmService.Application): crm.planned-visit.read/manage/confirm sabitleri.
 2) PlannedVisitsController wiring: GET list/detail/contract/calendar→read; POST create/PUT update/cancel/archive→manage; confirm→confirm (ayrı). DEV-ONLY territory fallback kaldır.
 3) AuthService: PpmPermissionCatalog+DataSeeder'a 3 izin (additive, territory deseni) + PlannedVisitPermissionSeedTests.
 4) 97c5 grant script (py+pymongo subtype-4, marker manual-grant-planned-visit-rbac) — saha-rep rolüne read+manage+confirm; KULLANICI çalıştırır (DB write bloklu), Create ETME.
 5) Backward-compat: route/istek/yanıt DEĞİŞMEZ.
KORU/YAPMA: route/istek/yanıt + PlannedVisit domain/handler DEĞİŞMEZ (yalnız HasPermission anahtarları); VisitPlanning/VisitReport DOKUNMA (F-RBAC follow); AuthService additive (mevcut seed/izin bozma, seed testleri kırma); confirm manage'den ayrı; TenantId zorunlu; Android'e DOKUNMA; B-02 ayrı.
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test services/Diten.CrmService/tests/Diten.CrmService.Application.Tests/Diten.CrmService.Application.Tests.csproj -c Release --nologo + AuthService.Application.Tests → yeşil (mevcut+yeni); CrmService.Api+AuthService 0 hata; git diff CrmService(PlannedVisitPermissions+controller)+AuthService(catalog+seed+test); route/istek/yanıt diff yok. Testler: izin var→200/201, yok→403, tenant-mismatch, anon→401, read/manage/confirm ayrımı; seed 3 anahtar. Ayrı commit ("feat(rbac): add planned visit permissions (read/manage/confirm) + wiring + seed [B-03]" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). Push YAPMA. §22 TÜRKÇE. K13.
Durma: canonical izin mevcut territory seed testini kırıyorsa (additive olmalı); confirm ayrımı mevcut testi bozuyorsa → DUR+raporla.
```

## §37 CT bağımsız doğrulama (2026-09-25) → **ACCEPTED (E2)**
```
Commits: bbd0a43e (B-03) + 34bb30ac (pre-existing baseline fix) + eaa514d8 (grant script) · Agent: PASS (Crm 1911/0, Auth 1019/0, sabotaj-kanıtlı) · CT: ACCEPTED E2 · izole worktree /c/tmp/ct-b03 @eaa514d8 → Crm 1911/0/5 + Auth 1019/0
```
- ✅ **Kapsam (bbd0a43e, 6 dosya):** DataSeeder (+58 katalog+97c5 Admin grant seed) · PlannedVisitPermissionSeedTests · permission-scope-baseline.csv (+planned-visit) · PlannedVisitsController (wiring) · PlannedVisitPermissions (fallback const sil) · PlannedVisitRbacTests (+180). **VisitPlanning/VisitReport/frontend/route-request-response diff YOK.**
- ✅ **Wiring (kod okundu):** GET contract/list/detail→`Read` · create/update/cancel/archive→`Manage` · **confirm→`Confirm` (ayrı, SoD)** · territory fallback KALKTI. Route/verb/istek/yanıt değişmedi (testler route'ları da kontrol ediyor).
- ✅ **AuthService additive:** 3 izin katalog+seed (Tenant scope) + 97c5 Admin idempotent grant (Admin territory fallback'i zaten tuttuğu için gerileme yok). Seed testi + baseline kaydı.
- ✅ **Build+test (CT izole):** CrmService **1911/0/5** (37 RBAC testi: anon→401, izin-yok→403 [eski territory artık açmıyor], izin-var→geçer, read/manage/confirm ayrımı, tenant-mismatch→400) + AuthService **1019/0** (seed testi). Sabotaj: confirm→manage 4 testi kırdı → geri alındı.
- ✅ **34bb30ac (ayrı, pre-existing):** main'de zaten kırmızı olan permission-scope-baseline.csv'ye eksik 10 SCMM anahtarı eklendi (B-03'ten bağımsız hijyen; canlı DB'de Tenant doğrulandı). KABUL.
- ✅ **eaa514d8 grant script:** dry-run varsayılan (yazmaz, rol/izin oluşturmaz), GUID subtype-4, tarih BSON tipi korunur, marker `manual-grant-planned-visit-rbac`.

### Açık / follow
- **⏳ KARAR: saha-rep rolü** — 97c5'te yok (Admin/DocMasterRegisterLinker/GQD/QADocumentation/Viewer). Mevcut rol adı ver VEYA saha-rep rolü aç → sonra `grant_planned_visit_rbac_97c5.py --role "<Rol>" [--apply]` (KULLANICI). Admin bu 3 izni zaten `manual-grant-mod0155` ile tutuyor.
- **F-follow (kapsam dışı):** (a) Contract yanıtı `CurrentLimitations` metni bayat ("fallback" diyor) — handler-dokunma kuralı yüzünden bırakıldı; (b) **frontend `PlannedVisitsController` hâlâ ReadFallback** — güvenlik açığı yok (backend enforce) ama kalıntı temizlenmeli; (c) VisitPlanning/VisitReport = F-RBAC follow.

**WP-MOB-B03 KOMPLE (E2).** E4 = rol grant + fleet restart + role-based smoke.

