# WORK PACKAGE — WP-HR-nav-A · HumanCapitalManifestProvider (23 modül + HCM/Employees)

> **CT (SoR).** Branch `fix/hr-integration-gaps`. Plan: PLAN-HR-nav-wiring-module-catalog.md. **Yalnız backend/Platform** (manifest provider + DI). Frontend/entitlement/grant ayrı (WP-C/D).

## Kapsam
1. **`HumanCapitalManifestProvider`** (Diten.Platform.Application/Features/**HumanCapital**/SelfRegistration/, CrmManifestProvider aynası): `IModuleManifestProvider`, `GetManifest()` → `ModuleManifestDocument(ModuleCode:"HUMAN-CAPITAL", ModuleName/DisplayName:"Human Capital", Domain:"Human Capital", Service:"DitenHumanCapitalService", IsTenantAssignable:true, IsBaseline:false, Icon, SortOrder, Pages:[...])`.
   - **Pages (23)** — PLAN'daki HumanCapital/ route listesi; her biri `ModuleManifestPage(CODE, "Label", "/HumanCapital/<Route>", <readPerm>, null, true, "List", order, [actions])`.
   - **readPerm/actions**: web controller izin sabitlerinden (`HasPermission(ViewPermission/SearchPermission/…)`) + backend HumanCapitalService `[HasPermission]` (ör. hcm.employee-projections.read/manage, hcm.sensitive-access.read/manage). Read→page readPerm; manage/create/archive→ModuleManifestAction.
2. **HCM/Employees (1)** — MOD-0251 employee master (HcmService 5060). Ayrı ModuleCode "HCM-EMPLOYEE-MASTER"; readPerm `mod0251.employee.view`, action `mod0251.employee.create_draft`. Shell/metadata sayfaları (TepShellMetadata vb.) nav'a EKLEME.
3. **DI:** `services.AddSingleton<IModuleManifestProvider, HumanCapitalManifestProvider>()` (DependencyInjection.cs ~426-439 CRM satırının yanı).

## YAPMA
- Frontend/web DEĞİŞTİR (sayfalar hazır). Entitlement/RBAC grant (WP-C). TalentEcosystem (WP-B). HR backend servis kodu DEĞİŞTİR. Uydurma route/izin — hepsi web controller Route + backend HasPermission'dan; emin olunmayan izinde DUR+raporla. legal_entities/token DOKUNMA (Ali gap). Başka modül.

## Acceptance
- **E2:** Platform build temiz; provider DI'de; GetManifest 23(+1) page geçerli route+readPerm; Platform test baseline-diff sıfır-yeni-fail.
- **E4 (CT/kullanıcı):** Platform restart → self-registration → `platform_module_catalog` HUMAN-CAPITAL + page_descriptors 23(+1) + izinler auto-registered (menü entitlement+grant WP-C sonrası).

## §36.1 Agent Prompt (paste-ready)

```text
@[.antigravity/agents/backend-architect.md]
WP: WP-HR-nav-A · HumanCapitalManifestProvider (HR nav wiring, 23+1 modül — Platform)

Repository: C:\Users\user\Desktop\ERP-vNext
Branch: fix/hr-integration-gaps · Expected HEAD: <PLAN commit> · Worktree: ana checkout

Önce oku:
1. execution/domains/commercial-suite/work-packs/PLAN-HR-nav-wiring-module-catalog.md (mekanizma + 4 kapı + 23 route enumerasyonu + manifest imzası) + WP-HR-nav-A-humancapital-manifest.md (bu WP)
2. MIRROR: services/Diten.Platform/src/Diten.Platform.Application/Features/Crm/SelfRegistration/CrmManifestProvider.cs (ModuleManifestDocument + ModuleManifestPage + ModuleManifestAction) + DependencyInjection.cs ~426-439 (AddSingleton IModuleManifestProvider)
3. Route kaynağı: frontend/Diten.Web/Controllers/**/*.cs — Route("HumanCapital/...") + Route("HCM/Employees"); readPerm/action için controller HasPermission(sabit) + backend services/Diten.HumanCapitalService + Diten.HcmService [HasPermission]

NE (yalnız Platform backend):
 1) Features/HumanCapital/SelfRegistration/HumanCapitalManifestProvider.cs (CrmManifestProvider aynası): ModuleCode "HUMAN-CAPITAL", Domain "Human Capital", Service "DitenHumanCapitalService", IsTenantAssignable true, IsBaseline false, uygun Icon/SortOrder.
 2) Pages (23): PLAN HumanCapital/ route listesi → ModuleManifestPage(CODE,"Label","/HumanCapital/<Route>",readPerm,null,true,"List",order,[actions]). readPerm=controller ViewPermission/backend read; manage/create/archive→ModuleManifestAction.
 3) HCM/Employees: ModuleManifestPage (readPerm mod0251.employee.view, action mod0251.employee.create_draft), ModuleCode "HCM-EMPLOYEE-MASTER". TepShellMetadata gibi shell sayfaları nav'a EKLEME.
 4) DI: AddSingleton<IModuleManifestProvider, HumanCapitalManifestProvider>().
NASIL: CrmManifestProvider birebir. Her route/izin web controller + backend HasPermission'dan; UYDURMA YOK — emin olmadığın izinde DUR+raporla.
YAPMA: frontend/web; entitlement/RBAC grant (WP-C); TalentEcosystem (WP-B); HR backend servis kodu; legal_entities/token; başka modül.
DOĞRULA (E2):
 - Platform build temiz; provider DI'de; GetManifest 23(+1) page geçerli route+readPerm; Platform test baseline-diff sıfır-yeni-fail.
Ayrı commit. §22 raporu TÜRKÇE. K13 — CT bağımsız doğrular (Platform restart→catalog/page_descriptors+izin auto-register; menü E4 entitlement+grant sonrası).

Durma koşulları: bir route'un readPerm'i controller/backend'de bulunamıyorsa · manifest imzası farklıysa · shell/metadata sayfası (hariç tut+belgele) · kapsam Platform dışına taşarsa. DUR + raporla.
```

## Kalan (bu WP dışı)
- WP-HR-nav-B (TalentEcosystem 31) · WP-C entitlement+RBAC grant · WP-D L10n. → menüde görünür → PR→main (Ali).
